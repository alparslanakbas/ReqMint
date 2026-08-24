# Linux packaging

ReqMint publishes self-contained portable archives for 64-bit Intel/AMD and ARM64 Linux systems. The .NET runtime is included, so users do not need to install .NET separately.

## Build locally

Run the packaging script on Linux:

```bash
bash ./eng/package-linux.sh 0.1.0-preview.1 x64
bash ./eng/package-linux.sh 0.1.0-preview.1 arm64
```

Each build creates a `.tar.gz` archive and matching `.sha256` checksum under `artifacts/packages/linux`. The archive contains the application, a `reqmint` launcher, its version, the project license, and short installation guidance. Debug symbols are excluded.

## Create CI artifacts

Open **Actions → Linux portable packages → Run workflow** and enter a semantic version. The workflow first runs the complete Release test suite on Linux, then creates and verifies independent `linux-x64` and `linux-arm64` artifacts.

## Run the portable package

Extract the archive and start ReqMint from that directory:

```bash
tar -xzf ReqMint-0.1.0-preview.1-linux-x64.tar.gz
cd ReqMint
./reqmint
```

ReqMint is portable in the sense that it does not require a package-manager installation. Local settings and request history still use the operating system's standard application-data location.

Avalonia requires native Linux desktop libraries even for a self-contained .NET application. On Debian and Ubuntu, install `libx11-6`, `libice6`, `libsm6`, and `libfontconfig1`; Fedora uses the equivalent `libX11`, `libICE`, `libSM`, and `fontconfig` packages.

## Current validation boundary

The archive and checksum are built on Ubuntu CI. Clean launch testing on supported Ubuntu, Debian, and Fedora desktops remains a public-beta release gate. A package-manager-native format such as `.deb` or `.rpm` will be selected after that compatibility pass; the portable archive remains the common fallback.

## References

- [.NET application publishing](https://learn.microsoft.com/dotnet/core/deploying/)
- [.NET runtime identifier catalog](https://learn.microsoft.com/dotnet/core/rid-catalog)
- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia desktop Linux deployment](https://docs.avaloniaui.net/docs/deployment/linux)
