using System.Text.Json.Serialization;

namespace ReqMint.Core.Workspaces;

public sealed record RequestAssertion
{
    public required RequestAssertionKind Kind { get; init; }

    public int? ExpectedStatusCode { get; init; }

    public int? MaximumDurationMilliseconds { get; init; }

    public string? JsonPointer { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<RequestAssertionKind>))]
public enum RequestAssertionKind
{
    StatusCodeEquals,
    MaximumDuration,
    JsonPointerExists,
}

public static class RequestAssertionValidator
{
    public const int MaximumAssertionCount = 50;
    public const int MaximumDurationMilliseconds = 600_000;
    public const int MaximumJsonPointerLength = 256;
    public const int MaximumJsonPointerDepth = 32;

    public static string? GetValidationError(IReadOnlyList<RequestAssertion>? assertions)
    {
        if (assertions is null)
        {
            return "Request assertions cannot be null.";
        }

        if (assertions.Count > MaximumAssertionCount)
        {
            return $"A request can contain at most {MaximumAssertionCount} assertions.";
        }

        if (assertions.Count(assertion => assertion.Kind == RequestAssertionKind.StatusCodeEquals) > 1)
        {
            return "A request can contain only one status-code assertion.";
        }

        if (assertions.Count(assertion => assertion.Kind == RequestAssertionKind.MaximumDuration) > 1)
        {
            return "A request can contain only one maximum-duration assertion.";
        }

        var jsonPointers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assertion in assertions)
        {
            var error = GetValidationError(assertion);
            if (error is not null)
            {
                return error;
            }

            if (assertion.Kind == RequestAssertionKind.JsonPointerExists
                && !jsonPointers.Add(assertion.JsonPointer!))
            {
                return "Duplicate JSON-pointer assertions are not allowed.";
            }
        }

        return null;
    }

    private static string? GetValidationError(RequestAssertion assertion)
    {
        if (!Enum.IsDefined(assertion.Kind))
        {
            return "The request contains an unsupported assertion kind.";
        }

        return assertion.Kind switch
        {
            RequestAssertionKind.StatusCodeEquals =>
                assertion.ExpectedStatusCode is not { } expectedStatusCode
                    || expectedStatusCode is < 100 or > 599
                    || assertion.MaximumDurationMilliseconds is not null
                    || assertion.JsonPointer is not null
                    ? "A status-code assertion requires an expected value between 100 and 599."
                    : null,
            RequestAssertionKind.MaximumDuration =>
                assertion.MaximumDurationMilliseconds is not { } maximumDuration
                    || maximumDuration is < 1 or > MaximumDurationMilliseconds
                    || assertion.ExpectedStatusCode is not null
                    || assertion.JsonPointer is not null
                    ? $"A duration assertion requires a limit between 1 and {MaximumDurationMilliseconds} milliseconds."
                    : null,
            RequestAssertionKind.JsonPointerExists =>
                assertion.ExpectedStatusCode is not null
                    || assertion.MaximumDurationMilliseconds is not null
                    || !IsValidJsonPointer(assertion.JsonPointer)
                    ? "A JSON-field assertion requires a valid JSON Pointer."
                    : null,
            _ => "The request contains an unsupported assertion kind.",
        };
    }

    private static bool IsValidJsonPointer(string? pointer)
    {
        if (string.IsNullOrEmpty(pointer)
            || pointer.Length > MaximumJsonPointerLength
            || pointer[0] != '/')
        {
            return false;
        }

        var segments = pointer[1..].Split('/');
        if (segments.Length > MaximumJsonPointerDepth)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            for (var index = 0; index < segment.Length; index++)
            {
                if (segment[index] != '~')
                {
                    continue;
                }

                if (++index >= segment.Length || segment[index] is not ('0' or '1'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
