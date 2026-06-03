{ config, lib, pkgs, ... }:
let
  cfg = config.chorbar.agentbox;
in
{
  options.chorbar.agentbox = {
    enable = lib.mkEnableOption "agentbox — isolated opencode sandbox container";

    workDir = lib.mkOption {
      type = lib.types.path;
      default = "/var/lib/agentbox/work";
      description = "Host directory bind-mounted into the container as /work.";
    };

    externalInterface = lib.mkOption {
      type = lib.types.str;
      default = "eth0";
      description = "VPS external interface used for NAT'ing the container's egress.";
    };

    hostAddress = lib.mkOption {
      type = lib.types.str;
      default = "10.231.0.1";
      description = "veth IP on the host side.";
    };

    localAddress = lib.mkOption {
      type = lib.types.str;
      default = "10.231.0.2";
      description = ''
        veth IP on the container side. opencode's web UI binds to
        this address on port 4096; the VPS reaches it directly via
        the veth without any port forwarding.
      '';
    };

    dnsServers = lib.mkOption {
      type = lib.types.listOf lib.types.str;
      default = [ "1.1.1.1" "9.9.9.9" ];
      description = ''
        Public DNS resolvers the container uses. Using public DNS keeps
        the container from depending on (or probing) the host's resolver.
      '';
    };
  };

  config = lib.mkIf cfg.enable {
    systemd.tmpfiles.rules = [
      "d ${cfg.workDir}      0755 root root - -"
      "d /etc/agentbox        0750 root root - -"
    ];

    # NAT the container's egress through the VPS's external interface so
    # opencode can reach Anthropic / OpenAI APIs and pull packages.
    networking.nat = {
      enable = true;
      internalInterfaces = [ "ve-agentbox" ];
      externalInterface = cfg.externalInterface;
    };

    # Lock down what the container can reach.
    #
    # INPUT  drop: container → host services (postgres, grafana, ssh, …).
    # FORWARD drops: container → other RFC1918 / loopback / link-local
    # ranges (so a session can't pivot to other VPS LAN hosts or the
    # cloud metadata service at 169.254.169.254). NAT'd egress to the
    # public internet still works — those packets don't match any drop.
    #
    # Note: deliberately NOT using `containers.<n>.forwardPorts` — its
    # DNAT rules bypass the host INPUT chain and would expose 4096 on
    # every interface, including public. Reach opencode at
    # ${cfg.localAddress}:4096 from the host instead (or SSH-tunnel it).
    networking.firewall.extraCommands = ''
      iptables -I INPUT   -i ve-agentbox -j DROP
      iptables -I FORWARD -i ve-agentbox -d 10.0.0.0/8     -j DROP
      iptables -I FORWARD -i ve-agentbox -d 172.16.0.0/12  -j DROP
      iptables -I FORWARD -i ve-agentbox -d 192.168.0.0/16 -j DROP
      iptables -I FORWARD -i ve-agentbox -d 127.0.0.0/8    -j DROP
      iptables -I FORWARD -i ve-agentbox -d 169.254.0.0/16 -j DROP
    '';
    networking.firewall.extraStopCommands = ''
      iptables -D INPUT   -i ve-agentbox -j DROP                       2>/dev/null || true
      iptables -D FORWARD -i ve-agentbox -d 10.0.0.0/8     -j DROP     2>/dev/null || true
      iptables -D FORWARD -i ve-agentbox -d 172.16.0.0/12  -j DROP     2>/dev/null || true
      iptables -D FORWARD -i ve-agentbox -d 192.168.0.0/16 -j DROP     2>/dev/null || true
      iptables -D FORWARD -i ve-agentbox -d 127.0.0.0/8    -j DROP     2>/dev/null || true
      iptables -D FORWARD -i ve-agentbox -d 169.254.0.0/16 -j DROP     2>/dev/null || true
    '';

    containers.agentbox = {
      autoStart = true;
      ephemeral = false;

      # Own network namespace + veth pair. Without this the container would
      # share the host's network and could hit postgres, grafana, etc.
      privateNetwork = true;
      hostAddress = cfg.hostAddress;
      localAddress = cfg.localAddress;

      bindMounts = {
        "/work" = {
          hostPath = cfg.workDir;
          isReadOnly = false;
        };
        # Provider keys (ANTHROPIC_API_KEY, etc.) live here on the host.
        "/etc/agentbox" = {
          hostPath = "/etc/agentbox";
          isReadOnly = true;
        };
      };

      config = { config, pkgs, lib, ... }: {
        system.stateVersion = "24.11";

        # Don't inherit the host's /etc/resolv.conf — resolve via public DNS
        # only, so the container never talks to the host's resolver.
        environment.etc."resolv.conf".text =
          lib.concatMapStrings (s: "nameserver ${s}\n") cfg.dnsServers;

        networking = {
          useHostResolvConf = lib.mkForce false;
          defaultGateway = cfg.hostAddress;
          firewall = {
            enable = true;
            # Only port the host needs to reach over the veth.
            allowedTCPPorts = [ 4096 ];
          };
        };

        environment.systemPackages = with pkgs; [
          opencode
          dotnet-sdk_10
          git
          gnumake
          nodejs_22
          coreutils
          gnused
          gnugrep
          curl
          jq
          which
        ];

        users.users.agent = {
          isNormalUser = true;
          home = "/home/agent";
          createHome = true;
          shell = pkgs.bashInteractive;
          uid = 1000;
        };

        # opencode's web server. Binds the veth IP only — the host reaches
        # it directly over the veth, and nothing else can route to it.
        systemd.services.opencode-web = {
          description = "opencode web server (sandboxed)";
          wantedBy = [ "multi-user.target" ];
          after = [ "network-online.target" ];
          wants = [ "network-online.target" ];

          environment = {
            HOME = "/home/agent";
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            DOTNET_ROOT = "${pkgs.dotnet-sdk_10}";
          };

          serviceConfig = {
            User = "agent";
            Group = "users";
            WorkingDirectory = "/work";
            ExecStart = "${pkgs.opencode}/bin/opencode serve --hostname ${cfg.localAddress} --port 4096";
            Restart = "on-failure";
            RestartSec = "5s";
            EnvironmentFile = "-/etc/agentbox/opencode.env";
          };
        };
      };
    };
  };
}
