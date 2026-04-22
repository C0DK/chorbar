{ lib, ... }:
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
}
