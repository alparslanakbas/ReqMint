using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReqMint.App.Services;
using ReqMint.App.ViewModels;
using ReqMint.App.Views;
using ReqMint.Http;
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
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainViewModel(
                _requestExecutor,
                new WorkspaceJsonStore(),
                new AvaloniaWorkspaceFolderPicker(mainWindow));
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => _requestExecutor.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
