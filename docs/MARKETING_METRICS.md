# Privacy-friendly market signals

ReqMint measures early demand without adding product telemetry or requiring an account. The first macOS signal is the aggregate `download_count` that GitHub maintains for each Release asset.

## Counting rules

- Count only application archives (`.zip`) as downloads; checksum-file downloads are supporting activity and must not be added to the total.
- Report Apple silicon and Intel separately so packaging and test priorities follow real demand.
- Treat the numbers as downloads, not unique people, installations, active users, or revenue. Retries, automation, and repeat downloads can increase them.
- Review support questions, reproducible issues, stars, and voluntary feedback alongside the archive count.
- Never collect request URLs, headers, bodies, credentials, workspace paths, environment values, or local history for marketing.

The current counts are available from the GitHub Releases page and API:

```powershell
gh api repos/alparslanakbas/ReqMint/releases --jq '.[] | {tag: .tag_name, assets: [.assets[] | select(.name | endswith(".zip")) | {name, downloads: .download_count}]}'
```

## Weekly review

Record one snapshot every Monday rather than reacting to daily noise:

| Week | Apple silicon | Intel | Total archives | Qualified feedback | Reproducible defects | Decision |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 2026-08-31 | — | — | — | — | — | Establish baseline |

## Decision gates

- **25 archive downloads:** ask early users for architecture and launch feedback through a public issue template that forbids sensitive data.
- **100 archive downloads plus 10 qualified feedback responses, or three credible requests for a verified build:** review Apple Developer enrollment and physical-device testing.
- **250 archive downloads with healthy launch feedback:** prepare a dedicated macOS campaign, localized landing copy, signed/notarized release evidence, and Mac App Store submission materials.

These are prioritization triggers rather than proof of commercial demand. Paid promotion starts only after the download, install, first-launch, documentation, and support journey is verified on physical Macs.
