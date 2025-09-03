{
  inputs = {
    flake-utils.url = "github:numtide/flake-utils";
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs =
    {
      nixpkgs,
      ...
    }@inputs:
    inputs.flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = (
          import nixpkgs {
            inherit system;
            config = {
              allowUnfree = true;
            };
          }
        );
        dotnet = pkgs.dotnetCorePackages.combinePackages [
          pkgs.dotnetCorePackages.dotnet_9.sdk
          pkgs.dotnetCorePackages.dotnet_8.sdk
        ];
      in
      {
        formatter = pkgs.nixfmt-rfc-style;
        devShell = pkgs.mkShell {
          buildInputs = [
            pkgs.dotnet-ef
            pkgs.protobuf_29
            pkgs.ilspycmd
            pkgs.grpcui
            pkgs.ghz
            pkgs.pkg-config
            pkgs.act
            dotnet
          ];
          # inputsFrom = [ self.packages.${system}.default ];
          DOTNET_ROOT = "${pkgs.dotnetCorePackages.sdk_9_0_1xx}/share/dotnet";
        };
      }
    );
}
