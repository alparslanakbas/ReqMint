using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReqMint.Core.History;
using ReqMint.Core.Workspaces;

namespace ReqMint.Storage;

public sealed class SqliteRequestHistoryStore : IRequestHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public SqliteRequestHistoryStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        }.ToString();
    }

    public async Task AddAsync(
        RequestHistoryEntry entry,
        int retentionLimit = 200,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionLimit, 1);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO request_history (
                    id, workspace_id, sent_at_utc, request_json, outcome, status_code,
                    reason_phrase, duration_ms, content_type, response_bytes)
                VALUES (
                    $id, $workspaceId, $sentAtUtc, $requestJson, $outcome, $statusCode,
                    $reasonPhrase, $durationMs, $contentType, $responseBytes);
                """;
            insert.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
            insert.Parameters.AddWithValue("$workspaceId", entry.WorkspaceId.ToString("D"));
            insert.Parameters.AddWithValue("$sentAtUtc", entry.SentAtUtc.ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$requestJson", JsonSerializer.Serialize(entry.Request, JsonOptions));
            insert.Parameters.AddWithValue("$outcome", entry.Outcome);
            insert.Parameters.AddWithValue("$statusCode", (object?)entry.StatusCode ?? DBNull.Value);
            insert.Parameters.AddWithValue("$reasonPhrase", (object?)entry.ReasonPhrase ?? DBNull.Value);
            insert.Parameters.AddWithValue("$durationMs", (object?)entry.DurationMilliseconds ?? DBNull.Value);
            insert.Parameters.AddWithValue("$contentType", (object?)entry.ContentType ?? DBNull.Value);
            insert.Parameters.AddWithValue("$responseBytes", (object?)entry.ResponseBytes ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var trim = connection.CreateCommand())
        {
            trim.Transaction = transaction;
            trim.CommandText =
                """
                DELETE FROM request_history
                WHERE workspace_id = $workspaceId
                  AND id NOT IN (
                      SELECT id
                      FROM request_history
                      WHERE workspace_id = $workspaceId
                      ORDER BY sent_at_utc DESC, rowid DESC
                      LIMIT $retentionLimit);
                """;
            trim.Parameters.AddWithValue("$workspaceId", entry.WorkspaceId.ToString("D"));
            trim.Parameters.AddWithValue("$retentionLimit", retentionLimit);
            await trim.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestHistoryEntry>> ListAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, workspace_id, sent_at_utc, request_json, outcome, status_code,
                   reason_phrase, duration_ms, content_type, response_bytes
            FROM request_history
            WHERE workspace_id = $workspaceId
            ORDER BY sent_at_utc DESC, rowid DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$workspaceId", workspaceId.ToString("D"));
        command.Parameters.AddWithValue("$take", take);

        var entries = new List<RequestHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var request = JsonSerializer.Deserialize<RequestDocument>(reader.GetString(3), JsonOptions)
                ?? throw new InvalidDataException("The request history entry is invalid.");
            entries.Add(new RequestHistoryEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                WorkspaceId = Guid.Parse(reader.GetString(1)),
                SentAtUtc = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                Request = request,
                Outcome = reader.GetString(4),
                StatusCode = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                ReasonPhrase = reader.IsDBNull(6) ? null : reader.GetString(6),
                DurationMilliseconds = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                ContentType = reader.IsDBNull(8) ? null : reader.GetString(8),
                ResponseBytes = reader.IsDBNull(9) ? null : reader.GetInt64(9),
            });
        }

        return entries;
    }

    public async Task ClearAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM request_history WHERE workspace_id = $workspaceId;";
        command.Parameters.AddWithValue("$workspaceId", workspaceId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS request_history (
                    id TEXT PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    sent_at_utc TEXT NOT NULL,
                    request_json TEXT NOT NULL,
                    outcome TEXT NOT NULL,
                    status_code INTEGER NULL,
                    reason_phrase TEXT NULL,
                    duration_ms REAL NULL,
                    content_type TEXT NULL,
                    response_bytes INTEGER NULL
                );
                CREATE INDEX IF NOT EXISTS ix_request_history_workspace_sent
                    ON request_history(workspace_id, sent_at_utc DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
