{ config, lib, pkgs, ... }:
let
  cfg = config.chorbar.agentbox;

  # Host-side directory the container can read. sops renders the agentbox
  # secrets here (not the default /run/secrets, which is root-only and not
  # convenient to bind-mount).
  hostSecretDir = "/run/agentbox-secrets";

  tailscaleAuthKeyPath = "${hostSecretDir}/tailscale_authkey";
  opencodeEnvPath      = "${hostSecretDir}/opencode.env";
in
{
  options.chorbar.agentbox = {
    enable = lib.mkEnableOption "agentbox — isolated opencode sandbox container";

    workDir = lib.mkOption {
      type = lib.types.path;
      default = "/var/lib/agentbox/work";
      description = "Host directory bind-mounted into the container as /work.";
    };

    port = lib.mkOption {
      type = lib.types.port;
      default = 80;
      description = ''
        Port opencode listens on inside the container. Defaults to 80 so
        the tailnet URL is bare-hostname (http://<tailnetHostname>).
        Binding 80 as the non-root agent user requires
        CAP_NET_BIND_SERVICE, which is granted on the systemd unit.
      '';
    };

    externalInterface = lib.mkOption {
      type = lib.types.str;
      default = "eth0";
      description = "VPS external interface used for NAT'ing the container's egress.";
    };

    hostAddress = lib.mkOption {
      type = lib.types.str;
      default = "10.231.0.1";
      description = "veth IP on the host side (used only for egress / DNS).";
    };

    localAddress = lib.mkOption {
      type = lib.types.str;
      default = "10.231.0.2";
      description = "veth IP on the container side.";
    };

    dnsServers = lib.mkOption {
      type = lib.types.listOf lib.types.str;
      default = [ "1.1.1.1" "9.9.9.9" ];
      description = "Public DNS resolvers the container uses.";
    };

    # ────────────────────────────────────────────────────────────────────
    # Secrets (sops). These map sops-key-names → what the container reads.
    # ────────────────────────────────────────────────────────────────────

    tailscaleAuthKeySecret = lib.mkOption {
      type = lib.types.str;
      default = "agentbox_tailscale_authkey";
      description = ''
        Name of the sops secret holding a tailscale auth key
        (one-off or reusable, optionally with tags=tag:agentbox).
        Created in secrets.yaml; rendered to ${tailscaleAuthKeyPath}
        and bind-mounted into the container.
      '';
    };

    envSecrets = lib.mkOption {
      type = lib.types.attrsOf lib.types.str;
      default = { };
      example = {
        ANTHROPIC_API_KEY = "anthropic_api_key";
        OPENAI_API_KEY    = "openai_api_key";
      };
      description = ''
        Map env-var name → sops secret key. Each entry becomes a line in
        the env file rendered to ${opencodeEnvPath} and loaded into
        opencode's systemd unit via EnvironmentFile=.
      '';
    };

    tailnetHostname = lib.mkOption {
      type = lib.types.str;
      default = "agentbox";
      description = "Hostname the container registers under in the tailnet.";
    };
  };

  config = lib.mkIf cfg.enable {
    # ────────────────────────────────────────────────────────────────────
    # SOPS WIRING — this is where you add secrets on the host.
    #
    # 1. Add these keys to your sops-encrypted secrets.yaml:
    #
    #      ${cfg.tailscaleAuthKeySecret}: tskey-auth-...
    #      anthropic_api_key: sk-ant-...
    #      # …and any other key referenced from chorbar.agentbox.envSecrets
    #
    # 2. In your host config, enable the module:
    #
    #      chorbar.agentbox = {
    #        enable = true;
    #        envSecrets = {
    #          ANTHROPIC_API_KEY = "anthropic_api_key";
    #        };
    #      };
    #
    # sops-nix decrypts each secret to a file under ${hostSecretDir} and
    # renders an env file with the opencode provider keys. Both are
    # bind-mounted into the container read-only.
    # ────────────────────────────────────────────────────────────────────

    sops.secrets =
      {
        ${cfg.tailscaleAuthKeySecret} = {
          path  = tailscaleAuthKeyPath;
          mode  = "0400";
          owner = "root";
        };
      }
      // lib.mapAttrs'
        (_envName: secretName: lib.nameValuePair secretName { })
        cfg.envSecrets;

    sops.templates."agentbox-opencode.env" = {
      path  = opencodeEnvPath;
      mode  = "0400";
      owner = "root";
      content = lib.concatStringsSep "\n" (
        lib.mapAttrsToList
          (envKey: secretName: "${envKey}=${config.sops.placeholder.${secretName}}")
          cfg.envSecrets
      );
    };

    systemd.tmpfiles.rules = [
      "d ${cfg.workDir}    0755 root root - -"
      "d ${hostSecretDir}  0750 root root - -"
    ];

    # Container networking:
    #
    # - NAT: egress through the VPS WAN so tailscale can phone home
    #   (login.tailscale.com, DERP relays) and opencode can reach AI APIs.
    # - INPUT drop: container → host services (postgres, grafana, ssh, …).
    # - FORWARD drops: container → RFC1918 / loopback / link-local — so a
    #   rogue session can't pivot to other LAN hosts or the cloud metadata
    #   service at 169.254.169.254.
    #
    # Public egress + tailscale traffic still flow (they don't match any
    # drop rule and get SNAT'd through cfg.externalInterface).
    networking = {
      nat = {
        enable = true;
        internalInterfaces = [ "ve-agentbox" ];
        externalInterface = cfg.externalInterface;
      };
      firewall = {
        extraCommands = ''
          iptables -I INPUT   -i ve-agentbox -j DROP
          iptables -I FORWARD -i ve-agentbox -d 10.0.0.0/8     -j DROP
          iptables -I FORWARD -i ve-agentbox -d 172.16.0.0/12  -j DROP
          iptables -I FORWARD -i ve-agentbox -d 192.168.0.0/16 -j DROP
          iptables -I FORWARD -i ve-agentbox -d 127.0.0.0/8    -j DROP
          iptables -I FORWARD -i ve-agentbox -d 169.254.0.0/16 -j DROP
        '';
        extraStopCommands = ''
          iptables -D INPUT   -i ve-agentbox -j DROP                       2>/dev/null || true
          iptables -D FORWARD -i ve-agentbox -d 10.0.0.0/8     -j DROP     2>/dev/null || true
          iptables -D FORWARD -i ve-agentbox -d 172.16.0.0/12  -j DROP     2>/dev/null || true
          iptables -D FORWARD -i ve-agentbox -d 192.168.0.0/16 -j DROP     2>/dev/null || true
          iptables -D FORWARD -i ve-agentbox -d 127.0.0.0/8    -j DROP     2>/dev/null || true
          iptables -D FORWARD -i ve-agentbox -d 169.254.0.0/16 -j DROP     2>/dev/null || true
        '';
      };
    };

    containers.agentbox = {
      autoStart = true;
      ephemeral = false;

      privateNetwork = true;
      hostAddress = cfg.hostAddress;
      localAddress = cfg.localAddress;

      # tailscale needs /dev/net/tun and the NET_ADMIN capability to manage
      # its own interface. NET_RAW lets it craft ICMP for path discovery.
      allowedDevices = [
        { node = "/dev/net/tun"; modifier = "rw"; }
      ];
      additionalCapabilities = [
        "CAP_NET_ADMIN"
        "CAP_NET_RAW"
      ];

      bindMounts = {
        "/work" = {
          hostPath = cfg.workDir;
          isReadOnly = false;
        };
        # sops-rendered secrets — read-only.
        ${hostSecretDir} = {
          hostPath = hostSecretDir;
          isReadOnly = true;
        };
      };

      config = { config, pkgs, lib, ... }: {
        system.stateVersion = "24.11";

        environment.etc."resolv.conf".text =
          lib.concatMapStrings (s: "nameserver ${s}\n") cfg.dnsServers;

        # System-wide gitconfig. Lets `git push` over HTTPS authenticate
        # with $GH_TOKEN (loaded into opencode-web.service's env from
        # sops), so the agent can push branches without any interactive
        # prompt. `gh` reads $GH_TOKEN natively.
        environment.etc."gitconfig".text = ''
          [credential "https://github.com"]
            helper = "!f() { echo username=x-access-token; echo password=$GH_TOKEN; }; f"
          [user]
            name = agentbox
            email = agentbox@chor.bar
          [init]
            defaultBranch = main
        '';

        networking = {
          hostName = cfg.tailnetHostname;
          useHostResolvConf = lib.mkForce false;
          defaultGateway = cfg.hostAddress;
          firewall = {
            enable = true;
            # opencode is reachable ONLY over tailscale0 — not the veth.
            interfaces.tailscale0.allowedTCPPorts = [ cfg.port ];
            # tailscale itself
            allowedUDPPorts = [ 41641 ];
            checkReversePath = "loose"; # tailscale recommends loose rpfilter
            trustedInterfaces = [ "tailscale0" ];
          };
        };

        # Tailscale daemon. authKeyFile points at the sops-rendered file
        # that the host bind-mounted in read-only.
        services.tailscale = {
          enable = true;
          authKeyFile = tailscaleAuthKeyPath;
          extraUpFlags = [
            "--hostname=${cfg.tailnetHostname}"
            "--ssh=false"
            "--accept-routes=false"
            # Optional: tag the node so you can write tailnet ACLs like
            #   "src": ["tag:admin"], "dst": ["tag:agentbox:*"]
            "--advertise-tags=tag:agentbox"
          ];
        };

        environment.systemPackages = with pkgs; [
          opencode
          dotnet-sdk_10
          git
          gh
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

        # opencode's web server. Binds 0.0.0.0:${cfg.port} inside the
        # container, but the container firewall only opens that port on
        # tailscale0 — so the only way in is from the tailnet. Default
        # port is 80 so the URL is bare-hostname: http://agentbox.
        systemd.services.opencode-web = {
          description = "opencode web server (sandboxed, tailnet-only)";
          wantedBy = [ "multi-user.target" ];
          # Don't start until tailscale has a stable IP, otherwise opencode
          # may bind before tailscale0 exists.
          after  = [ "network-online.target" "tailscaled.service" ];
          wants  = [ "network-online.target" "tailscaled.service" ];

          environment = {
            HOME = "/home/agent";
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            DOTNET_ROOT = "${pkgs.dotnet-sdk_10}";
          };

          serviceConfig = {
            User = "agent";
            Group = "users";
            WorkingDirectory = "/work";
            ExecStart = "${pkgs.opencode}/bin/opencode serve --hostname 0.0.0.0 --port ${toString cfg.port}";
            EnvironmentFile = "-${opencodeEnvPath}";
            Restart = "on-failure";
            RestartSec = "5s";
            # Let the non-root `agent` user bind privileged ports (<1024)
            # — needed when cfg.port is 80. Harmless when it isn't.
            AmbientCapabilities  = [ "CAP_NET_BIND_SERVICE" ];
            CapabilityBoundingSet = [ "CAP_NET_BIND_SERVICE" ];
          };
        };
      };
    };
  };
}
