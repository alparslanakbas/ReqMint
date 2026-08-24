using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.Services;

public partial class ThemeService : ObservableObject
{
    private readonly IAppSettingsService _settings;

    public IReadOnlyList<ThemeOption> Themes => ThemeCatalog.All;

    [ObservableProperty]
    public partial ThemeOption SelectedTheme { get; set; }

    public ThemeService(IAppSettingsService settings)
    {
        _settings = settings;
        SelectedTheme = ThemeCatalog.Find(settings.Current.Theme) ?? ThemeCatalog.Default;
        Apply(SelectedTheme);
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        Apply(value);
        if (!string.Equals(_settings.Current.Theme, value.Id, StringComparison.Ordinal))
        {
            _settings.Update(_settings.Current with { Theme = value.Id });
        }
    }

    private static void Apply(ThemeOption theme)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = theme.IsLight
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
        var resources = application.Resources;
        SetBrush(resources, "AppBackgroundBrush", theme.AppBackground);
        SetBrush(resources, "SurfaceBrush", theme.Surface);
        SetBrush(resources, "SurfaceRaisedBrush", theme.SurfaceRaised);
        SetBrush(resources, "SurfaceHoverBrush", theme.SurfaceHover);
        SetBrush(resources, "BorderBrush", theme.Border);
        SetBrush(resources, "TextPrimaryBrush", theme.TextPrimary);
        SetBrush(resources, "TextSecondaryBrush", theme.TextSecondary);
        SetBrush(resources, "AccentBrush", theme.Accent);
        SetBrush(resources, "AccentMutedBrush", theme.AccentMuted);
        SetBrush(resources, "MethodGetBrush", theme.MethodGet);
        SetBrush(resources, "WarningBrush", theme.Warning);
        SetBrush(resources, "DiffAddedBrush", theme.DiffAdded);
        SetBrush(resources, "DiffRemovedBrush", theme.DiffRemoved);
        SetBrush(resources, "DiffHunkBrush", theme.DiffHunk);
        SetBrush(resources, "OverlayBrush", theme.Overlay);
    }

    private static void SetBrush(IResourceDictionary resources, string key, string color) =>
        resources[key] = new SolidColorBrush(Color.Parse(color));
}

public static class ThemeCatalog
{
    public const string DefaultId = "graphite-mint";

    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(DefaultId, "Graphite Mint", false, "#08110F", "#0D1715", "#111D1B", "#162321", "#263734", "#EEF7F4", "#91A49F", "#21D6A0", "#0D6B53", "#4B9BFF", "#FFB454", "#4ADE80", "#FB7185", "#60A5FA", "#D908110F"),
        new("clean-light", "Clean Light", true, "#F7FAF9", "#FFFFFF", "#F0F5F3", "#E5EFEC", "#CAD8D4", "#17211F", "#60736D", "#0A9B73", "#BDEBDE", "#2563EB", "#B45309", "#16803A", "#C2415A", "#2563EB", "#99081412"),
        new("soft-gray", "Soft Gray", true, "#F2F3F5", "#FAFAFB", "#E8EAED", "#DEE1E5", "#C6CBD1", "#20242A", "#69717C", "#4F6BED", "#CDD5FA", "#2563EB", "#A15C00", "#18794E", "#C2415A", "#4F6BED", "#9920242A"),
        new("midnight", "Midnight", false, "#080B18", "#0E1326", "#151B31", "#1C2542", "#2B3659", "#F1F4FF", "#97A3C7", "#8B5CF6", "#4C2A85", "#60A5FA", "#FBBF24", "#34D399", "#FB7185", "#818CF8", "#D9080B18"),
        new("ocean", "Ocean", false, "#03131A", "#071E28", "#0B2834", "#103746", "#1C4B5C", "#EAFBFF", "#8FBBC7", "#22D3EE", "#0E6675", "#38BDF8", "#FBBF24", "#34D399", "#FB7185", "#38BDF8", "#D903131A"),
        new("forest", "Forest", false, "#07130B", "#0D1D12", "#13281A", "#1A3523", "#2B4C34", "#F0F9F1", "#9BB7A0", "#65D46E", "#286B35", "#4AA3FF", "#F2B84B", "#5FE38A", "#FF7B8B", "#69A7FF", "#D907130B"),
        new("ember", "Ember", false, "#160C08", "#21120D", "#2D1912", "#3A2117", "#593124", "#FFF5EF", "#C1A094", "#FF7A3D", "#8B3C1C", "#54A7FF", "#FFC857", "#5CDB95", "#FF6B81", "#7EA7FF", "#D9160C08"),
        new("rose", "Rose", false, "#160A11", "#22101B", "#2E1624", "#3C1C2E", "#5A2B45", "#FFF2F8", "#C5A0B3", "#F472B6", "#87365F", "#60A5FA", "#FBBF24", "#4ADE80", "#FB7185", "#A78BFA", "#D9160A11"),
        new("solar", "Solar", false, "#002B36", "#073642", "#0B414D", "#124E59", "#35616A", "#FDF6E3", "#93A1A1", "#B58900", "#665100", "#268BD2", "#CB4B16", "#859900", "#DC322F", "#6C71C4", "#D9002B36"),
        new("monochrome", "Monochrome", false, "#090909", "#111111", "#191919", "#242424", "#373737", "#F5F5F5", "#A3A3A3", "#D4D4D4", "#525252", "#E5E5E5", "#F5F5F5", "#B7F7C4", "#FF9CAB", "#C4D7FF", "#D9090909"),
        new("high-contrast", "High Contrast", false, "#000000", "#050505", "#101010", "#1D1D1D", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFF200", "#5C5700", "#00B7FF", "#FFF200", "#00FF66", "#FF4D6D", "#00B7FF", "#E6000000"),
        new("chroma-rgb", "Chroma RGB", false, "#07070C", "#0E0E17", "#151522", "#202033", "#373752", "#F8F7FF", "#A7A3C2", "#00F5D4", "#006B60", "#00BBF9", "#FEE440", "#00F5D4", "#F15BB5", "#9B5DE5", "#DC07070C"),
        new("aurora-glass", "Aurora Glass", false, "#081018", "#0D1822", "#122431", "#193342", "#2B4C5C", "#EEFAFF", "#91B5C2", "#5EEAD4", "#176B65", "#67E8F9", "#FBCB66", "#63E6A6", "#FF7FA3", "#A78BFA", "#D9081018"),
        new("titanium-frost", "Titanium Frost", true, "#EEF3F7", "#F8FAFC", "#E4EBF0", "#D8E3EA", "#BBC9D3", "#17212B", "#61717E", "#1677A8", "#B9DCEC", "#2563EB", "#A05A00", "#16805A", "#C23E5A", "#4F6BED", "#9917212B"),
    ];

    public static ThemeOption Default => All[0];

    public static ThemeOption? Find(string? id) => All.FirstOrDefault(
        theme => string.Equals(theme.Id, id, StringComparison.Ordinal));
}

public sealed record ThemeOption(
    string Id,
    string DisplayName,
    bool IsLight,
    string AppBackground,
    string Surface,
    string SurfaceRaised,
    string SurfaceHover,
    string Border,
    string TextPrimary,
    string TextSecondary,
    string Accent,
    string AccentMuted,
    string MethodGet,
    string Warning,
    string DiffAdded,
    string DiffRemoved,
    string DiffHunk,
    string Overlay);
