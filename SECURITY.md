# ReqMint Security Policy

ReqMint is currently pre-release. Security fixes are applied to the latest code on `main`; no released version is under long-term support yet.

## Reporting a vulnerability

Use GitHub's [private vulnerability reporting form](https://github.com/alparslanakbas/ReqMint/security/advisories/new) when available. Do not publish credentials, tokens, private request or response content, exploit details, or user data in a public issue.

Include the affected commit or version, operating system, reproducible steps, impact, and any suggested mitigation. Reports will be acknowledged as soon as practical, investigated before public disclosure, and credited when requested and appropriate.

## Scope

High-priority reports include credential disclosure, unsafe workspace or collection parsing, command or code execution, certificate-verification bypass, unauthorized network activity, insecure temporary files, Git operations outside the confirmed scope, and persistence of values that ReqMint promises to redact.

Package-signing certificates, store accounts, GitHub Actions secrets, and third-party services are managed outside the application repository. Never attach their real values to a report.

The current automated and manual review baseline is documented in
[`docs/SECURITY_REVIEW.md`](docs/SECURITY_REVIEW.md). A clean review reduces risk but does not
guarantee that the software is free of vulnerabilities.
