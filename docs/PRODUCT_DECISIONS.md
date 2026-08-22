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

## Commercial direction

- **Community**: capable free local API client.
- **Pro**: advanced productivity, automation, and personal power-user features.
- **Team**: collaboration, governance, and organization-oriented capabilities.
- Licensing should be portable across Windows, macOS, and Linux rather than tied to one store account.
- Pricing and payment infrastructure remain a later decision; Community functionality must not depend on them.

## Deferred decisions

- Exact Pro and Team feature boundaries.
- One-time purchase versus subscription pricing.
- Whether Store commerce or an independent commerce provider is used.
- Optional cloud services and their hosting architecture.
