# Guided onboarding

ReqMint offers an optional, localized first-run introduction. It explains the product's local-first boundary before the user opens a workspace, and can be skipped without disabling any feature.

## Local progress

The introduction has explicit not-started, in-progress, completed, and skipped states. The current bounded step and state are stored only in the local UI settings file. Closing ReqMint during the introduction resumes the saved step on the next launch. Settings provides an explicit restart action for completed or skipped users.

The flow does not create an account, contact an analytics service, open a network connection, or copy workspace content. Changing the application language updates the introduction through the same English and Turkish resource dictionaries as the rest of the interface.

## Guided sample request

The ready step offers an explicit local sample action. ReqMint creates a new workspace under a randomized application-owned temporary directory, opens a `Getting Started` collection with three safe example requests, selects a public `TUTORIAL_BASE_URL` environment variable, and prepares an unsaved `Say hello to ReqMint` request. The examples cover a health check, an API project list, and current release metadata so the request builder and Collection Runner are useful immediately. Existing workspace files and Git repositories are never written by this action. Unsaved request navigation still uses the normal save/discard/cancel prompt, while unsaved collection or environment edits block the switch.

Workspace, collection, environment, and request display names are created in the interface language selected when the tutorial starts. Changing the application language later does not silently rename user-visible workspace documents.

The sample API binds only to IPv4 loopback (`127.0.0.1`) on an operating-system-assigned port. Its bounded HTTP reader accepts only fixed `GET` routes under `/api`, times out incomplete local clients, caps request headers at 16 KiB, returns deterministic JSON, and does not echo user input. It never contacts a third party. The guided request history is not copied into the persistent history database.

The guide advances after a verified 200 response from the active loopback endpoint and completes when the user saves the draft into the temporary collection. On application exit, ReqMint stops the listener, deletes only the three expected tutorial documents, and removes their randomized validated directory only when it is empty. The introduction can be restarted from Settings; an active sample session itself is intentionally not restored across process launches because its port and files are ephemeral.

## Delivery stages

The first slice provides the welcome, privacy, and ready states plus skip, back, continue, finish, resume, and restart behavior. The second slice provides the disposable workspace, deterministic loopback endpoint, environment-template request, and guided send/review/save path.

Keyboard traversal, visible focus, screen-reader names, scaling, and high-contrast behavior remain release gates for the complete tutorial.
