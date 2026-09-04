using System.Security.Cryptography;
using System.Text;
using ReqMint.Core.Requests;
using ReqMint.Core.Security;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Templates;

public sealed class RequestTemplateResolver(ISecretVault secretVault)
{
    public async Task<ApiRequest> ResolveAsync(
        Guid workspaceId,
        EnvironmentDocument? environment,
        RequestDocument request,
        CancellationToken cancellationToken = default) => await ResolveAsync(
            workspaceId,
            environment,
            request,
            iterationVariables: null,
            cancellationToken);

    public async Task<ApiRequest> ResolveAsync(
        Guid workspaceId,
        EnvironmentDocument? environment,
        RequestDocument request,
        IReadOnlyDictionary<string, string>? iterationVariables,
        CancellationToken cancellationToken = default)
    {
        var variableNames = RequestTemplate.FindVariables(GetTemplateValues(request));
        var authenticationSecretVariables = GetAuthenticationSecretVariables(request.Authentication);
        var values = await GetVariableValuesAsync(
            workspaceId,
            environment,
            variableNames,
            authenticationSecretVariables,
            iterationVariables,
            cancellationToken);

        var queryParameters = request.QueryParameters
            .Where(field => field.IsEnabled)
            .Select(field => new RequestField(
                RequestTemplate.Resolve(field.Name, values),
                RequestTemplate.Resolve(field.Value, values)))
            .ToList();
        var headers = request.Headers
            .Where(field => field.IsEnabled)
            .Select(field => new RequestField(
                RequestTemplate.Resolve(field.Name, values),
                RequestTemplate.Resolve(field.Value, values)))
            .ToList();

        ApplyAuthentication(request.Authentication, values, queryParameters, headers);

        return ApiRequest.Create(
            request.Method,
            RequestTemplate.Resolve(request.Url, values)) with
        {
            QueryParameters = queryParameters,
            Headers = headers,
            Body = request.Body is null
                ? null
                : new ApiRequestBody(
                    RequestTemplate.Resolve(request.Body.Content, values),
                    RequestTemplate.Resolve(request.Body.ContentType, values))
                {
                    FormFields = request.Body.FormFields
                        .Where(field => field.IsEnabled)
                        .Select(field => new RequestField(
                            RequestTemplate.Resolve(field.Name, values),
                            RequestTemplate.Resolve(field.Value, values)))
                        .ToArray(),
                    FileFields = request.Body.FileFields
                        .Where(field => field.IsEnabled)
                        .Select(field => new RequestFileField(
                            RequestTemplate.Resolve(field.Name, values),
                            field.FileName)
                        {
                            LocalPath = field.LocalPath,
                        })
                        .ToArray(),
                },
            Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds),
        };
    }

    private async Task<IReadOnlyDictionary<string, string>> GetVariableValuesAsync(
        Guid workspaceId,
        EnvironmentDocument? environment,
        IReadOnlySet<string> variableNames,
        IReadOnlySet<string> authenticationSecretVariables,
        IReadOnlyDictionary<string, string>? iterationVariables,
        CancellationToken cancellationToken)
    {
        var definitions = environment?.Variables.ToDictionary(
            variable => variable.Name,
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, EnvironmentVariable>(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missingVariables = new List<string>();

        foreach (var variableName in variableNames)
        {
            if (authenticationSecretVariables.Contains(variableName))
            {
                if (!definitions.TryGetValue(variableName, out var secretDefinition) ||
                    !secretDefinition.IsSecret)
                {
                    throw new AuthenticationSecretNotProtectedException(variableName);
                }

                var secretValue = await secretVault.GetAsync(
                    new SecretReference(workspaceId, environment!.Id, secretDefinition.Name),
                    cancellationToken);
                if (secretValue is null)
                {
                    missingVariables.Add(variableName);
                }
                else
                {
                    values[variableName] = secretValue;
                }

                continue;
            }

            if (iterationVariables?.TryGetValue(variableName, out var iterationValue) == true)
            {
                values[variableName] = iterationValue;
                continue;
            }

            if (!definitions.TryGetValue(variableName, out var definition))
            {
                missingVariables.Add(variableName);
                continue;
            }

            string? value;
            if (definition.IsSecret)
            {
                value = await secretVault.GetAsync(
                    new SecretReference(workspaceId, environment!.Id, definition.Name),
                    cancellationToken);
            }
            else
            {
                value = definition.Value;
            }

            if (value is null)
            {
                missingVariables.Add(variableName);
            }
            else
            {
                values[variableName] = value;
            }
        }

        if (missingVariables.Count > 0)
        {
            throw new RequestTemplateResolutionException(missingVariables);
        }

        return values;
    }

    private static IEnumerable<string?> GetTemplateValues(RequestDocument request)
    {
        yield return request.Url;

        foreach (var field in request.QueryParameters
            .Concat(request.Headers)
            .Where(field => field.IsEnabled))
        {
            yield return field.Name;
            yield return field.Value;
        }

        if (request.Body is not null)
        {
            yield return request.Body.Content;
            yield return request.Body.ContentType;
            foreach (var field in request.Body.FormFields.Where(field => field.IsEnabled))
            {
                yield return field.Name;
                yield return field.Value;
            }

            foreach (var file in request.Body.FileFields.Where(field => field.IsEnabled))
            {
                yield return file.Name;
            }
        }

        if (request.Authentication is not { } authentication)
        {
            yield break;
        }

        switch (authentication.Type)
        {
            case RequestAuthenticationType.Bearer:
                yield return authentication.BearerToken;
                break;
            case RequestAuthenticationType.Basic:
                yield return authentication.BasicUsername;
                yield return authentication.BasicPassword;
                break;
            case RequestAuthenticationType.ApiKey:
                yield return authentication.ApiKeyName;
                yield return authentication.ApiKeyValue;
                break;
        }
    }

    private static IReadOnlySet<string> GetAuthenticationSecretVariables(
        RequestAuthentication? authentication)
    {
        var variable = authentication?.Type switch
        {
            RequestAuthenticationType.Bearer =>
                RequestTemplate.GetVariableReferenceName(authentication.BearerToken),
            RequestAuthenticationType.Basic =>
                RequestTemplate.GetVariableReferenceName(authentication.BasicPassword),
            RequestAuthenticationType.ApiKey =>
                RequestTemplate.GetVariableReferenceName(authentication.ApiKeyValue),
            _ => null,
        };

        return variable is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>([variable], StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyAuthentication(
        RequestAuthentication? authentication,
        IReadOnlyDictionary<string, string> values,
        List<RequestField> queryParameters,
        List<RequestField> headers)
    {
        if (authentication is null || authentication.Type == RequestAuthenticationType.None)
        {
            return;
        }

        switch (authentication.Type)
        {
            case RequestAuthenticationType.Bearer:
                SetHeader(
                    headers,
                    "Authorization",
                    $"Bearer {RequestTemplate.Resolve(authentication.BearerToken ?? string.Empty, values)}");
                break;
            case RequestAuthenticationType.Basic:
                var username = RequestTemplate.Resolve(authentication.BasicUsername ?? string.Empty, values);
                var password = RequestTemplate.Resolve(authentication.BasicPassword ?? string.Empty, values);
                var credentialBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
                try
                {
                    SetHeader(
                        headers,
                        "Authorization",
                        $"Basic {Convert.ToBase64String(credentialBytes)}");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(credentialBytes);
                }

                break;
            case RequestAuthenticationType.ApiKey:
                var name = RequestTemplate.Resolve(authentication.ApiKeyName ?? string.Empty, values);
                var value = RequestTemplate.Resolve(authentication.ApiKeyValue ?? string.Empty, values);
                if (authentication.ApiKeyLocation == ApiKeyLocation.Query)
                {
                    SetField(queryParameters, name, value);
                }
                else
                {
                    SetHeader(headers, name, value);
                }

                break;
        }
    }

    private static void SetHeader(List<RequestField> headers, string name, string value) =>
        SetField(headers, name, value);

    private static void SetField(List<RequestField> fields, string name, string value)
    {
        fields.RemoveAll(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase));
        fields.Add(new RequestField(name, value));
    }
}
