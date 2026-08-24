using CommunityToolkit.Mvvm.Input;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private const int EnvironmentEditorTabIndex = 3;
    private const int SettingsEditorTabIndex = 4;

    [RelayCommand]
    private void ShowEnvironmentEditor() => ShowRequestEditorTab(EnvironmentEditorTabIndex);

    [RelayCommand]
    private void ShowSettingsEditor() => ShowRequestEditorTab(SettingsEditorTabIndex);

    private void ShowRequestEditorTab(int tabIndex)
    {
        if (IsCollectionRunnerBusy || IsCollectionRunExportBusy || IsCollectionRunDataBusy)
        {
            return;
        }

        IsHistoryVisible = false;
        IsGitVisible = false;
        CloseGitDiff();
        CloseGitCommit();
        CloseGitRemote();
        CloseGitFastForward();
        CloseGitPush();
        CloseCollectionRunner();
        RequestEditorTabIndex = tabIndex;
    }
}
