using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReqMint.App.Services;
using ReqMint.App.ViewModels;
using ReqMint.Core.Templates;
using ReqMint.App.Views;
using ReqMint.Http;
using ReqMint.Platform.Security;
using ReqMint.Platform.Git;
using ReqMint.Storage;

namespace ReqMint.App;

public partial class App : Application
{
    private HttpRequestExecutor? _requestExecutor;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _requestExecutor = new HttpRequestExecutor();
            var secretVault = PlatformSecretVaultFactory.Create();
            var applicationData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReqMint");
            var settings = new JsonAppSettingsService(applicationData);
            var localization = new LocalizationService(settings);
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainViewModel(
                _requestExecutor,
                new WorkspaceJsonStore(),
                new AvaloniaWorkspaceFolderPicker(mainWindow),
                new RequestTemplateResolver(secretVault),
                secretVault,
                localization,
                new AvaloniaUnsavedChangesPrompt(mainWindow, localization),
                new SqliteRequestHistoryStore(Path.Combine(applicationData, "reqmint.db")),
                new AvaloniaHistoryClearPrompt(mainWindow, localization),
                settings,
                new SystemGitService(),
                new WorkspaceGitSecretScanner());
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => _requestExecutor.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
