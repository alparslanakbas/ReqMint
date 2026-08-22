namespace ReqMint.Core.Workspaces;

public interface IWorkspaceStore
{
    Task<WorkspaceSnapshot> LoadAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string workspaceDirectory,
        WorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

