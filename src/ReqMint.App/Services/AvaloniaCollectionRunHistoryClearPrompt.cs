using Avalonia.Controls;
using Avalonia.Layout;

namespace ReqMint.App.Services;

public sealed class AvaloniaCollectionRunHistoryClearPrompt(
    Window owner,
    LocalizationService localization) : ICollectionRunHistoryClearPrompt
{
    public Task<bool> ShowAsync(string collectionName, int entryCount)
    {
        var dialog = new Window
        {
            Title = localization.GetString("CollectionRunHistoryClearTitle")
                ?? "Clear collection run history",
            Width = 470,
            Height = 205,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var clear = new Button
        {
            Content = localization.GetString("CollectionRunHistoryClearConfirm")
                ?? "Clear run history",
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
                        localization.GetString("CollectionRunHistoryClearMessage")
                            ?? "Permanently remove {0} saved runs for '{1}'?",
                        entryCount,
                        collectionName),
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
