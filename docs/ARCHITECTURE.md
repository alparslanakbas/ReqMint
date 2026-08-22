# ReqMint Technical Architecture

Status: proposed baseline approved for implementation planning
Baseline date: 2026-08-22

## 1. Architecture style

ReqMint is a **modular monolith**. It uses explicit boundaries and dependency inversion without turning each feature into a separate service or package.

The architecture is organized around vertical product features:

- Workspaces
- Requests
- Collections
- Environments and secrets
- History
- Runner and assertions
- Git collaboration
- Settings and themes

The UI depends on application contracts. The HTTP, persistence, Git, and operating-system implementations remain replaceable infrastructure.

## 2. Technology baseline

- Runtime: [.NET 10 LTS](https://dotnet.microsoft.com/en-us/platform/support/policy)
- Desktop UI: [Avalonia 12](https://github.com/AvaloniaUI/Avalonia/releases)
- UI pattern: MVVM with compiled bindings
- Dependency injection, configuration, and logging: `Microsoft.Extensions.*`
- Local structured data: SQLite behind application-owned repositories
- Git: an `IGitService` abstraction with a system Git adapter first
- Tests: unit, contract, integration, UI headless, and packaging smoke tests

Package versions will be centrally pinned. Preview dependencies are not permitted in release builds.

## 3. Solution layout

```text
ReqMint.slnx
├─ src/
│  ├─ ReqMint.App/             Avalonia views, view models, navigation, composition root
│  ├─ ReqMint.Core/            Domain models, use cases, ports, validation, feature rules
│  ├─ ReqMint.Http/            Request construction, execution, response streaming
│  ├─ ReqMint.Storage/         Workspace documents, SQLite, migrations, secure secret ports
│  └─ ReqMint.Platform/        Clipboard, dialogs, credentials, OS paths, Git process, updates
├─ tests/
│  ├─ ReqMint.Core.Tests/
│  ├─ ReqMint.Http.Tests/
│  ├─ ReqMint.Storage.Tests/
│  ├─ ReqMint.App.Tests/
│  └─ ReqMint.EndToEnd.Tests/
├─ packaging/
│  ├─ windows/
│  ├─ macos/
│  └─ linux/
└─ docs/
```

This is the maximum initial project split. Features use folders and namespaces inside these projects rather than creating dozens of assemblies.

## 4. Dependency rules

```text
ReqMint.App ───────► ReqMint.Core
      │                    ▲
      ├────────────► ReqMint.Http
      ├────────────► ReqMint.Storage
      └────────────► ReqMint.Platform

ReqMint.Http ──────► ReqMint.Core
ReqMint.Storage ───► ReqMint.Core
ReqMint.Platform ──► ReqMint.Core
```

- `ReqMint.Core` has no Avalonia, database, filesystem, Git, or platform dependency.
- Infrastructure projects implement ports owned by Core.
- Views contain presentation only; view models invoke use cases.
- No static service locator and no mutable global application state.

## 5. Workspace and persistence model

ReqMint deliberately separates shareable files from machine-local state.

### Git-shareable workspace

```text
workspace-root/
├─ reqmint.workspace.json
├─ collections/
│  └─ commerce-api.json
├─ environments/
│  ├─ development.json
│  └─ staging.json
└─ data/
   └─ runner-sample.json
```

Rules:

- Documents contain a `schemaVersion`, stable IDs, and deterministic property ordering.
- Atomic writes use a temporary sibling followed by replace/rename.
- Unknown compatible fields are preserved where practical.
- Migrations are explicit, versioned, backed up, and tested.
- Secret values are never written to shareable workspace documents.

### Machine-local state

Stored under the operating system's application-data directory, keyed by workspace ID:

- request and runner history
- response metadata and bounded response cache
- UI layout, open tabs, and recent workspaces
- Git status cache and search indexes
- local-only environment overrides

SQLite is an implementation detail; the UI and Core do not issue SQL.

### Secrets

- Secret values are addressed by logical names such as `API_TOKEN`.
- Workspace files can declare that a variable is secret, but contain no secret value.
- `ISecretStore` maps logical names to the platform credential store.
- Windows uses the Windows credential facilities, macOS uses Keychain, and Linux targets Secret Service.
- A Linux fallback must be explicit, encrypted, and opt-in; plaintext fallback is prohibited.
- Logs, exports, Git diffs, exception messages, and telemetry pass through redaction.

## 6. HTTP execution engine

The engine models HTTP rather than JSON-only API calls.

Required behavior:

- all standard methods plus custom methods
- headers, query parameters, cookies, authentication, and raw/multipart/form bodies
- HTTP/1.1, HTTP/2, redirects, compression, proxy, certificates, and configurable TLS behavior
- cancellation, timeout, and streaming
- text, JSON, XML, HTML, image, and binary responses
- error response bodies and headers are first-class results
- timing breakdown and transferred byte counts
- bounded previews for large responses; do not load unbounded bodies into the UI
- per-workspace/session cookie isolation
- reusable handlers and clients; never create a client per request

Core request and response models do not expose `HttpRequestMessage`, `HttpResponseMessage`, or Avalonia types.

## 7. Runner and assertions

The first runner uses safe declarative assertions:

- status code
- response duration
- header presence/value
- JSON path value/existence
- JSON schema
- body contains/matches

Runs are cancellable and sequential by default. Concurrency is explicit and bounded. Arbitrary user scripting is deferred until a sandbox, timeout, memory limit, and secret exposure model are designed.

Run results store summaries and failures locally. Large response bodies follow the same retention limits as request history.

## 8. UI architecture and performance

- One application shell hosts feature navigation and tabs.
- Each feature exposes a small view model surface and cancellable asynchronous commands.
- Long lists use virtualization and incremental loading.
- Response formatting runs outside the UI thread and can be cancelled.
- Large documents use preview limits and on-demand loading.
- Theme resources use semantic tokens; all fourteen approved themes implement the same contract.
- Accessibility names, keyboard navigation, focus states, scaling, and high contrast are release requirements.
- Editor integration is behind an adapter so a text editor control can be replaced without changing feature logic.

Performance budgets will be measured in CI/release testing rather than asserted by impression alone.

## 9. Git collaboration

- ReqMint works without Git.
- V1 detects a repository and shells out to an installed Git executable through `IGitService`.
- Every mutating operation shows the exact scope and requires user intent.
- The app never auto-commits, auto-pulls, auto-pushes, rewrites history, or stores credentials.
- A preflight secret scan blocks likely credentials from ReqMint-managed files.
- Stable IDs and deterministic serialization keep diffs reviewable.
- Merge conflicts are surfaced as file conflicts first; a guided merge UI can be added later.

## 10. Security and privacy

- Validate imported documents before they enter the workspace.
- Treat imported collections, scripts, certificates, and response content as untrusted.
- Disable arbitrary code execution in the initial release.
- Redact `Authorization`, cookies, tokens, passwords, API keys, and user-defined secret variables.
- Use safe temporary-file permissions and delete temporary response files according to retention policy.
- Certificate verification is enabled by default. Any bypass is per-request, visibly warned, and never silently persisted.
- No forced account, cloud sync, or telemetry.

## 11. Packaging and release

### Windows

- Self-contained MSIX/MSIXBundle.
- Microsoft Store identity and publisher values are injected by release configuration.
- Store builds receive Microsoft signing, hosting, and managed updates.
- Run Windows App Certification Kit and clean-install/update/uninstall smoke tests.

### macOS

- Self-contained `.app` bundle and DMG.
- Code signing, hardened runtime, and notarization are release gates.

### Linux

- Self-contained build.
- Start with a portable archive and choose the first package format after supported-distribution testing.
- X11/XWayland is the conservative default; native Wayland remains opt-in while its Avalonia backend is experimental.

## 12. Quality gates

Every pull request must eventually satisfy:

- format and build with warnings treated as errors for owned code
- unit and contract tests
- persistence migration tests
- HTTP integration tests against an in-process test server
- headless UI tests for critical view-model/view bindings
- dependency vulnerability and license checks
- secret scanning

Release candidates additionally require Windows, macOS, and Linux smoke tests plus platform package validation.
