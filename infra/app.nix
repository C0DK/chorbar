{ config, lib, ... }:
{
  options.chorbar.envSecrets = lib.mkOption {
    type = lib.types.attrsOf lib.types.str;
    default = { };
    example = { BREVO_API_KEY = "brevo_api_key"; };
    description = ''
      Map env-var name → sops secret key. Each entry becomes a line in
      the env file rendered to /run/secrets/rendered/chorbar.env and
      injected into the chorbar-web container at boot.
    '';
  };

  config = {
    sops.secrets = lib.mapAttrs' (
      _: secretName: lib.nameValuePair secretName { }
    ) config.chorbar.envSecrets;

    sops.templates."chorbar.env".content = lib.concatStringsSep "\n" (
      lib.mapAttrsToList (
        envKey: secretName: "${envKey}=${config.sops.placeholder.${secretName}}"
      ) config.chorbar.envSecrets
    );

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
          OTEL_EXPORTER_OTLP_ENDPOINT = "http://host.containers.internal:4317";
        };
        environmentFiles = [ config.sops.templates."chorbar.env".path ];
        ports = [ "8080:8080" ];
      };
    };

    # The default NixOS firewall blocks 5432 inbound, including from the podman
    # bridge — so the chorbar-web container can't reach the host's postgres.
    # Trust the bridge: pg_hba.conf still gates auth at the postgres layer.
    # If you use a non-default podman network name, add it here.
    networking.firewall.trustedInterfaces = [ "podman0" ];
  };
}
