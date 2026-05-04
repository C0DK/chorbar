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

