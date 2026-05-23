{ config, lib, ... }:
{
  imports = [
    ./loki.nix
    ./alloy.nix
    ./prometheus.nix
  ];

  # Grafana cookie-signing key + admin password. Decrypted at boot to
  # /run/secrets/<name>, owned by grafana.
  sops.secrets.grafana_secret_key = {
    owner = "grafana";
    group = "grafana";
    mode = "0400";
  };
  sops.secrets.grafana_admin_password = {
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
        admin_password = "$__file{${config.sops.secrets.grafana_admin_password.path}}";
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
            name = "Prometheus";
            type = "prometheus";
            url = "http://127.0.0.1:9090";
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

        -- Revoke any prior broad grants from earlier setups.
        REVOKE ALL ON ALL TABLES IN SCHEMA public FROM grafana;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public
          REVOKE SELECT ON TABLES FROM grafana;

        -- Event metadata only. Payload (JSONB) may contain PII / emails.
        GRANT SELECT (household_id, version, timestamp, created_by)
          ON household_event TO grafana;

        -- Sign-in visibility without exposing the OTP code.
        GRANT SELECT (email, created) ON signin_otp TO grafana;

        -- data_protection_key: no grants. XML column is encryption material.
      SQL
    '';
  };
}
