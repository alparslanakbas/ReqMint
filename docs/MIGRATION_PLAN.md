# ReqMint Migration Plan

The current application is a useful proof of concept, not a codebase that needs a line-by-line UI port. Migration will preserve verified behavior while replacing the structure in vertical slices.

## Current baseline

The repository currently contains:

- a .NET 8 WinForms application
- one main form
- a small HTTP library
- GET, POST, PUT, PATCH, and DELETE support
- HTTPS URL validation
- JSON formatting, copy, and download actions

Important limitations to address:

- the UI owns orchestration and platform actions
- successful non-JSON responses cannot be represented safely
- unsuccessful response bodies and headers are discarded
- no cancellation, explicit timeout, streaming, or large-response guard
- progress is simulated before the real request
- download paths and dialogs are Windows-specific
- secrets, environments, collections, persistence, Git, and tests are absent

## Migration strategy

The old WinForms projects remain buildable as a reference until feature parity is demonstrated. New ReqMint projects are created alongside them. No big-bang deletion occurs.

### Phase 0 — Baseline and guardrails

- tag or otherwise preserve the last legacy build
- add central SDK/package configuration and repository-wide build settings
- add CI for Windows, macOS, and Linux
- record performance measurements for the legacy build where meaningful
- create an in-process HTTP test server and behavioral parity tests

Exit: the repository builds predictably and the existing behavior is reproducible.

### Phase 1 — ReqMint shell

- create the .NET 10 and Avalonia 12 solution structure
- implement the application shell, navigation, design tokens, and Graphite Mint theme
- add the remaining themes through the token contract
- implement settings persistence and platform abstractions

Exit: ReqMint starts on all three desktop platforms and displays the approved shell without sending requests.

### Phase 2 — HTTP vertical slice

- implement request and response domain models
- build the reusable streaming HTTP engine
- add cancellation, timeout, headers, query parameters, bodies, and error responses
- implement the request composer and response viewer
- add JSON formatting and copy/export through platform services

Exit: the new app exceeds the legacy request feature set and passes HTTP contract tests.

### Phase 3 — Workspaces, collections, and environments

- implement schema-versioned workspace documents
- add collection/request navigation and tabs
- add variables, environments, local overrides, and secure secret storage
- implement atomic persistence, backups, imports, and migrations

Exit: a user can create, close, reopen, and safely share a useful workspace.

### Phase 4 — History and resource controls

- add SQLite-backed history and search
- implement retention, response-size limits, and local cache cleanup
- measure startup, idle memory, large-response memory, and render latency

Exit: resource budgets are measured and large responses cannot freeze or exhaust the UI.

### Phase 5 — Git collaboration

- detect Git repositories and show branch/status
- display ReqMint file changes and diffs
- add explicit stage/commit/pull/push workflows only after safety review
- add secret preflight and conflict guidance

Exit: two developers can collaborate through Git without ReqMint committing secrets or changing repositories silently.

### Phase 6 — Collection Runner

- implement ordered execution, cancellation, environment resolution, and data iterations
- add declarative assertions and the approved results screen
- support local result export

Exit: deterministic runner tests verify totals, failures, cancellation, and redaction.

### Phase 7 — Packaging and public beta

- implement the optional guided first-run tutorial with a disposable sample workspace and local sample API
- support skip, resume, restart, localization, keyboard navigation, and screen-reader guidance
- verify that tutorial progress and sample data remain local and can be removed cleanly
- generate Windows MSIX/MSIXBundle and reserve the Microsoft Store identity
- add macOS bundles/signing/notarization pipeline
- add Linux portable package and validate target distributions
- run accessibility, security, migration, clean-install, update, and uninstall tests
- update public documentation, privacy policy, screenshots, and release notes

Exit: a new user can complete their first API request without external documentation, and signed beta artifacts are installable and updateable on every supported platform.

### Phase 8 — Store release and commercial layer

- submit the Windows package to Microsoft Store certification
- ship Community first
- validate demand and usage before locking Pro/Team boundaries
- add portable licensing only after the free product is stable

Exit: ReqMint is publicly available with a sustainable release and support process.

## First implementation milestone

The first coding milestone is intentionally narrow:

1. repository build configuration
2. new solution and project skeleton
3. Avalonia application shell
4. Graphite Mint token system
5. cross-platform CI smoke build
6. one tested GET request vertical slice

This milestone proves architecture, rendering, HTTP execution, and platform builds before broader feature work begins.
