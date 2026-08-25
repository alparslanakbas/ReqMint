# Native Arabic review workflow

This workflow produces review evidence; it does not replace a native Arabic reviewer. The reviewer should be a native Arabic speaker familiar with API and Git terminology and must inspect the real Release application, Store copy, documentation, and website.

## Prepare the review kit

Run from the repository root:

```powershell
./eng/New-ArabicLocalizationReviewKit.ps1
```

The ignored `artifacts/localization-review/ar-SA` directory contains:

- `strings.csv`, with every English and Arabic application resource, its category, and source usage;
- `review-evidence.json`, with the exact fingerprint of every Arabic application, Store, documentation, and website source included in the review.

Do not commit personal reviewer working files. Preserve the final evidence securely with the release-candidate records.

## Required real-application scenarios

The reviewer must use a Release build and complete all of these scenarios before approval:

1. Finish onboarding and the local tutorial at 100%, 125%, and 150% display scaling.
2. Build and send requests containing Arabic labels alongside LTR URLs, methods, headers, JSON, environment keys, file paths, hashes, and Git output.
3. Inspect collections, environments, history, Runner results, settings, tray actions, close prompts, confirmation dialogs, errors, and empty states.
4. Navigate the primary workflow with the keyboard and verify every icon-only action has an understandable Arabic accessible name.
5. Review the `ar-SA` Store listing, all Arabic website routes, privacy, security, support, and the terminology guide.
6. Record every truncation, ambiguous term, punctuation issue, reversed technical token, cursor-selection problem, and screen-reader issue before approval.

## Record approval

For every `strings.csv` row, replace `pending` with `approved` only after checking that string in context. Use `changes-requested` while any correction remains. Complete the reviewer identity, UTC review time, decision, checklist, and notes in `review-evidence.json`.

After editing the CSV, calculate its SHA-256 hash and place the lowercase value in `reviewedStringsSha256`:

```powershell
(Get-FileHash `
  ./artifacts/localization-review/ar-SA/strings.csv `
  -Algorithm SHA256).Hash.ToLowerInvariant()
```

Validate the completed evidence:

```powershell
./eng/Test-ArabicLocalizationReviewEvidence.ps1 `
  -EvidencePath ./artifacts/localization-review/ar-SA/review-evidence.json
```

The validator rejects stale fingerprints, missing reviewer identity, incomplete scenario checks, unapproved strings, altered CSV files, and review files outside the evidence directory.

Only validated evidence can unlock the `ar-SA` Store screenshot capture planner. A source change included in the review fingerprint invalidates earlier evidence and requires a fresh review.
