{ config, lib, ... }:
{
  config = {
    services.postgresql = {
      enable = true;
      ensureDatabases = [ "chorbar" ];
      ensureUsers = [
        {
          name = "chorbar-pod";
          ensureClauses.login = true;
        }
      ];
      enableTCPIP = true;
      # 10.88.0.0/16 is podman's default bridge network — that's where the
      # chorbar-web container lives. Keep this tight; using `samenet` here
      # would also match the host's external interface, which on a VPS
      # means anyone in the same datacenter subnet.
      authentication = lib.mkOverride 10 ''
        #type database DBuser        origin-address  auth-method
        local all      all                           trust
        host  chorbar  chorbar-pod   127.0.0.1/32    trust
        host  chorbar  grafana       127.0.0.1/32    trust
        host  chorbar  chorbar-pod   ::1/128         trust
        host  chorbar  grafana       ::1/128         trust
        host  chorbar  chorbar-pod   10.88.0.0/16    trust
      '';
    };

    # The default NixOS firewall blocks 5432 inbound, including from the podman
    # bridge — so the chorbar-web container can't reach the host's postgres.
    # Trust the bridge: pg_hba.conf still gates auth at the postgres layer.
    # If you use a non-default podman network name, add it here.
    networking.firewall.trustedInterfaces = [ "podman0" ];

    # Apply sql/init.sql against the chorbar database on every boot.
    # init.sql uses CREATE ... IF NOT EXISTS so this is safe to re-run on
    # existing clusters, and on a fresh cluster it just creates everything.
    # We don't use services.postgresql.initialScript: nixpkgs only fires
    # that on the cluster's first startup, and it targets the `postgres`
    # database — not `chorbar`.
    systemd.services.chorbar-init-schema = {
      description = "Chorbar DB schema (idempotent)";
      wantedBy = [ "multi-user.target" ];
      # postgresql-setup creates the chorbar database and users; we must
      # run after it so the DB exists when we connect.
      after = [ "postgresql.service" "postgresql-setup.service" ];
      requires = [ "postgresql.service" "postgresql-setup.service" ];
      serviceConfig = {
        Type = "oneshot";
        User = "postgres";
        Group = "postgres";
        RemainAfterExit = true;
      };
      path = [ config.services.postgresql.finalPackage ];
      script = ''
        # postgresql.service transitions to active before the cluster
        # is fully accepting connections; wait briefly.
        for i in 1 2 3 4 5 6 7 8 9 10; do
          if psql -d postgres -tAc 'SELECT 1' >/dev/null 2>&1; then break; fi
          sleep 1
        done
        psql -v ON_ERROR_STOP=1 -d chorbar -f ${../sql/init.sql}
      '';
    };
  };
}
