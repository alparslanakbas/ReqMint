using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;
using ReqMint.Core.Runner;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private CancellationTokenSource? _collectionRunCancellation;
    private CollectionRunResult? _latestCollectionRunResult;
    private CollectionRunDataSet? _collectionRunDataSet;
    private CollectionRunDataSet? _latestCollectionRunDataSet;
    private readonly List<CollectionRunItemViewModel> _allCollectionRunResultItems = [];

    [RelayCommand]
    private async Task OpenCollectionRunnerAsync(CancellationToken cancellationToken)
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
        IsApplicationSettingsVisible = false;
        CloseGitDiff();
        CloseGitCommit();
        CloseGitRemote();
        CloseGitFastForward();
        CloseGitPush();
        ResetCollectionRunResultState();
        ResetCollectionRunData();
        CollectionRunTitle = collection.Name;
        CollectionRunSummary = Localize(
            "CollectionRunReadySummary",
            "{0} saved requests are ready to run in order",
            collection.Requests.Count);
        CollectionRunProgress = string.Empty;
        IsCollectionRunnerVisible = true;
        await LoadCollectionRunHistoryAsync(cancellationToken);
    }

    [RelayCommand]
    private void CloseCollectionRunner()
    {
        if (IsCollectionRunnerBusy || IsCollectionRunExportBusy || IsCollectionRunDataBusy)
        {
            return;
        }

        IsCollectionRunnerVisible = false;
        CollectionRunTitle = string.Empty;
        CollectionRunSummary = string.Empty;
        CollectionRunProgress = string.Empty;
        ResetCollectionRunResultState();
        ResetCollectionRunData();
        SelectedCollectionRunHistoryItem = null;
        CollectionRunHistory.Clear();
        CollectionRunHistoryStatus = string.Empty;
        OnPropertyChanged(nameof(IsCollectionRunHistoryEmpty));
    }

    [RelayCommand]
    private Task StartCollectionRunAsync(CancellationToken cancellationToken) =>
        RunCollectionAsync([], _collectionRunDataSet, cancellationToken);

    [RelayCommand]
    private async Task RerunFailedCollectionRequestsAsync(CancellationToken cancellationToken)
    {
        var result = _latestCollectionRunResult;
        var collection = GetSelectedCollectionForRun();
        if (result is null
            || collection is null
            || !CanRerunFailedCollectionResults
            || !IsCollectionRunnerInteractionEnabled)
        {
            return;
        }

        var requestIds = collection.Requests.Select(request => request.Id).ToHashSet();
        var selection = result.Results
            .Where(request => request.State is
                CollectionRequestRunState.Failed or CollectionRequestRunState.Error)
            .Select(request => new CollectionRunExecutionKey(
                request.RequestId,
                request.IterationNumber))
            .Where(key => requestIds.Contains(key.RequestId))
            .Distinct()
            .ToArray();
        if (selection.Length != result.FailedCount || selection.Length == 0)
        {
            CanRerunFailedCollectionResults = false;
            CollectionRunRerunUnavailableReason = Localize(
                "CollectionRunRerunCollectionChanged",
                "The saved collection changed; run the full collection again");
            return;
        }

        await RunCollectionAsync(selection, _latestCollectionRunDataSet, cancellationToken);
    }

    private async Task RunCollectionAsync(
        IReadOnlyList<CollectionRunExecutionKey> executionSelection,
        CollectionRunDataSet? dataSet,
        CancellationToken cancellationToken)
    {
        var snapshot = _workspaceSnapshot;
        var collection = GetSelectedCollectionForRun();
        if (!IsCollectionRunnerVisible
            || IsCollectionRunnerBusy
            || IsCollectionRunExportBusy
            || IsCollectionRunDataBusy
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
        ResetCollectionRunResultState();
        SelectedCollectionRunHistoryItem = null;
        CollectionRunSummary = executionSelection.Count == 0
            ? Localize(
                "CollectionRunRunning",
                "Running saved requests sequentially")
            : Localize(
                "CollectionRunRerunRunning",
                "Rerunning failed requests sequentially");
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
                    DataRows = dataSet?.Rows ?? [],
                    ExecutionSelection = executionSelection,
                },
                progress,
                _collectionRunCancellation.Token);

            DisplayCollectionRunResult(
                result,
                dataSet,
                allowRerun: true);

            CollectionRunSummary = result.WasCancelled
                ? Localize(
                    "CollectionRunCancelledSummary",
                    "Run cancelled · {0} completed",
                    result.CompletedCount)
                : result.WasRerun
                    ? Localize(
                        "CollectionRunRerunCompletedSummary",
                        "Rerun completed · {0} passed · {1} failed",
                        result.PassedCount,
                        result.FailedCount)
                    : Localize(
                        "CollectionRunCompletedSummary",
                        "Completed · {0} passed · {1} failed",
                        result.PassedCount,
                        result.FailedCount);
            WorkspaceStatus = result.WasCancelled
                ? Localize("CollectionRunCancelled", "Collection run cancelled")
                : Localize("CollectionRunCompleted", "Collection run completed");
            await SaveCollectionRunHistoryAsync(
                snapshot.Workspace.Id,
                result,
                cancellationToken);
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
    private void ShowAllCollectionRunResults() =>
        SetCollectionRunResultFilter(CollectionRunResultFilter.All);

    [RelayCommand]
    private void ShowPassedCollectionRunResults() =>
        SetCollectionRunResultFilter(CollectionRunResultFilter.Passed);

    [RelayCommand]
    private void ShowFailedCollectionRunResults() =>
        SetCollectionRunResultFilter(CollectionRunResultFilter.Failed);

    [RelayCommand]
    private void ShowSkippedCollectionRunResults() =>
        SetCollectionRunResultFilter(CollectionRunResultFilter.Skipped);

    [RelayCommand]
    private async Task SelectCollectionRunDataFileAsync(CancellationToken cancellationToken)
    {
        var collection = GetSelectedCollectionForRun();
        if (collection is null
            || !IsCollectionRunnerVisible
            || !IsCollectionRunnerInteractionEnabled)
        {
            return;
        }

        IsCollectionRunDataBusy = true;
        try
        {
            var selection = await _collectionRunDataFileService.LoadAsync(cancellationToken);
            if (selection is null)
            {
                return;
            }

            var executionCount = (long)selection.DataSet.Rows.Count * collection.Requests.Count;
            if (executionCount > CollectionRunner.MaximumExecutionCount)
            {
                throw new CollectionRunDataException(
                    "The selected data would create too many request executions.");
            }

            _collectionRunDataSet = selection.DataSet;
            InvalidateCollectionRunRerun();
            HasCollectionRunData = true;
            CollectionRunDataFileName = selection.FileName;
            CollectionRunDataSummary = Localize(
                "CollectionRunDataSummary",
                "{0} rows · {1} request executions",
                selection.DataSet.Rows.Count,
                executionCount);
            CollectionRunSummary = Localize(
                "CollectionRunDataReadySummary",
                "{0} requests will run across {1} data rows",
                collection.Requests.Count,
                selection.DataSet.Rows.Count);
            WorkspaceStatus = Localize(
                "CollectionRunDataLoaded",
                "Collection run data loaded");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "CollectionRunDataInvalid",
                "Run data could not be loaded safely");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsCollectionRunDataBusy = false;
        }
    }

    [RelayCommand]
    private void ClearCollectionRunData()
    {
        if (!IsCollectionRunnerInteractionEnabled)
        {
            return;
        }

        ResetCollectionRunData();
        InvalidateCollectionRunRerun();
        var collection = GetSelectedCollectionForRun();
        if (collection is not null)
        {
            CollectionRunSummary = Localize(
                "CollectionRunReadySummary",
                "{0} saved requests are ready to run in order",
                collection.Requests.Count);
        }
    }

    [RelayCommand]
    private Task RefreshCollectionRunHistoryAsync(CancellationToken cancellationToken) =>
        LoadCollectionRunHistoryAsync(cancellationToken);

    [RelayCommand]
    private async Task ClearCollectionRunHistoryAsync(CancellationToken cancellationToken)
    {
        var snapshot = _workspaceSnapshot;
        var collection = GetSelectedCollectionForRun();
        if (snapshot is null
            || collection is null
            || CollectionRunHistory.Count == 0
            || !IsCollectionRunnerInteractionEnabled)
        {
            return;
        }

        if (!await _collectionRunHistoryClearPrompt.ShowAsync(
            collection.Name,
            CollectionRunHistory.Count))
        {
            return;
        }

        try
        {
            await _collectionRunHistoryStore.ClearAsync(
                snapshot.Workspace.Id,
                collection.Id,
                cancellationToken);
            SelectedCollectionRunHistoryItem = null;
            CollectionRunHistory.Clear();
            ResetCollectionRunResultState();
            CollectionRunProgress = string.Empty;
            CollectionRunSummary = Localize(
                "CollectionRunReadySummary",
                "{0} saved requests are ready to run in order",
                collection.Requests.Count);
            CollectionRunHistoryStatus = Localize(
                "CollectionRunHistoryCleared",
                "Run history cleared");
            OnPropertyChanged(nameof(IsCollectionRunHistoryEmpty));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CollectionRunHistoryStatus = Localize(
                "CollectionRunHistoryClearFailed",
                "Run history could not be cleared");
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

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

    partial void OnSelectedCollectionRunHistoryItemChanged(
        CollectionRunHistoryItemViewModel? value)
    {
        if (value is null || !IsCollectionRunnerInteractionEnabled)
        {
            return;
        }

        var result = value.Entry.ToRunResult();
        DisplayCollectionRunResult(
            result,
            dataSet: null,
            allowRerun: !result.UsedDataFile);
        CollectionRunProgress = string.Empty;
        CollectionRunSummary = Localize(
            "CollectionRunHistorySelectedSummary",
            "Previous run · {0} passed · {1} failed",
            result.PassedCount,
            result.FailedCount);
        WorkspaceStatus = Localize(
            "CollectionRunHistoryOpened",
            "Previous collection run opened");
    }

    partial void OnCollectionRunHistoryRetentionLimitChanged(decimal value)
    {
        var limit = (int)Math.Clamp(
            value,
            JsonAppSettingsService.MinimumCollectionRunHistoryRetentionLimit,
            JsonAppSettingsService.MaximumCollectionRunHistoryRetentionLimit);
        if (value != limit)
        {
            CollectionRunHistoryRetentionLimit = limit;
            return;
        }

        if (_appSettings is not null
            && _appSettings.Current.CollectionRunHistoryRetentionLimit != limit)
        {
            _appSettings.Update(_appSettings.Current with
            {
                CollectionRunHistoryRetentionLimit = limit,
            });
        }
    }

    private void DisplayCollectionRunResult(
        CollectionRunResult result,
        CollectionRunDataSet? dataSet,
        bool allowRerun)
    {
        _allCollectionRunResultItems.Clear();
        foreach (var requestResult in result.Results)
        {
            _allCollectionRunResultItems.Add(CreateCollectionRunItem(
                requestResult,
                result.IterationCount > 1));
        }

        _latestCollectionRunResult = result;
        _latestCollectionRunDataSet = dataSet;
        HasCollectionRunResult = result.Results.Count > 0;
        HasFailedCollectionRunResults = result.FailedCount > 0;
        CollectionRunRerunFailedLabel = Localize(
            "CollectionRunRerunFailed",
            "Rerun failed ({0})",
            result.FailedCount);
        CanRerunFailedCollectionResults = HasFailedCollectionRunResults
            && allowRerun
            && (!result.UsedDataFile
                || dataSet?.Rows.Count == result.IterationCount);
        CollectionRunRerunUnavailableReason = HasFailedCollectionRunResults
            && !CanRerunFailedCollectionResults
                ? Localize(
                    "CollectionRunRerunDataUnavailable",
                    "The original iteration data is not stored; run the full collection with its data file")
                : string.Empty;
        SetCollectionRunResultFilter(CollectionRunResultFilter.All);
    }

    private async Task SaveCollectionRunHistoryAsync(
        Guid workspaceId,
        CollectionRunResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = CollectionRunHistoryEntry.Create(
                workspaceId,
                result,
                DateTimeOffset.UtcNow);
            await _collectionRunHistoryStore.AddAsync(
                entry,
                (int)CollectionRunHistoryRetentionLimit,
                cancellationToken);
            await LoadCollectionRunHistoryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CollectionRunHistoryStatus = Localize(
                "CollectionRunHistorySaveFailed",
                "Run completed but history could not be saved");
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private async Task LoadCollectionRunHistoryAsync(CancellationToken cancellationToken)
    {
        var snapshot = _workspaceSnapshot;
        var collection = GetSelectedCollectionForRun();
        if (snapshot is null || collection is null)
        {
            return;
        }

        try
        {
            var entries = await _collectionRunHistoryStore.ListAsync(
                snapshot.Workspace.Id,
                collection.Id,
                (int)CollectionRunHistoryRetentionLimit,
                cancellationToken);
            SelectedCollectionRunHistoryItem = null;
            CollectionRunHistory.Clear();
            foreach (var entry in entries)
            {
                CollectionRunHistory.Add(new CollectionRunHistoryItemViewModel(
                    entry.RecordedAtUtc.ToLocalTime().ToString("g"),
                    entry.WasRerun
                        ? Localize(
                            "CollectionRunHistoryRerunItemSummary",
                            "Rerun · {0} passed · {1} failed",
                            entry.PassedCount,
                            entry.FailedCount)
                        : Localize(
                            "CollectionRunHistoryItemSummary",
                            "{0} passed · {1} failed",
                            entry.PassedCount,
                            entry.FailedCount),
                    entry));
            }

            CollectionRunHistoryStatus = entries.Count == 0
                ? Localize("CollectionRunHistoryEmpty", "No previous runs")
                : Localize(
                    "CollectionRunHistoryCount",
                    "{0} previous runs stored locally",
                    entries.Count);
            OnPropertyChanged(nameof(IsCollectionRunHistoryEmpty));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SelectedCollectionRunHistoryItem = null;
            CollectionRunHistory.Clear();
            CollectionRunHistoryStatus = Localize(
                "CollectionRunHistoryUnavailable",
                "Run history is unavailable");
            OnPropertyChanged(nameof(IsCollectionRunHistoryEmpty));
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private CollectionRunItemViewModel CreateCollectionRunItem(
        CollectionRequestRunResult result,
        bool hasMultipleIterations)
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

        var assertionSummary = CreateAssertionSummary(result.Assertions);
        var iterationSummary = hasMultipleIterations
            ? Localize(
                "CollectionRunIteration",
                "Iteration {0}",
                result.IterationNumber)
            : string.Empty;
        var metadata = string.Join(
            " · ",
            new[] { iterationSummary, assertionSummary }.Where(value =>
                !string.IsNullOrEmpty(value)));

        return new CollectionRunItemViewModel(
            result.RequestName,
            status,
            detail,
            result.Duration == TimeSpan.Zero
                ? "—"
                : $"{result.Duration.TotalMilliseconds:N0} ms",
            metadata,
            result.State);
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

    private void SetCollectionRunResultFilter(CollectionRunResultFilter filter)
    {
        SelectedCollectionRunResultFilter = filter;
        var items = filter switch
        {
            CollectionRunResultFilter.Passed => _allCollectionRunResultItems.Where(item =>
                item.State == CollectionRequestRunState.Passed),
            CollectionRunResultFilter.Failed => _allCollectionRunResultItems.Where(item =>
                item.State is CollectionRequestRunState.Failed or CollectionRequestRunState.Error),
            CollectionRunResultFilter.Skipped => _allCollectionRunResultItems.Where(item =>
                item.State is CollectionRequestRunState.Cancelled or CollectionRequestRunState.NotRun),
            _ => _allCollectionRunResultItems,
        };

        CollectionRunResults.Clear();
        foreach (var item in items)
        {
            CollectionRunResults.Add(item);
        }

        CollectionRunResultFilterStatus = _allCollectionRunResultItems.Count == 0
            ? string.Empty
            : Localize(
                "CollectionRunFilterStatus",
                "Showing {0} of {1}",
                CollectionRunResults.Count,
                _allCollectionRunResultItems.Count);
    }

    private void ResetCollectionRunResultState()
    {
        CollectionRunResults.Clear();
        _allCollectionRunResultItems.Clear();
        _latestCollectionRunResult = null;
        _latestCollectionRunDataSet = null;
        HasCollectionRunResult = false;
        HasFailedCollectionRunResults = false;
        CanRerunFailedCollectionResults = false;
        CollectionRunRerunFailedLabel = string.Empty;
        CollectionRunRerunUnavailableReason = string.Empty;
        SelectedCollectionRunResultFilter = CollectionRunResultFilter.All;
        CollectionRunResultFilterStatus = string.Empty;
    }

    private void InvalidateCollectionRunRerun()
    {
        _latestCollectionRunDataSet = null;
        if (!HasFailedCollectionRunResults)
        {
            return;
        }

        CanRerunFailedCollectionResults = false;
        CollectionRunRerunUnavailableReason = Localize(
            "CollectionRunRerunInputsChanged",
            "Iteration inputs changed; run the full collection before rerunning failures");
    }

    private void ResetCollectionRunData()
    {
        _collectionRunDataSet = null;
        HasCollectionRunData = false;
        CollectionRunDataFileName = Localize(
            "CollectionRunNoDataFile",
            "No data file selected");
        CollectionRunDataSummary = Localize(
            "CollectionRunDataOptional",
            "Optional · JSON or CSV · up to 100 rows");
    }
}
