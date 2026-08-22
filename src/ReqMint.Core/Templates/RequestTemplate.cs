using System.Text.RegularExpressions;

namespace ReqMint.Core.Templates;

public static partial class RequestTemplate
{
    public static bool ContainsVariables(string value) => VariablePattern().IsMatch(value);

    public static IReadOnlySet<string> FindVariables(IEnumerable<string?> values)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            foreach (Match match in VariablePattern().Matches(value))
            {
                variables.Add(match.Groups["name"].Value);
            }
        }

        return variables;
    }

    public static string Resolve(
        string value,
        IReadOnlyDictionary<string, string> variables) =>
        VariablePattern().Replace(
            value,
            match => variables[match.Groups["name"].Value]);

    [GeneratedRegex(
        "\\{\\{\\s*(?<name>[A-Za-z_][A-Za-z0-9_.-]*)\\s*\\}\\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}
