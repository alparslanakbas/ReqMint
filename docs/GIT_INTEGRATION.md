# Git integration

ReqMint's first Git collaboration slice is deliberately read-only. Opening a workspace detects whether its folder is inside a Git repository and displays the repository root, active branch, ahead/behind counts, and changed ReqMint workspace files.

The detailed list is intentionally limited to `reqmint.workspace.json` and JSON documents under `collections/`, `environments/`, and `data/`. Changes elsewhere in the repository are represented only by a count. This keeps the panel focused on ReqMint's responsibility without hiding the fact that the wider repository is dirty.

## Safety boundary

- Git remains optional; local ReqMint workspaces continue to work when Git is not installed.
- ReqMint currently invokes only `git rev-parse` and `git status`.
- Commands receive paths through process argument lists rather than shell command strings.
- Credential prompts and optional Git file locks are disabled during status inspection.
- Status commands have a bounded timeout and support cancellation.
- ReqMint does not stage, commit, pull, push, reset, checkout, or modify Git configuration.

Future mutating workflows require explicit user intent, a visible operation scope, secret preflight checks, and separate safety review before implementation.
