using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.Services;

public partial class LocalizationService : ObservableObject
{
    private readonly string _settingsPath;

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("tr", "Türkçe"),
    ];

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    public LocalizationService(string? settingsDirectory = null)
    {
        settingsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReqMint");
        _settingsPath = Path.Combine(settingsDirectory, "ui-settings.json");

        var savedLanguage = ReadSavedLanguage();
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SelectedLanguage = Languages.FirstOrDefault(language => language.Code == savedLanguage)
            ?? Languages.FirstOrDefault(language => language.Code == systemLanguage)
            ?? Languages[0];
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Apply(value);
        SaveLanguage(value.Code);
    }

    public string? GetString(string key) =>
        Application.Current?.Resources.TryGetResource(key, theme: null, out var value) == true
            ? value as string
            : null;

    private string? ReadSavedLanguage()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            using var stream = File.OpenRead(_settingsPath);
            return JsonSerializer.Deserialize<UiSettings>(stream)?.Language;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void SaveLanguage(string language)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (directory is null)
        {
            return;
        }

        var temporaryPath = $"{_settingsPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, new UiSettings(language));
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Language persistence is best-effort and must never prevent startup.
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    // A stale temporary settings file is safe to ignore.
                }
            }
        }
    }

    private static void Apply(LanguageOption language)
    {
        var culture = CultureInfo.GetCultureInfo(language.Code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var uri = new Uri($"avares://ReqMint.App/Localization/{language.Code}.json");
        using var stream = AssetLoader.Open(uri);
        var resources = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Localization resource '{uri}' is empty.");
        var applicationResources = Application.Current?.Resources
            ?? throw new InvalidOperationException("Application resources are not available.");

        foreach (var resource in resources)
        {
            applicationResources[resource.Key] = resource.Value;
        }
    }

    private sealed record UiSettings(string Language);
}
