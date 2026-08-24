# International expansion

ReqMint expands only when the entire customer journey is trustworthy in the target language. A translated language name or Store paragraph alone is not a release.

## Approved sequence

### 1. Arabic-speaking Gulf

Arabic is the first expansion language. ReqMint uses Modern Standard Arabic for the product, onboarding, documentation, Store listing, and support content. The application shell mirrors right-to-left while URLs, methods, headers, JSON, environment keys, file paths, command output, and Git content remain explicitly left-to-right.

Market validation begins in Saudi Arabia and the United Arab Emirates. Qatar, Kuwait, Bahrain, and Oman join the same release track after regional Store availability, support readiness, price presentation, and screenshot checks pass. Store-managed regional pricing is used when Pro exists; ReqMint does not charge a higher price based on nationality or an assumed willingness to spend.

Arabic release gates:

- every English application resource has a reviewed Arabic equivalent;
- onboarding, empty states, prompts, errors, settings, tray actions, and accessibility names are translated;
- navigation mirrors correctly at the minimum supported window size;
- protocol and code-oriented fields preserve left-to-right selection, caret movement, and copy behavior;
- documentation, privacy, security, and support pages are available in Arabic;
- Partner Center copy and all five real application screenshots are localized;
- a native Arabic reviewer approves terminology, truncation, and bidirectional-text behavior;
- packaging, localization parity, accessibility, and release-readiness checks pass.

### 2. Simplified Chinese

Simplified Chinese (`zh-Hans`) is the second expansion language. It is not exposed until the application resources, onboarding, documentation, support pages, Store listing, screenshots, and native-language review are complete.

The Chinese release also requires a distribution review: public documentation, downloads, support links, and update instructions must be reachable through the approved channels without assuming that GitHub is the only customer entry point.

Chinese release gates:

- every English application resource has a reviewed Simplified Chinese equivalent;
- terminology is consistent across the application, website, and Store;
- dense screens pass text expansion and font fallback checks;
- documentation, privacy, security, support, and download guidance are localized and reachable;
- all five Store screenshots use the real localized release build;
- a native Simplified Chinese reviewer approves terminology and layout;
- packaging, localization parity, accessibility, and release-readiness checks pass.

## Excluded scope

Russian localization and Russian-market monetization are not on the approved roadmap. No Russian language placeholder, Store listing, pricing experiment, or support promise should be published unless this product decision is explicitly revisited.

## Measurement and privacy

Expansion decisions use platform Store analytics, release downloads, reviews, support questions, documentation traffic, and voluntary feedback. ReqMint does not add forced in-app telemetry or weaken its local-first data boundary for market measurement.
