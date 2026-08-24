# ReqMint Privacy

Last updated: 24 August 2026

ReqMint is a local-first developer tool. The current public-preview code does not require an account, upload workspaces to a ReqMint service, display advertising, or enable product analytics or crash telemetry.

## Data stored on the device

ReqMint stores application settings, onboarding progress, request history, collection-run summaries, and user-selected workspaces on the user's device. Workspace files remain in the folder selected by the user and can be reviewed or shared with Git like ordinary files. Request-history retention is bounded, and sensitive request headers and configured secret values are redacted before history is persisted.

Users can remove local history from ReqMint, delete a workspace through the operating system, and remove application settings through the operating system's application-data controls. Uninstall behavior and removal guidance must be verified for every supported release package before public distribution.

## Network activity

ReqMint sends HTTP traffic only when the user runs a request, a collection, or the local onboarding sample. Requests are sent to the destination chosen by the user and are subject to that destination's privacy practices.

Git network operations require an explicit user action and use the repository remote configured by the user. ReqMint does not provide a ReqMint-hosted synchronization service. Operating-system stores and Git hosting providers may independently process installation, update, repository, or diagnostic data under their own terms.

## Credentials and sensitive content

Authorization headers, cookies, tokens, passwords, API keys, response bodies, and workspace variables can contain sensitive information. ReqMint minimizes persisted secrets and scans managed Git workspace files before supported publish operations, but users remain responsible for the endpoints and repositories they choose.

## Changes and questions

Material privacy changes will update this document and its date before release. Privacy questions may be opened in the [ReqMint issue tracker](https://github.com/alparslanakbas/ReqMint/issues) without including credentials, request bodies, or other private data. Security vulnerabilities should follow [SECURITY.md](SECURITY.md).
