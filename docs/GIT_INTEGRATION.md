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

## Explicit commit workflow

The commit action appears only when at least one ReqMint file is staged, no merge conflict exists, and no staged file falls outside ReqMint's managed paths. Opening the review reruns an exact Git-index scan and lists every included path. The user supplies a trimmed, single-line summary of 3–72 characters and confirms separately.

Confirmation reruns the full preflight immediately before `git commit`. Any newly introduced conflict, non-ReqMint staged file, secret finding, or unscannable index snapshot blocks the operation. ReqMint never uses bulk working-tree additions during commit and does not pull or push afterward. The initial safe workflow executes the commit with an empty temporary hooks directory so repository hooks cannot introduce hidden side effects; future hook support requires a separate explicit-consent design.

## Explicit remote check

ReqMint reads the current branch's configured upstream locally and displays only the remote name and branch; remote URLs are never passed to the UI because they may embed credentials. A separate confirmation is required before network access. The fetch command disables tags, submodule recursion, terminal prompts, and pagers, uses a bounded network timeout, and updates remote-tracking references without merging, rebasing, switching branches, changing working files, or pushing.

Detached HEADs, missing upstreams, local-only upstreams, and remote names outside the conservative safe-character set fail closed with localized guidance. Fetch errors are sanitized before reaching the UI.

## Fast-forward workspace update

After an explicit remote check reports incoming commits, ReqMint can build a bounded local preview of commit summaries and changed paths. No network request occurs while opening this preview. The update is offered only when the entire repository is clean, no conflict exists, the local branch has no unique commits, and the upstream is strictly ahead. Unsaved in-memory request, environment, and collection edits also block the app workflow. Every changed path must belong to ReqMint's managed workspace scope; incoming commits that also modify application source or any other repository file are delegated to an external Git tool. Previews above 50 commits or 200 changed paths likewise fail closed so the user never confirms a partially visible scope.

Confirmation reruns the complete preflight and executes only `git merge --ff-only` against the configured upstream reference. ReqMint never stashes, creates a merge commit, rebases, fetches, or pushes as part of this action. Repository hooks are disabled to avoid hidden file mutations. After success the workspace is reloaded from disk; if reload fails, the UI explicitly reports that Git succeeded and asks the user to reopen the workspace.

Push remains disabled pending its own safety review.
