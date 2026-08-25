# macOS packaging

ReqMint creates self-contained `.app` bundles for Apple silicon (`osx-arm64`) and Intel (`osx-x64`) Macs. The bundle includes the .NET runtime, ReqMint resources, a native macOS icon, version metadata, and an ad-hoc hardened-runtime signature for internal testing.

## Create test packages

Open **Actions → macOS test packages → Run workflow** and provide:

- a three-part application version such as `0.1.0`;
- a positive, monotonically increasing Apple bundle build number.

The workflow runs the full Release test suite on macOS, creates both architecture-specific `.app` bundles, validates their metadata, signatures, and machine architecture, then uploads ZIP archives with SHA-256 checksums.

These artifacts are test packages. An ad-hoc signature does not establish the developer's identity and the archives are not notarized, so they must not be presented as public production downloads.

A versioned GitHub pre-release may expose an ad-hoc package only as a voluntary **early-access test build** when the release notes and website disclose the missing Developer ID identity and Apple notarization before download. The notice must also state that physical-device lifecycle checks are pending, real credentials and sensitive data must not be used, Gatekeeper must not be disabled system-wide, and the package is not a production or marketplace release. This exception never satisfies or waives the public production release gates below.

## Build locally

On a Mac with the .NET 10 SDK and Xcode command-line tools:

```bash
bash ./eng/package-macos.sh 0.1.0 1 arm64
bash ./eng/package-macos.sh 0.1.0 1 x64
```

Artifacts are written under `artifacts/packages/macos`. ReqMint currently supports macOS 14 and newer in line with the active .NET 10 support boundary.

## Public distribution workflow

Direct public distribution requires a valid **Developer ID Application** certificate, hardened runtime, a secure timestamp, submission through `notarytool`, successful Apple notarization, and a stapled ticket. Certificate data, passwords, and App Store Connect API keys must be stored only as GitHub Actions secrets.

Configure these repository secrets with values from the Apple Developer portal and the exported Developer ID certificate:

| Repository secret | Value |
| --- | --- |
| `REQMINT_APPLE_CERTIFICATE_BASE64` | Base64-encoded Developer ID Application `.p12` file |
| `REQMINT_APPLE_CERTIFICATE_PASSWORD` | Password used when exporting the `.p12` file |
| `REQMINT_APPLE_SIGNING_IDENTITY` | Full identity beginning with `Developer ID Application:` |
| `REQMINT_APPLE_NOTARY_KEY_BASE64` | Base64-encoded App Store Connect API `.p8` key |
| `REQMINT_APPLE_NOTARY_KEY_ID` | App Store Connect API key ID |
| `REQMINT_APPLE_NOTARY_ISSUER_ID` | App Store Connect issuer ID |

After all six secrets are configured, run **Actions → macOS notarized packages**. The workflow imports credentials into an ephemeral keychain, signs every native component with hardened runtime and a secure timestamp, submits both architecture packages through `notarytool`, staples and validates the tickets, then removes the temporary certificate, API key, and keychain even if the job fails.

The workflow deliberately fails before packaging when any credential is absent or the signing identity is not a `Developer ID Application` identity. The separate ad-hoc test workflow never receives Apple secrets.

## References

- [Publish .NET apps for macOS](https://learn.microsoft.com/dotnet/core/deploying/macos)
- [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos/)
- [Apple Developer ID](https://developer.apple.com/support/developer-id/)
- [Notarizing macOS software](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
