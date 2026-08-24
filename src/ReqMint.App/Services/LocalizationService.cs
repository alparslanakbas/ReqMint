using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.Services;

public partial class LocalizationService : ObservableObject
{
    private readonly IAppSettingsService _settings;

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English", "en-US"),
        new("tr", "Türkçe", "tr-TR"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlowDirection))]
    public partial LanguageOption SelectedLanguage { get; set; }

    public FlowDirection FlowDirection => SelectedLanguage.IsRightToLeft
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;

    public LocalizationService(IAppSettingsService settings)
    {
        _settings = settings;

        var savedLanguage = settings.Current.Language;
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SelectedLanguage = Languages.FirstOrDefault(language => language.Code == savedLanguage)
            ?? Languages.FirstOrDefault(language => language.Code == systemLanguage)
            ?? Languages[0];
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Apply(value);
        if (!string.Equals(_settings.Current.Language, value.Code, StringComparison.Ordinal))
        {
            _settings.Update(_settings.Current with { Language = value.Code });
        }
    }

    public string? GetString(string key) =>
        Application.Current?.Resources.TryGetResource(key, theme: null, out var value) == true
            ? value as string
            : null;

    private static void Apply(LanguageOption language)
    {
        var culture = CultureInfo.GetCultureInfo(language.CultureName);
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
}
