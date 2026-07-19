#!/usr/bin/env python3
"""
AgentBox — LLM-assisted PR review.

Calls an OpenAI-compatible chat-completions endpoint (opencode Zen by default)
and posts severity-tagged inline review comments on the pull request.

AgentBox behaves like a senior engineer doing a human review, NOT a linter.
Formatting / style nits are already enforced by the project's existing tooling
(csharpier, dotnet-analyzers, black, statix, psqldef) and are NOT reported
here. Reviews run with temperature 0 so consecutive runs on the same diff
produce the same findings rather than snowballing new ones.

Severity levels:
  - suggestion : optional refactors, minor architectural improvements
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
from dataclasses import dataclass
from pathlib import Path

# Env reads happen lazily in `Config.from_env()` so a missing secret produces
# a friendly `die()` message from `main()` instead of a KeyError traceback at
# module import time.
SEVERITY_ORDER = {"suggestion": 0, "warning": 1, "critical": 2}
SEVERITY_EMOJI = {"suggestion": "💡", "warning": "⚠️", "critical": "🛑"}
REVIEW_MARKER = "<!-- agentbox sha:{sha} -->"
REVIEWER_NAME = "AgentBox"
DEFAULT_API_BASE = "https://opencode.ai/zen/v1"
DEFAULT_MODEL = "big-pickle"


@dataclass
class Config:
    repo: str
    pr_number: int
    sha: str
    diff_path: Path
    meta_path: Path
    api_base: str
    model: str
    api_key: str
    agents_md_path: Path

    @classmethod
    def from_env(cls) -> "Config":
        def required(name: str) -> str:
            val = os.environ.get(name)
            if not val:
                die(
                    f"required environment variable '{name}' is not set; "
                    f"check the workflow job env/secret configuration."
                )
            return val

        repo = required("REPO")
        pr_number = int(required("PR_NUMBER"))
        sha = required("SHA")
        diff_path = Path(required("DIFF_PATH"))
        meta_path = Path(required("META_PATH"))
        api_base = (
            os.environ.get("OPENCODE_API_BASE", DEFAULT_API_BASE).rstrip("/")
            or DEFAULT_API_BASE
        )
        model = os.environ.get("OPENCODE_MODEL") or DEFAULT_MODEL
        api_key = required("OPENCODE_API_KEY")
        agents_md_path = Path(os.environ.get("AGENTS_MD_PATH", "AGENTS.md"))
        return cls(
            repo,
            pr_number,
            sha,
            diff_path,
            meta_path,
            api_base,
            model,
            api_key,
            agents_md_path,
        )


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


def call_llm(cfg: Config, prompt: str, system: str) -> dict:
    url = f"{cfg.api_base}/chat/completions"
    payload = {
        "model": cfg.model,
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": prompt},
        ],
        # temperature=0 keeps successive runs on the same diff stable so the
        # review doesn't snowball new low-value findings every push.
        "temperature": 0,
        "response_format": {"type": "json_object"},
    }
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={
            "Authorization": f"Bearer {cfg.api_key}",
            "Content-Type": "application/json",
            "Accept": "application/json",
            # Cloudflare in front of the opencode Zen endpoint rejects the
            # default Python-urllib UA with HTTP 1010 (browser_signature_banned).
            # Send a conventional browser-like UA so the request reaches the API.
            "User-Agent": "agentbox/1.0 (github-actions; +https://opencode.ai)",
            "Accept-Language": "en-US,en;q=0.9",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=180) as resp:
            status = resp.status
            raw = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")[:800]
        die(f"AgentBox: Zen API HTTP {e.code} at {url}\nresponse body:\n{body}")
    except urllib.error.URLError as e:
        die(f"AgentBox: could not reach Zen API at {url}: {e}")

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
    """Tolerant JSON parser. Handles fenced ```json blocks, trailing
    commas, JS-style // comments, and embedded prose around the JSON
    object. Never raises — returns None on hard failure.
    """
    s = content.strip()

    # Strip a leading ```json or ``` fence and its closing fence.
    if s.startswith("```"):
        s = re.sub(r"^```(?:json)?\s*", "", s)
        # Remove the first closing ``` we find after that.
        fence = s.find("```")
        if fence != -1:
            s = s[:fence] + s[fence + 3 :]
    s = s.strip()

    # Some models wrap JSON in <json>…</json> tags.
    m = re.search(r"<json[^>]*>(.*?)</json>", s, re.DOTALL | re.IGNORECASE)
    if m:
        s = m.group(1).strip()

    # First attempt: as-is.
    try:
        return json.loads(s)
    except json.JSONDecodeError:
        pass

    # Second attempt: clean trailing commas and // line comments
    # outside of string literals.
    cleaned = _lenient_clean_json(s)
    try:
        return json.loads(cleaned)
    except json.JSONDecodeError:
        pass

    # Third attempt: find the outermost {...} block, then clean.
    start = s.find("{")
    end = s.rfind("}")
    if start != -1 and end != -1 and end > start:
        fragment = s[start : end + 1]
        cleaned = _lenient_clean_json(fragment)
        try:
            return json.loads(cleaned)
        except json.JSONDecodeError:
            pass

    # Last resort: dump content to logs for debugging.
    print("::warning::Could not parse LLM JSON response. Raw content:")
    print("---BEGIN RAW---")
    print(content[:4000])
    print("---END RAW---")
    return None


def _lenient_clean_json(s: str) -> str:
    """Remove trailing commas and // comments outside of string literals."""
    out = []
    i = 0
    in_str = False
    str_quote = ""
    while i < len(s):
        ch = s[i]
        if in_str:
            out.append(ch)
            if ch == "\\" and i + 1 < len(s):
                out.append(s[i + 1])
                i += 2
                continue
            if ch == str_quote:
                in_str = False
            i += 1
            continue
        if ch in ('"', "'"):
            in_str = True
            str_quote = ch
            out.append(ch)
            i += 1
            continue
        # // comment to end of line
        if ch == "/" and i + 1 < len(s) and s[i + 1] == "/":
            while i < len(s) and s[i] != "\n":
                i += 1
            continue
        # /* block comment */
        if ch == "/" and i + 1 < len(s) and s[i + 1] == "*":
            i += 2
            while i + 1 < len(s) and not (s[i] == "*" and s[i + 1] == "/"):
                i += 1
            i += 2
            continue
        # trailing comma before } or ]
        if ch == ",":
            j = i + 1
            while j < len(s) and s[j].isspace():
                j += 1
            if j < len(s) and s[j] in "}]":
                i += 1
                continue
        out.append(ch)
        i += 1
    return "".join(out)


def build_prompt(cfg: Config, diff: str, meta: dict, agents_md: str) -> tuple[str, str]:
    title = meta.get("title", "")
    body = meta.get("body", "") or ""
    files = [f.get("path") for f in meta.get("files", []) if f.get("path")]
    stats = f"+{meta.get('additions', 0)} -{meta.get('deletions', 0)}"

    system = (
        "You are AgentBox, a senior staff engineer reviewing a PR the way a "
        "thoughtful human reviewer would — not a linter. You read the diff "
        "in the context of the PR title/body and the project conventions, and "
        "you comment only on things that would actually change a reviewer's "
        "approve/request-changes decision.\n\n"
        "## What to focus on\n"
        "- Correctness: bugs, off-by-one, wrong type, broken contracts, "
        "missing error handling, race conditions, resource leaks.\n"
        "- Security: injection, auth/authz bypasses, secret leakage, unsafe "
        "deserialization, SSRF, XSS-path, missing CSRF.\n"
        "- Architecture: wrong layer, leaked abstraction, tight coupling "
        "introduced by the change, missing or wrong abstraction.\n"
        "- Tests: behaviour change with no test, test that doesn't actually "
        "assert the new behaviour, flaky-test risk.\n"
        "- Domain-rule violations against the AGENTS.md conventions.\n\n"
        "## What NOT to report (these are enforced by other tools)\n"
        "Do NOT comment on:\n"
        "- Whitespace, indentation, trailing newlines, line length, brace "
        "placement, trailing commas, formatting.\n"
        "- Identifier naming style (camelCase vs snake_case etc.), import "
        "ordering, comment style.\n"
        "- Missing types/annotations that a static analyser would catch.\n"
        "- Unused variables / dead code that an analyser flags.\n"
        "- Docstring/doc-comment presence or formatting.\n\n"
        "These are already enforced in this repo by: csharpier (C# format), "
        "dotnet-analyzers (C# static analysis), black .github/scripts "
        "(Python format), statix + nix flake check (Nix), psqldef + SQL "
        "schema validation (SQL).\n\n"
        "If you spot a recurring formatting/style pattern that NO existing "
        "linter in this repo covers, you may emit AT MOST ONE suggestion "
        'recommending the linter to add (e.g. "add ruff to CI for unused '
        'imports") — do NOT enumerate individual instances.\n\n'
        "## How to behave like a human reviewer\n"
        "- Prefer fewer, high-signal findings. A clean diff is a good "
        "outcome — return an empty issues list.\n"
        "- Each finding must explain the real-world consequence and a "
        "concrete fix, not just describe the code.\n"
        "- Stay anchored to the diff. Never fabricate files, symbols, or "
        "behaviour you cannot see.\n\n"
        "## Severity\n"
        "- suggestion : optional refactor or architectural improvement\n"
        "- warning    : correctness smell, missing test, risky path\n"
        "- critical   : bug, security hole, data loss, broken contract, "
        "broken build/auth\n\n"
        "## Output\n"
        "Respond with STRICT JSON only — no prose, no markdown fences — "
        "with this shape:\n"
        "{\n"
        '  "issues": [\n'
        "    {\n"
        '      "severity": "suggestion" | "warning" | "critical",\n'
        '      "path": "<file in \\"+++ b/...\\" form, no prefix>",\n'
        '      "line": <new-file line number where the issue lives, integer>,\n'
        '      "start_line": <optional, inclusive, for multi-line issues, '
        "integer>,\n"
        '      "rule": "<short stable id, e.g. sql-injection, missing-test, '
        'race>",\n'
        '      "title": "<one line, <=80 chars>",\n'
        '      "detail": "<2-5 sentences. Real-world consequence. Why. '
        'Concrete fix.>",\n'
        "    }\n"
        "  ]\n"
        "}\n\n"
        "Rules:\n"
        "- Only reference files and line numbers actually present in the diff.\n"
        "- If an issue doesn't map to a single diff line, omit `line`.\n"
        '- An empty {"issues": []} is valid and good when the diff is clean.\n'
        "- Never fabricate files, symbols, or behaviour you cannot see.\n"
    )

    project_context = ""
    if agents_md.strip():
        project_context = (
            "\n\nProject conventions (from AGENTS.md), apply them as "
            "authoritative review rules:\n```\n" + agents_md + "\n```\n"
        )

    prompt = (
        f"PR title: {title}\n"
        f"PR body:\n{body[:4000]}\n\n"
        f"Files changed: {', '.join(files)}\n"
        f"Diff stats: {stats}\n"
        f"Head SHA: {cfg.sha}{project_context}\n\n"
        "Unified diff to review:\n```diff\n"
        f"{diff[:120000]}\n"
        "```\n\n"
        "Return the JSON now."
    )
    return prompt, system


def existing_bot_review_for_sha(cfg: Config) -> bool:
    """Has the bot already posted a review for this exact SHA?"""
    marker = REVIEW_MARKER.format(sha=cfg.sha)
    try:
        out = gh(
            "api",
            f"--header=Accept: application/vnd.github+json",
            f"/repos/{cfg.repo}/pulls/{cfg.pr_number}/reviews",
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


def post_review(
    cfg: Config, issues: list[dict], valid_lines: dict[str, set[int]]
) -> tuple[int, int, int]:
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
        if (
            path
            and line is not None
            and path in valid_lines
            and int(line) in valid_lines[path]
        ):
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
        f"### {REVIEWER_NAME} review — `{cfg.sha[:7]}`",
        REVIEW_MARKER.format(sha=cfg.sha),
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
        "commit_id": cfg.sha,
        "event": "COMMENT",
        "body": "\n".join(summary_lines),
        "comments": inline,
    }
    payload_path = Path(".llm-review/review-payload.json")
    payload_path.write_text(json.dumps(payload))

    gh(
        "api",
        "--method",
        "POST",
        "--header=Accept: application/vnd.github+json",
        f"/repos/{cfg.repo}/pulls/{cfg.pr_number}/reviews",
        "--input",
        str(payload_path),
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
    cfg = Config.from_env()
    if not cfg.diff_path.exists():
        die(f"diff file not found: {cfg.diff_path}")

    diff = cfg.diff_path.read_text(errors="replace")
    if not diff.strip():
        print("::notice::Empty diff, nothing to review.")
        _set_outputs(0, 0, 0)
        return

    meta = {}
    if cfg.meta_path.exists():
        try:
            meta = json.loads(cfg.meta_path.read_text())
        except json.JSONDecodeError:
            meta = {}

    agents_md = ""
    if cfg.agents_md_path.exists():
        agents_md = cfg.agents_md_path.read_text(errors="replace")

    # De-duplicate: skip posting if bot already reviewed this exact SHA.
    if existing_bot_review_for_sha(cfg):
        print(
            f"::notice::A {REVIEWER_NAME} review for {cfg.sha[:7]} already "
            f"exists on PR #{cfg.pr_number}; skipping to avoid duplicate comments."
        )
        _set_outputs(0, 0, 0)
        return

    valid_lines = parse_diff(diff)
    prompt, system = build_prompt(cfg, diff, meta, agents_md)
    result = call_llm(cfg, prompt, system)
    issues = result.get("issues", []) if isinstance(result, dict) else []
    if not isinstance(issues, list):
        die(f"LLM `issues` field was not a list: {result!r}")

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
        path = i.get("path")
        if path and path not in valid_lines and i.get("line") is not None:
            i["line"] = None
        cleaned.append(i)

    sugg, warn, crit = post_review(cfg, cleaned, valid_lines)
    print(
        f"::notice::Posted {REVIEWER_NAME} review on PR #{cfg.pr_number}: "
        f"{sugg} suggestion, {warn} warning, {crit} critical"
    )
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
