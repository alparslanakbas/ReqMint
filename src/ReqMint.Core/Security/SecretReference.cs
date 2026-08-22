namespace ReqMint.Core.Security;

public sealed record SecretReference(
    Guid WorkspaceId,
    Guid EnvironmentId,
    string VariableName);
