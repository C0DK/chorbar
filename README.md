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

### Pre-deployment: secrets via sops-nix

The observability module declares `sops.secrets.grafana_secret_key` for
Grafana's cookie-signing key. sops-nix is wired in transitively from
chorbar's flake — you don't add it as a separate input — but the host
needs an age key and an encrypted secrets file.

1. **Generate an age key on the host** (one-time, per machine):

   ```sh
   sudo mkdir -p /var/lib/sops-nix
   sudo nix shell nixpkgs#age -c \
     age-keygen -o /var/lib/sops-nix/key.txt
   sudo nix shell nixpkgs#age -c \
     age-keygen -y /var/lib/sops-nix/key.txt   # prints the public key
   ```

2. **Create `secrets.yaml`** in your config repo, encrypted to that
   public key:

   ```sh
   SECRET=$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 48)
   nix shell nixpkgs#sops -c sops --age <pubkey-from-step-1> \
     --set '["grafana_secret_key"] "'"$SECRET"'"' secrets.yaml
   ```

3. **Point sops-nix at the file and key** in your host config:

   ```nix
   sops.defaultSopsFile = ./secrets.yaml;
   sops.age.keyFile = "/var/lib/sops-nix/key.txt";
   ```

At boot sops-nix decrypts `grafana_secret_key` to
`/run/secrets/grafana_secret_key` (owned by `grafana`, mode `0400`).
Grafana reads it via `$__file{...}`. Rotate by re-encrypting
`secrets.yaml` with a new value and redeploying.

### What the observability module sets up

- **`services.loki`** — log storage on `127.0.0.1:3100` (not public).
- **`services.alloy`** — Grafana Alloy reads `podman-chorbar-web.service`
  journal entries, drops human-readable lines, parses Serilog compact-JSON,
  and extracts `@l` as a `level` label before shipping to Loki.
- **`services.grafana`** — listens on `:3000` with two pre-provisioned
  datasources: *Loki* (default) and *Chorbar DB* (read-only Postgres).
  Sign-up and anonymous access are disabled.
- **`sops.secrets.grafana_secret_key`** — declared but not encrypted by
  chorbar; you supply the encrypted value (see above).
- **`chorbar-grafana-db-grants`** — oneshot systemd service that grants
  `CONNECT`, `USAGE`, and `SELECT` on all current and future chorbar
  tables to the `grafana` role.

The module does **not** open the firewall. Add `networking.firewall.
allowedTCPPorts = [ 3000 ];` in your host config if you want Grafana
reachable from outside. Loki and Alloy bind to `127.0.0.1` only.

### Hardening checklist

- [ ] Change Grafana admin password on first login (`/profile`).
- [ ] Put Grafana behind a TLS reverse proxy (nginx, Caddy) and then
  set `services.grafana.settings.security.cookie_secure = true`.

