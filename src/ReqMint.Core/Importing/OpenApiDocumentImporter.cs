using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ReqMint.Core.Requests;
using ReqMint.Core.Security;
using ReqMint.Core.Workspaces;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace ReqMint.Core.Importing;

public enum OpenApiImportWarningKind
{
    DefaultServerUsed,
    PlaceholderGenerated,
    SensitiveExampleOmitted,
    UnsupportedSecurity,
    AuthenticationEnvironmentRequired,
    CookieParameterOmitted,
    UnsupportedRequestBody,
    UploadFileMustBeSelected,
    ExternalReferenceOmitted,
}

public sealed record OpenApiImportWarning(OpenApiImportWarningKind Kind, string ItemName);

public sealed record OpenApiImportResult(
    IReadOnlyList<CollectionDocument> Collections,
    IReadOnlyList<OpenApiImportWarning> Warnings)
{
    public int RequestCount => Collections.Sum(collection => collection.Requests.Count);
}

public sealed class OpenApiImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenApiDocumentImporter
{
    private static readonly IReadOnlyDictionary<string, string> HttpOperations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["get"] = "GET",
            ["post"] = "POST",
            ["put"] = "PUT",
            ["patch"] = "PATCH",
            ["delete"] = "DELETE",
            ["head"] = "HEAD",
            ["options"] = "OPTIONS",
            ["trace"] = "TRACE",
        };

    public OpenApiImportResult Import(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        try
        {
            using var document = ParseDocument(content);
            var root = document.RootElement;
            var version = GetString(root, "openapi");
            if (root.ValueKind != JsonValueKind.Object ||
                (!version.StartsWith("3.0", StringComparison.Ordinal) &&
                 !version.StartsWith("3.1", StringComparison.Ordinal)))
            {
                throw new OpenApiImportException("The file is not an OpenAPI 3.0 or 3.1 document.");
            }

            var title = root.TryGetProperty("info", out var info)
                ? GetString(info, "title")
                : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Imported OpenAPI";
            }

            if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                throw new OpenApiImportException("The OpenAPI document does not contain paths.");
            }

            var warnings = new List<OpenApiImportWarning>();
            var groups = new Dictionary<string, List<RequestDocument>>(StringComparer.OrdinalIgnoreCase);
            var groupOrder = new List<string>();
            foreach (var pathProperty in paths.EnumerateObject())
            {
                var pathItem = Resolve(root, pathProperty.Value, warnings, pathProperty.Name);
                if (pathItem is null || pathItem.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var operationProperty in pathItem.Value.EnumerateObject())
                {
                    if (!HttpOperations.TryGetValue(operationProperty.Name, out var method))
                    {
                        continue;
                    }

                    var operation = Resolve(root, operationProperty.Value, warnings, $"{method} {pathProperty.Name}");
                    if (operation is null || operation.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var tag = FirstString(operation.Value, "tags");
                    var groupName = string.IsNullOrWhiteSpace(tag) ? title : $"{title} — {tag}";
                    if (!groups.TryGetValue(groupName, out var requests))
                    {
                        requests = [];
                        groups[groupName] = requests;
                        groupOrder.Add(groupName);
                    }

                    requests.Add(ParseOperation(
                        root,
                        pathProperty.Name,
                        method,
                        pathItem.Value,
                        operation.Value,
                        warnings));
                }
            }

            if (groups.Count == 0)
            {
                throw new OpenApiImportException("The OpenAPI document does not contain supported operations.");
            }

            var collections = groupOrder.Select(name => new CollectionDocument
            {
                Id = Guid.NewGuid(),
                Name = name,
                Requests = groups[name],
            }).ToArray();
            return new OpenApiImportResult(collections, warnings);
        }
        catch (JsonException exception)
        {
            throw new OpenApiImportException("The OpenAPI document contains invalid JSON.", exception);
        }
        catch (YamlException exception)
        {
            throw new OpenApiImportException("The OpenAPI document contains invalid YAML.", exception);
        }
    }

    private static JsonDocument ParseDocument(string content)
    {
        if (content.AsSpan().TrimStart().StartsWith("{"))
        {
            return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 96 });
        }

        var deserializer = new DeserializerBuilder().Build();
        var yaml = deserializer.Deserialize<object>(content);
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        return JsonDocument.Parse(serializer.Serialize(yaml), new JsonDocumentOptions { MaxDepth = 96 });
    }

    private static RequestDocument ParseOperation(
        JsonElement root,
        string path,
        string method,
        JsonElement pathItem,
        JsonElement operation,
        ICollection<OpenApiImportWarning> warnings)
    {
        var operationName = GetString(operation, "summary");
        if (string.IsNullOrWhiteSpace(operationName))
        {
            operationName = GetString(operation, "operationId");
        }
        if (string.IsNullOrWhiteSpace(operationName))
        {
            operationName = $"{method} {path}";
        }

        var serverUrl = GetServerUrl(root, pathItem, operation, warnings, operationName);
        var url = CombineUrl(serverUrl, path);
        var query = new List<RequestField>();
        var headers = new List<RequestField>();
        foreach (var parameter in GetParameters(root, pathItem, operation, warnings, operationName))
        {
            var name = GetString(parameter, "name");
            var location = GetString(parameter, "in").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
            {
                continue;
            }

            var value = GetParameterValue(root, parameter, name, warnings, operationName);
            switch (location)
            {
                case "path":
                    url = url.Replace($"{{{name}}}", value, StringComparison.Ordinal);
                    break;
                case "query":
                    query.Add(new RequestField(name, value));
                    break;
                case "header":
                    headers.Add(new RequestField(name, value));
                    break;
                case "cookie":
                    warnings.Add(new OpenApiImportWarning(
                        OpenApiImportWarningKind.CookieParameterOmitted,
                        operationName));
                    break;
            }
        }

        url = Regex.Replace(
            url,
            @"(?<!\{)\{([^{}]+)\}(?!\})",
            match =>
            {
                warnings.Add(new OpenApiImportWarning(
                    OpenApiImportWarningKind.PlaceholderGenerated,
                    operationName));
                return Placeholder(match.Groups[1].Value);
            });

        var body = ParseRequestBody(root, operation, operationName, warnings);
        var authentication = ParseSecurity(root, operation, operationName, warnings);
        return new RequestDocument
        {
            Id = Guid.NewGuid(),
            Name = operationName,
            Method = method,
            Url = url,
            QueryParameters = query,
            Headers = headers,
            Authentication = authentication,
            Body = body,
        };
    }

    private static string GetServerUrl(
        JsonElement root,
        JsonElement pathItem,
        JsonElement operation,
        ICollection<OpenApiImportWarning> warnings,
        string operationName)
    {
        var server = FirstObject(operation, "servers")
            ?? FirstObject(pathItem, "servers")
            ?? FirstObject(root, "servers");
        if (server is null)
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.DefaultServerUsed, operationName));
            return "https://api.example.com";
        }

        var url = GetString(server.Value, "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.DefaultServerUsed, operationName));
            return "https://api.example.com";
        }

        if (server.Value.TryGetProperty("variables", out var variables) && variables.ValueKind == JsonValueKind.Object)
        {
            foreach (var variable in variables.EnumerateObject())
            {
                var value = GetString(variable.Value, "default");
                if (SensitiveDataClassifier.IsSensitiveName(variable.Name) || string.IsNullOrWhiteSpace(value))
                {
                    value = Placeholder(variable.Name);
                    warnings.Add(new OpenApiImportWarning(
                        OpenApiImportWarningKind.PlaceholderGenerated,
                        operationName));
                }

                url = url.Replace($"{{{variable.Name}}}", value, StringComparison.Ordinal);
            }
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.DefaultServerUsed, operationName));
            return $"https://api.example.com{url}";
        }

        return url;
    }

    private static IReadOnlyList<JsonElement> GetParameters(
        JsonElement root,
        JsonElement pathItem,
        JsonElement operation,
        ICollection<OpenApiImportWarning> warnings,
        string operationName)
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        AddParameters(pathItem);
        AddParameters(operation);
        return parameters.Values.ToArray();

        void AddParameters(JsonElement owner)
        {
            if (!owner.TryGetProperty("parameters", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var candidate in array.EnumerateArray())
            {
                var resolved = Resolve(root, candidate, warnings, operationName);
                if (resolved is null || resolved.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = $"{GetString(resolved.Value, "in")}:{GetString(resolved.Value, "name")}";
                parameters[key] = resolved.Value;
            }
        }
    }

    private static string GetParameterValue(
        JsonElement root,
        JsonElement parameter,
        string name,
        ICollection<OpenApiImportWarning> warnings,
        string operationName)
    {
        if (SensitiveDataClassifier.IsSensitiveName(name))
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.SensitiveExampleOmitted, operationName));
            return Placeholder(name);
        }

        if (TryScalar(parameter, "example", out var value))
        {
            return value;
        }

        if (parameter.TryGetProperty("schema", out var schemaCandidate) &&
            Resolve(root, schemaCandidate, warnings, operationName) is { } schema)
        {
            if (TryScalar(schema, "example", out value) || TryScalar(schema, "default", out value))
            {
                return value;
            }

            if (schema.TryGetProperty("enum", out var values) &&
                values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0)
            {
                return ScalarText(values[0]);
            }
        }

        warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.PlaceholderGenerated, operationName));
        return Placeholder(name);
    }

    private static ApiRequestBody? ParseRequestBody(
        JsonElement root,
        JsonElement operation,
        string operationName,
        ICollection<OpenApiImportWarning> warnings)
    {
        if (!operation.TryGetProperty("requestBody", out var requestBodyCandidate) ||
            Resolve(root, requestBodyCandidate, warnings, operationName) is not { } requestBody ||
            !requestBody.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var media = SelectMediaType(content);
        if (media is null)
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.UnsupportedRequestBody, operationName));
            return null;
        }

        var (contentType, definition) = media.Value;
        if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRequestBody(string.Empty, contentType)
            {
                FormFields = CreateFormFields(root, definition, operationName, warnings),
            };
        }

        if (contentType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var (fields, files) = CreateMultipartFields(root, definition, operationName, warnings);
            return new ApiRequestBody(string.Empty, contentType)
            {
                FormFields = fields,
                FileFields = files,
            };
        }

        var example = CreateExample(root, definition, operationName, warnings);
        return new ApiRequestBody(
            example is null
                ? string.Empty
                : contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                    ? example.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                    : ScalarText(JsonSerializer.SerializeToElement(example)),
            contentType);
    }

    private static IReadOnlyList<RequestField> CreateFormFields(
        JsonElement root,
        JsonElement media,
        string operationName,
        ICollection<OpenApiImportWarning> warnings)
    {
        var result = new List<RequestField>();
        foreach (var property in GetSchemaProperties(root, media, warnings, operationName))
        {
            var value = SensitiveDataClassifier.IsSensitiveName(property.Name)
                ? Placeholder(property.Name)
                : ExampleValueForSchema(root, property.Value, property.Name, warnings, operationName);
            if (SensitiveDataClassifier.IsSensitiveName(property.Name))
            {
                warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.SensitiveExampleOmitted, operationName));
            }
            result.Add(new RequestField(property.Name, value));
        }
        return result;
    }

    private static (IReadOnlyList<RequestField> Fields, IReadOnlyList<RequestFileField> Files)
        CreateMultipartFields(
            JsonElement root,
            JsonElement media,
            string operationName,
            ICollection<OpenApiImportWarning> warnings)
    {
        var fields = new List<RequestField>();
        var files = new List<RequestFileField>();
        foreach (var property in GetSchemaProperties(root, media, warnings, operationName))
        {
            var schema = Resolve(root, property.Value, warnings, operationName) ?? property.Value;
            if (GetString(schema, "format").Equals("binary", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(new RequestFileField(property.Name, "select-file"));
                warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.UploadFileMustBeSelected, operationName));
                continue;
            }

            var value = SensitiveDataClassifier.IsSensitiveName(property.Name)
                ? Placeholder(property.Name)
                : ExampleValueForSchema(root, schema, property.Name, warnings, operationName);
            if (SensitiveDataClassifier.IsSensitiveName(property.Name))
            {
                warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.SensitiveExampleOmitted, operationName));
            }
            fields.Add(new RequestField(property.Name, value));
        }
        return (fields, files);
    }

    private static IEnumerable<JsonProperty> GetSchemaProperties(
        JsonElement root,
        JsonElement media,
        ICollection<OpenApiImportWarning> warnings,
        string operationName)
    {
        if (!media.TryGetProperty("schema", out var schemaCandidate) ||
            Resolve(root, schemaCandidate, warnings, operationName) is not { } schema ||
            !schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }
        return properties.EnumerateObject().ToArray();
    }

    private static JsonNode? CreateExample(
        JsonElement root,
        JsonElement media,
        string operationName,
        ICollection<OpenApiImportWarning> warnings)
    {
        if (media.TryGetProperty("example", out var example))
        {
            return SanitizeExample(JsonNode.Parse(example.GetRawText()), operationName, warnings);
        }
        if (!media.TryGetProperty("schema", out var schema))
        {
            return null;
        }
        return SanitizeExample(
            GenerateSchemaExample(root, schema, operationName, warnings, 0, new HashSet<string>(StringComparer.Ordinal)),
            operationName,
            warnings);
    }

    private static JsonNode? SanitizeExample(
        JsonNode? node,
        string operationName,
        ICollection<OpenApiImportWarning> warnings)
    {
        if (node is JsonObject valueObject)
        {
            foreach (var property in valueObject.ToArray())
            {
                if (SensitiveDataClassifier.IsSensitiveName(property.Key))
                {
                    valueObject[property.Key] = Placeholder(property.Key);
                    warnings.Add(new OpenApiImportWarning(
                        OpenApiImportWarningKind.SensitiveExampleOmitted,
                        operationName));
                }
                else
                {
                    SanitizeExample(property.Value, operationName, warnings);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                SanitizeExample(item, operationName, warnings);
            }
        }

        return node;
    }

    private static JsonNode? GenerateSchemaExample(
        JsonElement root,
        JsonElement schemaCandidate,
        string operationName,
        ICollection<OpenApiImportWarning> warnings,
        int depth,
        ISet<string> references)
    {
        if (depth > 8)
        {
            return null;
        }

        if (schemaCandidate.ValueKind == JsonValueKind.Object &&
            schemaCandidate.TryGetProperty("$ref", out var referenceElement))
        {
            var reference = referenceElement.GetString() ?? string.Empty;
            if (!references.Add(reference))
            {
                return null;
            }
        }

        var schema = Resolve(root, schemaCandidate, warnings, operationName) ?? schemaCandidate;
        if (schema.TryGetProperty("example", out var example) || schema.TryGetProperty("default", out example))
        {
            return JsonNode.Parse(example.GetRawText());
        }
        if (schema.TryGetProperty("enum", out var enumValues) &&
            enumValues.ValueKind == JsonValueKind.Array && enumValues.GetArrayLength() > 0)
        {
            return JsonNode.Parse(enumValues[0].GetRawText());
        }

        var type = GetString(schema, "type");
        if (type == "object" || schema.TryGetProperty("properties", out _))
        {
            var result = new JsonObject();
            if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (SensitiveDataClassifier.IsSensitiveName(property.Name))
                    {
                        result[property.Name] = Placeholder(property.Name);
                        warnings.Add(new OpenApiImportWarning(
                            OpenApiImportWarningKind.SensitiveExampleOmitted,
                            operationName));
                    }
                    else
                    {
                        result[property.Name] = GenerateSchemaExample(
                            root,
                            property.Value,
                            operationName,
                            warnings,
                            depth + 1,
                            new HashSet<string>(references, StringComparer.Ordinal));
                    }
                }
            }
            return result;
        }
        if (type == "array" && schema.TryGetProperty("items", out var items))
        {
            return new JsonArray(GenerateSchemaExample(
                root,
                items,
                operationName,
                warnings,
                depth + 1,
                new HashSet<string>(references, StringComparer.Ordinal)));
        }
        return type switch
        {
            "integer" => JsonValue.Create(0),
            "number" => JsonValue.Create(0m),
            "boolean" => JsonValue.Create(false),
            _ => JsonValue.Create("string"),
        };
    }

    private static string ExampleValueForSchema(
        JsonElement root,
        JsonElement schema,
        string name,
        ICollection<OpenApiImportWarning> warnings,
        string operationName)
    {
        var value = GenerateSchemaExample(
            root,
            schema,
            operationName,
            warnings,
            0,
            new HashSet<string>(StringComparer.Ordinal));
        if (value is null)
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.PlaceholderGenerated, operationName));
            return Placeholder(name);
        }
        return value is JsonValue ? value.ToString() : value.ToJsonString();
    }

    private static RequestAuthentication? ParseSecurity(
        JsonElement root,
        JsonElement operation,
        string operationName,
        ICollection<OpenApiImportWarning> warnings)
    {
        var requirement = FirstSecurityRequirement(operation)
            ?? FirstSecurityRequirement(root);
        if (requirement is null || !requirement.Value.EnumerateObject().Any())
        {
            return null;
        }

        var schemeName = requirement.Value.EnumerateObject().First().Name;
        if (!root.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("securitySchemes", out var schemes) ||
            !schemes.TryGetProperty(schemeName, out var schemeCandidate) ||
            Resolve(root, schemeCandidate, warnings, operationName) is not { } scheme)
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.UnsupportedSecurity, operationName));
            return null;
        }

        var prefix = NormalizeVariableName(schemeName);
        if (GetString(scheme, "type").Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            var httpScheme = GetString(scheme, "scheme");
            if (httpScheme.Equals("bearer", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new OpenApiImportWarning(
                    OpenApiImportWarningKind.AuthenticationEnvironmentRequired,
                    operationName));
                return new RequestAuthentication
                {
                    Type = RequestAuthenticationType.Bearer,
                    BearerToken = Placeholder($"{prefix}_TOKEN"),
                };
            }
            if (httpScheme.Equals("basic", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new OpenApiImportWarning(
                    OpenApiImportWarningKind.AuthenticationEnvironmentRequired,
                    operationName));
                return new RequestAuthentication
                {
                    Type = RequestAuthenticationType.Basic,
                    BasicUsername = Placeholder($"{prefix}_USERNAME"),
                    BasicPassword = Placeholder($"{prefix}_PASSWORD"),
                };
            }
        }
        else if (GetString(scheme, "type").Equals("apiKey", StringComparison.OrdinalIgnoreCase))
        {
            var name = GetString(scheme, "name");
            var location = GetString(scheme, "in").ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(name) && location is "header" or "query")
            {
                warnings.Add(new OpenApiImportWarning(
                    OpenApiImportWarningKind.AuthenticationEnvironmentRequired,
                    operationName));
                return new RequestAuthentication
                {
                    Type = RequestAuthenticationType.ApiKey,
                    ApiKeyName = name,
                    ApiKeyValue = Placeholder($"{prefix}_KEY"),
                    ApiKeyLocation = location == "query"
                        ? ReqMint.Core.Requests.ApiKeyLocation.Query
                        : ReqMint.Core.Requests.ApiKeyLocation.Header,
                };
            }
        }

        warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.UnsupportedSecurity, operationName));
        return null;
    }

    private static (string ContentType, JsonElement Definition)? SelectMediaType(JsonElement content)
    {
        var entries = content.EnumerateObject().ToArray();
        if (entries.Length == 0)
        {
            return null;
        }
        foreach (var preferred in new[]
        {
            "application/json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
        })
        {
            foreach (var entry in entries)
            {
                if (entry.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                {
                    return (entry.Name, entry.Value);
                }
            }
        }
        var first = entries[0];
        return (first.Name, first.Value);
    }

    private static JsonElement? Resolve(
        JsonElement root,
        JsonElement candidate,
        ICollection<OpenApiImportWarning> warnings,
        string itemName)
    {
        if (candidate.ValueKind != JsonValueKind.Object ||
            !candidate.TryGetProperty("$ref", out var referenceElement))
        {
            return candidate;
        }

        var reference = referenceElement.GetString() ?? string.Empty;
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            warnings.Add(new OpenApiImportWarning(OpenApiImportWarningKind.ExternalReferenceOmitted, itemName));
            return null;
        }

        var current = root;
        foreach (var segment in reference[2..].Split('/'))
        {
            var property = segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out current))
            {
                return null;
            }
        }
        return current;
    }

    private static JsonElement? FirstObject(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0 ||
            array[0].ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return array[0];
    }

    private static JsonElement? FirstSecurityRequirement(JsonElement owner)
    {
        if (!owner.TryGetProperty("security", out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0 ||
            array[0].ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return array[0];
    }

    private static string FirstString(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0 ||
            array[0].ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return array[0].GetString() ?? string.Empty;
    }

    private static bool TryScalar(JsonElement owner, string propertyName, out string value)
    {
        if (owner.TryGetProperty(propertyName, out var element) &&
            element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            value = ScalarText(element);
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string ScalarText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

    private static string GetString(JsonElement owner, string propertyName) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string CombineUrl(string server, string path) =>
        $"{server.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string Placeholder(string name) => $"{{{{{NormalizeVariableName(name)}}}}}";

    private static string NormalizeVariableName(string name)
    {
        var normalized = new string(name.Select(character =>
            char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "VALUE" : normalized;
    }
}
