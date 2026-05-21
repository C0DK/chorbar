{ config, lib, ... }:
{
  imports = [
    ./loki.nix
    ./alloy.nix
  ];

  # Grafana cookie-signing key. Decrypted at boot to
  # /run/secrets/grafana_secret_key, owned by grafana.
  sops.secrets.grafana_secret_key = {
    owner = "grafana";
    group = "grafana";
    mode = "0400";
  };

  services.postgresql.ensureUsers = [
    {
      name = "grafana";
      ensureClauses.login = true;
    }
  ];

  services.grafana = {
    enable = true;
    settings = {
      server = {
        http_port = 3000;
        domain = lib.mkDefault "localhost";
        enable_gzip = true;
      };
      security = {
        secret_key = "$__file{${config.sops.secrets.grafana_secret_key.path}}";
        disable_gravatar = true;
        # Override to true in host config when Grafana is behind HTTPS.
        cookie_secure = lib.mkDefault false;
      };
      users = {
        allow_sign_up = false;
        allow_org_create = false;
        default_theme = "dark";
      };
      "auth.anonymous".enabled = false;
    };
    provision = {
      enable = true;
      datasources.settings = {
        apiVersion = 1;
        datasources = [
          {
            name = "Loki";
            type = "loki";
            url = "http://127.0.0.1:3100";
            isDefault = true;
          }
          {
            name = "Chorbar DB";
            type = "postgres";
            url = "localhost:5432";
            user = "grafana";
            jsonData = {
              database = "chorbar";
              sslmode = "disable";
            };
          }
        ];
      };
    };
  };

  # Runs on every boot; all statements are idempotent in PostgreSQL.
  systemd.services.chorbar-grafana-db-grants = {
    description = "Grant grafana readonly access to chorbar DB";
    wantedBy = [ "multi-user.target" ];
    after = [ "postgresql.service" ];
    requires = [ "postgresql.service" ];
    serviceConfig = {
      Type = "oneshot";
      RemainAfterExit = true;
      User = "postgres";
    };
    script = ''
      ${config.services.postgresql.package}/bin/psql -d chorbar <<'SQL'
        GRANT CONNECT ON DATABASE chorbar TO grafana;
        GRANT USAGE ON SCHEMA public TO grafana;
        GRANT SELECT ON ALL TABLES IN SCHEMA public TO grafana;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO grafana;
      SQL
    '';
  };
}
