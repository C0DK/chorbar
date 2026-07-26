# Chorbar

Chore management system.

## Run with NixOS as flake

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
sudo nixos-rebuild switch --flake .#your-host
curl http://localhost:8080/
```

## App-level secrets (Brevo, etc.)

Point `chorbar.envFile` at an env file (`KEY=VALUE` lines) to inject it
into the chorbar-web container at boot; leave it `null` (the default)
to skip:

```nix
chorbar.envFile = "/run/secrets/chorbar.env";
```

The module is agnostic about how that file gets produced. Render it
with whatever secret manager your host config uses — sops-nix
`sops.templates`, agenix, etc.

## What the module sets up

- **`services.postgresql`** — `chorbar` database, `chorbar-pod` login
  role, TCP enabled on 127.0.0.1 with trust auth for local connections.
- **`virtualisation.podman`** + an `oci-containers.chorbar-web`
  container running `ghcr.io/c0dk/chorbar:latest`. Exposes port `8080`
  on the host.

The module does **not** open the firewall, configure SSH, set up
networking, or define users — that's intentionally left to the host
config that imports it.

## Container image

The image is built by `.github/workflows/build-and-push.yml` (a
multi-stage `Containerfile`) and pushed to `ghcr.io/c0dk/chorbar` on
every push to `main`. Two tags are produced: `:latest` and `:<git-sha>`.
No Nix tooling is required to build or consume the image.

## `db-migrate` and `db-diff` apps

The flake exposes two Nix apps for schema management via `psqldef`:

```sh
nix run .#db-migrate          # apply sql/schema.sql to the live DB
nix run .#db-diff             # dry-run: show DDL psqldef would apply
```

Connection is controlled via libpq env vars (`PGHOST`, `PGPORT`,
`PGUSER`, `PGDATABASE`). Defaults target localhost as the
`chorbar-migrator` role.