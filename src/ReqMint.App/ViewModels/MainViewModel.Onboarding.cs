using CommunityToolkit.Mvvm.Input;
using ReqMint.App.Services;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
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
}
