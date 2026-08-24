# macOS packaging

ReqMint creates self-contained `.app` bundles for Apple silicon (`osx-arm64`) and Intel (`osx-x64`) Macs. The bundle includes the .NET runtime, ReqMint resources, a native macOS icon, version metadata, and an ad-hoc hardened-runtime signature for internal testing.

## Create test packages

Open **Actions → macOS test packages → Run workflow** and provide:

- a three-part application version such as `0.1.0`;
- a positive, monotonically increasing Apple bundle build number.

The workflow runs the full Release test suite on macOS, creates both architecture-specific `.app` bundles, validates their metadata, signatures, and machine architecture, then uploads ZIP archives with SHA-256 checksums.

These artifacts are test packages. An ad-hoc signature does not establish the developer's identity and the archives are not notarized, so they must not be presented as public production downloads.

## Build locally

On a Mac with the .NET 10 SDK and Xcode command-line tools:

```bash
bash ./eng/package-macos.sh 0.1.0 1 arm64
bash ./eng/package-macos.sh 0.1.0 1 x64
```

Artifacts are written under `artifacts/packages/macos`. ReqMint currently supports macOS 14 and newer in line with the active .NET 10 support boundary.

## Public distribution gate

Direct public distribution requires a valid **Developer ID Application** certificate, hardened runtime, a secure timestamp, submission through `notarytool`, successful Apple notarization, and a stapled ticket. Certificate data, passwords, and App Store Connect API keys must be stored only as GitHub Actions secrets.

The next macOS release slice will add this opt-in signing and notarization workflow. It will fail closed unless every required secret is configured; the ad-hoc test workflow will remain separate.

## References

- [Publish .NET apps for macOS](https://learn.microsoft.com/dotnet/core/deploying/macos)
- [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos/)
- [Apple Developer ID](https://developer.apple.com/support/developer-id/)
- [Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
