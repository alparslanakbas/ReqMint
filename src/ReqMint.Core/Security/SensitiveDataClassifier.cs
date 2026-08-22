using System.Text.RegularExpressions;

namespace ReqMint.Core.Security;

public static partial class SensitiveDataClassifier
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.Ordinal)
    {
        "authorization",
        "proxyauthorization",
        "cookie",
        "setcookie",
        "xapikey",
        "apikey",
        "accesstoken",
        "refreshtoken",
        "clientsecret",
        "password",
        "passwd",
        "secret",
        "token",
        "credential",
        "privatekey",
    };

    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = new string(name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return SensitiveNames.Contains(normalized)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("password", StringComparison.Ordinal)
            || normalized.Contains("secret", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("privatekey", StringComparison.Ordinal);
    }

    public static bool IsPlaceholderOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("[redacted]", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("<secret>", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var literal = TemplatePattern().Replace(trimmed, string.Empty).Trim();
        return string.IsNullOrEmpty(literal)
            || literal.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || literal.Equals("Basic", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsKnownCredentialPattern(string? value) =>
        !string.IsNullOrEmpty(value) && CredentialPattern().IsMatch(value);

    public static bool ContainsSensitiveAssignment(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (Match match in SensitiveAssignmentPattern().Matches(value))
        {
            if (!IsPlaceholderOnly(match.Groups["value"].Value))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\{\{[^{}]+\}\}|\$\{[^{}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplatePattern();

    [GeneratedRegex(
        @"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|\b(?:gh[pousr]_[A-Za-z0-9]{20,})\b|\b(?:AKIA|ASIA)[A-Z0-9]{16}\b|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();

    [GeneratedRegex(
        @"(?:authorization|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd|private[_-]?key)[\\\""']*\s*[:=]\s*[\\\""']*(?<value>[^\\\""';,&\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();
}
