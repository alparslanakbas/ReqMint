# ReqMint

**API work, without the weight.**

> ReqMint is under active development. A public preview is coming soon.

<img width="1586" height="992" alt="ReqMint main workspace preview" src="https://github.com/user-attachments/assets/2a265184-f4ec-4d8d-9153-163a1acc4256" />

ReqMint is a fast, local-first desktop workspace for building, sending, organizing, and testing HTTP requests. It is designed for individual developers and Git-based teams without requiring an account, cloud workspace, or forced telemetry.

## Project status

ReqMint is being rebuilt as a cross-platform .NET desktop application. The new application targets Windows, macOS, and Linux from one Avalonia UI codebase.

The previous Windows Forms proof of concept is preserved under `legacy/` until the new application reaches verified feature parity.

## Technical direction

- .NET 10 LTS
- Avalonia 12
- MVVM and compiled bindings
- streaming, cancellable HTTP execution
- Git-friendly workspace documents
- local history and secure platform credential storage
- Microsoft Store-ready MSIX distribution for Windows

## Repository layout

```text
src/       ReqMint application and product libraries
tests/     automated tests
legacy/    preserved Windows Forms proof of concept
docs/      product, architecture, and migration decisions
```

## Documentation

- [Product decisions](docs/PRODUCT_DECISIONS.md)
- [Technical architecture](docs/ARCHITECTURE.md)
- [Migration plan](docs/MIGRATION_PLAN.md)
- [Collection Runner safety model](docs/COLLECTION_RUNNER.md)
- [Collection Runner data files](docs/COLLECTION_RUN_DATA.md)
- [Workspace format](docs/WORKSPACE_FORMAT.md)
- [Localization](docs/LOCALIZATION.md)
- [Guided onboarding](docs/ONBOARDING.md)
- [Background mode](docs/BACKGROUND_MODE.md)
- [Git integration](docs/GIT_INTEGRATION.md)
- [Windows packaging](docs/WINDOWS_PACKAGING.md)
- [Linux packaging](docs/LINUX_PACKAGING.md)

## Build

The active application is available through `ReqMint.slnx`. Detailed contributor instructions will be finalized with the first executable milestone.

## License

See [LICENSE](LICENSE).
