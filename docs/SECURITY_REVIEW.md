# Security review baseline

Date: 2026-08-25

This baseline records the checks completed before ReqMint's first public preview. It is not a
penetration-test certificate or a guarantee that no vulnerability exists. Repeat the automated
checks for every release candidate and review the threat model whenever a feature adds executable
scripts, a cloud service, authentication, payments, or remote collaboration.

## Scope and checks

- Current files and Git history were scanned for credentials, private keys, signing material, and
  high-confidence token formats. No real secret was found.
- NuGet direct and transitive dependencies and the production npm dependency tree were checked
  against their vulnerability feeds. No known vulnerability was reported.
- GitHub secret scanning and push protection are enabled, with no open alert at the baseline date.
- Dependabot alerts, automated security updates, private vulnerability reporting, and CodeQL's
  extended query suite are enabled.
- Every GitHub Actions dependency is pinned to an immutable commit SHA and every workflow has
  read-only repository permissions.
- Request execution, history redaction, workspace parsing, secret storage, Git command execution,
  local tutorial networking, SQLite access, external links, packaging, and release workflows were
  reviewed for their principal trust-boundary risks.

## Findings addressed

- Workspace documents now reject symbolic links and filesystem reparse points so a crafted
  workspace cannot redirect collection or environment reads and writes outside its folder.
- Workspace JSON documents are limited to 16 MiB before parsing or replacement to bound resource
  consumption from untrusted files.
- Request history now redacts credentials embedded in URL authority components as well as sensitive
  query parameters and headers; request bodies and response bodies are not persisted.
- HTTP header values containing CR or LF and invalid header names are rejected before transport.
- Local ignore rules cover environment files, private keys, signing certificates, provisioning
  profiles, and common keystore formats.

## Security properties verified

- TLS certificate validation uses the platform default; ReqMint does not install an accept-all
  certificate callback.
- Git commands use structured process arguments, disable interactive credential prompts, enforce
  time and output limits, and disable repository hooks for ReqMint-managed mutations.
- SQLite statements containing user-controlled values use parameters.
- Windows secrets use Credential Manager, and sensitive temporary byte buffers are cleared. Other
  platforms fail closed until their native vault adapters are implemented.
- The tutorial server binds only to the loopback interface, exposes fixed read-only sample routes,
  bounds request headers, and applies a request timeout.
- External support and documentation links require absolute HTTPS URLs without embedded user info.

## Release requirements

- Do not commit or print signing keys, store credentials, tester identities, or notarization
  material. Keep them in the platform's secret store.
- Do not introduce arbitrary request scripting until it has process isolation, execution and memory
  limits, a permission model, and tests proving secrets cannot escape its scope.
- Do not publish a release while CodeQL, dependency audits, secret scanning, cross-platform tests,
  package validation, signing, or Store certification has an unresolved failure.
- Treat a discovered credential as compromised: revoke or rotate it first, then remove it from the
  current tree and Git history using a coordinated procedure.
