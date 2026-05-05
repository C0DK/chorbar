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

  # Persist the ASP.NET Core Data Protection keyring across container
  # restarts. Without this, the in-memory keyring is regenerated on every
  # restart and all auth cookies become undecryptable (everyone is logged
  # out). Rootful podman runs the container as host root, so root:root
  # ownership matches the in-container uid.
  systemd.tmpfiles.rules = [
    "d /var/lib/chorbar          0755 root root - -"
    "d /var/lib/chorbar/keys     0700 root root - -"
  ];

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
        DATA_PROTECTION_KEY_DIR = "/var/lib/chorbar/keys";
      };
      environmentFiles = [ "/etc/chorbar/app.env" ];
      ports = [ "8080:8080" ];
      volumes = [
        "/var/lib/chorbar/keys:/var/lib/chorbar/keys"
      ];
    };
  };

  # The default NixOS firewall blocks 5432 inbound, including from the podman
  # bridge — so the chorbar-web container can't reach the host's postgres.
  # Trust the bridge: pg_hba.conf still gates auth at the postgres layer.
  # If you use a non-default podman network name, add it here.
  networking.firewall.trustedInterfaces = [ "podman0" ];
}
