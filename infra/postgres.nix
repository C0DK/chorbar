{ config, lib, ... }:
let
  # GRANT statements to run as the postgres superuser after the standard
  # ensureDatabases / ensureUsers setup. Idempotent. Lets chorbar-migrator
  # CREATE TABLE / SEQUENCE on the chorbar database without owning it.
  grantScript = ''
    psql -d chorbar <<'SQL'
    GRANT CREATE ON SCHEMA public TO chorbar-migrator;
    GRANT USAGE  ON SCHEMA public TO chorbar-pod;
    SQL
  '';
in
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
        {
          # Used by `nix run .#db-migrate`. Has CREATEDB so it can create
          # the chorbar database on a fresh cluster. The GRANTS that let
          # it CREATE on schema public are applied as part of the standard
          # postgresql-setup service below. Trusts pg_hba for TCP from
          # localhost only — never reachable from the podman bridge.
          name = "chorbar-migrator";
          ensureClauses = {
            login = true;
            createdb = true;
          };
        }
      ];
      enableTCPIP = true;
      # 10.88.0.0/16 is podman's default bridge network — that's where the
      # chorbar-web container lives. Keep this tight; using `samenet` here
      # would also match the host's external interface, which on a VPS
      # means anyone in the same datacenter subnet.
      authentication = lib.mkOverride 10 ''
        #type database DBuser             origin-address  auth-method
        local all      all                                 trust
        host  chorbar  chorbar-pod       127.0.0.1/32    trust
        host  chorbar  chorbar-migrator  127.0.0.1/32    trust
        host  chorbar  grafana           127.0.0.1/32    trust
        host  chorbar  chorbar-pod       ::1/128         trust
        host  chorbar  chorbar-migrator  ::1/128         trust
        host  chorbar  grafana           ::1/128         trust
        host  chorbar  chorbar-pod       10.88.0.0/16    trust
      '';
    };

    # The default NixOS firewall blocks 5432 inbound, including from the podman
    # bridge — so the chorbar-web container can't reach the host's postgres.
    # Trust the bridge: pg_hba.conf still gates auth at the postgres layer.
    # If you use a non-default podman network name, add it here.
    networking.firewall.trustedInterfaces = [ "podman0" ];

    # Append the chorbar GRANTS to the standard postgresql-setup script.
    # That service already runs as the postgres superuser after the
    # ensureDatabases / ensureUsers steps, so we get the privileges in
    # place before any other service can connect. We don't run init.sql
    # here — `nix run .#db-migrate` (or a manual `psql -f`) applies the
    # schema, and CREATE ... IF NOT EXISTS makes the apply idempotent.
    systemd.services.postgresql-setup.serviceConfig.ExecStartPost = [
      "/bin/sh" "-c" grantScript
    ];
  };
}
