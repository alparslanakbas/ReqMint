using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.History;

public static class RequestHistoryPrivacy
{
    public const string RedactedValue = "[redacted]";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "cookie",
        "set-cookie",
        "x-api-key",
        "api-key",
        "apikey",
        "access-token",
        "refresh-token",
        "password",
        "secret",
        "token",
    };

    public static RequestDocument CreateSafeSnapshot(RequestDocument request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request with
        {
            Url = RedactUrl(request.Url),
            QueryParameters = Redact(request.QueryParameters),
            Headers = Redact(request.Headers),
            Body = null,
        };
    }

    private static IReadOnlyList<RequestField> Redact(IEnumerable<RequestField> fields) =>
        fields.Select(field => IsSensitive(field.Name)
            ? field with { Value = RedactedValue }
            : field).ToArray();

    private static bool IsSensitive(string name)
    {
        var normalized = name.Trim().Replace('_', '-');
        return SensitiveNames.Contains(normalized) ||
            normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("api-key", StringComparison.OrdinalIgnoreCase);
    }

    private static string RedactUrl(string url)
    {
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
        {
            return url;
        }

        var fragmentStart = url.IndexOf('#', queryStart);
        var queryEnd = fragmentStart < 0 ? url.Length : fragmentStart;
        var query = url[(queryStart + 1)..queryEnd];
        var redactedQuery = string.Join('&', query.Split('&').Select(RedactUrlParameter));
        return string.Concat(
            url.AsSpan(0, queryStart + 1),
            redactedQuery,
            url.AsSpan(queryEnd));
    }

    private static string RedactUrlParameter(string parameter)
    {
        var equalsIndex = parameter.IndexOf('=');
        var encodedName = equalsIndex < 0 ? parameter : parameter[..equalsIndex];
        var name = Uri.UnescapeDataString(encodedName.Replace('+', ' '));
        return IsSensitive(name)
            ? $"{encodedName}={Uri.EscapeDataString(RedactedValue)}"
            : parameter;
    }
}
