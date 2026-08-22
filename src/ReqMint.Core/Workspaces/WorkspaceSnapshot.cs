namespace ReqMint.Core.Workspaces;

public sealed record WorkspaceSnapshot(
    WorkspaceDocument Workspace,
    IReadOnlyList<CollectionDocument> Collections,
    IReadOnlyList<EnvironmentDocument> Environments);

