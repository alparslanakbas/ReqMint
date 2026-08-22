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

## Secret preflight

Changed ReqMint JSON files receive a bounded, read-only secret scan. The scan detects persisted values marked as secret, literal values assigned to sensitive names such as authorization, token, password, and API key, plus selected well-known credential formats. Safe variable templates such as `{{API_TOKEN}}` are not treated as secrets.

Findings contain only the file path, JSON location, and risk category; credential values are never returned to the UI or written to logs. Reads are bounded to 2 MB per file and their temporary byte buffers are cleared after inspection. Oversized, malformed, unreadable, or symbolic-link files fail closed and are reported as unscanned. Future mutating workflows must rerun the scan against their exact operation scope—including the Git index before a commit—rather than trusting an earlier UI result.

## Diff previews

Selecting a changed ReqMint file opens a read-only unified diff in the main workspace. Working-tree and staged changes are presented separately, command output is bounded, and long previews are truncated visibly. Added, removed, hunk, and header lines use semantic colors for quick review.

Every preview performs a fresh security check against the exact version being displayed. Staged previews inspect the Git index rather than trusting the working copy, so a credential staged and then removed locally remains blocked. Unsafe or unscannable versions return only a localized warning; their diff content is never passed to the view model.

Future mutating workflows require explicit user intent, a visible operation scope, secret preflight checks, and separate safety review before implementation.
