using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Copies a saved request into the same collection under a free name.
    /// </summary>
    private async Task DuplicateRequestAsync(RequestDocument request, Guid collectionId)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();

        try
        {
            var collections = _workspaceSnapshot.Collections.ToList();
            var index = collections.FindIndex(collection => collection.Id == collectionId);
            if (index < 0)
            {
                return;
            }

            var requests = collections[index].Requests.ToList();
            var copy = request with
            {
                Id = Guid.NewGuid(),
                Name = CreateUniqueRequestName(request.Name, requests),
            };
            requests.Add(copy);
            collections[index] = collections[index] with { Requests = requests };

            var updatedSnapshot = _workspaceSnapshot with { Collections = collections };
            await _workspaceStore.SaveAsync(
                _workspaceDirectory,
                updatedSnapshot,
                CancellationToken.None);
            ApplyWorkspace(updatedSnapshot, _workspaceDirectory, _selectedRequestId, collectionId);
            WorkspaceStatus = Localize("StatusRequestDuplicated", "Duplicated {0}", copy.Name);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowWorkspaceError(
                Localize("ErrorDuplicateRequest", "Could not duplicate request"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    /// <summary>
    /// Removes a saved request after the user confirms. The composer is reset
    /// when the deleted request was the one being edited, so the editor never
    /// keeps pointing at something that no longer exists.
    /// </summary>
    private async Task DeleteRequestAsync(RequestDocument request, Guid collectionId)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        if (!await _requestDeletePrompt.ShowAsync(request.Name))
        {
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();

        try
        {
            var collections = _workspaceSnapshot.Collections.ToList();
            var index = collections.FindIndex(collection => collection.Id == collectionId);
            if (index < 0)
            {
                return;
            }

            var requests = collections[index].Requests
                .Where(item => item.Id != request.Id)
                .ToArray();
            collections[index] = collections[index] with { Requests = requests };

            var updatedSnapshot = _workspaceSnapshot with { Collections = collections };
            await _workspaceStore.SaveAsync(
                _workspaceDirectory,
                updatedSnapshot,
                CancellationToken.None);

            var wasOpen = _selectedRequestId == request.Id;
            ApplyWorkspace(
                updatedSnapshot,
                _workspaceDirectory,
                wasOpen ? null : _selectedRequestId,
                collectionId);
            if (wasOpen)
            {
                ResetRequestDraft();
            }

            WorkspaceStatus = Localize("StatusRequestDeleted", "Deleted {0}", request.Name);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowWorkspaceError(
                Localize("ErrorDeleteRequest", "Could not delete request"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    private static string CreateUniqueRequestName(
        string name,
        IEnumerable<RequestDocument> requests)
    {
        var taken = requests.Select(request => request.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = $"{name} (2)";
        for (var index = 2; taken.Contains(candidate); index++)
        {
            candidate = $"{name} ({index + 1})";
        }

        return candidate;
    }
}
