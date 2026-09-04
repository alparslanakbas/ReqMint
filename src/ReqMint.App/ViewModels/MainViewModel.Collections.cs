using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Importing;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(CanManageCollection))]
    private async Task ImportPostmanCollectionAsync(CancellationToken cancellationToken)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        IsWorkspaceBusy = true;
        ClearWorkspaceError();
        try
        {
            var source = await _postmanImportService.PickAsync(cancellationToken);
            if (source is null)
            {
                WorkspaceStatus = Localize("StatusReady", "Ready");
                return;
            }

            var result = new PostmanCollectionImporter().Import(source.Content);
            var existingNames = _workspaceSnapshot.Collections
                .Select(collection => collection.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var imported = result.Collections.Select(collection => collection with
            {
                Name = CreateUniqueImportedCollectionName(collection.Name, existingNames),
            }).ToArray();
            var references = imported.Select(collection => new WorkspaceFileReference(
                collection.Id,
                collection.Name,
                $"collections/{collection.Id:N}.json"));
            var snapshot = _workspaceSnapshot with
            {
                Workspace = _workspaceSnapshot.Workspace with
                {
                    Collections = _workspaceSnapshot.Workspace.Collections.Concat(references).ToArray(),
                },
                Collections = _workspaceSnapshot.Collections.Concat(imported).ToArray(),
            };

            await _workspaceStore.SaveAsync(_workspaceDirectory, snapshot, cancellationToken);
            ApplyWorkspace(snapshot, _workspaceDirectory, selectedCollectionId: imported[0].Id);
            WorkspaceStatus = Localize(
                "StatusPostmanImported",
                "Imported {0} requests into {1} collections",
                result.RequestCount,
                imported.Length);
            ResponseStatus = Localize("StatusImportCompleted", "Import completed");
            ResponseStatusKind = ResponseStatusKind.Success;
            ResponseBody = BuildPostmanImportReport(result);
            ResponseTime = "—";
            HasResponse = true;
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = Localize("StatusReady", "Ready");
        }
        catch (Exception exception)
        {
            ShowWorkspaceError(
                Localize("ErrorImportPostman", "Could not import Postman collection"),
                exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

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

    private static string CreateUniqueImportedCollectionName(string requested, ISet<string> names)
    {
        var candidate = requested;
        for (var suffix = 2; !names.Add(candidate); suffix++)
        {
            candidate = $"{requested} {suffix}";
        }

        return candidate;
    }

    private string BuildPostmanImportReport(PostmanImportResult result)
    {
        var lines = new List<string>
        {
            Localize(
                "PostmanImportSummary",
                "Imported {0} requests into {1} collections.",
                result.RequestCount,
                result.Collections.Count),
        };
        if (result.Warnings.Count == 0)
        {
            lines.Add(Localize("PostmanImportNoWarnings", "No compatibility warnings."));
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add(string.Empty);
        lines.Add(Localize("PostmanImportWarnings", "Compatibility warnings:"));
        lines.AddRange(result.Warnings.Take(20).Select(warning =>
            $"• {FormatPostmanImportWarning(warning)}"));
        if (result.Warnings.Count > 20)
        {
            lines.Add(Localize(
                "PostmanImportMoreWarnings",
                "…and {0} more warnings.",
                result.Warnings.Count - 20));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string FormatPostmanImportWarning(PostmanImportWarning warning) => warning.Kind switch
    {
        PostmanImportWarningKind.UnsupportedAuthentication => Localize(
            "PostmanWarningUnsupportedAuth",
            "Unsupported authentication was omitted from '{0}'.",
            warning.ItemName),
        PostmanImportWarningKind.SensitiveValueOmitted => Localize(
            "PostmanWarningSecretOmitted",
            "A literal sensitive value was omitted from '{0}'. Use a secret environment variable instead.",
            warning.ItemName),
        PostmanImportWarningKind.UnsupportedBody => Localize(
            "PostmanWarningUnsupportedBody",
            "An unsupported request body was omitted from '{0}'.",
            warning.ItemName),
        PostmanImportWarningKind.ScriptOmitted => Localize(
            "PostmanWarningScriptOmitted",
            "Postman scripts were omitted from '{0}'.",
            warning.ItemName),
        PostmanImportWarningKind.FileMustBeReselected => Localize(
            "PostmanWarningFileReselect",
            "Choose the local upload file again in '{0}'.",
            warning.ItemName),
        PostmanImportWarningKind.EmptyFolderSkipped => Localize(
            "PostmanWarningEmptyFolder",
            "Empty folder '{0}' was skipped.",
            warning.ItemName),
        PostmanImportWarningKind.CollectionVariablesOmitted => Localize(
            "PostmanWarningVariablesOmitted",
            "Collection variables from '{0}' were not imported. Add them to a ReqMint environment.",
            warning.ItemName),
        _ => warning.ItemName,
    };
}
