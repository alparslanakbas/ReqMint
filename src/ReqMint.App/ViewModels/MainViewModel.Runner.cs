using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Runner;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private CancellationTokenSource? _collectionRunCancellation;
    private CollectionRunResult? _latestCollectionRunResult;

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
        _latestCollectionRunResult = null;
        HasCollectionRunResult = false;
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
        if (IsCollectionRunnerBusy || IsCollectionRunExportBusy)
        {
            return;
        }

        IsCollectionRunnerVisible = false;
        CollectionRunTitle = string.Empty;
        CollectionRunSummary = string.Empty;
        CollectionRunProgress = string.Empty;
        CollectionRunResults.Clear();
        _latestCollectionRunResult = null;
        HasCollectionRunResult = false;
    }

    [RelayCommand]
    private async Task StartCollectionRunAsync(CancellationToken cancellationToken)
    {
        var snapshot = _workspaceSnapshot;
        var collection = GetSelectedCollectionForRun();
        if (!IsCollectionRunnerVisible
            || IsCollectionRunnerBusy
            || IsCollectionRunExportBusy
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
        _latestCollectionRunResult = null;
        HasCollectionRunResult = false;
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

            _latestCollectionRunResult = result;
            HasCollectionRunResult = result.Results.Count > 0;

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

    [RelayCommand]
    private Task ExportCollectionRunJsonAsync(CancellationToken cancellationToken) =>
        ExportCollectionRunAsync(CollectionRunExportFormat.Json, cancellationToken);

    [RelayCommand]
    private Task ExportCollectionRunJUnitAsync(CancellationToken cancellationToken) =>
        ExportCollectionRunAsync(CollectionRunExportFormat.JUnitXml, cancellationToken);

    private async Task ExportCollectionRunAsync(
        CollectionRunExportFormat format,
        CancellationToken cancellationToken)
    {
        var result = _latestCollectionRunResult;
        if (result is null
            || !HasCollectionRunResult
            || IsCollectionRunnerBusy
            || IsCollectionRunExportBusy)
        {
            return;
        }

        IsCollectionRunExportBusy = true;
        try
        {
            var extension = format == CollectionRunExportFormat.Json ? "json" : "xml";
            var saved = await _collectionRunExportService.ExportAsync(
                result,
                format,
                $"{CreateSafeExportName(result.CollectionName)}-run.{extension}",
                cancellationToken);
            if (saved)
            {
                WorkspaceStatus = format == CollectionRunExportFormat.Json
                    ? Localize("CollectionRunJsonExported", "JSON run report exported")
                    : Localize("CollectionRunJUnitExported", "JUnit XML run report exported");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "CollectionRunExportFailed",
                "Run report could not be exported safely");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsCollectionRunExportBusy = false;
        }
    }

    private static string CreateSafeExportName(string collectionName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        invalidCharacters.Add('/');
        invalidCharacters.Add('\\');
        var safeName = new string(collectionName
            .Trim()
            .Take(80)
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray()).Trim(' ', '.', '-');
        return string.IsNullOrEmpty(safeName) ? "reqmint-collection" : safeName;
    }

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
                : $"{result.Duration.TotalMilliseconds:N0} ms",
            CreateAssertionSummary(result.Assertions));
    }

    private string CreateAssertionSummary(
        IReadOnlyList<CollectionAssertionResult> assertions) => string.Join(
            " · ",
            assertions.Select(assertion =>
            {
                var name = assertion.Kind switch
                {
                    ReqMint.Core.Workspaces.RequestAssertionKind.StatusCodeEquals => Localize(
                        "CollectionAssertionStatus",
                        "Status"),
                    ReqMint.Core.Workspaces.RequestAssertionKind.MaximumDuration => Localize(
                        "CollectionAssertionDuration",
                        "Duration"),
                    _ => Localize("CollectionAssertionJsonField", "JSON field"),
                };
                var outcome = assertion.Outcome switch
                {
                    CollectionAssertionOutcome.Passed => Localize(
                        "CollectionAssertionPassed",
                        "passed"),
                    CollectionAssertionOutcome.Failed => Localize(
                        "CollectionAssertionFailed",
                        "failed"),
                    _ => Localize(
                        "CollectionAssertionUnable",
                        "not evaluated"),
                };
                return $"{name}: {outcome}";
            }));

    private ReqMint.Core.Workspaces.CollectionDocument? GetSelectedCollectionForRun() =>
        _workspaceSnapshot?.Collections.FirstOrDefault(
            collection => collection.Id == _selectedCollectionId);

    private void UpdateCollectionRunAvailability()
    {
        IsCollectionRunAvailable = GetSelectedCollectionForRun()?.Requests.Count > 0;
    }
}
