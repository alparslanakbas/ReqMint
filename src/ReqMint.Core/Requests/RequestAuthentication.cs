using System.Text.Json.Serialization;

namespace ReqMint.Core.Requests;

[JsonConverter(typeof(JsonStringEnumConverter<RequestAuthenticationType>))]
public enum RequestAuthenticationType
{
    None,
    Bearer,
    Basic,
    ApiKey,
}

[JsonConverter(typeof(JsonStringEnumConverter<ApiKeyLocation>))]
public enum ApiKeyLocation
{
    Header,
    Query,
}

/// <summary>
/// Authentication configuration persisted with a request. Secret-bearing values
/// are variable references (for example {{TOKEN}}); their resolved values remain
/// in the operating-system secret vault through the environment model.
/// </summary>
public sealed record RequestAuthentication
{
    public RequestAuthenticationType Type { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BearerToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BasicUsername { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BasicPassword { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKeyName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKeyValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiKeyLocation? ApiKeyLocation { get; init; }
}
