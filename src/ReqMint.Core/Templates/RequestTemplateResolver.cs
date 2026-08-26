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
        var values = await GetVariableValuesAsync(
            workspaceId,
            environment,
            variableNames,
            iterationVariables,
            cancellationToken);

        return ApiRequest.Create(
            request.Method,
            RequestTemplate.Resolve(request.Url, values)) with
        {
            QueryParameters = request.QueryParameters
                .Where(field => field.IsEnabled)
                .Select(field => new RequestField(
                    RequestTemplate.Resolve(field.Name, values),
                    RequestTemplate.Resolve(field.Value, values)))
                .ToArray(),
            Headers = request.Headers
                .Where(field => field.IsEnabled)
                .Select(field => new RequestField(
                    RequestTemplate.Resolve(field.Name, values),
                    RequestTemplate.Resolve(field.Value, values)))
                .ToArray(),
            Body = request.Body is null
                ? null
                : new ApiRequestBody(
                    RequestTemplate.Resolve(request.Body.Content, values),
                    RequestTemplate.Resolve(request.Body.ContentType, values)),
            Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds),
        };
    }

    private async Task<IReadOnlyDictionary<string, string>> GetVariableValuesAsync(
        Guid workspaceId,
        EnvironmentDocument? environment,
        IReadOnlySet<string> variableNames,
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
        }
    }
}
