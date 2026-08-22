namespace ReqMint.Core.Templates;

public sealed class RequestTemplateResolutionException : Exception
{
    public RequestTemplateResolutionException(IEnumerable<string> missingVariables)
        : this(missingVariables.Order(StringComparer.OrdinalIgnoreCase).ToArray())
    {
    }

    private RequestTemplateResolutionException(IReadOnlyList<string> missingVariables)
        : base($"Missing environment values: {string.Join(", ", missingVariables)}.")
    {
        MissingVariables = missingVariables;
    }

    public IReadOnlyList<string> MissingVariables { get; }
}
