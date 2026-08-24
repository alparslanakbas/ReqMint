using ReqMint.Core.Workspaces;

namespace ReqMint.App.Services;

public interface ITutorialSessionService : IDisposable
{
    Task<TutorialSession> StartAsync(CancellationToken cancellationToken = default);
}

public sealed record TutorialSession(
    string WorkspaceDirectory,
    Uri BaseUri,
    WorkspaceSnapshot Snapshot,
    RequestDocument DraftRequest,
    Guid CollectionId,
    Guid EnvironmentId);
