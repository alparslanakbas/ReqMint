using CommunityToolkit.Mvvm.Input;
using ReqMint.Core.Security;
using ReqMint.Core.Workspaces;

namespace ReqMint.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(CanEditEnvironment))]
    private void NewEnvironment()
    {
        _editingEnvironmentId = null;
        EnvironmentDraftName = "New environment";
        EnvironmentVariables.Clear();
        EnvironmentVariables.Add(new EnvironmentVariableViewModel("BASE_URL"));
        WorkspaceStatus = "New environment";
    }

    [RelayCommand(CanExecute = nameof(CanEditEnvironment))]
    private void AddEnvironmentVariable() =>
        EnvironmentVariables.Add(new EnvironmentVariableViewModel());

    [RelayCommand(CanExecute = nameof(CanEditEnvironment))]
    private async Task SaveEnvironmentAsync(CancellationToken cancellationToken)
    {
        if (_workspaceSnapshot is null || _workspaceDirectory is null)
        {
            return;
        }

        IsWorkspaceBusy = true;
        WorkspaceStatus = "Saving environment...";

        try
        {
            var environmentName = EnvironmentDraftName.Trim();
            if (string.IsNullOrWhiteSpace(environmentName))
            {
                throw new ArgumentException("An environment name is required.");
            }

            if (_workspaceSnapshot.Environments.Any(environment =>
                environment.Id != _editingEnvironmentId &&
                string.Equals(environment.Name, environmentName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Environment '{environmentName}' already exists.");
            }

            var variables = EnvironmentVariables
                .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
                .Select(variable => new EnvironmentVariable(
                    variable.Name.Trim(),
                    variable.IsSecret ? null : variable.Value,
                    variable.IsSecret))
                .ToArray();
            var environmentId = _editingEnvironmentId ?? Guid.NewGuid();
            var environment = new EnvironmentDocument
            {
                Id = environmentId,
                Name = environmentName,
                Variables = variables,
            };

            var environments = _workspaceSnapshot.Environments.ToList();
            var environmentIndex = environments.FindIndex(item => item.Id == environmentId);
            if (environmentIndex >= 0)
            {
                environments[environmentIndex] = environment;
            }
            else
            {
                environments.Add(environment);
            }

            var references = _workspaceSnapshot.Workspace.Environments.ToList();
            var referenceIndex = references.FindIndex(reference => reference.Id == environmentId);
            var reference = new WorkspaceFileReference(
                environmentId,
                environmentName,
                referenceIndex >= 0
                    ? references[referenceIndex].File
                    : $"environments/{environmentId:N}.json");
            if (referenceIndex >= 0)
            {
                references[referenceIndex] = reference;
            }
            else
            {
                references.Add(reference);
            }

            var updatedSnapshot = _workspaceSnapshot with
            {
                Workspace = _workspaceSnapshot.Workspace with { Environments = references },
                Environments = environments,
            };
            await _workspaceStore.SaveAsync(
                _workspaceDirectory,
                updatedSnapshot,
                cancellationToken);

            await SaveSecretValuesAsync(
                _workspaceSnapshot.Workspace.Id,
                environment,
                cancellationToken);

            ApplyWorkspace(
                updatedSnapshot,
                _workspaceDirectory,
                _selectedRequestId,
                _selectedCollectionId,
                environment.Id);
            WorkspaceStatus = $"Saved {environment.Name}";
        }
        catch (OperationCanceledException)
        {
            WorkspaceStatus = "Environment save cancelled";
        }
        catch (Exception exception)
        {
            ShowWorkspaceError("Could not save environment", exception);
        }
        finally
        {
            IsWorkspaceBusy = false;
        }
    }

    private async Task SaveSecretValuesAsync(
        Guid workspaceId,
        EnvironmentDocument environment,
        CancellationToken cancellationToken)
    {
        foreach (var variable in EnvironmentVariables)
        {
            if (variable.WasSecret &&
                (!variable.IsSecret ||
                 !string.Equals(variable.OriginalName, variable.Name, StringComparison.Ordinal)))
            {
                await _secretVault.DeleteAsync(
                    new SecretReference(workspaceId, environment.Id, variable.OriginalName),
                    cancellationToken);
            }

            if (variable.IsSecret && !string.IsNullOrEmpty(variable.Value))
            {
                await _secretVault.SetAsync(
                    new SecretReference(workspaceId, environment.Id, variable.Name.Trim()),
                    variable.Value,
                    cancellationToken);
            }
        }
    }

    private void LoadEnvironmentEditor(EnvironmentDocument? environment)
    {
        _editingEnvironmentId = environment?.Id;
        EnvironmentDraftName = environment?.Name ?? "Development";
        EnvironmentVariables.Clear();

        if (environment is null)
        {
            return;
        }

        foreach (var variable in environment.Variables)
        {
            EnvironmentVariables.Add(new EnvironmentVariableViewModel(
                variable.Name,
                variable.IsSecret ? string.Empty : variable.Value ?? string.Empty,
                variable.IsSecret));
        }
    }
}
