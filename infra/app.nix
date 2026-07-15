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
  };
}
