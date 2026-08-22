using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ReqMint.Core.Runner;
using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class CollectionRunResultExporterTests
{
    [Fact]
    public async Task ExportJsonAsync_WritesVersionedSanitizedResult()
    {
        var exporter = new CollectionRunResultExporter();
        await using var destination = new MemoryStream();

        await exporter.ExportAsync(
            CreateResult(),
            destination,
            CollectionRunExportFormat.Json);

        using var document = JsonDocument.Parse(destination.ToArray());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Commerce & Orders", root.GetProperty("collectionName").GetString());
        Assert.Equal(1, root.GetProperty("passedCount").GetInt32());
        Assert.Equal(2, root.GetProperty("failedCount").GetInt32());
        Assert.Equal(2, root.GetProperty("iterationCount").GetInt32());
        Assert.True(root.GetProperty("wasRerun").GetBoolean());
        Assert.True(root.GetProperty("usedDataFile").GetBoolean());
        var requests = root.GetProperty("requests");
        Assert.Equal(5, requests.GetArrayLength());
        Assert.Equal("passed", requests[0].GetProperty("state").GetString());
        Assert.Equal(
            "statusCodeEquals",
            requests[0].GetProperty("assertions")[0].GetProperty("kind").GetString());

        var report = Encoding.UTF8.GetString(destination.ToArray());
        Assert.DoesNotContain("https://api.example.com", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", report, StringComparison.Ordinal);
        Assert.DoesNotContain("response-body", report, StringComparison.Ordinal);
        Assert.DoesNotContain("stack trace", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportJUnitAsync_MapsOutcomesAndEscapesNames()
    {
        var exporter = new CollectionRunResultExporter();
        await using var destination = new MemoryStream();

        await exporter.ExportAsync(
            CreateResult(),
            destination,
            CollectionRunExportFormat.JUnitXml);

        var document = XDocument.Parse(Encoding.UTF8.GetString(destination.ToArray()));
        var suite = Assert.IsType<XElement>(document.Root);
        Assert.Equal("5", suite.Attribute("tests")?.Value);
        Assert.Equal("1", suite.Attribute("failures")?.Value);
        Assert.Equal("1", suite.Attribute("errors")?.Value);
        Assert.Equal("2", suite.Attribute("skipped")?.Value);
        var cases = suite.Elements("testcase").ToArray();
        Assert.Equal("List <orders> [iteration 1]", cases[0].Attribute("name")?.Value);
        Assert.Null(cases[0].Element("failure"));
        Assert.NotNull(cases[1].Element("failure"));
        Assert.Equal("Timeout", cases[2].Element("error")?.Attribute("type")?.Value);
        Assert.All(cases.Skip(3), testCase => Assert.NotNull(testCase.Element("skipped")));

        var report = Encoding.UTF8.GetString(destination.ToArray());
        Assert.DoesNotContain("secret-token", report, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_RejectsUnsupportedFormat()
    {
        var exporter = new CollectionRunResultExporter();
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => exporter.ExportAsync(
            CreateResult(),
            destination,
            (CollectionRunExportFormat)999));
    }

    private static CollectionRunResult CreateResult() => new()
    {
        CollectionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CollectionName = "Commerce & Orders",
        EnvironmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Duration = TimeSpan.FromMilliseconds(1250),
        WasCancelled = true,
        WasRerun = true,
        UsedDataFile = true,
        IterationCount = 2,
        Results =
        [
            CreateRequest(
                "33333333-3333-3333-3333-333333333331",
                "List <orders>",
                CollectionRequestRunState.Passed,
                iterationNumber: 1,
                statusCode: 200,
                assertions:
                [
                    new CollectionAssertionResult(
                        RequestAssertionKind.StatusCodeEquals,
                        CollectionAssertionOutcome.Passed),
                ]),
            CreateRequest(
                "33333333-3333-3333-3333-333333333332",
                "Reject invalid order",
                CollectionRequestRunState.Failed,
                iterationNumber: 1,
                statusCode: 422),
            CreateRequest(
                "33333333-3333-3333-3333-333333333333",
                "Slow request",
                CollectionRequestRunState.Error,
                iterationNumber: 1,
                errorKind: CollectionRunErrorKind.Timeout),
            CreateRequest(
                "33333333-3333-3333-3333-333333333334",
                "Cancelled request",
                CollectionRequestRunState.Cancelled,
                iterationNumber: 2),
            CreateRequest(
                "33333333-3333-3333-3333-333333333335",
                "Later request",
                CollectionRequestRunState.NotRun,
                iterationNumber: 2),
        ],
    };

    private static CollectionRequestRunResult CreateRequest(
        string id,
        string name,
        CollectionRequestRunState state,
        int iterationNumber,
        int? statusCode = null,
        CollectionRunErrorKind errorKind = CollectionRunErrorKind.None,
        IReadOnlyList<CollectionAssertionResult>? assertions = null) => new()
        {
            RequestId = Guid.Parse(id),
            RequestName = name,
            IterationNumber = iterationNumber,
            State = state,
            StatusCode = statusCode,
            ErrorKind = errorKind,
            Duration = TimeSpan.FromMilliseconds(25),
            Assertions = assertions ?? [],
        };
}
