using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace ReqMint.Core.Runner;

public interface ICollectionRunResultExporter
{
    Task ExportAsync(
        CollectionRunResult result,
        Stream destination,
        CollectionRunExportFormat format,
        CancellationToken cancellationToken = default);
}

public sealed class CollectionRunResultExporter : ICollectionRunResultExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public async Task ExportAsync(
        CollectionRunResult result,
        Stream destination,
        CollectionRunExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The export destination must be writable.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        switch (format)
        {
            case CollectionRunExportFormat.Json:
                await ExportJsonAsync(result, destination, cancellationToken);
                break;
            case CollectionRunExportFormat.JUnitXml:
                await ExportJUnitAsync(result, destination, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static Task ExportJsonAsync(
        CollectionRunResult result,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var report = new JsonRunReport(
            SchemaVersion: 1,
            result.CollectionId,
            result.CollectionName,
            result.EnvironmentId,
            DurationMilliseconds: ToMilliseconds(result.Duration),
            result.WasCancelled,
            result.WasRerun,
            result.UsedDataFile,
            result.IterationCount,
            result.PassedCount,
            result.FailedCount,
            Requests: result.Results.Select(request => new JsonRequestReport(
                request.RequestId,
                request.RequestName,
                request.IterationNumber,
                request.State,
                request.StatusCode,
                DurationMilliseconds: ToMilliseconds(request.Duration),
                request.ErrorKind,
                Assertions: request.Assertions.Select(assertion => new JsonAssertionReport(
                    assertion.Kind,
                    assertion.Outcome)).ToArray())).ToArray());

        return JsonSerializer.SerializeAsync(
            destination,
            report,
            JsonOptions,
            cancellationToken);
    }

    private static async Task ExportJUnitAsync(
        CollectionRunResult result,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var failures = result.Results.Count(request =>
            request.State == CollectionRequestRunState.Failed);
        var errors = result.Results.Count(request =>
            request.State == CollectionRequestRunState.Error);
        var skipped = result.Results.Count(request => request.State is
            CollectionRequestRunState.Cancelled or CollectionRequestRunState.NotRun);
        var suite = new XElement(
            "testsuite",
            new XAttribute("name", result.CollectionName),
            new XAttribute("tests", result.Results.Count),
            new XAttribute("failures", failures),
            new XAttribute("errors", errors),
            new XAttribute("skipped", skipped),
            new XAttribute("time", ToSeconds(result.Duration)));

        suite.Add(new XElement(
            "properties",
            new XElement("property",
                new XAttribute("name", "reqmint.schemaVersion"),
                new XAttribute("value", "1")),
            new XElement("property",
                new XAttribute("name", "reqmint.collectionId"),
                new XAttribute("value", result.CollectionId)),
            new XElement("property",
                new XAttribute("name", "reqmint.cancelled"),
                new XAttribute("value", result.WasCancelled.ToString().ToLowerInvariant())),
            new XElement("property",
                new XAttribute("name", "reqmint.rerun"),
                new XAttribute("value", result.WasRerun.ToString().ToLowerInvariant())),
            new XElement("property",
                new XAttribute("name", "reqmint.usedDataFile"),
                new XAttribute("value", result.UsedDataFile.ToString().ToLowerInvariant())),
            new XElement("property",
                new XAttribute("name", "reqmint.iterationCount"),
                new XAttribute("value", result.IterationCount))));

        foreach (var request in result.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var testCase = new XElement(
                "testcase",
                new XAttribute("classname", result.CollectionName),
                new XAttribute(
                    "name",
                    result.IterationCount > 1
                        ? $"{request.RequestName} [iteration {request.IterationNumber}]"
                        : request.RequestName),
                new XAttribute("time", ToSeconds(request.Duration)),
                new XAttribute("reqmint-request-id", request.RequestId));

            var properties = new List<XElement>();
            if (result.IterationCount > 1)
            {
                properties.Add(new XElement(
                    "property",
                    new XAttribute("name", "reqmint.iteration"),
                    new XAttribute("value", request.IterationNumber)));
            }

            if (request.StatusCode is { } statusCode)
            {
                properties.Add(new XElement(
                    "property",
                    new XAttribute("name", "reqmint.statusCode"),
                    new XAttribute("value", statusCode)));
            }

            properties.AddRange(request.Assertions.Select((assertion, index) => new XElement(
                "property",
                new XAttribute("name", $"reqmint.assertion.{index + 1}.{ToCamelCase(assertion.Kind)}"),
                new XAttribute("value", ToCamelCase(assertion.Outcome)))));
            if (properties.Count > 0)
            {
                testCase.Add(new XElement("properties", properties));
            }

            AddJUnitOutcome(testCase, request);
            suite.Add(testCase);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            suite);
        using var writer = XmlWriter.Create(destination, new XmlWriterSettings
        {
            Async = true,
            CloseOutput = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
        });
        await document.SaveAsync(writer, cancellationToken);
        await writer.FlushAsync();
    }

    private static void AddJUnitOutcome(
        XElement testCase,
        CollectionRequestRunResult request)
    {
        switch (request.State)
        {
            case CollectionRequestRunState.Failed:
                testCase.Add(new XElement(
                    "failure",
                    new XAttribute("type", "AssertionFailure"),
                    new XAttribute("message", "The request or one of its assertions failed.")));
                break;
            case CollectionRequestRunState.Error:
                testCase.Add(new XElement(
                    "error",
                    new XAttribute("type", request.ErrorKind),
                    new XAttribute("message", GetSafeErrorMessage(request.ErrorKind))));
                break;
            case CollectionRequestRunState.Cancelled:
                testCase.Add(new XElement(
                    "skipped",
                    new XAttribute("message", "The request was cancelled.")));
                break;
            case CollectionRequestRunState.NotRun:
                testCase.Add(new XElement(
                    "skipped",
                    new XAttribute("message", "The request was not run.")));
                break;
        }
    }

    private static string GetSafeErrorMessage(CollectionRunErrorKind errorKind) => errorKind switch
    {
        CollectionRunErrorKind.MissingVariables => "Required environment values were unavailable.",
        CollectionRunErrorKind.Timeout => "The request timed out.",
        CollectionRunErrorKind.Transport => "The network request failed.",
        CollectionRunErrorKind.InvalidRequest => "The request configuration was invalid.",
        _ => "The request could not be completed.",
    };

    private static double ToMilliseconds(TimeSpan duration) =>
        Math.Max(0, duration.TotalMilliseconds);

    private static string ToSeconds(TimeSpan duration) =>
        Math.Max(0, duration.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture);

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return text.Length == 0
            ? text
            : char.ToLowerInvariant(text[0]) + text[1..];
    }

    private sealed record JsonRunReport(
        int SchemaVersion,
        Guid CollectionId,
        string CollectionName,
        Guid? EnvironmentId,
        double DurationMilliseconds,
        bool WasCancelled,
        bool WasRerun,
        bool UsedDataFile,
        int IterationCount,
        int PassedCount,
        int FailedCount,
        IReadOnlyList<JsonRequestReport> Requests);

    private sealed record JsonRequestReport(
        Guid RequestId,
        string RequestName,
        int IterationNumber,
        CollectionRequestRunState State,
        int? StatusCode,
        double DurationMilliseconds,
        CollectionRunErrorKind ErrorKind,
        IReadOnlyList<JsonAssertionReport> Assertions);

    private sealed record JsonAssertionReport(
        Workspaces.RequestAssertionKind Kind,
        CollectionAssertionOutcome Outcome);
}

public enum CollectionRunExportFormat
{
    Json,
    JUnitXml,
}
