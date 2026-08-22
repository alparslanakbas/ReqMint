namespace ReqMint.Core.Workspaces;

public sealed record EnvironmentDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<EnvironmentVariable> Variables { get; init; } = [];
}

public sealed record EnvironmentVariable(string Name, string? Value, bool IsSecret = false);

