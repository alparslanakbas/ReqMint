using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task StartTutorialSampleAsync(CancellationToken cancellationToken)
    {
        if (!IsOnboardingVisible
            || !IsOnboardingReadyStep
            || IsWorkspaceBusy
            || IsSending)
        {
            return;
        }

        if (!await ConfirmNavigationAsync(cancellationToken))
        {
            return;
        }

        if (HasUnsavedNonRequestChanges())
        {
            WorkspaceStatus = Localize(
                "TutorialUnsavedWorkspaceChanges",
                "Save or discard workspace edits before opening the tutorial");
            return;
        }

        IsWorkspaceBusy = true;
        WorkspaceStatus = Localize(
            "TutorialStartingStatus",
            "Preparing the local tutorial");
        try
        {
            var session = await _tutorialSessionService.StartAsync(cancellationToken);
            ApplyWorkspace(
                session.Snapshot,
                session.WorkspaceDirectory,
                selectedCollectionId: session.CollectionId,
                selectedEnvironmentId: session.EnvironmentId);
            await LoadHistoryAsync(session.Snapshot.Workspace.Id, cancellationToken);
            await RefreshGitStatusAsync(session.WorkspaceDirectory, cancellationToken);
            ResetRequestDraft();
            LoadRequestDraft(session.DraftRequest);
            _activeTutorialSession = session;
            TutorialGuideStage = TutorialGuideStage.Send;
            IsTutorialGuideVisible = true;
            IsOnboardingVisible = false;
            SaveOnboardingProgress(OnboardingStatus.Completed);
            WorkspaceStatus = Localize(
                "TutorialReadyStatus",
                "Local tutorial ready");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorkspaceStatus = Localize(
                "TutorialCancelledStatus",
                "Tutorial preparation cancelled");
        }
        catch (Exception exception)
        {
            WorkspaceStatus = Localize(
                "TutorialFailedStatus",
                "The local tutorial could not be prepared");
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand]
    private void ContinueOnboarding()
    {
        if (!IsOnboardingVisible)
        {
            return;
        }

        if (OnboardingStep < JsonAppSettingsService.MaximumOnboardingStep)
        {
            OnboardingStep++;
            SaveOnboardingProgress(OnboardingStatus.InProgress);
            return;
        }

        IsOnboardingVisible = false;
        SaveOnboardingProgress(OnboardingStatus.Completed);
        WorkspaceStatus = Localize(
            "OnboardingCompletedStatus",
            "Welcome to ReqMint");
    }

    [RelayCommand]
    private void PreviousOnboardingStep()
    {
        if (!IsOnboardingVisible || OnboardingStep == 0)
        {
            return;
        }

        OnboardingStep--;
        SaveOnboardingProgress(OnboardingStatus.InProgress);
    }

    [RelayCommand]
    private void SkipOnboarding()
    {
        if (!IsOnboardingVisible)
        {
            return;
        }

        IsOnboardingVisible = false;
        SaveOnboardingProgress(OnboardingStatus.Skipped);
    }

    [RelayCommand]
    private void RestartOnboarding()
    {
        OnboardingStep = 0;
        IsOnboardingVisible = true;
        SaveOnboardingProgress(OnboardingStatus.InProgress);
    }

    [RelayCommand]
    private void DismissTutorialGuide() => IsTutorialGuideVisible = false;

    private void InitializeOnboarding(AppSettings settings)
    {
        var shouldResume = settings.OnboardingStatus is
            OnboardingStatus.NotStarted or OnboardingStatus.InProgress;
        OnboardingStep = settings.OnboardingStatus == OnboardingStatus.InProgress
            ? Math.Clamp(
                settings.OnboardingStep,
                0,
                JsonAppSettingsService.MaximumOnboardingStep)
            : 0;
        IsOnboardingVisible = shouldResume;
    }

    private void SaveOnboardingProgress(OnboardingStatus status) =>
        _appSettings.Update(_appSettings.Current with
        {
            OnboardingStatus = status,
            OnboardingStep = OnboardingStep,
        });

    private void AdvanceTutorialAfterResponse(
        ReqMint.Core.Requests.ApiRequest request,
        ReqMint.Core.Requests.ApiResponse response)
    {
        var session = _activeTutorialSession;
        if (!IsTutorialGuideVisible
            || TutorialGuideStage != TutorialGuideStage.Send
            || session is null
            || response.StatusCode != 200
            || request.Url != new Uri(session.BaseUri, "api/hello"))
        {
            return;
        }

        TutorialGuideStage = TutorialGuideStage.Save;
        WorkspaceStatus = Localize(
            "TutorialResponseReceivedStatus",
            "Tutorial response received");
    }

    private void AdvanceTutorialAfterSave(ReqMint.Core.Workspaces.RequestDocument request)
    {
        var session = _activeTutorialSession;
        if (!IsTutorialGuideVisible
            || TutorialGuideStage != TutorialGuideStage.Save
            || session is null
            || !string.Equals(
                _workspaceDirectory,
                session.WorkspaceDirectory,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                request.Url,
                session.DraftRequest.Url,
                StringComparison.Ordinal))
        {
            return;
        }

        TutorialGuideStage = TutorialGuideStage.Complete;
        WorkspaceStatus = Localize(
            "TutorialCompletedStatus",
            "First local request completed");
    }

    private async Task RecordHistoryUnlessTutorialAsync(
        ReqMint.Core.Workspaces.RequestDocument requestDocument,
        ReqMint.Core.Requests.ApiRequest request,
        ReqMint.Core.Requests.ApiResponse? response,
        string outcome)
    {
        var session = _activeTutorialSession;
        if (session is not null
            && string.Equals(
                _workspaceDirectory,
                session.WorkspaceDirectory,
                StringComparison.OrdinalIgnoreCase)
            && request.Url == new Uri(session.BaseUri, "api/hello"))
        {
            return;
        }

        await RecordHistoryAsync(requestDocument, response, outcome);
    }
}

public enum TutorialGuideStage
{
    Send,
    Save,
    Complete,
}
