# Microsoft Store listing kit

This kit keeps the first Microsoft Store submission consistent, reviewable, and ready for English and Turkish customers. The canonical Partner Center copy lives in:

- `packaging/windows/store-listing/en-US.json`
- `packaging/windows/store-listing/tr-TR.json`

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

Capture real release UI rather than design mockups. Prepare the same five scenes for `en-US` and `tr-TR`, using the captions stored in each listing JSON file:

1. Request builder and response inspector
2. Collections and environment switching
3. Collection Runner results and assertions
4. Git changes and explicit repository actions
5. Settings, appearance, language, background mode, and support

Use PNG images at 1920 × 1080 when possible. Desktop screenshots must be at least 1366 × 768 and no larger than 50 MB. Partner Center permits up to ten desktop screenshots; Microsoft recommends at least four. Keep the most important UI in the upper two-thirds because Store layouts may crop images. Also prepare a 300 × 300 app tile icon.

Use these filenames under `packaging/windows/store-listing/screenshots/<locale>` for both `en-US` and `tr-TR`:

1. `01-request-builder.png`
2. `02-collections-environments.png`
3. `03-collection-runner.png`
4. `04-git-workflow.png`
5. `05-settings-support.png`

Generate the committed 300 × 300 Store tile after a branding change:

```powershell
./eng/New-WindowsStoreListingAssets.ps1
```

After capturing the real release UI, validate both localized screenshot sets before submission:

```powershell
./eng/Test-WindowsStoreScreenshots.ps1
```

## Final Partner Center checklist

1. Reserve the exact `ReqMint` product name.
2. Configure the package identity variables described in `docs/WINDOWS_PACKAGING.md`.
3. Make the website public and verify the website, privacy, and support routes anonymously.
4. Copy the validated `en-US` and `tr-TR` listing content into Partner Center.
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
