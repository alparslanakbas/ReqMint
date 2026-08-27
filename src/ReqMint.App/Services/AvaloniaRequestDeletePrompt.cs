using Avalonia.Controls;
using Avalonia.Layout;

namespace ReqMint.App.Services;

public sealed class AvaloniaRequestDeletePrompt(
    Window owner,
    LocalizationService localization) : IRequestDeletePrompt
{
    public Task<bool> ShowAsync(string requestName)
    {
        var dialog = new Window
        {
            Title = localization.GetString("DeleteRequestTitle") ?? "Delete request",
            Icon = owner.Icon,
            Width = 460,
            Height = 205,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var delete = new Button
        {
            Content = localization.GetString("DeleteRequestConfirm") ?? "Delete request",
        };
        var cancel = new Button
        {
            Content = localization.GetString("UnsavedCancel") ?? "Cancel",
        };
        delete.Click += (_, _) => dialog.Close(true);
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
                        localization.GetString("DeleteRequestMessage")
                            ?? "Permanently remove '{0}' from this workspace?",
                        requestName),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, delete },
                },
            },
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
