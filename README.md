# Chorbar

Chore management system.

## Run with NixOs as flake

In your NixOS flake:

```nix
{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    chorbar.url = "github:C0DK/chorbar";
  };

  outputs = { self, nixpkgs, chorbar, ... }: {
    nixosConfigurations.your-host = nixpkgs.lib.nixosSystem {
      system = "x86_64-linux";
      modules = [
        ./your-existing-config.nix
        chorbar.nixosModules.default
      ];
    };
  };
}
```

Then:

```sh
sudo mkdir -p /etc/chorbar
sudo touch /etc/chorbar/app.env       # or fill with connection-string overrides
sudo nixos-rebuild switch --flake .#your-host
curl http://localhost:8080/
```

`/etc/chorbar/app.env` must exist or systemd fails the chorbar-web
service — empty is fine if you don't need to override defaults.

## What the module sets up

- **`services.postgresql`** — `chorbar` database, `chorbar-pod` login
  role, TCP enabled on 127.0.0.1 with trust auth for local connections.
- **`virtualisation.podman`** + an `oci-containers.chorbar-web`
  container running `chorbar:main` from the locally-built image
  tarball. Exposes port `8080` on the host.

The module does **not** open the firewall, configure SSH, set up
networking, or define users — that's intentionally left to the host
config that imports it.

## Observability (optional)

`chorbar.nixosModules.observability` adds Grafana (port 3000), Loki
(log storage), and Grafana Alloy (ships container logs from journald to
Loki). It also creates a `grafana` PostgreSQL role with read-only
(`SELECT`) access to the chorbar database.

Import it alongside the default module:

```nix
modules = [
  ./your-existing-config.nix
  chorbar.nixosModules.default
  chorbar.nixosModules.observability   # ← add this
];
```

### Pre-deployment: create secrets

Grafana reads its admin password and cookie-signing key from files at
runtime so they are never written to the Nix store.

```sh
sudo mkdir -p /etc/grafana

# Choose a strong admin password.
read -rs -p "Grafana admin password: " pw
printf '%s' "$pw" | sudo tee /etc/grafana/admin_password > /dev/null

# 48-character random secret key for session signing.
LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48 \
  | sudo tee /etc/grafana/secret_key > /dev/null

sudo chown grafana:grafana /etc/grafana/admin_password /etc/grafana/secret_key
sudo chmod 600 /etc/grafana/admin_password /etc/grafana/secret_key
```

> **Note:** The `grafana` OS user is created by `nixos-rebuild switch`.
> Run the `chown` step after your first rebuild, then `systemctl restart
> grafana` to pick up the files.

### What the observability module sets up

- **`services.loki`** — log storage on `127.0.0.1:3100` (not public).
- **`services.alloy`** — Grafana Alloy reads `podman-chorbar-web.service`
  journal entries, drops human-readable lines, parses Serilog compact-JSON,
  and extracts `@l` as a `level` label before shipping to Loki.
- **`services.grafana`** — listens on `:3000` with two pre-provisioned
  datasources: *Loki* (default) and *Chorbar DB* (read-only Postgres).
  Sign-up and anonymous access are disabled.
- **`chorbar-grafana-db-grants`** — oneshot systemd service that grants
  `CONNECT`, `USAGE`, and `SELECT` on all current and future chorbar
  tables to the `grafana` role.
- Firewall: opens TCP 3000. Loki and Alloy bind to `127.0.0.1` only.

### Hardening checklist

- [ ] Change Grafana admin password on first login (`/profile`).
- [ ] Put Grafana behind a TLS reverse proxy (nginx, Caddy) and then
  set `services.grafana.settings.security.cookie_secure = true` and
  remove port 3000 from `allowedTCPPorts`.

