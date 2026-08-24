# Localization

ReqMint currently includes English (`en`, culture `en-US`), Turkish (`tr`, culture `tr-TR`), and an Arabic application preview (`ar`). UI text is loaded from JSON resources under `src/ReqMint.App/Localization` and applied through dynamic Avalonia resources, allowing the language to change without restarting the application.

Arabic is available in development builds for full RTL layout and terminology review. It is not considered a public Gulf release until native-language review, localized documentation, Store listing, screenshots, and support paths pass the release gates in [INTERNATIONAL_EXPANSION.md](INTERNATIONAL_EXPANSION.md). Simplified Chinese (`zh-Hans`) follows it. Russian is outside the approved expansion scope.

The selected language and other device-level preferences, such as request-history retention, are stored in the user's local application-data folder as `ReqMint/ui-settings.json`. The file is deliberately kept outside workspaces so personal preferences are never committed to Git or shared with teammates. Existing language-only settings files remain compatible as new preferences receive safe defaults.

`LanguageOption` separates the resource code from the formatting culture and derives text direction from .NET culture metadata. The main shell mirrors automatically for right-to-left languages. Protocol-oriented content—including URLs, headers, JSON, environment keys, paths, Git diffs, and response bodies—uses an explicit left-to-right direction so technical values remain readable inside an Arabic shell.

To add a language:

1. Copy `en.json` using the new ISO language code as the file name.
2. Translate every existing key without renaming keys.
3. Add the resource code, native display name, and formatting culture to `LocalizationService.Languages`.
4. Run localization key-parity tests.
5. Verify text expansion, keyboard navigation, technical left-to-right fields, and—when applicable—the complete mirrored layout at supported window sizes.
6. Publish matching documentation, support content, Store copy, and real localized screenshots before exposing the language selector option.

English remains the fallback when the operating-system or saved language is not supported. Invalid or unreadable local settings never prevent ReqMint from starting.
