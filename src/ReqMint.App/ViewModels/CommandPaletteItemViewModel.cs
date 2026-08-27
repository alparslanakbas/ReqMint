using CommunityToolkit.Mvvm.ComponentModel;

namespace ReqMint.App.ViewModels;

/// <summary>
/// A single entry in the command palette. <see cref="SearchText"/> is the folded
/// form used for matching, so a query typed without Turkish diacritics still
/// finds an entry that has them.
/// </summary>
public sealed partial class CommandPaletteItemViewModel : ViewModelBase
{
    public CommandPaletteItemViewModel(
        string title,
        string category,
        Func<Task> invoke,
        string? keywords = null)
    {
        Title = title;
        Category = category;
        Invoke = invoke;
        SearchText = CommandPaletteSearch.Fold(
            keywords is null ? $"{title} {category}" : $"{title} {category} {keywords}");
    }

    public string Title { get; }

    public string Category { get; }

    public Func<Task> Invoke { get; }

    public string SearchText { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Matching helpers for the command palette.
/// </summary>
public static class CommandPaletteSearch
{
    /// <summary>
    /// Lower-cases a value and strips Turkish diacritics so "sifirla" matches
    /// "Sıfırla" and "cevir" matches "Çevir". Casing is folded culture
    /// independently on purpose: a Turkish culture would turn the English "I"
    /// in words like "API" into a dotless "ı" and break matching.
    /// </summary>
    public static string Fold(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var folded = new char[value.Length];
        for (var index = 0; index < value.Length; index++)
        {
            folded[index] = value[index] switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                'Â' or 'â' => 'a',
                var character => char.ToLowerInvariant(character),
            };
        }

        return new string(folded);
    }

    /// <summary>
    /// True when every whitespace separated part of the query appears in the
    /// entry, so "tema koyu" finds "Tema: Nocturne Koyu" regardless of order.
    /// </summary>
    public static bool Matches(string foldedEntry, IReadOnlyList<string> foldedQueryParts)
    {
        foreach (var part in foldedQueryParts)
        {
            if (!foldedEntry.Contains(part, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
