using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ReqMint.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Ctrl+K is handled here rather than as a window key binding because the
        // shortcut has to move keyboard focus into the palette box, and focus is
        // something only the view can hand out.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.K || !args.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        CommandPaletteBox.Focus();
        CommandPaletteBox.SelectAll();
        args.Handled = true;
    }
}
