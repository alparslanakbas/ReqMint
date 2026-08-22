using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReqMint.Core.Runner;

public static partial class CollectionRunDataParser
{
    public const int MaximumFileBytes = 1024 * 1024;
    public const int MaximumCharacterCount = 1024 * 1024;
    public const int MaximumRowCount = 100;
    public const int MaximumFieldCount = 100;
    public const int MaximumValueLength = 4096;

    public static CollectionRunDataSet Parse(
        string content,
        CollectionRunDataFormat format)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaximumCharacterCount)
        {
            throw new CollectionRunDataException("The data file is too large.");
        }

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        var rows = format switch
        {
            CollectionRunDataFormat.Json => ParseJson(content),
            CollectionRunDataFormat.Csv => ParseCsv(content),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        var error = CollectionRunDataValidator.GetValidationError(rows);
        if (error is not null)
        {
            throw new CollectionRunDataException(error);
        }

        return new CollectionRunDataSet { Rows = rows };
    }

    private static IReadOnlyList<CollectionRunDataRow> ParseJson(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new CollectionRunDataException("JSON run data must be an array of objects.");
            }

            if (document.RootElement.GetArrayLength() > MaximumRowCount)
            {
                throw new CollectionRunDataException(
                    $"Run data is limited to {MaximumRowCount} rows.");
            }

            var rows = new List<CollectionRunDataRow>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new CollectionRunDataException(
                        "Every JSON run-data row must be an object.");
                }

                var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!variables.TryAdd(property.Name, ConvertJsonValue(property.Value)))
                    {
                        throw new CollectionRunDataException(
                            "Run-data field names must be unique ignoring letter case.");
                    }

                    if (variables.Count > MaximumFieldCount)
                    {
                        throw new CollectionRunDataException(
                            $"Run-data rows are limited to {MaximumFieldCount} fields.");
                    }
                }

                rows.Add(new CollectionRunDataRow { Variables = variables });
            }

            return rows;
        }
        catch (JsonException exception)
        {
            throw new CollectionRunDataException("The JSON run-data file is invalid.", exception);
        }
    }

    private static string ConvertJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
        JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
        _ => throw new CollectionRunDataException(
            "JSON run-data values must be strings, numbers, or booleans."),
    };

    private static IReadOnlyList<CollectionRunDataRow> ParseCsv(string content)
    {
        var records = ReadCsvRecords(content);
        if (records.Count < 2)
        {
            throw new CollectionRunDataException(
                "CSV run data requires a header and at least one data row.");
        }

        var headers = records[0].Select(header => header.Trim()).ToArray();
        if (headers.Length > MaximumFieldCount)
        {
            throw new CollectionRunDataException(
                $"Run-data rows are limited to {MaximumFieldCount} fields.");
        }

        var rows = new List<CollectionRunDataRow>(records.Count - 1);
        foreach (var record in records.Skip(1))
        {
            if (record.Count != headers.Length)
            {
                throw new CollectionRunDataException(
                    "Every CSV row must have the same number of fields as the header.");
            }

            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Length; index++)
            {
                if (!variables.TryAdd(headers[index], record[index]))
                {
                    throw new CollectionRunDataException(
                        "CSV headers must be unique ignoring letter case.");
                }
            }

            rows.Add(new CollectionRunDataRow { Variables = variables });
        }

        return rows;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadCsvRecords(string content)
    {
        var records = new List<IReadOnlyList<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoteClosed = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                    continue;
                }

                if (index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                inQuotes = false;
                quoteClosed = true;
                continue;
            }

            if (character == '"')
            {
                if (field.Length > 0 || quoteClosed)
                {
                    throw new CollectionRunDataException("The CSV run-data file is invalid.");
                }

                inQuotes = true;
            }
            else if (character == ',')
            {
                AddCsvField(record, field);
                quoteClosed = false;
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                AddCsvField(record, field);
                AddCsvRecord(records, record);
                record = [];
                quoteClosed = false;
            }
            else
            {
                if (quoteClosed)
                {
                    throw new CollectionRunDataException(
                        "Only a delimiter or line ending may follow a quoted CSV field.");
                }

                field.Append(character);
            }
        }

        if (inQuotes)
        {
            throw new CollectionRunDataException("A quoted CSV field was not closed.");
        }

        if (field.Length > 0 || record.Count > 0 || quoteClosed)
        {
            AddCsvField(record, field);
            AddCsvRecord(records, record);
        }

        return records;
    }

    private static void AddCsvField(ICollection<string> record, StringBuilder field)
    {
        record.Add(field.ToString());
        if (record.Count > MaximumFieldCount)
        {
            throw new CollectionRunDataException(
                $"Run-data rows are limited to {MaximumFieldCount} fields.");
        }

        field.Clear();
    }

    private static void AddCsvRecord(
        ICollection<IReadOnlyList<string>> records,
        IReadOnlyList<string> record)
    {
        records.Add(record);
        if (records.Count > MaximumRowCount + 1)
        {
            throw new CollectionRunDataException(
                $"Run data is limited to {MaximumRowCount} rows.");
        }
    }

    internal static bool IsValidVariableName(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 64
        && VariableNamePattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableNamePattern();
}

public static class CollectionRunDataValidator
{
    public static string? GetValidationError(IReadOnlyList<CollectionRunDataRow>? rows)
    {
        if (rows is null)
        {
            return "Run-data rows are required.";
        }

        if (rows.Count == 0)
        {
            return "Run data must contain at least one row.";
        }

        if (rows.Count > CollectionRunDataParser.MaximumRowCount)
        {
            return $"Run data is limited to {CollectionRunDataParser.MaximumRowCount} rows.";
        }

        foreach (var row in rows)
        {
            if (row?.Variables is null || row.Variables.Count == 0)
            {
                return "Every run-data row must contain at least one field.";
            }

            if (row.Variables.Count > CollectionRunDataParser.MaximumFieldCount)
            {
                return $"Run-data rows are limited to {CollectionRunDataParser.MaximumFieldCount} fields.";
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in row.Variables)
            {
                if (!CollectionRunDataParser.IsValidVariableName(variable.Key))
                {
                    return "Run-data field names must be valid template variable names.";
                }

                if (!names.Add(variable.Key))
                {
                    return "Run-data field names must be unique ignoring letter case.";
                }

                if (variable.Value is null
                    || variable.Value.Length > CollectionRunDataParser.MaximumValueLength)
                {
                    return $"Run-data values are limited to {CollectionRunDataParser.MaximumValueLength} characters.";
                }
            }
        }

        return null;
    }
}

public sealed record CollectionRunDataSet
{
    public required IReadOnlyList<CollectionRunDataRow> Rows { get; init; }
}

public sealed record CollectionRunDataRow
{
    public required IReadOnlyDictionary<string, string> Variables { get; init; }
}

public enum CollectionRunDataFormat
{
    Json,
    Csv,
}

public sealed class CollectionRunDataException : Exception
{
    public CollectionRunDataException(string message)
        : base(message)
    {
    }

    public CollectionRunDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
