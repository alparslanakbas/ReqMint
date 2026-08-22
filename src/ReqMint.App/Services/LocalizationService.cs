using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.Services;

public partial class LocalizationService : ObservableObject
{
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("tr", "Türkçe"),
    ];

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    public LocalizationService()
    {
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SelectedLanguage = Languages.FirstOrDefault(language => language.Code == systemLanguage)
            ?? Languages[0];
        Apply(SelectedLanguage);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value) => Apply(value);

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
}
