using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(CanManageCollection))]
    private async Task CreateCollectionAsync(CancellationToken cancellationToken)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();
        try
        {
            var name = CreateUniqueCollectionName(_workspaceSnapshot.Collections);
            var id = Guid.NewGuid();
            var collection = new CollectionDocument { Id = id, Name = name };
            var collections = _workspaceSnapshot.Collections.Append(collection).ToArray();
            var references = _workspaceSnapshot.Workspace.Collections
                .Append(new WorkspaceFileReference(id, name, $"collections/{id:N}.json"))
                .ToArray();
            var snapshot = _workspaceSnapshot with
            {
                Workspace = _workspaceSnapshot.Workspace with { Collections = references },
                Collections = collections,
            };

            await _workspaceStore.SaveAsync(_workspaceDirectory, snapshot, cancellationToken);
            ApplyWorkspace(snapshot, _workspaceDirectory, selectedCollectionId: id);
            CollectionDraftName = name;
            WorkspaceStatus = Localize("StatusCollectionCreated", "Collection created");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowWorkspaceError(
                Localize("ErrorCreateCollection", "Could not create collection"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageCollection))]
    private async Task RenameCollectionAsync(CancellationToken cancellationToken)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null || _selectedCollectionId is null)
        {
            return;
        }

        var name = CollectionDraftName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWorkspaceError(
                Localize("ErrorRenameCollection", "Could not rename collection"),
                new ArgumentException(Localize(
                    "ValidationCollectionNameRequired",
                    "A collection name is required.")));
            return;
        }

        if (_workspaceSnapshot.Collections.Any(collection =>
            collection.Id != _selectedCollectionId &&
            string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowWorkspaceError(
                Localize("ErrorRenameCollection", "Could not rename collection"),
                new ArgumentException(Localize(
                    "ValidationCollectionExists",
                    "Collection '{0}' already exists.",
                    name)));
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();
        try
        {
            var collections = _workspaceSnapshot.Collections
                .Select(collection => collection.Id == _selectedCollectionId
                    ? collection with { Name = name }
                    : collection)
                .ToArray();
            var references = _workspaceSnapshot.Workspace.Collections
                .Select(reference => reference.Id == _selectedCollectionId
                    ? reference with { Name = name }
                    : reference)
                .ToArray();
            var snapshot = _workspaceSnapshot with
            {
                Workspace = _workspaceSnapshot.Workspace with { Collections = references },
                Collections = collections,
            };

            await _workspaceStore.SaveAsync(_workspaceDirectory, snapshot, cancellationToken);
            ApplyWorkspace(snapshot, _workspaceDirectory, selectedCollectionId: _selectedCollectionId);
            WorkspaceStatus = Localize("StatusCollectionRenamed", "Collection renamed");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowWorkspaceError(
                Localize("ErrorRenameCollection", "Could not rename collection"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    private static string CreateUniqueCollectionName(IEnumerable<CollectionDocument> collections)
    {
        var names = collections.Select(collection => collection.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains("New collection"))
        {
            return "New collection";
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"New collection {index}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
