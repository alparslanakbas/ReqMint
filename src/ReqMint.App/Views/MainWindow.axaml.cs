using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReqMint.App.ViewModels;

namespace ReqMint.App.Views;

public partial class MainWindow : Window
{
    /// <summary>Carries the dragged tab inside the process only.</summary>
    private static readonly DataFormat<RequestTabViewModel> TabFormat =
        DataFormat.CreateInProcessFormat<RequestTabViewModel>("reqmint-request-tab");

    /// <summary>How far the pointer travels before a press becomes a drag.</summary>
    private const double DragThreshold = 8;

    private PointerPressedEventArgs? _tabPress;
    private RequestTabViewModel? _tabBeingDragged;

    public MainWindow()
    {
        InitializeComponent();

        // Ctrl+K is handled here rather than as a window key binding because the
        // shortcut has to move keyboard focus into the palette box, and focus is
        // something only the view can hand out.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // Reordering tabs is a pointer gesture, so it belongs to the view. The
        // view model exposes MoveTabTo and owns the ordering itself.
        DragDrop.SetAllowDrop(RequestTabStrip, true);
        RequestTabStrip.AddHandler(
            PointerPressedEvent,
            OnTabPointerPressed,
            RoutingStrategies.Tunnel);
        RequestTabStrip.AddHandler(
            PointerMovedEvent,
            OnTabPointerMoved,
            RoutingStrategies.Tunnel);
        RequestTabStrip.AddHandler(DragDrop.DragOverEvent, OnTabDragOver);
        RequestTabStrip.AddHandler(DragDrop.DropEvent, OnTabDrop);
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

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(RequestTabStrip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _tabPress = args;
        _tabBeingDragged = FindTab(args.Source);
    }

    private async void OnTabPointerMoved(object? sender, PointerEventArgs args)
    {
        if (_tabPress is not { } press || _tabBeingDragged is not { } tab)
        {
            return;
        }

        if (!args.GetCurrentPoint(RequestTabStrip).Properties.IsLeftButtonPressed)
        {
            ClearDragState();
            return;
        }

        var travelled = args.GetPosition(RequestTabStrip).X
            - press.GetPosition(RequestTabStrip).X;
        if (Math.Abs(travelled) < DragThreshold)
        {
            // Still within a click: leave selection and the close button alone.
            return;
        }

        ClearDragState();

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(TabFormat, tab));
        await DragDrop.DoDragDropAsync(press, transfer, DragDropEffects.Move);
    }

    private void OnTabDragOver(object? sender, DragEventArgs args) =>
        args.DragEffects = args.DataTransfer.Contains(TabFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;

    private void OnTabDrop(object? sender, DragEventArgs args)
    {
        if (DataContext is not MainViewModel viewModel ||
            args.DataTransfer.TryGetValue(TabFormat) is not { } dragged ||
            FindTab(args.Source) is not { } target ||
            ReferenceEquals(dragged, target))
        {
            return;
        }

        viewModel.MoveTabTo(dragged, viewModel.Tabs.IndexOf(target));
        args.Handled = true;
    }

    private void ClearDragState()
    {
        _tabPress = null;
        _tabBeingDragged = null;
    }

    private static RequestTabViewModel? FindTab(object? source) =>
        (source as Control)?.DataContext as RequestTabViewModel;
}
