using ReqMint.Core.Runner;

namespace ReqMint.Core.Tests;

public sealed class CollectionRunDataParserTests
{
    [Fact]
    public void ParseJson_AcceptsFlatScalarRows()
    {
        const string content = """
            [
              { "orderId": "A-1", "quantity": 2, "enabled": true },
              { "orderId": "A-2", "quantity": 3, "enabled": false }
            ]
            """;

        var data = CollectionRunDataParser.Parse(content, CollectionRunDataFormat.Json);

        Assert.Equal(2, data.Rows.Count);
        Assert.Equal("A-1", data.Rows[0].Variables["ORDERID"]);
        Assert.Equal("2", data.Rows[0].Variables["quantity"]);
        Assert.Equal("true", data.Rows[0].Variables["enabled"]);
        Assert.Equal("false", data.Rows[1].Variables["enabled"]);
    }

    [Fact]
    public void ParseCsv_AcceptsQuotedCommasQuotesAndNewLines()
    {
        const string content = "id,note\r\n1,\"mint, fresh\"\r\n2,\"line 1\r\nline \"\"2\"\"\"\r\n";

        var data = CollectionRunDataParser.Parse(content, CollectionRunDataFormat.Csv);

        Assert.Equal(2, data.Rows.Count);
        Assert.Equal("mint, fresh", data.Rows[0].Variables["note"]);
        Assert.Equal("line 1\r\nline \"2\"", data.Rows[1].Variables["note"]);
    }

    [Theory]
    [InlineData("[{\"id\":null}]")]
    [InlineData("[{\"id\":{\"nested\":1}}]")]
    [InlineData("[{\"id\":[1,2]}]")]
    public void ParseJson_RejectsValuesThatCouldWidenTheTemplateSurface(string content)
    {
        Assert.Throws<CollectionRunDataException>(() =>
            CollectionRunDataParser.Parse(content, CollectionRunDataFormat.Json));
    }

    [Theory]
    [InlineData("id,id\n1,2")]
    [InlineData("invalid name\nvalue")]
    [InlineData("id,name\n1")]
    [InlineData("id\n\"unterminated")]
    public void ParseCsv_RejectsInvalidShapes(string content)
    {
        Assert.Throws<CollectionRunDataException>(() =>
            CollectionRunDataParser.Parse(content, CollectionRunDataFormat.Csv));
    }

    [Fact]
    public void Parse_RejectsOversizedContent()
    {
        var content = new string('x', CollectionRunDataParser.MaximumCharacterCount + 1);

        Assert.Throws<CollectionRunDataException>(() =>
            CollectionRunDataParser.Parse(content, CollectionRunDataFormat.Csv));
    }

    [Fact]
    public void Validator_RejectsEmptyRowsFromNonParserCallers()
    {
        var rows = new[]
        {
            new CollectionRunDataRow
            {
                Variables = new Dictionary<string, string>(),
            },
        };

        Assert.NotNull(CollectionRunDataValidator.GetValidationError(rows));
    }
}
