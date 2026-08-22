using System.Globalization;
using System.Text.Json;
using ReqMint.Core.Requests;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Runner;

internal static class RequestAssertionEvaluator
{
    public static IReadOnlyList<CollectionAssertionResult> Evaluate(
        IReadOnlyList<RequestAssertion> assertions,
        ApiResponse response)
    {
        if (assertions.Count == 0)
        {
            return [];
        }

        JsonDocument? jsonDocument = null;
        var jsonUnavailable = false;
        try
        {
            var results = new List<CollectionAssertionResult>(assertions.Count);
            foreach (var assertion in assertions)
            {
                var outcome = assertion.Kind switch
                {
                    RequestAssertionKind.StatusCodeEquals =>
                        response.StatusCode == assertion.ExpectedStatusCode
                            ? CollectionAssertionOutcome.Passed
                            : CollectionAssertionOutcome.Failed,
                    RequestAssertionKind.MaximumDuration =>
                        response.Duration.TotalMilliseconds <=
                            assertion.MaximumDurationMilliseconds!.Value
                            ? CollectionAssertionOutcome.Passed
                            : CollectionAssertionOutcome.Failed,
                    RequestAssertionKind.JsonPointerExists => EvaluateJsonPointer(
                        assertion.JsonPointer!,
                        response,
                        ref jsonDocument,
                        ref jsonUnavailable),
                    _ => CollectionAssertionOutcome.UnableToEvaluate,
                };
                results.Add(new CollectionAssertionResult(assertion.Kind, outcome));
            }

            return results;
        }
        finally
        {
            jsonDocument?.Dispose();
        }
    }

    private static CollectionAssertionOutcome EvaluateJsonPointer(
        string pointer,
        ApiResponse response,
        ref JsonDocument? document,
        ref bool jsonUnavailable)
    {
        if (response.IsBodyTruncated)
        {
            return CollectionAssertionOutcome.UnableToEvaluate;
        }

        if (document is null && !jsonUnavailable)
        {
            try
            {
                document = JsonDocument.Parse(
                    response.Body,
                    new JsonDocumentOptions { MaxDepth = 64 });
            }
            catch (JsonException)
            {
                jsonUnavailable = true;
            }
        }

        return document is not null && TryResolvePointer(document.RootElement, pointer)
            ? CollectionAssertionOutcome.Passed
            : jsonUnavailable
                ? CollectionAssertionOutcome.UnableToEvaluate
                : CollectionAssertionOutcome.Failed;
    }

    private static bool TryResolvePointer(JsonElement root, string pointer)
    {
        var current = root;
        foreach (var encodedSegment in pointer[1..].Split('/'))
        {
            var segment = encodedSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current.ValueKind != JsonValueKind.Array
                || !int.TryParse(
                    segment,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var index)
                || index < 0
                || index >= current.GetArrayLength())
            {
                return false;
            }

            current = current[index];
        }

        return true;
    }
}
