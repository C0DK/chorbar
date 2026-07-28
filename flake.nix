{
  description = "Chorbar — Chore management system - full deployment with database";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs =
    {
      self,
      nixpkgs,
    }:
    let
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
    in
    {
      apps.${system} = {
        db-migrate = {
          type = "app";
          program = toString (
            pkgs.writeShellScript "db-migrate" ''
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
            ''
          );
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
          program = toString (
            pkgs.writeShellScript "db-diff" ''
              set -euo pipefail
              exec ${pkgs.sqldef}/bin/psqldef \
                --host="''${PGHOST:-localhost}" \
                --port="''${PGPORT:-5432}" \
                --user="''${PGUSER:-chorbar-migrator}" \
                --dry-run \
                --file=${./sql/schema.sql} \
                "''${PGDATABASE:-chorbar}"
            ''
          );
          meta = {
            description = "Show the DDL psqldef would apply to bring the chorbar database to the desired schema.";
          };
        };
      };

      nixosModules.default =
        { ... }:
        {
          imports = [ ./infra/app.nix ];
        };
    };
}
