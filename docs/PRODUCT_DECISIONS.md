# ReqMint Product Decisions

This document records the product decisions approved before implementation. It is the reference point for product, design, architecture, and release choices.

## Product identity

- Product name: **ReqMint**
- Positioning: a fast, local-first API workspace for individual developers and Git-based teams.
- Tagline: **API work, without the weight.**
- Brand mark: an uppercase `R` whose leg ends in a right-facing send arrow.
- Default appearance: **Graphite Mint**.

## Product principles

1. Local-first: creating and sending a request never requires an account or cloud service.
2. Lightweight: startup time, idle memory, response rendering, and package size are product requirements.
3. Private by default: secrets, request history, response bodies, and logs stay on the device unless the user explicitly exports them.
4. Git-friendly: collections and shareable environment definitions use deterministic, reviewable text files.
5. Cross-platform: Windows, macOS, and Linux share one product model and one main UI codebase.
6. Progressive complexity: the first-run experience stays simple while advanced features remain discoverable.
7. No forced telemetry: diagnostics require explicit consent and must redact secrets.

## Guided onboarding

- First launch will offer an optional, localized, step-by-step product tour rather than opening into an unexplained empty workspace.
- The tutorial will create a disposable sample workspace and guide the user through sending a small API request, reading status/headers/body, using an environment variable, and saving the request to a collection.
- The sample API must be deterministic and local-first. Prefer a temporary loopback tutorial service so onboarding does not require an account, internet access, or sending user data to a third party.
- Users can skip, resume, or restart the tutorial from Help at any time. Progress is stored locally and the normal application remains usable throughout.
- Advanced Git, runner, and secret-vault concepts are introduced progressively after the first successful request, not placed in the initial path.
- Tutorial analytics remain disabled unless the user explicitly opts into privacy-preserving diagnostics.

## Platforms and distribution

### Windows

- Primary launch platform.
- Microsoft Store distribution is a committed release target.
- Store builds use MSIX/MSIXBundle, self-contained .NET deployment, Store signing, and Store-managed updates.
- Initial architectures: `win-x64`; add `win-arm64` before the public Store release if validation capacity permits.
- A direct-download build may be offered later, but it must not become a separate product fork.

### macOS

- Target signed and notarized `.app`/DMG packages for Apple silicon and Intel as demand requires.
- Apple signing/notarization costs are release costs, not requirements for local development.

### Linux

- Start with a portable archive and one broadly usable package format.
- Add distribution-specific packages only after usage data justifies their maintenance cost.

## Background and system tray behavior

- ReqMint will support an optional **minimize to system tray** mode to protect users from accidental window closure and provide faster reopening.
- On the first close attempt, ask whether ReqMint should keep running in the background or exit, with an option to remember the choice.
- The tray menu will provide **Open ReqMint**, **New Request**, and **Exit** actions; double-clicking the tray icon restores the main window.
- Hiding the window does not trigger the unsaved-changes prompt because the application remains running. An explicit exit continues to use the approved save, discard, or cancel protection.
- The behavior remains configurable in Settings and must clearly communicate that background mode continues using system resources.
- Start at login is a separate opt-in setting and stays disabled by default.
- Windows is the primary implementation target. macOS uses the equivalent menu-bar behavior, while Linux support is validated per desktop environment.

## Accounts, sync, and collaboration

- No ReqMint account is required for Community features.
- Git collaboration is optional and works with ordinary repositories.
- ReqMint must not silently push, pull, commit, or modify Git configuration.
- Cloud sync, if ever introduced, is an optional service and cannot replace local workspaces.

## Theme system

The approved theme gallery contains fourteen themes:

1. Graphite Mint
2. Clean Light
3. Soft Gray
4. Midnight
5. Ocean
6. Forest
7. Ember
8. Rose
9. Solar
10. Monochrome
11. High Contrast
12. Chroma RGB
13. Aurora Glass
14. Titanium Frost

Themes are token-based. Feature views must not contain hard-coded theme colors.

## International expansion

- The first expansion market is the Arabic-speaking Gulf, using complete Modern Standard Arabic localization and a right-to-left application shell.
- Initial market validation covers Saudi Arabia and the United Arab Emirates; Qatar, Kuwait, Bahrain, and Oman follow through the same Arabic release after Store, support, and pricing checks.
- Simplified Chinese is the second expansion language after the Arabic release reaches its quality gate.
- Russian localization and Russian-market monetization are not part of the approved roadmap.
- Technical content such as URLs, headers, JSON, environment keys, file paths, and Git diffs remains left-to-right inside a mirrored Arabic shell.
- A language is never exposed as a partial preview. Application resources, onboarding, documentation, support, Store copy, screenshots, and native-language review ship as one release unit.
- The detailed sequence and release gates are recorded in [INTERNATIONAL_EXPANSION.md](INTERNATIONAL_EXPANSION.md).

## Public support surfaces

- Settings displays the installed application version, operating system, architecture, and .NET runtime so support reports can identify the affected build without collecting telemetry.
- Copy support info is user-initiated and contains only those four application/platform fields plus the public release channel; request content, URLs, workspace paths, environment values, and credentials are outside the formatter's input.
- Documentation, privacy, security, and support actions open the canonical ReqMint website through explicit HTTPS links.
- ReqMint does not upload diagnostics automatically. Users decide what to include in a public issue and security reports use GitHub's private vulnerability channel.

## Commercial direction

- **Public preview**: free on every supported platform, with no artificial request limit or payment requirement.
- **Community**: capable free local API client that keeps the complete daily request loop, workspace portability, privacy, accessibility, themes, localization, and core Git-friendly behavior.
- **Pro**: target USD 39.99/year with a store-managed trial after paid workflows are implemented and validated; do not anchor the product with a permanently discounted launch price.
- **Team**: target USD 6/user/month billed annually only after real administration, policy, audit, portable entitlement, and support value exists.
- The website has no checkout or ReqMint account system. Windows commerce begins through Microsoft Store; other platforms link only to approved distribution channels.
- Community remains consistent across platforms. Never claim that a Store purchase is portable until a secure cross-platform entitlement actually exists.
- The complete rationale and rollout gates are recorded in [COMMERCIAL_PLAN.md](COMMERCIAL_PLAN.md).

## Deferred decisions

- Final Pro and Team feature boundaries after public-preview evidence.
- Cross-platform Pro entitlement after Windows Store commerce is validated.
- Optional cloud services and their hosting architecture.
