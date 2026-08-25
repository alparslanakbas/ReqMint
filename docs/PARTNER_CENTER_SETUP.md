# Partner Center and private preview setup

ReqMint's first Microsoft Store release is a private-audience preview. Do not use development identity values, expose the product publicly, or upload a locally invented package identity.

## 1. Reserve the product

In the product owner's Partner Center account, reserve the exact product name **ReqMint**. Open the reserved product's package identity page and copy these three values exactly:

| ReqMint input | Partner Center field | GitHub variable |
| --- | --- | --- |
| Identity name | Package/Identity/Name | `REQMINT_STORE_IDENTITY_NAME` |
| Publisher | Package/Identity/Publisher | `REQMINT_STORE_PUBLISHER` |
| Publisher display name | Publisher display name | `REQMINT_STORE_PUBLISHER_DISPLAY_NAME` |

These values are public package metadata, not signing secrets. Never add a certificate, certificate password, Partner Center password, or API credential to repository variables.

## 2. Validate before changing GitHub

Use `-WhatIf` first. It validates the values and shows only the three variable names that would change:

```powershell
./eng/Set-GitHubWindowsStoreIdentity.ps1 `
  -IdentityName '<Package/Identity/Name>' `
  -Publisher '<Package/Identity/Publisher>' `
  -PublisherDisplayName '<Publisher display name>' `
  -WhatIf
```

After comparing the values with Partner Center, run the same command without `-WhatIf`. The script updates only the expected GitHub Actions variables and verifies their names without printing their values.

## 3. Build the real Store bundle

Run **Actions → Windows Store bundle → Run workflow** with a four-part version whose final component is `0`. The workflow rejects missing, malformed, whitespace-padded, and development identity values before building the x64 and ARM64 bundle.

Download the resulting `.msixbundle`; do not use the development MSIX as a Store candidate.

## 4. Pass the private-preview preflight

The website, privacy, and support URLs must first work in a signed-out browser. Then run:

```powershell
./eng/Test-WindowsStorePrivatePreviewReadiness.ps1 `
  -BundlePath '<downloaded ReqMint .msixbundle>' `
  -IdentityName '<Package/Identity/Name>' `
  -Publisher '<Package/Identity/Publisher>' `
  -PublisherDisplayName '<Publisher display name>' `
  -ExpectedVersion '<four-part Store version>' `
  -WebsiteAnonymousAccessVerified
```

The preflight validates the bundle and inner-package versions, real identity, publisher display name, exact x64/ARM64 bundle shape, Windows Desktop target, required `runFullTrust` declaration, SHA-256 evidence, and approved English/Turkish screenshots. It deliberately cannot attest to Partner Center questionnaires, restricted-capability approval, selected private-audience accounts, Windows App Certification Kit results, or installation from Microsoft Store; record those manually for the release candidate.

Arabic remains outside the first private submission until its separate native-review and screenshot evidence passes.
