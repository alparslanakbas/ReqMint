# Localization

ReqMint currently ships with English (`en`) and Turkish (`tr`). UI text is loaded from JSON resources under `src/ReqMint.App/Localization` and applied through dynamic Avalonia resources, allowing the language to change without restarting the application.

The selected language and other device-level preferences, such as request-history retention, are stored in the user's local application-data folder as `ReqMint/ui-settings.json`. The file is deliberately kept outside workspaces so personal preferences are never committed to Git or shared with teammates. Existing language-only settings files remain compatible as new preferences receive safe defaults.

To add a language:

1. Copy `en.json` using the new ISO language code as the file name.
2. Translate every existing key without renaming keys.
3. Add the language to `LocalizationService.Languages`.
4. Build the Avalonia application and verify text expansion at supported window sizes.

English remains the fallback when the operating-system or saved language is not supported. Invalid or unreadable local settings never prevent ReqMint from starting.
