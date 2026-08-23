# Guided onboarding

ReqMint offers an optional, localized first-run introduction. It explains the product's local-first boundary before the user opens a workspace, and can be skipped without disabling any feature.

## Local progress

The introduction has explicit not-started, in-progress, completed, and skipped states. The current bounded step and state are stored only in the local UI settings file. Closing ReqMint during the introduction resumes the saved step on the next launch. Settings provides an explicit restart action for completed or skipped users.

The flow does not create an account, contact an analytics service, open a network connection, or copy workspace content. Changing the application language updates the introduction through the same English and Turkish resource dictionaries as the rest of the interface.

## Delivery stages

The first slice provides the welcome, privacy, and ready states plus skip, back, continue, finish, resume, and restart behavior. A later slice will add the disposable sample workspace and deterministic loopback tutorial API. That sample must remain removable, work without internet access, and never become part of a user's existing workspace without explicit approval.

Keyboard traversal, visible focus, screen-reader names, scaling, and high-contrast behavior remain release gates for the complete tutorial.
