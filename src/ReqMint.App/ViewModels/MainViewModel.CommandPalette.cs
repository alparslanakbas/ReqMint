using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    private const int MaximumCommandPaletteResults = 8;
    private const int MaximumCommandPaletteRequests = 5;

    public ObservableCollection<CommandPaletteItemViewModel> CommandPaletteResults { get; } = [];

    [ObservableProperty]
    public partial string CommandPaletteQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCommandPaletteOpen { get; set; }

    [ObservableProperty]
    public partial string CommandPaletteEmptyMessage { get; set; } = string.Empty;

    public bool HasCommandPaletteResults => CommandPaletteResults.Count > 0;

    private int _commandPaletteSelectedIndex;

    partial void OnCommandPaletteQueryChanged(string value) => RefreshCommandPalette();

    [RelayCommand]
    private void OpenCommandPalette() => RefreshCommandPalette(forceOpen: true);

    [RelayCommand]
    private void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        CommandPaletteQuery = string.Empty;
    }

    [RelayCommand]
    private void MoveCommandPaletteSelectionDown() => MoveCommandPaletteSelection(1);

    [RelayCommand]
    private void MoveCommandPaletteSelectionUp() => MoveCommandPaletteSelection(-1);

    [RelayCommand]
    private async Task RunSelectedCommandPaletteItemAsync()
    {
        if (!IsCommandPaletteOpen || CommandPaletteResults.Count == 0)
        {
            return;
        }

        await RunCommandPaletteItemAsync(CommandPaletteResults[_commandPaletteSelectedIndex]);
    }

    [RelayCommand]
    private async Task RunCommandPaletteItemAsync(CommandPaletteItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        CloseCommandPalette();
        await item.Invoke();
    }

    private void MoveCommandPaletteSelection(int offset)
    {
        if (CommandPaletteResults.Count == 0)
        {
            return;
        }

        var count = CommandPaletteResults.Count;
        SelectCommandPaletteItem(((_commandPaletteSelectedIndex + offset) % count + count) % count);
    }

    private void SelectCommandPaletteItem(int index)
    {
        _commandPaletteSelectedIndex = index;
        for (var position = 0; position < CommandPaletteResults.Count; position++)
        {
            CommandPaletteResults[position].IsSelected = position == index;
        }
    }

    private void RefreshCommandPalette(bool forceOpen = false)
    {
        var query = CommandPaletteQuery.Trim();
        if (query.Length == 0 && !forceOpen)
        {
            CommandPaletteResults.Clear();
            OnPropertyChanged(nameof(HasCommandPaletteResults));
            IsCommandPaletteOpen = false;
            return;
        }

        var parts = CommandPaletteSearch.Fold(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = BuildCommandPaletteEntries()
            .Where(entry => parts.Length == 0 || CommandPaletteSearch.Matches(entry.SearchText, parts))
            .Take(MaximumCommandPaletteResults)
            .ToArray();

        CommandPaletteResults.Clear();
        foreach (var match in matches)
        {
            CommandPaletteResults.Add(match);
        }

        OnPropertyChanged(nameof(HasCommandPaletteResults));
        CommandPaletteEmptyMessage = matches.Length == 0
            ? Localize("CommandPaletteNoResults", "No matching command")
            : string.Empty;
        SelectCommandPaletteItem(0);
        IsCommandPaletteOpen = true;
    }

    private IEnumerable<CommandPaletteItemViewModel> BuildCommandPaletteEntries()
    {
        var actions = Localize("CommandPaletteActions", "Action");
        var navigation = Localize("CommandPaletteNavigation", "Go to");
        var appearance = Localize("CommandPaletteAppearance", "Appearance");
        var requests = Localize("CommandPaletteRequests", "Request");

        yield return Entry(Localize("TextSend", "Send"), actions, () => SendCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TooltipCreateRequest", "Create a new request"),
            actions,
            () => NewRequestCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TextSave", "Save"),
            actions,
            () => SaveRequestCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TooltipCloseRequest", "Close the open request"),
            actions,
            () => CloseRequestCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TextCopy", "Copy"),
            actions,
            () => CopyResponseCommand.ExecuteAsync(null),
            Localize("TextResponse", "Response"));
        yield return Entry(
            Localize("TooltipOpenWorkspace", "Open a local ReqMint workspace"),
            actions,
            () => OpenWorkspaceCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TooltipCreateWorkspace", "Create a workspace in a local folder"),
            actions,
            () => CreateWorkspaceCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("TextNewEnvironment", "New environment"),
            actions,
            () => RunSynchronously(() => NewEnvironmentCommand.Execute(null)));
        yield return Entry(
            Localize("CollectionRunAction", "Run collection"),
            actions,
            () => OpenCollectionRunnerCommand.ExecuteAsync(null));

        yield return Entry(
            Localize("NavRequests", "Requests"),
            navigation,
            () => RunSynchronously(() => ShowCollectionsCommand.Execute(null)));
        yield return Entry(
            Localize("TextEnvironment", "Environment"),
            navigation,
            () => RunSynchronously(() => ShowEnvironmentEditorCommand.Execute(null)));
        yield return Entry(
            Localize("TextHistory", "History"),
            navigation,
            () => ShowHistoryCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("NavGit", "Git"),
            navigation,
            () => ShowGitCommand.ExecuteAsync(null));
        yield return Entry(
            Localize("NavSettings", "Settings"),
            navigation,
            () => RunSynchronously(() => ShowSettingsEditorCommand.Execute(null)));

        if (Themes is { } themes)
        {
            foreach (var theme in themes.Themes)
            {
                var option = theme;
                yield return Entry(
                    Localize("CommandPaletteTheme", "Theme: {0}", option.DisplayName),
                    appearance,
                    () =>
                    {
                        themes.SelectedTheme = option;
                        WorkspaceStatus = Localize(
                            "CommandPaletteTheme",
                            "Theme: {0}",
                            option.DisplayName);
                        return Task.CompletedTask;
                    });
            }
        }

        if (Localization is { } localization)
        {
            foreach (var language in localization.Languages)
            {
                var option = language;
                yield return Entry(
                    Localize("CommandPaletteLanguage", "Language: {0}", option.DisplayName),
                    appearance,
                    () =>
                    {
                        localization.SelectedLanguage = option;
                        return Task.CompletedTask;
                    });
            }
        }

        // Saved requests make the box live up to its "search or run a command"
        // placeholder instead of only running commands.
        foreach (var collection in Collections)
        {
            foreach (var request in collection.Requests.Take(MaximumCommandPaletteRequests))
            {
                var saved = request;
                yield return Entry(
                    saved.Name,
                    requests,
                    () => saved.OpenCommand.ExecuteAsync(null),
                    $"{saved.Method} {collection.Name}");
            }
        }
    }

    private static CommandPaletteItemViewModel Entry(
        string title,
        string category,
        Func<Task> invoke,
        string? keywords = null) =>
        new(title, category, invoke, keywords);

    private static Task RunSynchronously(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
