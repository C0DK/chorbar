_ :
{
  services.alloy.enable = true;

  environment.etc."alloy/config.alloy".text = ''
    // Read chorbar-web container logs from the systemd journal.
    loki.source.journal "chorbar" {
      max_age    = "12h"
      matches    = "_SYSTEMD_UNIT=podman-chorbar-web.service"
      labels     = {"job" = "chorbar"}
      forward_to = [loki.process.chorbar.receiver]
    }

    // The app emits two lines per log event: a human-readable line and a
    // Serilog compact-JSON line. Drop the former, parse the latter.
    loki.process "chorbar" {
      stage.drop {
        expression = "^[^{]"
      }

      stage.json {
        // @l is the Serilog compact-JSON level field (absent for Information).
        expressions = {"level" = "@l"}
      }

      stage.labels {
        values = {"level" = "level"}
      }

      forward_to = [loki.write.local.receiver]
    }

    // Push to the local Loki instance.
    loki.write "local" {
      endpoint {
        url = "http://127.0.0.1:3100/loki/api/v1/push"
      }
    }
  '';
}
