namespace ReqMint.Core.Templates;

public sealed class AuthenticationSecretNotProtectedException(string variableName)
    : ArgumentException($"Authentication variable '{variableName}' must exist and be marked Secret in the active environment.")
{
    public string VariableName { get; } = variableName;
}
