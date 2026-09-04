using System.Text.Json;
using ReqMint.Core.Requests;
using ReqMint.Core.Security;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Importing;

public enum PostmanImportWarningKind
{
    UnsupportedAuthentication,
    SensitiveValueOmitted,
    UnsupportedBody,
    ScriptOmitted,
    FileMustBeReselected,
    EmptyFolderSkipped,
    CollectionVariablesOmitted,
}

public sealed record PostmanImportWarning(PostmanImportWarningKind Kind, string ItemName);

public sealed record PostmanImportResult(
    IReadOnlyList<CollectionDocument> Collections,
    IReadOnlyList<PostmanImportWarning> Warnings)
{
    public int RequestCount => Collections.Sum(collection => collection.Requests.Count);
}

public sealed class PostmanImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PostmanCollectionImporter
{
    public PostmanImportResult Import(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 64,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("info", out var info) ||
                !GetString(info, "schema").Contains("/v2.1", StringComparison.OrdinalIgnoreCase))
            {
                throw new PostmanImportException("The file is not a Postman Collection v2.1 document.");
            }

            var rootName = GetString(info, "name");
            if (string.IsNullOrWhiteSpace(rootName))
            {
                rootName = "Imported Postman Collection";
            }

            if (!root.TryGetProperty("item", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                throw new PostmanImportException("The Postman collection does not contain any items.");
            }

            var warnings = new List<PostmanImportWarning>();
            if (root.TryGetProperty("event", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
            {
                warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.ScriptOmitted, rootName));
            }
            if (HasInheritedAuthentication(root))
            {
                warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.UnsupportedAuthentication, rootName));
            }
            if (root.TryGetProperty("variable", out var variables) &&
                variables.ValueKind == JsonValueKind.Array && variables.GetArrayLength() > 0)
            {
                warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.CollectionVariablesOmitted, rootName));
            }

            var collections = new List<CollectionDocument>();
            var rootRequests = new List<RequestDocument>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("request", out _))
                {
                    rootRequests.Add(ParseRequest(item, [], warnings));
                    continue;
                }

                if (!item.TryGetProperty("item", out var folderItems) || folderItems.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var folderName = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    folderName = "Folder";
                }
                if (HasInheritedAuthentication(item))
                {
                    warnings.Add(new PostmanImportWarning(
                        PostmanImportWarningKind.UnsupportedAuthentication,
                        folderName));
                }

                var requests = new List<RequestDocument>();
                CollectRequests(folderItems, [], warnings, requests);
                if (requests.Count == 0)
                {
                    warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.EmptyFolderSkipped, folderName));
                    continue;
                }

                var collectionName = UniqueName($"{rootName} — {folderName}", usedNames);
                collections.Add(new CollectionDocument
                {
                    Id = Guid.NewGuid(),
                    Name = collectionName,
                    Requests = requests,
                });
            }

            if (rootRequests.Count > 0)
            {
                var rootCollection = new CollectionDocument
                {
                    Id = Guid.NewGuid(),
                    Name = UniqueName(rootName, usedNames),
                    Requests = rootRequests,
                };
                collections.Insert(0, rootCollection);
            }

            if (collections.Count == 0)
            {
                throw new PostmanImportException("The Postman collection does not contain any requests.");
            }

            return new PostmanImportResult(collections, warnings);
        }
        catch (JsonException exception)
        {
            throw new PostmanImportException("The Postman collection contains invalid JSON.", exception);
        }
    }

    private static void CollectRequests(
        JsonElement items,
        IReadOnlyList<string> path,
        ICollection<PostmanImportWarning> warnings,
        ICollection<RequestDocument> requests)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("request", out _))
            {
                requests.Add(ParseRequest(item, path, warnings));
                continue;
            }

            if (item.TryGetProperty("item", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                var folderName = GetString(item, "name");
                if (HasInheritedAuthentication(item))
                {
                    warnings.Add(new PostmanImportWarning(
                        PostmanImportWarningKind.UnsupportedAuthentication,
                        folderName));
                }
                CollectRequests(
                    children,
                    string.IsNullOrWhiteSpace(folderName) ? path : [.. path, folderName],
                    warnings,
                    requests);
            }
        }
    }

    private static RequestDocument ParseRequest(
        JsonElement item,
        IReadOnlyList<string> path,
        ICollection<PostmanImportWarning> warnings)
    {
        var itemName = GetString(item, "name");
        if (string.IsNullOrWhiteSpace(itemName))
        {
            itemName = "Imported request";
        }

        var requestName = path.Count == 0 ? itemName : $"{string.Join(" / ", path)} / {itemName}";
        if (item.TryGetProperty("event", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
        {
            warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.ScriptOmitted, requestName));
        }

        var request = item.GetProperty("request");
        if (request.ValueKind == JsonValueKind.String)
        {
            return new RequestDocument
            {
                Id = Guid.NewGuid(),
                Name = requestName,
                Method = "GET",
                Url = request.GetString() ?? string.Empty,
            };
        }

        if (request.ValueKind != JsonValueKind.Object)
        {
            throw new PostmanImportException($"Request '{requestName}' has an invalid definition.");
        }

        var method = GetString(request, "method");
        var (url, query) = ParseUrl(request, requestName, warnings);
        var headers = ParseFields(request, "header", requestName, warnings);
        var contentType = headers.FirstOrDefault(field =>
            field.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        var body = ParseBody(request, requestName, contentType, warnings);
        if (body is not null)
        {
            headers = headers.Where(field =>
                !field.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        var authentication = ParseAuthentication(request, requestName, warnings);
        return new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = requestName,
            Method = string.IsNullOrWhiteSpace(method) ? "GET" : method.ToUpperInvariant(),
            Url = url,
            QueryParameters = query,
            Headers = headers,
            Authentication = authentication,
            Body = body,
        };
    }

    private static (string Url, IReadOnlyList<RequestField> Query) ParseUrl(
        JsonElement request,
        string requestName,
        ICollection<PostmanImportWarning> warnings)
    {
        if (!request.TryGetProperty("url", out var urlElement))
        {
            return (string.Empty, []);
        }

        if (urlElement.ValueKind == JsonValueKind.String)
        {
            return (urlElement.GetString() ?? string.Empty, []);
        }

        if (urlElement.ValueKind != JsonValueKind.Object)
        {
            return (string.Empty, []);
        }

        var raw = GetString(urlElement, "raw");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = BuildUrl(urlElement);
        }

        var query = ParseFields(urlElement, "query", requestName, warnings);
        return query.Count == 0 ? (raw, query) : (StripQuery(raw), query);
    }

    private static string BuildUrl(JsonElement url)
    {
        var protocol = GetString(url, "protocol");
        var host = JoinArray(url, "host", ".");
        var path = JoinArray(url, "path", "/");
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        return $"{(string.IsNullOrWhiteSpace(protocol) ? "https" : protocol)}://{host}"
            + (string.IsNullOrWhiteSpace(path) ? string.Empty : $"/{path}");
    }

    private static IReadOnlyList<RequestField> ParseFields(
        JsonElement owner,
        string propertyName,
        string requestName,
        ICollection<PostmanImportWarning> warnings)
    {
        if (!owner.TryGetProperty(propertyName, out var fields) || fields.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RequestField>();
        foreach (var field in fields.EnumerateArray())
        {
            var name = GetString(field, "key");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var value = GetString(field, "value");
            if (SensitiveDataClassifier.IsSensitiveName(name) &&
                !SensitiveDataClassifier.IsPlaceholderOnly(value))
            {
                warnings.Add(new PostmanImportWarning(
                    PostmanImportWarningKind.SensitiveValueOmitted,
                    requestName));
                continue;
            }

            result.Add(new RequestField(name, value, !GetBoolean(field, "disabled")));
        }

        return result;
    }

    private static ApiRequestBody? ParseBody(
        JsonElement request,
        string requestName,
        string? headerContentType,
        ICollection<PostmanImportWarning> warnings)
    {
        if (!request.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var mode = GetString(body, "mode");
        if (mode.Equals("raw", StringComparison.OrdinalIgnoreCase))
        {
            var content = GetString(body, "raw");
            if (SensitiveDataClassifier.ContainsSensitiveAssignment(content))
            {
                warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.SensitiveValueOmitted, requestName));
                content = string.Empty;
            }

            return new ApiRequestBody(content, headerContentType ?? InferRawContentType(body));
        }

        if (mode.Equals("urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRequestBody(string.Empty, "application/x-www-form-urlencoded")
            {
                FormFields = ParseFields(body, "urlencoded", requestName, warnings),
            };
        }

        if (mode.Equals("formdata", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMultipartBody(body, requestName, warnings);
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            warnings.Add(new PostmanImportWarning(PostmanImportWarningKind.UnsupportedBody, requestName));
        }

        return null;
    }

    private static ApiRequestBody ParseMultipartBody(
        JsonElement body,
        string requestName,
        ICollection<PostmanImportWarning> warnings)
    {
        var fields = new List<RequestField>();
        var files = new List<RequestFileField>();
        if (body.TryGetProperty("formdata", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var name = GetString(part, "key");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var enabled = !GetBoolean(part, "disabled");
                var type = GetString(part, "type");
                if (type.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var source in GetFileSources(part))
                    {
                        files.Add(new RequestFileField(name, SafeFileName(source), enabled));
                        warnings.Add(new PostmanImportWarning(
                            PostmanImportWarningKind.FileMustBeReselected,
                            requestName));
                    }
                }
                else
                {
                    var value = GetString(part, "value");
                    if (SensitiveDataClassifier.IsSensitiveName(name) &&
                        !SensitiveDataClassifier.IsPlaceholderOnly(value))
                    {
                        warnings.Add(new PostmanImportWarning(
                            PostmanImportWarningKind.SensitiveValueOmitted,
                            requestName));
                        continue;
                    }

                    fields.Add(new RequestField(name, value, enabled));
                }
            }
        }

        return new ApiRequestBody(string.Empty, "multipart/form-data")
        {
            FormFields = fields,
            FileFields = files,
        };
    }

    private static RequestAuthentication? ParseAuthentication(
        JsonElement request,
        string requestName,
        ICollection<PostmanImportWarning> warnings)
    {
        if (!request.TryGetProperty("auth", out var auth) || auth.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = GetString(auth, "type").ToLowerInvariant();
        if (type is "" or "noauth")
        {
            return null;
        }

        RequestAuthentication? result = type switch
        {
            "bearer" => CreateBearer(auth),
            "basic" => CreateBasic(auth),
            "apikey" => CreateApiKey(auth),
            _ => null,
        };
        if (result is null)
        {
            warnings.Add(new PostmanImportWarning(
                type is "bearer" or "basic" or "apikey"
                    ? PostmanImportWarningKind.SensitiveValueOmitted
                    : PostmanImportWarningKind.UnsupportedAuthentication,
                requestName));
        }

        return result;
    }

    private static RequestAuthentication? CreateBearer(JsonElement auth)
    {
        var token = GetAuthValue(auth, "bearer", "token");
        return IsSafeSecretReference(token)
            ? new RequestAuthentication { Type = RequestAuthenticationType.Bearer, BearerToken = token }
            : null;
    }

    private static RequestAuthentication? CreateBasic(JsonElement auth)
    {
        var username = GetAuthValue(auth, "basic", "username");
        var password = GetAuthValue(auth, "basic", "password");
        return IsSafeSecretReference(password)
            ? new RequestAuthentication
            {
                Type = RequestAuthenticationType.Basic,
                BasicUsername = username,
                BasicPassword = password,
            }
            : null;
    }

    private static RequestAuthentication? CreateApiKey(JsonElement auth)
    {
        var name = GetAuthValue(auth, "apikey", "key");
        var value = GetAuthValue(auth, "apikey", "value");
        var location = GetAuthValue(auth, "apikey", "in");
        return !string.IsNullOrWhiteSpace(name) && IsSafeSecretReference(value)
            ? new RequestAuthentication
            {
                Type = RequestAuthenticationType.ApiKey,
                ApiKeyName = name,
                ApiKeyValue = value,
                ApiKeyLocation = location.Equals("query", StringComparison.OrdinalIgnoreCase)
                    ? ReqMint.Core.Requests.ApiKeyLocation.Query
                    : ReqMint.Core.Requests.ApiKeyLocation.Header,
            }
            : null;
    }

    private static string GetAuthValue(JsonElement auth, string type, string key)
    {
        if (!auth.TryGetProperty(type, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var value in values.EnumerateArray())
        {
            if (GetString(value, "key").Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return GetString(value, "value");
            }
        }

        return string.Empty;
    }

    private static bool IsSafeSecretReference(string value) =>
        !string.IsNullOrWhiteSpace(value) && SensitiveDataClassifier.IsPlaceholderOnly(value);

    private static IEnumerable<string> GetFileSources(JsonElement part)
    {
        if (!part.TryGetProperty("src", out var source))
        {
            yield break;
        }

        if (source.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(source.GetString()))
        {
            yield return source.GetString()!;
        }
        else if (source.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in source.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    yield return item.GetString()!;
                }
            }
        }
    }

    private static string SafeFileName(string source)
    {
        var normalized = source.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..];
        return string.IsNullOrWhiteSpace(name) ? "imported-file" : name;
    }

    private static string InferRawContentType(JsonElement body)
    {
        if (body.TryGetProperty("options", out var options) &&
            options.TryGetProperty("raw", out var raw) &&
            GetString(raw, "language") is { } language)
        {
            return language.ToLowerInvariant() switch
            {
                "json" => "application/json",
                "xml" => "application/xml",
                "html" => "text/html",
                "javascript" => "application/javascript",
                _ => "text/plain",
            };
        }

        return "text/plain";
    }

    private static string StripQuery(string url)
    {
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
        {
            return url;
        }

        var fragmentIndex = url.IndexOf('#', queryIndex);
        return fragmentIndex < 0 ? url[..queryIndex] : url[..queryIndex] + url[fragmentIndex..];
    }

    private static string JoinArray(JsonElement owner, string propertyName, string separator)
    {
        if (!owner.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(separator, values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()));
    }

    private static string GetString(JsonElement owner, string propertyName) =>
        owner.TryGetProperty(propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
                _ => string.Empty,
            }
            : string.Empty;

    private static bool GetBoolean(JsonElement owner, string propertyName) =>
        owner.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool HasInheritedAuthentication(JsonElement owner)
    {
        if (!owner.TryGetProperty("auth", out var auth) || auth.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var type = GetString(auth, "type");
        return !string.IsNullOrWhiteSpace(type) &&
            !type.Equals("noauth", StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueName(string requested, ISet<string> names)
    {
        var candidate = requested;
        for (var suffix = 2; !names.Add(candidate); suffix++)
        {
            candidate = $"{requested} {suffix}";
        }

        return candidate;
    }
}
