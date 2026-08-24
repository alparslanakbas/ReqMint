# Windows packaging

ReqMint uses a self-contained MSIX package for Microsoft Store distribution. The package contains the .NET runtime, keeps installation and removal clean, and lets the Store manage signing and updates.

## Build a development package

Run the packaging script on Windows with the Windows 10 or 11 SDK installed:

```powershell
./eng/package-windows.ps1 -Version 1.0.0.0 -Architecture x64
```

The unsigned package is written to `artifacts/packages/windows`. An unsigned MSIX is intended for Store submission, not direct public distribution. Direct downloads and ordinary side-loading require a trusted signature.

Use `-LayoutOnly` to prepare and validate the self-contained package layout on a Windows development machine that does not have `MakeAppx.exe`:

```powershell
./eng/package-windows.ps1 -Version 1.0.0.0 -Architecture x64 -LayoutOnly
```

## Connect the Microsoft Store identity

Reserve `ReqMint` in Partner Center before the public submission. Copy the exact values shown under the product's package identity into these GitHub repository variables:

| Repository variable | Partner Center value |
| --- | --- |
| `REQMINT_STORE_IDENTITY_NAME` | Package/Identity/Name |
| `REQMINT_STORE_PUBLISHER` | Package/Identity/Publisher |
| `REQMINT_STORE_PUBLISHER_DISPLAY_NAME` | Publisher display name |

The identity and publisher are public package metadata, so they are variables rather than secrets. Do not commit signing certificates or certificate passwords. Microsoft signs an accepted Store package. A package distributed outside the Store needs a separate trusted signing process.

## Create a development CI artifact

Open **Actions → Windows development MSIX → Run workflow**, enter a Store-compatible four-part version, and select `x64` or `arm64`. For Windows 10 and 11 Store submissions:

- the first version component must be greater than zero;
- every component must be between 0 and 65535;
- the fourth component must remain zero because Microsoft Store reserves it.

The workflow runs the release test suite, publishes a self-contained Windows app, creates the MSIX, and uploads it as a workflow artifact. Until the repository variables are configured, it deliberately uses a development identity that is suitable only for validating the packaging pipeline.

## Create the Microsoft Store bundle

After reserving the app name and configuring all three repository variables, open **Actions → Windows Store bundle → Run workflow**. The workflow:

1. refuses to continue when a Store identity value is missing or still uses the development placeholder;
2. runs the release test suite;
3. creates self-contained x64 and ARM64 packages with the same identity, publisher, and version;
4. combines them into `ReqMint_<version>.msixbundle` and uploads that Store submission artifact.

The bundle workflow has no development identity fallback. This prevents a structurally valid but unusable package from being mistaken for the real Store submission.

## Release checklist

Before submitting a public build:

1. Reserve the app name and configure the exact Partner Center identity values as repository variables.
2. Run the **Windows Store bundle** workflow and download the generated `.msixbundle` artifact.
3. Validate clean install, launch, update, uninstall, file pickers, credential storage, and tray behavior on supported Windows versions.
4. Run the Windows App Certification Kit.
5. Complete the [Microsoft Store listing kit](MICROSOFT_STORE_LISTING.md), including its anonymous website-access gate, localized copy, screenshots, privacy disclosures, and private-audience preview.
6. Review package capabilities against the privacy questionnaire immediately before submission.

## Microsoft references

- [Windows packaging overview](https://learn.microsoft.com/windows/apps/package-and-deploy/packaging/)
- [Microsoft Store MSIX package requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Bundle MSIX packages](https://learn.microsoft.com/windows/msix/packaging-tool/bundle-msix-packages)
- [Upload app packages to Partner Center](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/upload-app-packages)
- [MSIX signing options](https://learn.microsoft.com/windows/msix/package/sign-msix-package-guide)
