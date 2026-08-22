using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Runner;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private CancellationTokenSource? _collectionRunCancellation;

    [RelayCommand]
    private void OpenCollectionRunner()
    {
        var collection = GetSelectedCollectionForRun();
        if (!IsCollectionRunAvailable || collection is null || IsCollectionRunnerBusy)
        {
            return;
        }

        if (HasUnsavedWorkspaceChanges())
        {
            WorkspaceStatus = Localize(
                "CollectionRunUnsavedChanges",
                "Save or discard workspace edits before running the collection");
            return;
        }

        IsHistoryVisible = false;
        IsGitVisible = false;
        CloseGitDiff();
        CloseGitCommit();
        CloseGitRemote();
        CloseGitFastForward();
        CloseGitPush();
        CollectionRunResults.Clear();
        CollectionRunTitle = collection.Name;
        CollectionRunSummary = Localize(
            "CollectionRunReadySummary",
            "{0} saved requests are ready to run in order",
            collection.Requests.Count);
        CollectionRunProgress = string.Empty;
        IsCollectionRunnerVisible = true;
    }

    [RelayCommand]
    private void CloseCollectionRunner()
    {
        if (IsCollectionRunnerBusy)
        {
            return;
        }

        IsCollectionRunnerVisible = false;
        CollectionRunTitle = string.Empty;
        CollectionRunSummary = string.Empty;
        CollectionRunProgress = string.Empty;
        CollectionRunResults.Clear();
    }

    [RelayCommand]
    private async Task StartCollectionRunAsync(CancellationToken cancellationToken)
    {
        var snapshot = _workspaceSnapshot;
        var collection = GetSelectedCollectionForRun();
        if (!IsCollectionRunnerVisible
            || IsCollectionRunnerBusy
            || snapshot is null
            || collection is null)
        {
            return;
        }

        if (HasUnsavedWorkspaceChanges())
        {
            WorkspaceStatus = Localize(
                "CollectionRunUnsavedChanges",
                "Save or discard workspace edits before running the collection");
            return;
        }

        IsCollectionRunnerBusy = true;
        CollectionRunResults.Clear();
        CollectionRunSummary = Localize(
            "CollectionRunRunning",
            "Running saved requests sequentially");
        _collectionRunCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var progress = new Progress<CollectionRunProgress>(UpdateCollectionRunProgress);
        try
        {
            var result = await _collectionRunner.RunAsync(
                new CollectionRunDefinition
                {
                    WorkspaceId = snapshot.Workspace.Id,
                    Collection = collection,
                    Environment = _activeEnvironment,
                    StopOnFailure = CollectionRunStopOnFailure,
                },
                progress,
                _collectionRunCancellation.Token);

            CollectionRunResults.Clear();
            foreach (var requestResult in result.Results)
            {
                CollectionRunResults.Add(CreateCollectionRunItem(requestResult));
            }

            CollectionRunSummary = result.WasCancelled
                ? Localize(
                    "CollectionRunCancelledSummary",
                    "Run cancelled · {0} completed",
                    result.CompletedCount)
                : Localize(
                    "CollectionRunCompletedSummary",
                    "Completed · {0} passed · {1} failed",
                    result.PassedCount,
                    result.FailedCount);
            WorkspaceStatus = result.WasCancelled
                ? Localize("CollectionRunCancelled", "Collection run cancelled")
                : Localize("CollectionRunCompleted", "Collection run completed");
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = Localize("CollectionRunCancelled", "Collection run cancelled");
        }
        catch (Exception exception)
        {
            CollectionRunSummary = Localize(
                "CollectionRunFailed",
                "Collection run could not be completed safely");
            WorkspaceStatus = CollectionRunSummary;
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            _collectionRunCancellation.Dispose();
            _collectionRunCancellation = null;
            IsCollectionRunnerBusy = false;
        }
    }

    [RelayCommand]
    private void CancelCollectionRun() => _collectionRunCancellation?.Cancel();

    private void UpdateCollectionRunProgress(CollectionRunProgress progress)
    {
        CollectionRunProgress = Localize(
            "CollectionRunProgress",
            "{0} / {1} completed",
            progress.CompletedRequestCount,
            progress.TotalRequestCount);
    }

    private CollectionRunItemViewModel CreateCollectionRunItem(
        CollectionRequestRunResult result)
    {
        var status = result.State switch
        {
            CollectionRequestRunState.Passed => Localize("CollectionRunPassed", "Passed"),
            CollectionRequestRunState.Failed => Localize("CollectionRunFailedStatus", "Failed"),
            CollectionRequestRunState.Error => Localize("CollectionRunError", "Error"),
            CollectionRequestRunState.Cancelled => Localize("CollectionRunCancelledStatus", "Cancelled"),
            _ => Localize("CollectionRunNotRun", "Not run"),
        };
        var detail = result.StatusCode is { } statusCode
            ? $"HTTP {statusCode}"
            : result.ErrorKind switch
            {
                CollectionRunErrorKind.MissingVariables => Localize(
                    "CollectionRunMissingVariables",
                    "Missing environment values"),
                CollectionRunErrorKind.Timeout => Localize(
                    "CollectionRunTimeout",
                    "Request timed out"),
                CollectionRunErrorKind.Transport => Localize(
                    "CollectionRunTransport",
                    "Network request failed"),
                CollectionRunErrorKind.InvalidRequest => Localize(
                    "CollectionRunInvalidRequest",
                    "Invalid request configuration"),
                _ => string.Empty,
            };

        return new CollectionRunItemViewModel(
            result.RequestName,
            status,
            detail,
            result.Duration == TimeSpan.Zero
                ? "—"
                : $"{result.Duration.TotalMilliseconds:N0} ms");
    }

    private ReqMint.Core.Workspaces.CollectionDocument? GetSelectedCollectionForRun() =>
        _workspaceSnapshot?.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId);

    private void UpdateCollectionRunAvailability()
    {
        IsCollectionRunAvailable = GetSelectedCollectionForRun()?.Requests.Count > 0;
    }
}
