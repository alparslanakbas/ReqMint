# Git integration

ReqMint's Git collaboration panel detects whether a workspace is inside a Git repository and displays the repository root, active branch, ahead/behind counts, and changed ReqMint workspace files. Inspection remains read-only until the user explicitly reviews and confirms staging one eligible file.

The detailed list is intentionally limited to `reqmint.workspace.json` and JSON documents under `collections/`, `environments/`, and `data/`. Changes elsewhere in the repository are represented only by a count. This keeps the panel focused on ReqMint's responsibility without hiding the fact that the wider repository is dirty.

## Safety boundary

- Git remains optional; local ReqMint workspaces continue to work when Git is not installed.
- Read operations use `git rev-parse`, `git status`, and bounded `git diff`/index snapshots.
- Commands receive paths through process argument lists rather than shell command strings.
- Credential prompts and optional Git file locks are disabled during status inspection.
- Status commands have a bounded timeout and support cancellation.
- ReqMint never commits, pulls, pushes, checks out branches, or modifies Git configuration automatically.
- The only mutating workflow is explicit single-file staging for a ReqMint-managed path.

## Secret preflight

Changed ReqMint JSON files receive a bounded, read-only secret scan. The scan detects persisted values marked as secret, literal values assigned to sensitive names such as authorization, token, password, and API key, plus selected well-known credential formats. Safe variable templates such as `{{API_TOKEN}}` are not treated as secrets.

Findings contain only the file path, JSON location, and risk category; credential values are never returned to the UI or written to logs. Reads are bounded to 2 MB per file and their temporary byte buffers are cleared after inspection. Oversized, malformed, unreadable, or symbolic-link files fail closed and are reported as unscanned. Future mutating workflows must rerun the scan against their exact operation scope—including the Git index before a commit—rather than trusting an earlier UI result.

## Diff previews

Selecting a changed ReqMint file opens a read-only unified diff in the main workspace. Working-tree and staged changes are presented separately, command output is bounded, and long previews are truncated visibly. Added, removed, hunk, and header lines use semantic colors for quick review.

Every preview performs a fresh security check against the exact version being displayed. Staged previews inspect the Git index rather than trusting the working copy, so a credential staged and then removed locally remains blocked. Unsafe or unscannable versions return only a localized warning; their diff content is never passed to the view model.

## Explicit single-file staging

Staging is offered only for a ReqMint-managed file that has working-tree changes, has no pre-existing staged portion, and is not conflicted. The user first reviews the working-tree diff, opens a confirmation card naming the exact file, and then confirms the operation. No bulk-stage action exists.

The service re-reads Git status immediately before mutation and rejects stale or ineligible state. It scans the working copy, stages the exact path through a process argument list, then scans the resulting Git-index snapshot. A failed post-stage scan is removed from the index. Other repository files are never included, and staging never triggers commit, pull, or push.

Commit and network workflows remain disabled pending their own safety review.
