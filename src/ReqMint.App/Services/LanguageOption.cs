using System.Globalization;

namespace ReqMint.App.Services;

public sealed record LanguageOption(
    string Code,
    string DisplayName,
    string CultureName)
{
    public bool IsRightToLeft =>
        CultureInfo.GetCultureInfo(CultureName).TextInfo.IsRightToLeft;
}
