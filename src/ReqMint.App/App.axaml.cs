using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReqMint.App.ViewModels;
using ReqMint.App.Views;
using ReqMint.Http;

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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(_requestExecutor),
            };
            desktop.Exit += (_, _) => _requestExecutor.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
