using System.Text.Json;
using ReqMint.Core.Requests;
using ReqMint.Core.Templates;
using ReqMint.Core.Workspaces;

namespace ReqMint.Storage;

public sealed class WorkspaceJsonStore : IWorkspaceStore
{
    public const string WorkspaceFileName = "reqmint.workspace.json";
    public const int MaximumDocumentBytes = 16 * 1024 * 1024;

    private const string CollectionsDirectory = "collections";
    private const string EnvironmentsDirectory = "environments";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<WorkspaceSnapshot> LoadAsync(
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        var root = NormalizeWorkspaceDirectory(workspaceDirectory);
        var workspacePath = Path.Combine(root, WorkspaceFileName);
        EnsurePathDoesNotTraverseLink(root, workspacePath);
        var workspace = await ReadAsync<WorkspaceDocument>(workspacePath, cancellationToken);

        ValidateWorkspace(workspace, root);

        var collections = new List<CollectionDocument>(workspace.Collections.Count);
        foreach (var reference in workspace.Collections)
        {
            var path = ResolveReferencedPath(root, reference.File, CollectionsDirectory);
            var collection = await ReadAsync<CollectionDocument>(path, cancellationToken);
            ValidateCollection(collection, reference);
            collections.Add(collection);
        }

        var environments = new List<EnvironmentDocument>(workspace.Environments.Count);
        foreach (var reference in workspace.Environments)
        {
            var path = ResolveReferencedPath(root, reference.File, EnvironmentsDirectory);
            var environment = await ReadAsync<EnvironmentDocument>(path, cancellationToken);
            ValidateEnvironment(environment, reference);
            environments.Add(environment);
        }

        return new WorkspaceSnapshot(workspace, collections, environments);
    }

    public async Task SaveAsync(
        string workspaceDirectory,
        WorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var root = NormalizeWorkspaceDirectory(workspaceDirectory);
        ValidateSnapshot(snapshot, root);

        Directory.CreateDirectory(root);

        var collectionsById = snapshot.Collections.ToDictionary(collection => collection.Id);
        foreach (var reference in snapshot.Workspace.Collections)
        {
            var path = ResolveReferencedPath(root, reference.File, CollectionsDirectory);
            await WriteAtomicallyAsync(root, path, collectionsById[reference.Id], cancellationToken);
        }

        var environmentsById = snapshot.Environments.ToDictionary(environment => environment.Id);
        foreach (var reference in snapshot.Workspace.Environments)
        {
            var path = ResolveReferencedPath(root, reference.File, EnvironmentsDirectory);
            await WriteAtomicallyAsync(root, path, environmentsById[reference.Id], cancellationToken);
        }

        var workspacePath = Path.Combine(root, WorkspaceFileName);
        await WriteAtomicallyAsync(root, workspacePath, snapshot.Workspace, cancellationToken);
    }

    private static void ValidateSnapshot(WorkspaceSnapshot snapshot, string root)
    {
        ValidateWorkspace(snapshot.Workspace, root);

        EnsureUniqueIds(snapshot.Collections.Select(collection => collection.Id), "collection");
        EnsureUniqueIds(snapshot.Environments.Select(environment => environment.Id), "environment");

        var collectionsById = snapshot.Collections.ToDictionary(collection => collection.Id);
        if (collectionsById.Count != snapshot.Workspace.Collections.Count)
        {
            throw new WorkspaceFormatException(
                "Every collection must have exactly one workspace reference.");
        }

        foreach (var reference in snapshot.Workspace.Collections)
        {
            if (!collectionsById.TryGetValue(reference.Id, out var collection))
            {
                throw new WorkspaceFormatException(
                    $"Collection reference '{reference.Name}' has no matching document.");
            }

            ValidateCollection(collection, reference);
        }

        var environmentsById = snapshot.Environments.ToDictionary(environment => environment.Id);
        if (environmentsById.Count != snapshot.Workspace.Environments.Count)
        {
            throw new WorkspaceFormatException(
                "Every environment must have exactly one workspace reference.");
        }

        foreach (var reference in snapshot.Workspace.Environments)
        {
            if (!environmentsById.TryGetValue(reference.Id, out var environment))
            {
                throw new WorkspaceFormatException(
                    $"Environment reference '{reference.Name}' has no matching document.");
            }

            ValidateEnvironment(environment, reference);
        }
    }

    private static void ValidateWorkspace(WorkspaceDocument workspace, string root)
    {
        ValidateSchemaVersion(
            workspace.SchemaVersion,
            WorkspaceDocument.CurrentSchemaVersion,
            "workspace");
        ValidateIdentity(workspace.Id, workspace.Name, "workspace");

        if (workspace.Collections is null || workspace.Environments is null)
        {
            throw new WorkspaceFormatException("Workspace references cannot be null.");
        }

        EnsureUniqueIds(workspace.Collections.Select(reference => reference.Id), "collection reference");
        EnsureUniqueIds(workspace.Environments.Select(reference => reference.Id), "environment reference");

        foreach (var reference in workspace.Collections)
        {
            ValidateReference(reference, root, CollectionsDirectory);
        }

        EnsureUniqueReferenceFiles(workspace.Collections, root, CollectionsDirectory);

        foreach (var reference in workspace.Environments)
        {
            ValidateReference(reference, root, EnvironmentsDirectory);
        }

        EnsureUniqueReferenceFiles(workspace.Environments, root, EnvironmentsDirectory);
    }

    private static void ValidateCollection(
        CollectionDocument collection,
        WorkspaceFileReference reference)
    {
        ValidateSchemaVersion(
            collection.SchemaVersion,
            CollectionDocument.CurrentSchemaVersion,
            $"collection '{reference.Name}'");
        ValidateIdentity(collection.Id, collection.Name, "collection");
        ValidateReferenceMatch(collection.Id, collection.Name, reference, "Collection");

        if (collection.Requests is null)
        {
            throw new WorkspaceFormatException(
                $"Requests in collection '{collection.Name}' cannot be null.");
        }

        EnsureUniqueIds(collection.Requests.Select(request => request.Id), "request");
        foreach (var request in collection.Requests)
        {
            ValidateIdentity(request.Id, request.Name, "request");

            if (string.IsNullOrWhiteSpace(request.Method))
            {
                throw new WorkspaceFormatException($"Request '{request.Name}' must have a method.");
            }

            var isHttpUrl = Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!isHttpUrl && !RequestTemplate.ContainsVariables(request.Url))
            {
                throw new WorkspaceFormatException(
                    $"Request '{request.Name}' must have an HTTP URL or a URL template.");
            }

            if (request.TimeoutSeconds is < 1 or > 600)
            {
                throw new WorkspaceFormatException(
                    $"Request '{request.Name}' timeout must be between 1 and 600 seconds.");
            }

            if (request.QueryParameters is null || request.Headers is null)
            {
                throw new WorkspaceFormatException(
                    $"Request fields in '{request.Name}' cannot be null.");
            }

            ValidateRequestFields(request.QueryParameters, request.Name, "query parameter");
            ValidateRequestFields(request.Headers, request.Name, "header");
            ValidateRequestAuthentication(request.Authentication, request.Name);

            if (request.Body is not null &&
                (request.Body.Content is null || string.IsNullOrWhiteSpace(request.Body.ContentType)))
            {
                throw new WorkspaceFormatException(
                    $"Request body in '{request.Name}' must contain content and a content type.");
            }

            if (request.Body is not null)
            {
                ValidateRequestFields(request.Body.FormFields, request.Name, "form field");
            }

            var assertionError = RequestAssertionValidator.GetValidationError(request.Assertions);
            if (assertionError is not null)
            {
                throw new WorkspaceFormatException(
                    $"Assertions in request '{request.Name}' are invalid: {assertionError}");
            }
        }
    }

    private static void ValidateEnvironment(
        EnvironmentDocument environment,
        WorkspaceFileReference reference)
    {
        ValidateSchemaVersion(
            environment.SchemaVersion,
            EnvironmentDocument.CurrentSchemaVersion,
            $"environment '{reference.Name}'");
        ValidateIdentity(environment.Id, environment.Name, "environment");
        ValidateReferenceMatch(environment.Id, environment.Name, reference, "Environment");

        if (environment.Variables is null)
        {
            throw new WorkspaceFormatException(
                $"Variables in environment '{environment.Name}' cannot be null.");
        }

        var variableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in environment.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                throw new WorkspaceFormatException(
                    $"Environment '{environment.Name}' contains a variable without a name.");
            }

            if (!variableNames.Add(variable.Name))
            {
                throw new WorkspaceFormatException(
                    $"Environment '{environment.Name}' contains duplicate variable '{variable.Name}'.");
            }

            if (variable.IsSecret && variable.Value is not null)
            {
                throw new WorkspaceFormatException(
                    $"Secret variable '{variable.Name}' cannot be persisted in a workspace file.");
            }
        }
    }

    private static void ValidateReference(
        WorkspaceFileReference reference,
        string root,
        string requiredDirectory)
    {
        ValidateIdentity(reference.Id, reference.Name, $"{requiredDirectory} reference");

        if (string.IsNullOrWhiteSpace(reference.File))
        {
            throw new WorkspaceFormatException(
                $"Reference '{reference.Name}' must specify a file.");
        }

        _ = ResolveReferencedPath(root, reference.File, requiredDirectory);
    }

    private static void ValidateReferenceMatch(
        Guid documentId,
        string documentName,
        WorkspaceFileReference reference,
        string documentType)
    {
        if (documentId != reference.Id ||
            !string.Equals(documentName, reference.Name, StringComparison.Ordinal))
        {
            throw new WorkspaceFormatException(
                $"{documentType} document '{documentName}' does not match its workspace reference.");
        }
    }

    private static void ValidateRequestFields(
        IEnumerable<ReqMint.Core.Requests.RequestField> fields,
        string requestName,
        string fieldType)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || field.Value is null)
            {
                throw new WorkspaceFormatException(
                    $"Request '{requestName}' contains an invalid {fieldType}.");
            }
        }
    }

    private static void ValidateRequestAuthentication(
        RequestAuthentication? authentication,
        string requestName)
    {
        if (authentication is null)
        {
            return;
        }

        if (!Enum.IsDefined(authentication.Type))
        {
            throw new WorkspaceFormatException(
                $"Request '{requestName}' contains an unsupported authentication type.");
        }

        ValidateOptionalSecretReference(
            authentication.BearerToken,
            requestName,
            "bearer token");
        ValidateOptionalSecretReference(
            authentication.BasicPassword,
            requestName,
            "Basic Auth password");
        ValidateOptionalSecretReference(
            authentication.ApiKeyValue,
            requestName,
            "API key value");

        if (authentication.Type == RequestAuthenticationType.None)
        {
            return;
        }

        switch (authentication.Type)
        {
            case RequestAuthenticationType.Bearer:
                ValidateSecretReference(authentication.BearerToken, requestName, "bearer token");
                break;
            case RequestAuthenticationType.Basic:
                if (string.IsNullOrWhiteSpace(authentication.BasicUsername))
                {
                    throw new WorkspaceFormatException(
                        $"Request '{requestName}' must specify a Basic Auth username.");
                }

                ValidateSecretReference(authentication.BasicPassword, requestName, "Basic Auth password");
                break;
            case RequestAuthenticationType.ApiKey:
                if (string.IsNullOrWhiteSpace(authentication.ApiKeyName))
                {
                    throw new WorkspaceFormatException(
                        $"Request '{requestName}' must specify an API key name.");
                }

                if (authentication.ApiKeyLocation is null ||
                    !Enum.IsDefined(authentication.ApiKeyLocation.Value))
                {
                    throw new WorkspaceFormatException(
                        $"Request '{requestName}' contains an unsupported API key location.");
                }

                ValidateSecretReference(authentication.ApiKeyValue, requestName, "API key value");
                break;
        }
    }

    private static void ValidateSecretReference(string? value, string requestName, string fieldName)
    {
        if (!RequestTemplate.IsVariableReference(value))
        {
            throw new WorkspaceFormatException(
                $"Request '{requestName}' {fieldName} must be a single environment variable reference. " +
                "Authentication secrets cannot be persisted in a workspace file.");
        }
    }

    private static void ValidateOptionalSecretReference(
        string? value,
        string requestName,
        string fieldName)
    {
        if (!string.IsNullOrEmpty(value))
        {
            ValidateSecretReference(value, requestName, fieldName);
        }
    }

    private static void ValidateIdentity(Guid id, string name, string documentType)
    {
        if (id == Guid.Empty)
        {
            throw new WorkspaceFormatException($"The {documentType} ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new WorkspaceFormatException($"The {documentType} name cannot be empty.");
        }
    }

    private static void ValidateSchemaVersion(int actual, int supported, string documentType)
    {
        if (actual != supported)
        {
            throw new WorkspaceFormatException(
                $"Unsupported {documentType} schema version {actual}. Supported version: {supported}.");
        }
    }

    private static void EnsureUniqueIds(IEnumerable<Guid> ids, string itemType)
    {
        var seen = new HashSet<Guid>();
        foreach (var id in ids)
        {
            if (!seen.Add(id))
            {
                throw new WorkspaceFormatException($"Duplicate {itemType} ID '{id}'.");
            }
        }
    }

    private static void EnsureUniqueReferenceFiles(
        IEnumerable<WorkspaceFileReference> references,
        string root,
        string requiredDirectory)
    {
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seen = new HashSet<string>(pathComparer);

        foreach (var reference in references)
        {
            var path = ResolveReferencedPath(root, reference.File, requiredDirectory);
            if (!seen.Add(path))
            {
                throw new WorkspaceFormatException(
                    $"Multiple workspace references cannot use the same file '{reference.File}'.");
            }
        }
    }

    private static string NormalizeWorkspaceDirectory(string workspaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        return Path.GetFullPath(workspaceDirectory);
    }

    private static string ResolveReferencedPath(
        string root,
        string relativePath,
        string requiredDirectory)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new WorkspaceFormatException(
                $"Workspace reference '{relativePath}' must be a relative path.");
        }

        var resolvedPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var normalizedRelativePath = Path.GetRelativePath(root, resolvedPath);
        var expectedPrefix = requiredDirectory + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedRelativePath.StartsWith(expectedPrefix, comparison) ||
            string.Equals(normalizedRelativePath, requiredDirectory, comparison))
        {
            throw new WorkspaceFormatException(
                $"Workspace reference '{relativePath}' must stay inside '{requiredDirectory}'.");
        }

        EnsurePathDoesNotTraverseLink(root, resolvedPath);
        return resolvedPath;
    }

    private static void EnsurePathDoesNotTraverseLink(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        var currentPath = root;
        foreach (var segment in relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!Path.Exists(currentPath))
            {
                continue;
            }

            try
            {
                if (File.GetAttributes(currentPath).HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new WorkspaceFormatException(
                        $"Workspace path '{relativePath}' cannot traverse a symbolic link or reparse point.");
                }
            }
            catch (WorkspaceFormatException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or NotSupportedException
                    or System.Security.SecurityException)
            {
                throw new WorkspaceFormatException(
                    $"Could not validate workspace path '{relativePath}'.",
                    exception);
            }
        }
    }

    private static async Task<T> ReadAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaximumDocumentBytes)
            {
                throw new WorkspaceFormatException(
                    $"Workspace document '{path}' exceeds the {MaximumDocumentBytes} byte limit.");
            }

            var document = await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonOptions,
                cancellationToken);

            return document ?? throw new WorkspaceFormatException(
                $"Workspace document '{path}' is empty.");
        }
        catch (WorkspaceFormatException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new WorkspaceFormatException(
                $"Could not read workspace document '{path}'.",
                exception);
        }
    }

    private static async Task WriteAtomicallyAsync<T>(
        string root,
        string path,
        T document,
        CancellationToken cancellationToken)
    {
        EnsurePathDoesNotTraverseLink(root, path);
        var directory = Path.GetDirectoryName(path)
            ?? throw new WorkspaceFormatException($"Invalid workspace path '{path}'.");
        Directory.CreateDirectory(directory);
        EnsurePathDoesNotTraverseLink(root, path);

        var temporaryPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                if (stream.Length > MaximumDocumentBytes)
                {
                    throw new WorkspaceFormatException(
                        $"Workspace document '{path}' exceeds the {MaximumDocumentBytes} byte limit.");
                }
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new WorkspaceFormatException(
                $"Could not write workspace document '{path}'.",
                exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
