using Avalonia.Controls;
using Avalonia.Layout;

namespace ReqMint.App.Services;

public sealed class AvaloniaUnsavedChangesPrompt(
    Window owner,
    LocalizationService localization) : IUnsavedChangesPrompt
{
    public Task<UnsavedChangesChoice> ShowAsync(string requestName, bool canSave)
    {
        var dialog = new Window
        {
            Title = localization.GetString("UnsavedTitle") ?? "Unsaved changes",
            Icon = owner.Icon,
            Width = 440,
            Height = 190,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var save = new Button
        {
            Content = localization.GetString("UnsavedSave") ?? "Save",
            IsEnabled = canSave,
        };
        var discard = new Button
        {
            Content = localization.GetString("UnsavedDiscard") ?? "Discard",
        };
        var cancel = new Button
        {
            Content = localization.GetString("UnsavedCancel") ?? "Cancel",
        };
        save.Click += (_, _) => dialog.Close(UnsavedChangesChoice.Save);
        discard.Click += (_, _) => dialog.Close(UnsavedChangesChoice.Discard);
        cancel.Click += (_, _) => dialog.Close(UnsavedChangesChoice.Cancel);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = string.Format(
                        localization.GetString("UnsavedMessage")
                            ?? "Save changes to '{0}' before continuing?",
                        requestName),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, discard, save },
                },
            },
        };

        return dialog.ShowDialog<UnsavedChangesChoice>(owner);
    }
}
