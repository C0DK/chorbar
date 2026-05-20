{ lib, pkgs, config, ... }:
{
  services.postgresql = {
    enable = true;
    ensureDatabases = [ "chorbar" ];
    ensureUsers = [
      {
        name = "chorbar-pod";
        ensureClauses.login = true;
      }
      {
        name = "chorbar-admin";
        ensureClauses = {
          login = true;
          superuser = true;
        };
      }
      {
        name = "grafana";
        ensureClauses.login = true;
      }
    ];
    enableTCPIP = true;
    # 10.88.0.0/16 is podman's default bridge network — that's where the
    # chorbar-web container lives. Keep this tight; using `samenet` here
    # would also match the host's external interface, which on a VPS
    # means anyone in the same datacenter subnet.
    authentication = lib.mkOverride 10 ''
      #type database DBuser   origin-address  auth-method
      local all      all                      trust
      host  all      all      127.0.0.1/32    trust
      host  chorbar  all      10.88.0.0/16    trust
    '';
  };

  # Grant grafana SELECT on all current and future tables in chorbar.
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

  # https://bkiran.com/blog/deploying-containers-nixos
  virtualisation.podman.enable = true;
  virtualisation.oci-containers = {
    backend = "podman";
    containers.chorbar-web = {
      image = "chorbar:main";
      environment = {
        DEV_MODE = "false";
        ASPNETCORE_FORWARDEDHEADERS_ENABLED = "true";
        DB_CONNECTION_STRING = "Host=host.containers.internal;Username=chorbar-pod;Database=chorbar";
      };
      environmentFiles = [ "/etc/chorbar/app.env" ];
      ports = [ "8080:8080" ];
    };
  };

  # The default NixOS firewall blocks 5432 inbound, including from the podman
  # bridge — so the chorbar-web container can't reach the host's postgres.
  # Trust the bridge: pg_hba.conf still gates auth at the postgres layer.
  # If you use a non-default podman network name, add it here.
  networking.firewall.trustedInterfaces = [ "podman0" ];
  networking.firewall.allowedTCPPorts = [ 3000 ];

  # ── Loki: log storage ─────────────────────────────────────────────────────
  services.loki = {
    enable = true;
    configuration = {
      server.http_listen_port = 3100;
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

  # ── Promtail: ship chorbar-web container logs → Loki ──────────────────────
  services.promtail = {
    enable = true;
    configuration = {
      server = {
        http_listen_port = 9080;
        grpc_listen_port = 0;
      };
      clients = [ { url = "http://localhost:3100/loki/api/v1/push"; } ];
      scrape_configs = [
        {
          job_name = "chorbar";
          journal = {
            max_age = "12h";
            labels.job = "chorbar";
          };
          relabel_configs = [
            {
              source_labels = [ "__journal__systemd_unit" ];
              target_label = "unit";
            }
            {
              # Keep only the chorbar-web container's stdout/stderr.
              source_labels = [ "__journal__systemd_unit" ];
              regex = "podman-chorbar-web\\.service";
              action = "keep";
            }
          ];
          pipeline_stages = [
            # The app writes two lines per log event: a human-readable line
            # and a Serilog compact-JSON line. Drop the non-JSON lines so
            # only structured entries reach Loki.
            { drop.expression = "^[^{]"; }
            {
              json.expressions = {
                level = "@l";
                message = "@m";
              };
            }
            # Promote the parsed level to a Loki label for filtering.
            { labels.level = ""; }
          ];
        }
      ];
    };
  };

  # ── Grafana ────────────────────────────────────────────────────────────────
  # Default credentials are admin / admin — change on first login.
  services.grafana = {
    enable = true;
    settings.server = {
      http_port = 3000;
      domain = "localhost";
    };
    provision = {
      enable = true;
      datasources.settings = {
        apiVersion = 1;
        datasources = [
          {
            name = "Loki";
            type = "loki";
            url = "http://localhost:3100";
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
}
