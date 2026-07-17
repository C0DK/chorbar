{
  description = "Chorbar — Chore management system - full deployment with database";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    sops-nix = {
      url = "github:Mic92/sops-nix";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs =
    {
      self,
      nixpkgs,
      sops-nix,
    }:
    let
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
      inherit (pkgs) lib;

      # MS .NET 10 SDK as a base layer. Building dotnet from source via
      # nixpkgs needs ~80 GB of disk, so we pull MS's official image and
      # publish on top of it at container start.
      sdkImage = pkgs.dockerTools.pullImage {
        imageName = "mcr.microsoft.com/dotnet/sdk";
        imageDigest = "sha256:6d7f69bc7bc9d4510ca255977b1f53ce52a79307e048a91450b2aecd63627cc3";
        finalImageName = "mcr.microsoft.com/dotnet/sdk";
        finalImageTag = "10.0";
        sha256 = lib.removeSuffix "\n" (builtins.readFile ./infra/sdk-image.x86_64-linux.sha256);
      };

      src = lib.cleanSourceWith {
        src = ./.;
        filter =
          path: _:
          let
            base = baseNameOf (toString path);
          in
          !lib.elem base [
            "bin"
            "obj"
            ".git"
            "result"
          ];
      };

      # Stable per-source-content marker. Different source = different store
      # path = different marker, which triggers a republish in the entrypoint.
      srcMarker = "${src}";

      entrypoint = pkgs.writeShellScript "chorbar-entrypoint" ''
        set -euo pipefail
        export HOME=/tmp
        export DOTNET_CLI_TELEMETRY_OPTOUT=1
        PUBLISH=/var/lib/chorbar/publish
        IMAGE_MARKER=/etc/chorbar/image-id
        CACHE_MARKER="$PUBLISH/.image-id"

        # Republish if first run OR if the image's source has changed since
        # the last publish (so a new image bump invalidates the cached publish
        # in the writable layer / persistent volume).
        if [ ! -f "$PUBLISH/Chorbar.dll" ] \
           || [ ! -f "$CACHE_MARKER" ] \
           || ! cmp -s "$IMAGE_MARKER" "$CACHE_MARKER"; then
          rm -rf "$PUBLISH"
          mkdir -p "$PUBLISH"
          rm -rf /tmp/build
          cp -r /app /tmp/build
          cd /tmp/build
          dotnet publish Chorbar/Chorbar.csproj -c Release -o "$PUBLISH"
          cp "$IMAGE_MARKER" "$CACHE_MARKER"
        fi

        cd "$PUBLISH"
        exec dotnet "$PUBLISH/Chorbar.dll" "$@"
      '';

      dockerImage = pkgs.dockerTools.buildImage {
        name = "chorbar";
        tag = "main";
        fromImage = sdkImage;
        copyToRoot = pkgs.runCommand "chorbar-root" { } ''
          mkdir -p $out/app $out/etc/chorbar
          cp -r ${src}/. $out/app/
          # Build-time marker — busts the runtime publish cache when source changes.
          echo "${srcMarker}" > $out/etc/chorbar/image-id
        '';
        config = {
          Entrypoint = [ "${entrypoint}" ];
          ExposedPorts."8080/tcp" = { };
          WorkingDir = "/var/lib/chorbar/publish";
          Env = [
            "ASPNETCORE_URLS=http://+:8080"
            "ASPNETCORE_CONTENTROOT=/var/lib/chorbar/publish"
          ];
        };
      };
    in
    {
      packages.${system}.dockerImage = dockerImage;

      # CI / manual entry point: apply sql/init.sql to the chorbar database.
      # Pass a libpq conninfo string as the first arg; defaults connect over
      # TCP to localhost as the chorbar-migrator role, which has CREATEDB
      # and is trusted on 127.0.0.1/::1 by the postgres module.
      # Usage: nix run .#db-migrate -- "host=localhost user=chorbar-migrator dbname=chorbar"
      apps.${system}.db-migrate = {
        type = "app";
        program = toString (pkgs.writeShellScript "db-migrate" ''
          set -euo pipefail
          conn="''${1:-host=localhost user=chorbar-migrator dbname=chorbar}"
          psql_chorbar() { ${pkgs.postgresql}/bin/psql -v ON_ERROR_STOP=1 "$conn" "$@"; }
          psql_chorbar -tAc "SELECT 1 FROM pg_database WHERE datname='chorbar'" | grep -q 1 \
            || psql_chorbar -c "CREATE DATABASE chorbar"
          exec ${pkgs.postgresql}/bin/psql -v ON_ERROR_STOP=1 "$conn" -f ${./sql/init.sql}
        '');
        meta = {
          description = "Apply sql/init.sql to the chorbar database (idempotent).";
          longDescription = ''
            Pass a libpq conninfo string as the first arg to target a
            remote host. Defaults to TCP localhost as the chorbar-migrator
            role, which the postgres module grants CREATEDB and trusts
            on 127.0.0.1/::1.
          '';
        };
      };

      nixosModules.default =
        { ... }:
        {
          imports = [
            sops-nix.nixosModules.sops
            ./infra/app.nix
            ./infra/postgres.nix
          ];
          virtualisation.oci-containers.containers.chorbar-web.imageFile = dockerImage;
        };

      # Optional: add this module alongside nixosModules.default to enable
      # Grafana + Loki + Alloy log collection. See README for setup.
      nixosModules.observability =
        { ... }:
        {
          imports = [ ./infra/observability.nix ];
        };
    };
}
