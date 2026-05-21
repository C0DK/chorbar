_ :
{
  services.loki = {
    enable = true;
    configuration = {
      server = {
        http_listen_address = "127.0.0.1";
        http_listen_port = 3100;
        grpc_listen_address = "127.0.0.1";
        grpc_listen_port = 9096;
      };
      auth_enabled = false;
      # Inter-component gRPC traffic stays on loopback. Without this,
      # frontend/scheduler/querier register their external IP and then
      # fail to dial each other (grpc only binds 127.0.0.1).
      common = {
        instance_addr = "127.0.0.1";
        ring = {
          instance_addr = "127.0.0.1";
          kvstore.store = "inmemory";
        };
        replication_factor = 1;
      };
      ingester = {
        chunk_idle_period = "5m";
        chunk_retain_period = "30s";
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
}
