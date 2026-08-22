using ReqMint.Core.Requests;

namespace ReqMint.Core.Workspaces;

public sealed record CollectionDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<RequestDocument> Requests { get; init; } = [];
}

public sealed record RequestDocument
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Method { get; init; }

    public required string Url { get; init; }

    public IReadOnlyList<RequestField> QueryParameters { get; init; } = [];

    public IReadOnlyList<RequestField> Headers { get; init; } = [];

    public ApiRequestBody? Body { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}

