# Release readiness

ReqMint release candidates use two kinds of gates: repeatable automated checks and witnessed platform acceptance checks. A candidate is not public-ready until every required row has evidence for the exact commit and package version being released.

## Automated gates

The **Quality gates** workflow runs the Release test suite on Windows, macOS, and Ubuntu for every pull request and every push to `main`. A separate job rejects known vulnerable direct or transitive NuGet dependencies. Dependabot proposes grouped NuGet and GitHub Actions updates each week; an update still has to pass the same gates.

Platform packaging workflows additionally verify package structure, target architecture, metadata, signatures where applicable, and SHA-256 checksums. The Windows Store readiness jobs validate approved listing screenshots and self-contained layouts for both x64 and ARM64. Apple notarization and Microsoft Store certification remain external gates and cannot be replaced by a unit test.

## Release-candidate evidence

| Area | Required evidence | Gate type |
| --- | --- | --- |
| Core behavior | Green cross-platform Release test matrix for the candidate commit | Automated |
| Dependencies | Green direct and transitive NuGet vulnerability audit | Automated |
| Workspace migration | Fixture-based load, migrate, save, reopen, and rollback tests for every supported prior format | Automated |
| Accessibility | Keyboard-only tutorial/request flow, visible focus, screen-reader names, 200% scaling, and supported high-contrast themes | Witnessed per OS |
| Privacy | Confirm no unexpected outbound traffic; verify history redaction, retention controls, onboarding locality, and removal guidance | Automated plus witnessed |
| Windows lifecycle | Clean install, first launch, update from previous candidate, retained user data, uninstall, and Windows App Certification Kit | Witnessed on supported Windows versions |
| Microsoft Store preview | Exact Partner Center identity, x64/ARM64 bundle hash, anonymously reachable policy/support URLs, selected private audience, certification result, and Store-delivered installation | Automated plus witnessed |
| macOS lifecycle | Clean launch on Intel and Apple Silicon, Developer ID verification, notarization, Gatekeeper assessment, update, and removal | Automated plus witnessed |
| Linux lifecycle | Clean launch and removal on the declared Ubuntu, Debian, and Fedora versions under supported display servers | Witnessed per distribution |
| Git safety | No network action without confirmation; secret scan and managed-path limits fail closed | Automated plus witnessed |
| Documentation | Privacy policy, security policy, screenshots, onboarding, release notes, checksums, and known limitations match the candidate | Review |

## Candidate record

For each candidate, record:

- commit SHA, semantic version, build number, and creation date;
- links to the Quality gates and platform-package workflow runs;
- package filenames and SHA-256 values;
- public package identity metadata and the private-audience configuration result, without committing tester account details;
- device/OS version and tester for every witnessed gate;
- defects found, disposition, and retest evidence;
- signing, notarization, and store-certification results without copying secrets.

Any failed required gate blocks publication. A waived gate must identify the owner, user impact, mitigation, expiry, and follow-up issue; security, credential protection, signing, notarization, and store-certification gates cannot be waived for a public package.
