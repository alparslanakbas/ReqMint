using Avalonia.Controls;
using Avalonia.Layout;

namespace ReqMint.App.Services;

public sealed class AvaloniaHistoryClearPrompt(
    Window owner,
    LocalizationService localization) : IHistoryClearPrompt
{
    public Task<bool> ShowAsync(string workspaceName, int entryCount)
    {
        var dialog = new Window
        {
            Title = localization.GetString("ClearHistoryTitle") ?? "Clear request history",
            Icon = owner.Icon,
            Width = 460,
            Height = 205,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var clear = new Button
        {
            Content = localization.GetString("ClearHistoryConfirm") ?? "Clear history",
        };
        var cancel = new Button
        {
            Content = localization.GetString("UnsavedCancel") ?? "Cancel",
        };
        clear.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = string.Format(
                        localization.GetString("ClearHistoryMessage")
                            ?? "Permanently remove {0} history entries from '{1}'?",
                        entryCount,
                        workspaceName),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, clear },
                },
            },
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
