# Microsoft Store listing kit

This kit keeps Microsoft Store submissions consistent and reviewable. English and Turkish are the initial reviewed listing sources. Arabic (`ar-SA`) is a Gulf-expansion draft and remains blocked from public submission until the native-language and screenshot gates pass. The canonical Partner Center copy lives in:

- `packaging/windows/store-listing/en-US.json`
- `packaging/windows/store-listing/tr-TR.json`
- `packaging/windows/store-listing/ar-SA.json`

Do not maintain separate copy in private notes. Update these files, run the test suite, and copy the validated values into Partner Center.

## Submission position

- Product name and short title: **ReqMint**
- Pricing for the first public preview: **Free**
- Initial audience: **Private audience** with selected Microsoft accounts
- Category: select the closest developer-tools category presented by Partner Center and verify it at submission time
- First submission: leave **What's new** empty
- Later public submission: change the existing private product to public after all release gates pass

Private audience is the safe beta path because Microsoft permits a private product to become public later, but a product first published publicly cannot subsequently be changed to private audience visibility.

## Public URLs

| Partner Center field | URL |
| --- | --- |
| Website | `https://reqmint.alparslanayt.chatgpt.site` |
| Privacy policy | `https://reqmint.alparslanayt.chatgpt.site/privacy` |
| Support | `https://reqmint.alparslanayt.chatgpt.site/support` |

The current website deployment is owner-only. **Do not submit these URLs while anonymous visitors receive an access prompt.** Publishing the site and verifying all three URLs in a signed-out browser is a required Store submission gate. Keep payments out of the website; Store purchase and installation remain the trusted route.

## Website hosting and domain decision

Keep the production website on its existing Sites deployment. It already uses the Cloudflare-compatible application build and can accept a custom domain, so migrating the same marketing site to Vercel would add a second hosting system without improving the customer experience. Vercel Hobby is restricted to personal, non-commercial use; a revenue-oriented ReqMint launch would require a paid commercial plan.

The preferred launch domain is `reqmint.dev`, subject to availability when it is purchased. `getreqmint.dev` and `reqmintapp.com` are fallback candidates. Domain registration is separate from hosting. Do not purchase or connect a domain without an explicit owner approval.

When the domain is available and approved:

1. Purchase it from a registrar account controlled by the product owner.
2. Add it in the Site settings and apply the provided DNS records.
3. Verify HTTPS and all public routes in a signed-out browser.
4. Replace the temporary `chatgpt.site` website, privacy, support, and documentation URLs across the app, website metadata, Store listing JSON, and release documentation.
5. Run the full release and Store-listing checks before public submission.

## Screenshot capture plan

Capture real release UI rather than design mockups. Prepare the same five scenes for `en-US` and `tr-TR`, using the captions stored in each listing JSON file. Capture the matching `ar-SA` scenes only after a native reviewer approves the application terminology and RTL behavior:

Use the disposable **ReqMint Tutorial** workspace as the screenshot dataset. Its local demo API contains no credentials, personal information, external URLs, or third-party requests. The application deliberately replaces its physical temporary path with a localized safe label. Run the seeded collection before capturing the Runner scene, and never substitute a real customer workspace.

1. Request builder and response inspector
2. Collections and environment switching
3. Collection Runner results and assertions
4. Git changes and explicit repository actions
5. Settings, appearance, language, background mode, and support

Use PNG images at 1920 × 1080 when possible. Desktop screenshots must be at least 1366 × 768 and no larger than 50 MB. Partner Center permits up to ten desktop screenshots; Microsoft recommends at least four. Keep the most important UI in the upper two-thirds because Store layouts may crop images. Also prepare a 300 × 300 app tile icon.

Use these filenames under `packaging/windows/store-listing/screenshots/<locale>` for `en-US`, `tr-TR`, and—after approval—`ar-SA`:

1. `01-request-builder.png`
2. `02-collections-environments.png`
3. `03-collection-runner.png`
4. `04-git-workflow.png`
5. `05-settings-support.png`

Generate the committed 300 × 300 Store tile after a branding change:

```powershell
./eng/New-WindowsStoreListingAssets.ps1
```

After capturing the real release UI, validate the required localized screenshot sets before submission. The validator intentionally covers only locales with approved, committed screenshot sets; add `ar-SA` to it in the same commit as the five reviewed Arabic captures:

```powershell
./eng/Test-WindowsStoreScreenshots.ps1
```

Prepare and complete the [native Arabic review workflow](localization/ar-SA/NATIVE_REVIEW.md). After its evidence validator passes, create a disposable `ar-SA` capture session. The capture planner revalidates the evidence fingerprint and every string approval before it creates the ignored capture artifact:

```powershell
./eng/New-WindowsStoreScreenshotCapturePlan.ps1 `
  -Locale ar-SA `
  -ReviewEvidencePath ./artifacts/localization-review/ar-SA/review-evidence.json
```

Place the five real Release-build PNG files beside the generated `capture-plan.json`, then validate the draft without adding Arabic to the default approved locale set:

```powershell
./eng/Test-WindowsStoreScreenshots.ps1 `
  -ScreenshotRoot ./artifacts/store-capture `
  -Locales ar-SA
```

Only after reviewer approval, visual inspection, and successful validation should the five PNG files move to `packaging/windows/store-listing/screenshots/ar-SA`. Add `ar-SA` to the validator's default locale list in that same commit. Never commit `capture-plan.json`; it is session evidence, not Store artwork.

## Final Partner Center checklist

Use [Partner Center and private preview setup](PARTNER_CENTER_SETUP.md) for the identity-variable and bundle-preflight commands.

1. Reserve the exact `ReqMint` product name.
2. Configure the package identity variables described in `docs/WINDOWS_PACKAGING.md`.
3. Make the website public and verify the website, privacy, and support routes anonymously.
4. Copy the validated `en-US` and `tr-TR` listing content into Partner Center. Add `ar-SA` only after its native-language review, public Arabic support routes, and screenshots are complete.
5. Complete the age-rating and privacy questionnaires using the shipped application's actual behavior.
6. Upload real localized screenshots and the app tile artwork.
7. Add selected tester Microsoft accounts to the private audience.
8. Build the Store bundle, run the Windows App Certification Kit, and complete the release-readiness checklist.
9. Submit the private preview and test installation through Microsoft Store before changing visibility to public.

## Field limits enforced by tests

The listing contract tests enforce ReqMint's initial-submission rules and the relevant Partner Center limits:

- short title: 50 characters
- short description: 270 characters for a concise Store presentation, stricter than the 1,000-character platform maximum
- description: 10,000 characters and plain text without links
- What's new: blank for the first submission, with a 1,500-character ceiling
- features: 1–20 entries, 200 characters each, without manually typed bullets
- keywords: at most 7 entries, 40 characters each, and 21 words combined
- copyright/trademark: 200 characters
- developed by: 255 characters
- screenshot captions: 200 characters each

## Microsoft references

- [Add and edit Store listing information](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [Additional Store listing information and keywords](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-additional-information)
- [Support information and privacy policy](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
- [Screenshots and Store images](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [Store visibility options](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/visibility-options)
- [ChatGPT Sites custom domains](https://help.openai.com/en/articles/20001339)
- [Vercel Hobby plan](https://vercel.com/docs/plans/hobby)
