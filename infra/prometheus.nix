_ :
{
  services.prometheus = {
    enable = true;
    listenAddress = "127.0.0.1";
    port = 9090;

    # Keep retention modest; this is for local Grafana, not long-term storage.
    retentionTime = "30d";

    globalConfig = {
      scrape_interval = "15s";
      evaluation_interval = "15s";
    };

    scrapeConfigs = [
      {
        job_name = "chorbar";
        metrics_path = "/metrics";
        static_configs = [
          {
            # chorbar-web is a podman container that publishes 8080 to the
            # host, so prometheus scrapes it over loopback. The app exposes
            # /metrics via prometheus-net (see MetricsMiddleware).
            targets = [ "127.0.0.1:8080" ];
            labels.app = "chorbar";
          }
        ];
      }
      {
        # Prometheus self-scrape — useful for spotting scrape failures
        # in Grafana.
        job_name = "prometheus";
        static_configs = [
          {
            targets = [ "127.0.0.1:9090" ];
          }
        ];
      }
    ];
  };
}
