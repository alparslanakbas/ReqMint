# Git integration

ReqMint's first Git collaboration slice is deliberately read-only. Opening a workspace detects whether its folder is inside a Git repository and displays the repository root, active branch, ahead/behind counts, and changed files.

## Safety boundary

- Git remains optional; local ReqMint workspaces continue to work when Git is not installed.
- ReqMint currently invokes only `git rev-parse` and `git status`.
- Commands receive paths through process argument lists rather than shell command strings.
- Credential prompts and optional Git file locks are disabled during status inspection.
- Status commands have a bounded timeout and support cancellation.
- ReqMint does not stage, commit, pull, push, reset, checkout, or modify Git configuration.

Future mutating workflows require explicit user intent, a visible operation scope, secret preflight checks, and separate safety review before implementation.
