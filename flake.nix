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

      apps.${system} = {
        db-migrate = {
          type = "app";
          program = toString (pkgs.writeShellScript "db-migrate" ''
            set -euo pipefail
            export PGHOST="''${PGHOST:-localhost}"
            export PGPORT="''${PGPORT:-5432}"
            export PGUSER="''${PGUSER:-chorbar-migrator}"
            export PGDATABASE="''${PGDATABASE:-chorbar}"
            unset PGDATABASE
            ${pkgs.postgresql}/bin/psql -v ON_ERROR_STOP=1 -tAc \
              "SELECT 1 FROM pg_database WHERE datname='chorbar'" | grep -q 1 \
              || ${pkgs.postgresql}/bin/psql -v ON_ERROR_STOP=1 \
                -c "CREATE DATABASE chorbar"
            export PGDATABASE="''${PGDATABASE:-chorbar}"
            ${pkgs.postgresql}/bin/psql -v ON_ERROR_STOP=1 -c \
              'ALTER DATABASE chorbar REFRESH COLLATION VERSION;' 2>/dev/null || true
            exec ${pkgs.sqldef}/bin/psqldef \
              --host="$PGHOST" \
              --port="$PGPORT" \
              --user="$PGUSER" \
              --apply \
              --file=${./sql/schema.sql} \
              "$PGDATABASE"
          '');
          meta = {
            description = "Apply sql/schema.sql to the chorbar database via psqldef.";
            longDescription = ''
              Diffs the desired schema in sql/schema.sql against the live
              database and applies only the DDL needed to bring the DB to
              the desired state. Idempotent: re-running on an already-equal
              schema is a no-op.

              Connection is controlled via libpq env vars (PGHOST, PGPORT,
              PGUSER, PGDATABASE). Defaults target localhost as the
              chorbar-migrator role. The chorbar-init-schema oneshot (in
              infra/app.nix) grants chorbar-migrator CREATE on schema
              public. chorbar-pod's read/write privileges are applied via
              sql/grants.sql.
            '';
          };
        };

        db-diff = {
          type = "app";
          program = toString (pkgs.writeShellScript "db-diff" ''
            set -euo pipefail
            exec ${pkgs.sqldef}/bin/psqldef \
              --host="''${PGHOST:-localhost}" \
              --port="''${PGPORT:-5432}" \
              --user="''${PGUSER:-chorbar-migrator}" \
              --dry-run \
              --file=${./sql/schema.sql} \
              "''${PGDATABASE:-chorbar}"
          '');
          meta = {
            description = "Show the DDL psqldef would apply to bring the chorbar database to the desired schema.";
          };
        };
      };

      nixosModules.default =
        { ... }:
        {
          imports = [
            sops-nix.nixosModules.sops
            ./infra/app.nix
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
