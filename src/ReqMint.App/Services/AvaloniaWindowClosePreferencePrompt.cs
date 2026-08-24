using Avalonia.Controls;
using Avalonia.Layout;

namespace ReqMint.App.Services;

public sealed class AvaloniaWindowClosePreferencePrompt(
    Window owner,
    LocalizationService localization) : IWindowClosePreferencePrompt
{
    public Task<WindowClosePromptResult?> ShowAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = Text("CloseBehaviorPromptTitle", "Close ReqMint"),
            Width = 520,
            Height = 245,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var remember = new CheckBox
        {
            Content = Text("CloseBehaviorRemember", "Remember my choice"),
        };
        var keepRunning = new Button
        {
            Content = Text("CloseBehaviorKeepRunning", "Keep running"),
        };
        var exit = new Button
        {
            Content = Text("CloseBehaviorExit", "Exit ReqMint"),
        };

        keepRunning.Click += (_, _) => dialog.Close(new WindowClosePromptResult(
            WindowCloseBehavior.KeepRunning,
            remember.IsChecked == true));
        exit.Click += (_, _) => dialog.Close(new WindowClosePromptResult(
            WindowCloseBehavior.Exit,
            remember.IsChecked == true));

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = Text(
                        "CloseBehaviorPromptMessage",
                        "Should ReqMint continue running in the system tray when you close the window?"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 16,
                },
                new TextBlock
                {
                    Text = Text(
                        "CloseBehaviorPromptHelp",
                        "Background mode keeps your current workspace available and continues using system resources."),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Classes = { "secondary" },
                },
                remember,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { exit, keepRunning },
                },
            },
        };

        return dialog.ShowDialog<WindowClosePromptResult?>(owner);
    }

    private string Text(string key, string fallback) =>
        localization.GetString(key) ?? fallback;
}
