{ config, ... }:
{
  # ── PostgreSQL: grafana readonly role ──────────────────────────────────────

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

  # ── Loki: log storage (localhost only) ────────────────────────────────────
  services = {
    postgresql.ensureUsers = [
      {
        name = "grafana";
        ensureClauses.login = true;
      }
    ];
    loki = {
      enable = true;
      configuration = {
        server = {
          http_listen_address = "127.0.0.1";
          http_listen_port = 3100;
          grpc_listen_address = "127.0.0.1";
          grpc_listen_port = 9096;
        };
        auth_enabled = false;
        ingester = {
          lifecycler = {
            address = "127.0.0.1";
            ring = {
              kvstore.store = "inmemory";
              replication_factor = 1;
            };
          };
          chunk_idle_period = "5m";
          chunk_retain_period = "30s";
          max_transfer_retries = 0;
        };
        schema_config.configs = [
          {
            from = "2024-01-01";
            store = "tsdb";
            object_store = "filesystem";
            schema = "v13";
            index = {
              prefix = "index_";
              period = "24h";
            };
          }
        ];
        storage_config = {
          tsdb_shipper = {
            active_index_directory = "/var/lib/loki/tsdb-index";
            cache_location = "/var/lib/loki/tsdb-cache";
          };
          filesystem.directory = "/var/lib/loki/chunks";
        };
        limits_config = {
          allow_structured_metadata = false;
          reject_old_samples = true;
          reject_old_samples_max_age = "168h";
        };
        compactor = {
          working_directory = "/var/lib/loki/compactor";
          compaction_interval = "10m";
        };
      };
    };

    # ── Grafana Alloy: ship chorbar-web container logs → Loki ─────────────────
    # Alloy replaces the deprecated Promtail agent. Configuration is in
    # River/Alloy syntax at /etc/alloy/config.alloy.
    alloy.enable = true;

    # ── Grafana ────────────────────────────────────────────────────────────────
    # Secrets are read at runtime from /etc/grafana/ — never stored in the
    # Nix store. See README for pre-deployment setup.
    grafana = {
      enable = true;
      settings = {
        server = {
          http_port = 3000;
          domain = "localhost";
        };
        security = {
          admin_password = "$__file{/etc/grafana/admin_password}";
          secret_key = "$__file{/etc/grafana/secret_key}";
          disable_gravatar = true;
          # Enable cookie_secure once Grafana is behind HTTPS.
          cookie_secure = false;
        };
        users = {
          allow_sign_up = false;
          allow_org_create = false;
          default_theme = "dark";
        };
        "auth.anonymous" = {
          enabled = false;
        };
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
  };

  environment.etc."alloy/config.alloy".text = ''
    // Read chorbar-web container logs from the systemd journal.
    loki.source.journal "chorbar" {
      max_age    = "12h"
      matches    = "_SYSTEMD_UNIT=podman-chorbar-web.service"
      labels     = {"job" = "chorbar"}
      forward_to = [loki.process.chorbar.receiver]
    }

    // The app emits two lines per log event: a human-readable line and a
    // Serilog compact-JSON line. Drop the former, parse the latter.
    loki.process "chorbar" {
      stage.drop {
        expression = "^[^{]"
      }

      stage.json {
        // @l is the Serilog compact-JSON level field (absent for Information).
        expressions = {"level" = "@l"}
      }

      stage.labels {
        values = {"level" = "level"}
      }

      forward_to = [loki.write.local.receiver]
    }

    // Push to the local Loki instance.
    loki.write "local" {
      endpoint {
        url = "http://127.0.0.1:3100/loki/api/v1/push"
      }
    }
  '';

  # Open port 3000 for Grafana. Consider fronting with a TLS reverse proxy
  # (nginx/caddy) and removing this once HTTPS is in place.
  networking.firewall.allowedTCPPorts = [ 3000 ];
}
