{
  description = "Chorbar — Chore management system - full deployment with database";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
      lib = pkgs.lib;

      # MS .NET 10 SDK as a base layer. Building dotnet from source via
      # nixpkgs needs ~80 GB of disk, so we pull MS's official image and
      # publish on top of it at container start.
      sdkImage = pkgs.dockerTools.pullImage {
        imageName = "mcr.microsoft.com/dotnet/sdk";
        imageDigest = "sha256:6d7f69bc7bc9d4510ca255977b1f53ce52a79307e048a91450b2aecd63627cc3";
        finalImageName = "mcr.microsoft.com/dotnet/sdk";
        finalImageTag = "10.0";
        sha256 = lib.removeSuffix "\n"
          (builtins.readFile ./infra/sdk-image.x86_64-linux.sha256);
      };

      src = lib.cleanSourceWith {
        src = ./.;
        filter = path: _:
          let base = baseNameOf (toString path); in
          !(builtins.elem base [ "bin" "obj" ".git" "result" ]);
      };

      # Stable per-source-content marker. Different source = different store
      # path = different marker, which triggers a republish in the entrypoint.
      srcMarker = "${src}";

      entrypoint = pkgs.writeShellScript "chorbar-entrypoint" ''
        set -euo pipefail
        export HOME=/tmp
        export DOTNET_CLI_TELEMETRY_OPTOUT=1
        PUBLISH=/var/lib/chorbar/publish
        IMAGE_MARKER=/etc/chorbar/image-id
        CACHE_MARKER="$PUBLISH/.image-id"

        # Republish if first run OR if the image's source has changed since
        # the last publish (so a new image bump invalidates the cached publish
        # in the writable layer / persistent volume).
        if [ ! -f "$PUBLISH/Chorbar.dll" ] \
           || [ ! -f "$CACHE_MARKER" ] \
           || ! cmp -s "$IMAGE_MARKER" "$CACHE_MARKER"; then
          rm -rf "$PUBLISH"
          mkdir -p "$PUBLISH"
          rm -rf /tmp/build
          cp -r /app /tmp/build
          cd /tmp/build
          dotnet publish Chorbar/Chorbar.csproj -c Release -o "$PUBLISH"
          cp "$IMAGE_MARKER" "$CACHE_MARKER"
        fi

        cd "$PUBLISH"
        exec dotnet "$PUBLISH/Chorbar.dll" "$@"
      '';

      dockerImage = pkgs.dockerTools.buildImage {
        name = "chorbar";
        tag = "main";
        fromImage = sdkImage;
        copyToRoot = pkgs.runCommand "chorbar-root" { } ''
          mkdir -p $out/app $out/etc/chorbar
          cp -r ${src}/. $out/app/
          # Build-time marker — busts the runtime publish cache when source changes.
          echo "${srcMarker}" > $out/etc/chorbar/image-id
        '';
        config = {
          Entrypoint = [ "${entrypoint}" ];
          ExposedPorts."8080/tcp" = { };
          WorkingDir = "/var/lib/chorbar/publish";
          Env = [
            "ASPNETCORE_URLS=http://+:8080"
            "ASPNETCORE_CONTENTROOT=/var/lib/chorbar/publish"
          ];
        };
      };
    in
    {
      packages.${system}.dockerImage = dockerImage;

      nixosModules.default = { ... }: {
        imports = [ ./infra/app.nix ];
        virtualisation.oci-containers.containers.chorbar-web.imageFile = dockerImage;
      };
    };
}
