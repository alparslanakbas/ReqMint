using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReqMint.App.ViewModels;

namespace ReqMint.App.Services;

public sealed class DesktopApplicationController : IDisposable
{
    private readonly Application _application;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly Window _window;
    private readonly MainViewModel _viewModel;
    private readonly LocalizationService _localization;
    private readonly WindowCloseCoordinator _closeCoordinator;
    private readonly Bitmap _trayBitmap;
    private readonly TrayIcon _trayIcon;
    private readonly NativeMenuItem _openMenuItem;
    private readonly NativeMenuItem _newRequestMenuItem;
    private readonly NativeMenuItem _exitMenuItem;
    private bool _allowShutdown;
    private bool _isHandlingClose;
    private bool _isExitPending;
    private bool _isDisposed;

    public DesktopApplicationController(
        Application application,
        IClassicDesktopStyleApplicationLifetime lifetime,
        Window window,
        MainViewModel viewModel,
        LocalizationService localization,
        WindowCloseCoordinator closeCoordinator)
    {
        _application = application;
        _lifetime = lifetime;
        _window = window;
        _viewModel = viewModel;
        _localization = localization;
        _closeCoordinator = closeCoordinator;

        using var iconStream = AssetLoader.Open(
            new Uri("avares://ReqMint.App/Assets/TrayIcon.png"));
        _trayBitmap = new Bitmap(iconStream);
        _openMenuItem = new NativeMenuItem();
        _newRequestMenuItem = new NativeMenuItem();
        _exitMenuItem = new NativeMenuItem();
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(_trayBitmap),
            ToolTipText = "ReqMint",
            IsVisible = false,
            Menu = new NativeMenu
            {
                Items =
                {
                    _openMenuItem,
                    _newRequestMenuItem,
                    new NativeMenuItemSeparator(),
                    _exitMenuItem,
                },
            },
        };

        ApplyLocalizedText();
        _trayIcon.Clicked += OnTrayIconClicked;
        _openMenuItem.Click += OnOpenClicked;
        _newRequestMenuItem.Click += OnNewRequestClicked;
        _exitMenuItem.Click += OnExitClicked;
        _window.Closing += OnWindowClosing;
        _localization.PropertyChanged += OnLocalizationPropertyChanged;
        TrayIcon.SetIcons(_application, new TrayIcons { _trayIcon });
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= OnLocalizationPropertyChanged;
        _window.Closing -= OnWindowClosing;
        _trayIcon.Clicked -= OnTrayIconClicked;
        _openMenuItem.Click -= OnOpenClicked;
        _newRequestMenuItem.Click -= OnNewRequestClicked;
        _exitMenuItem.Click -= OnExitClicked;
        TrayIcon.SetIcons(_application, null);
        _trayIcon.Dispose();
        _trayBitmap.Dispose();
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowShutdown || e.CloseReason == WindowCloseReason.OSShutdown)
        {
            return;
        }

        e.Cancel = true;
        if (_isHandlingClose)
        {
            return;
        }

        _isHandlingClose = true;
        try
        {
            if (e.CloseReason == WindowCloseReason.ApplicationShutdown)
            {
                await ExitAsync();
                return;
            }

            var decision = await _closeCoordinator.DecideAsync();
            _viewModel.RefreshWindowClosePreference();

            if (decision == WindowCloseDecision.Hide)
            {
                HideWindow();
                return;
            }

            if (decision == WindowCloseDecision.Exit)
            {
                await ExitAsync();
            }
        }
        finally
        {
            _isHandlingClose = false;
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => RestoreWindow();

    private void OnOpenClicked(object? sender, EventArgs e) => RestoreWindow();

    private async void OnNewRequestClicked(object? sender, EventArgs e)
    {
        RestoreWindow();
        await _viewModel.NewRequestCommand.ExecuteAsync(null);
    }

    private async void OnExitClicked(object? sender, EventArgs e) => await ExitAsync();

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalizationService.SelectedLanguage))
        {
            ApplyLocalizedText();
        }
    }

    private async Task ExitAsync()
    {
        if (_isExitPending)
        {
            return;
        }

        _isExitPending = true;
        try
        {
            RestoreWindow();
            if (!await _viewModel.ConfirmExitAsync())
            {
                return;
            }

            _allowShutdown = true;
            _trayIcon.IsVisible = false;
            _lifetime.TryShutdown();
        }
        finally
        {
            _isExitPending = false;
        }
    }

    private void HideWindow()
    {
        _trayIcon.IsVisible = true;
        _window.Hide();
    }

    private void RestoreWindow()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _trayIcon.IsVisible = false;
        _window.Activate();
    }

    private void ApplyLocalizedText()
    {
        _openMenuItem.Header = Text("TrayOpen", "Open ReqMint");
        _newRequestMenuItem.Header = Text("TrayNewRequest", "New request");
        _exitMenuItem.Header = Text("TrayExit", "Exit");
    }

    private string Text(string key, string fallback) =>
        _localization.GetString(key) ?? fallback;
}
