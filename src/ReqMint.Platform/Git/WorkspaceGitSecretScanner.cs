using System.Security.Cryptography;
using System.Text.Json;
using ReqMint.Core.Git;
using ReqMint.Core.Security;

namespace ReqMint.Platform.Git;

public sealed class WorkspaceGitSecretScanner : IGitSecretScanner
{
    private const int MaximumFileBytes = 2 * 1024 * 1024;

    public async Task<GitSecretScanResult> ScanAsync(
        string workspaceDirectory,
        IReadOnlyList<string> workspaceRelativePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(workspaceRelativePaths);

        var root = Path.GetFullPath(workspaceDirectory);
        var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var findings = new List<GitSecretFinding>();
        var unscannedFiles = new HashSet<string>(pathComparer);

        foreach (var relativePath in workspaceRelativePaths.Distinct(pathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReqMintGitFileClassifier.IsManaged(relativePath))
            {
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(
                    Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(rootPrefix, pathComparison))
                {
                    unscannedFiles.Add(relativePath);
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var file = new FileInfo(fullPath);
                if (file.Length > MaximumFileBytes
                    || file.LinkTarget is not null
                    || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    unscannedFiles.Add(relativePath);
                    continue;
                }

                await using var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 16 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var content = new byte[MaximumFileBytes + 1];
                try
                {
                    var length = await ReadBoundedAsync(stream, content, cancellationToken);
                    if (length > MaximumFileBytes)
                    {
                        unscannedFiles.Add(relativePath);
                        continue;
                    }

                    using var document = JsonDocument.Parse(
                        content.AsMemory(0, length),
                        new JsonDocumentOptions { MaxDepth = 64 });
                    JsonSecretDetector.Scan(relativePath, document.RootElement, findings);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(content);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or ArgumentException
                    or NotSupportedException
                    or System.Security.SecurityException)
            {
                unscannedFiles.Add(relativePath);
            }
        }

        return new GitSecretScanResult
        {
            Findings = findings,
            UnscannedFiles = unscannedFiles.Order(pathComparer).ToArray(),
        };
    }

    internal static GitSecretScanResult ScanText(string relativePath, string content)
    {
        try
        {
            using var document = JsonDocument.Parse(
                content,
                new JsonDocumentOptions { MaxDepth = 64 });
            var findings = new List<GitSecretFinding>();
            JsonSecretDetector.Scan(relativePath, document.RootElement, findings);
            return new GitSecretScanResult { Findings = findings };
        }
        catch (JsonException)
        {
            return new GitSecretScanResult { UnscannedFiles = [relativePath] };
        }
    }

    private static async Task<int> ReadBoundedAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static class JsonSecretDetector
    {
        public static void Scan(
            string relativePath,
            JsonElement root,
            ICollection<GitSecretFinding> findings)
        {
            var uniqueFindings = new HashSet<(string Location, GitSecretFindingKind Kind)>();
            ScanElement(relativePath, root, "$", findings, uniqueFindings);
        }

        private static void ScanElement(
            string relativePath,
            JsonElement element,
            string location,
            ICollection<GitSecretFinding> findings,
            ISet<(string Location, GitSecretFindingKind Kind)> uniqueFindings)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                ScanObject(relativePath, element, location, findings, uniqueFindings);
                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ScanElement(relativePath, item, $"{location}[{index}]", findings, uniqueFindings);
                    index++;
                }
            }
        }

        private static void ScanObject(
            string relativePath,
            JsonElement element,
            string location,
            ICollection<GitSecretFinding> findings,
            ISet<(string Location, GitSecretFindingKind Kind)> uniqueFindings)
        {
            var properties = element.EnumerateObject().ToArray();
            var valueProperty = FindProperty(properties, "value");
            var nameProperty = FindProperty(properties, "name");
            var secretProperty = FindProperty(properties, "isSecret");
            var hasPersistedSecret = secretProperty is { Value.ValueKind: JsonValueKind.True }
                && valueProperty is { } secretValue
                && HasLiteralValue(secretValue.Value);

            if (hasPersistedSecret)
            {
                AddFinding(
                    relativePath,
                    $"{location}.value",
                    GitSecretFindingKind.PersistedSecretValue,
                    findings,
                    uniqueFindings);
            }
            else if (nameProperty is { } semanticName
                && semanticName.Value.ValueKind == JsonValueKind.String
                && SensitiveDataClassifier.IsSensitiveName(semanticName.Value.GetString())
                && valueProperty is { } namedValue
                && HasLiteralValue(namedValue.Value))
            {
                AddFinding(
                    relativePath,
                    $"{location}.value",
                    GitSecretFindingKind.SensitiveNamedValue,
                    findings,
                    uniqueFindings);
            }

            foreach (var property in properties)
            {
                var propertyLocation = $"{location}.{property.Name}";
                if (!property.Name.Equals("value", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("isSecret", StringComparison.OrdinalIgnoreCase)
                    && SensitiveDataClassifier.IsSensitiveName(property.Name)
                    && HasLiteralValue(property.Value))
                {
                    AddFinding(
                        relativePath,
                        propertyLocation,
                        GitSecretFindingKind.SensitiveNamedValue,
                        findings,
                        uniqueFindings);
                }

                if (property.Value.ValueKind == JsonValueKind.String
                    && (SensitiveDataClassifier.ContainsKnownCredentialPattern(
                            property.Value.GetString())
                        || SensitiveDataClassifier.ContainsSensitiveAssignment(
                            property.Value.GetString())))
                {
                    AddFinding(
                        relativePath,
                        propertyLocation,
                        GitSecretFindingKind.CredentialPattern,
                        findings,
                        uniqueFindings);
                }

                ScanElement(
                    relativePath,
                    property.Value,
                    propertyLocation,
                    findings,
                    uniqueFindings);
            }
        }

        private static JsonProperty? FindProperty(
            IEnumerable<JsonProperty> properties,
            string name)
        {
            foreach (var property in properties)
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }

        private static bool HasLiteralValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !SensitiveDataClassifier.IsPlaceholderOnly(value.GetString()),
            _ => true,
        };

        private static void AddFinding(
            string relativePath,
            string location,
            GitSecretFindingKind kind,
            ICollection<GitSecretFinding> findings,
            ISet<(string Location, GitSecretFindingKind Kind)> uniqueFindings)
        {
            if (uniqueFindings.Add((location, kind)))
            {
                findings.Add(new GitSecretFinding(relativePath, location, kind));
            }
        }
    }
}
