using CommunityToolkit.Mvvm.Input;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private const int EnvironmentEditorTabIndex = 3;
    [RelayCommand]
    private void ShowEnvironmentEditor() => ShowRequestEditorTab(EnvironmentEditorTabIndex);

    [RelayCommand]
    private void ShowSettingsEditor()
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
        IsApplicationSettingsVisible = true;
    }

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
        IsApplicationSettingsVisible = false;
        RequestEditorTabIndex = tabIndex;
    }
}
