namespace ReqMint.Core.Workspaces;

public sealed record WorkspaceDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<WorkspaceFileReference> Collections { get; init; } = [];

    public IReadOnlyList<WorkspaceFileReference> Environments { get; init; } = [];
}

public sealed record WorkspaceFileReference(Guid Id, string Name, string File);

