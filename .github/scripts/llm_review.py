#!/usr/bin/env python3
"""
LLM-assisted PR review.

Calls an OpenAI-compatible chat-completions endpoint (opencode-go by default)
and posts severity-tagged inline review comments on the pull request.

Severity levels:
  - suggestion : nits, style, minor improvements
  - warning    : correctness smells, missing tests, risky patterns
  - critical   : bugs, security holes, data-loss risks, broken contracts

The GitHub Actions check fails iff at least one critical issue is reported.
"""

import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path

REPO = os.environ["REPO"]
PR_NUMBER = int(os.environ["PR_NUMBER"])
SHA = os.environ["SHA"]
DIFF_PATH = Path(os.environ["DIFF_PATH"])
META_PATH = Path(os.environ["META_PATH"])
API_BASE = os.environ.get("OPENCODE_API_BASE", "https://api.opencode.ai/v1").rstrip("/")
MODEL = os.environ.get("OPENCODE_MODEL", "opencode-go/glm-5.2")
API_KEY = os.environ["OPENCODE_API_KEY"]
AGENTS_MD_PATH = Path(os.environ.get("AGENTS_MD_PATH", "AGENTS.md"))

SEVERITY_ORDER = {"suggestion": 0, "warning": 1, "critical": 2}
SEVERITY_EMOJI = {"suggestion": "💡", "warning": "⚠️", "critical": "🛑"}
REVIEW_MARKER = "<!-- llm-review sha:{sha} -->"


def die(msg: str, code: int = 1) -> None:
    print(f"::error::{msg}", file=sys.stderr)
    sys.exit(code)


def gh(*args: str, check: bool = True, input_text: str | None = None) -> str:
    """Thin wrapper around the `gh` CLI."""
    cmd = ["gh", *args]
    res = subprocess.run(cmd, input=input_text, text=True, capture_output=True)
    if check and res.returncode != 0:
        die(f"gh {' '.join(args)} failed:\n{res.stderr}")
    return res.stdout


def parse_diff(diff: str) -> dict[str, set[int]]:
    """Return {file_path: set_of_new_file_line_numbers_in_diff} for inline comments."""
    files: dict[str, set[int]] = {}
    current_file: str | None = None
    current_lines: set[int] = set()
    new_line = 0
    in_hunk = False
    for line in diff.splitlines():
        m = re.match(r"^\+\+\+ b/(.+?)\s*$", line)
        if m:
            if current_file is not None:
                files[current_file] = current_lines
            current_file = m.group(1)
            current_lines = set()
            in_hunk = False
            continue
        if current_file is None:
            continue
        hunk = re.match(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@", line)
        if hunk:
            new_line = int(hunk.group(1))
            in_hunk = True
            continue
        if not in_hunk:
            continue
        if line.startswith("+++"):
            continue
        if line.startswith("-"):
            continue
        # context line or added line → advances the new-file line counter
        if line.startswith("+"):
            current_lines.add(new_line)
        new_line += 1
    if current_file is not None:
        files[current_file] = current_lines
    # Drop binary / deleted-file empty sets
    return {p: s for p, s in files.items() if s}


def call_llm(prompt: str, system: str) -> dict:
    url = f"{API_BASE}/chat/completions"
    payload = {
        "model": MODEL,
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": prompt},
        ],
        "temperature": 0.2,
        "response_format": {"type": "json_object"},
    }
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={
            "Authorization": f"Bearer {API_KEY}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=180) as resp:
            raw = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        die(f"opencode-go API {e.code}: {e.read().decode('utf-8', 'replace')[:500]}")
    except urllib.error.URLError as e:
        die(f"could not reach opencode-go API: {e}")

    try:
        data = json.loads(raw)
        content = data["choices"][0]["message"]["content"]
    except (KeyError, IndexError, json.JSONDecodeError) as e:
        die(f"unexpected API response: {e}\n{raw[:500]}")

    parsed = parse_llm_json(content)
    if not isinstance(parsed, dict):
        die(f"LLM did not return a JSON object: {content[:500]}")
    return parsed


def parse_llm_json(content: str) -> object:
    """Tolerant parser: handles fenced ```json blocks or raw JSON."""
    s = content.strip()
    if s.startswith("```"):
        s = re.sub(r"^```(?:json)?\s*", "", s)
        s = re.sub(r"\s*```\s*$", "", s)
    try:
        return json.loads(s)
    except json.JSONDecodeError:
        # Last-ditch: grab the outermost {...}
        m = re.search(r"\{.*\}", content, re.DOTALL)
        if m:
            return json.loads(m.group(0))
        return None


def build_prompt(diff: str, meta: dict, agents_md: str) -> tuple[str, str]:
    title = meta.get("title", "")
    body = meta.get("body", "") or ""
    files = [f.get("path") for f in meta.get("files", []) if f.get("path")]
    stats = f"+{meta.get('additions', 0)} -{meta.get('deletions', 0)}"

    system = (
        "You are a meticulous senior code reviewer. You analyse unified git diffs "
        "and surface only meaningful issues. You do NOT comment just to comment: "
        "if there is nothing important to say about a hunk, say nothing. "
        "Be specific, cite code, and propose a concrete fix when possible.\n\n"
        "Score every issue with exactly one severity:\n"
        "  - suggestion : nitpick, style, minor improvement, doc tweak\n"
        "  - warning    : correctness smell, missing test, risky path, dead code\n"
        "  - critical   : bug, security hole, data-loss, broken contract, "
        "broken build, broken auth\n\n"
        "Respond with STRICT JSON only, no prose, with this shape:\n"
        "{\n"
        '  "issues": [\n'
        "    {\n"
        '      "severity": "suggestion" | "warning" | "critical",\n'
        '      "path": "<file in \\"+++ b/...\\" form, no prefix>",\n'
        '      "line": <new-file line number where the issue lives, integer>,\n'
        '      "start_line": <optional, inclusive, for multi-line issues, integer>,\n'
        '      "rule": "<short stable id, e.g. sql-injection, missing-test, npe>",\n'
        '      "title": "<one line, <=80 chars>",\n'
        '      "detail": "<2-5 sentences. What is wrong. Why. Concrete fix.>",\n'
        "    }\n"
        "  ]\n"
        "}\n\n"
        "Rules:\n"
        "- Only reference files and line numbers actually present in the diff.\n"
        "- If an issue doesn't map to a single diff line, omit `line` entirely.\n"
        "- Prefer fewer, high-signal issues over many low-value ones.\n"
        "- An empty {\"issues\": []} is a valid and good answer when the diff is clean.\n"
        "- Never fabricate files, symbols, or behaviour you cannot see.\n"
    )

    project_context = ""
    if agents_md.strip():
        project_context = (
            "\n\nProject conventions (from AGENTS.md), apply them as "
            "authoritative review rules:\n```\n"
            + agents_md
            + "\n```\n"
        )

    prompt = (
        f"PR title: {title}\n"
        f"PR body:\n{body[:4000]}\n\n"
        f"Files changed: {', '.join(files)}\n"
        f"Diff stats: {stats}\n"
        f"Head SHA: {SHA}{project_context}\n\n"
        "Unified diff to review:\n```diff\n"
        f"{diff[:120000]}\n"
        "```\n\n"
        "Return the JSON now."
    )
    return prompt, system


def existing_bot_review_for_sha() -> bool:
    """Has the bot already posted a review for this exact SHA?"""
    marker = REVIEW_MARKER.format(sha=SHA)
    try:
        out = gh(
            "api",
            f"--header=Accept: application/vnd.github+json",
            f"/repos/{REPO}/pulls/{PR_NUMBER}/reviews",
            "--paginate",
            check=False,
        )
    except SystemExit:
        return False
    if not out.strip():
        return False
    try:
        reviews = json.loads(out)
    except json.JSONDecodeError:
        return False
    for r in reviews:
        user = (r.get("user") or {}).get("login", "")
        if user.endswith("[bot]") and marker in (r.get("body") or ""):
            return True
    return False


def post_review(issues: list[dict], valid_lines: dict[str, set[int]]) -> tuple[int, int, int]:
    """Post a single review with inline comments. Returns (sugg, warn, crit) counts."""
    inline: list[dict] = []
    summary_only: list[dict] = []

    for issue in issues:
        path = issue.get("path")
        line = issue.get("line")
        sev = (issue.get("severity") or "").lower()
        if sev not in SEVERITY_ORDER:
            sev = "suggestion"

        body = format_comment_body(issue, sev)
        if path and line is not None and path in valid_lines and int(line) in valid_lines[path]:
            comment = {"path": path, "line": int(line), "body": body, "side": "RIGHT"}
            start = issue.get("start_line")
            if isinstance(start, int) and start != line and start in valid_lines[path]:
                comment["start_line"] = start
                comment["start_side"] = "RIGHT"
            inline.append(comment)
        elif path and line is not None and path in valid_lines:
            # Try the closest valid line in the same hunk for robustness
            closest = min(valid_lines[path], key=lambda n: abs(n - int(line)))
            if abs(closest - int(line)) <= 3:
                inline.append(
                    {"path": path, "line": closest, "body": body, "side": "RIGHT"}
                )
            else:
                summary_only.append(issue)
        else:
            summary_only.append(issue)

    counts = {"suggestion": 0, "warning": 0, "critical": 0}
    for i in issues:
        counts[(i.get("severity") or "suggestion").lower()] += 1

    summary_lines = [
        f"### opencode-go review — `{SHA[:7]}`",
        REVIEW_MARKER.format(sha=SHA),
        "",
        f"- 💡 suggestions: **{counts['suggestion']}**",
        f"- ⚠️ warnings: **{counts['warning']}**",
        f"- 🛑 critical: **{counts['critical']}**",
        "",
    ]

    if summary_only:
        summary_lines.append("<details><summary>Non-inline findings</summary>")
        summary_lines.append("")
        for i in summary_only:
            sev = (i.get("severity") or "suggestion").lower()
            summary_lines.append(
                f"- {SEVERITY_EMOJI[sev]} **{sev.upper()}** — "
                f"`{i.get('path', '?')}"
                + (f":{i.get('line')}" if i.get("line") else "")
                + f"` — {i.get('title', '')}\n  {i.get('detail', '').strip()}"
            )
        summary_lines.append("")
        summary_lines.append("</details>")

    if not issues:
        summary_lines.insert(2, "")
        summary_lines.insert(2, "✅ Diff reviewed — no significant issues found.")

    payload = {
        "commit_id": SHA,
        "event": "COMMENT",
        "body": "\n".join(summary_lines),
        "comments": inline,
    }
    payload_path = Path(".llm-review/review-payload.json")
    payload_path.write_text(json.dumps(payload))

    gh(
        "api",
        "--method", "POST",
        "--header=Accept: application/vnd.github+json",
        f"/repos/{REPO}/pulls/{PR_NUMBER}/reviews",
        "--input", str(payload_path),
    )
    return counts["suggestion"], counts["warning"], counts["critical"]


def format_comment_body(issue: dict, sev: str) -> str:
    rule = issue.get("rule", "")
    head = f"{SEVERITY_EMOJI[sev]} **{sev.upper()}**"
    if rule:
        head += f" · `{rule}`"
    title = issue.get("title", "")
    detail = issue.get("detail", "").strip()
    parts = [head, ""]
    if title:
        parts.append(f"**{title}**")
    if detail:
        parts.append(detail)
    return "\n".join(parts)


def main() -> None:
    if not API_KEY:
        die("OPENCODE_API_KEY secret is not set.")
    if not DIFF_PATH.exists():
        die(f"diff file not found: {DIFF_PATH}")

    diff = DIFF_PATH.read_text(errors="replace")
    if not diff.strip():
        print("::notice::Empty diff, nothing to review.")
        _set_outputs(0, 0, 0)
        return

    meta = {}
    if META_PATH.exists():
        try:
            meta = json.loads(META_PATH.read_text())
        except json.JSONDecodeError:
            meta = {}

    agents_md = ""
    if AGENTS_MD_PATH.exists():
        agents_md = AGENTS_MD_PATH.read_text(errors="replace")

    # De-duplicate: skip posting if bot already reviewed this exact SHA.
    if existing_bot_review_for_sha():
        print(
            f"::notice::A bot review for {SHA[:7]} already exists on PR #{PR_NUMBER}; "
            "skipping to avoid duplicate comments."
        )
        _set_outputs(0, 0, 0)
        return

    valid_lines = parse_diff(diff)
    prompt, system = build_prompt(diff, meta, agents_md)
    result = call_llm(prompt, system)
    issues = result.get("issues", []) if isinstance(result, dict) else []
    if not isinstance(issues, list):
        die(f"LLM `issues` field was not a list: {result!r}")

    # Sanitise
    cleaned: list[dict] = []
    for i in issues:
        if not isinstance(i, dict):
            continue
        sev = (i.get("severity") or "").lower()
        if sev not in SEVERITY_ORDER:
            sev = "suggestion"
        i["severity"] = sev
        if isinstance(i.get("line"), str) and i["line"].isdigit():
            i["line"] = int(i["line"])
        if isinstance(i.get("start_line"), str) and i["start_line"].isdigit():
            i["start_line"] = int(i["start_line"])
        # Drop issues referencing files not in the diff at all
        path = i.get("path")
        if path and path not in valid_lines and i.get("line") is not None:
            # Likely off-base: demote to summary-only by clearing line.
            i["line"] = None
        cleaned.append(i)

    sugg, warn, crit = post_review(cleaned, valid_lines)
    print(f"::notice::Posted review on PR #{PR_NUMBER}: "
          f"{sugg} suggestion, {warn} warning, {crit} critical")
    _set_outputs(sugg, warn, crit)


def _set_outputs(sugg: int, warn: int, crit: int) -> None:
    out = os.environ.get("GITHUB_OUTPUT", "/dev/null")
    with open(out, "a") as f:
        for k, v in (
            ("suggestion_count", sugg),
            ("warning_count", warn),
            ("critical_count", crit),
        ):
            f.write(f"{k}={v}\n")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:  # noqa: BLE001
        die(f"unexpected error: {e!r}")