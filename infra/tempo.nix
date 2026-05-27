{ config, pkgs, ... }:
{
  services.tempo = {
    enable = true;
    settings = {
      server = {
        http_listen_address = "127.0.0.1";
        http_listen_port = 3200;
        grpc_listen_address = "127.0.0.1";
        grpc_listen_port = 9097;
      };
      distributor.receivers = {
        otlp.protocols = {
          grpc.endpoint = "0.0.0.0:4317";
          http.endpoint = "0.0.0.0:4318";
        };
      };
      storage.trace = {
        backend = "local";
        local.path = "/var/lib/tempo/traces";
        wal.path = "/var/lib/tempo/wal";
      };
      metrics_generator = {
        storage = {
          path = "/var/lib/tempo/metrics";
          remote_write = [
            { url = "http://127.0.0.1:9090/api/v1/write"; }
          ];
        };
      };
    };
  };
}
