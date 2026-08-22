using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using ReqMint.Core.Runner;

namespace ReqMint.Storage;

public sealed class SqliteCollectionRunHistoryStore : ICollectionRunHistoryStore
{
    public const int MaximumSerializedResultBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public SqliteCollectionRunHistoryStore(string databasePath)
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
        CollectionRunHistoryEntry entry,
        int retentionLimit = 50,
        CancellationToken cancellationToken = default)
    {
        CollectionRunHistoryValidator.Validate(entry);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionLimit, 1);
        var requestsJson = JsonSerializer.Serialize(entry.Requests, JsonOptions);
        if (Encoding.UTF8.GetByteCount(requestsJson) > MaximumSerializedResultBytes)
        {
            throw new InvalidDataException("The collection run history result is too large.");
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(
            cancellationToken);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO collection_run_history (
                    id, workspace_id, recorded_at_utc, collection_id, collection_name,
                    environment_id, duration_ms, was_cancelled, was_rerun,
                    used_data_file, iteration_count, requests_json)
                VALUES (
                    $id, $workspaceId, $recordedAtUtc, $collectionId, $collectionName,
                    $environmentId, $durationMs, $wasCancelled, $wasRerun,
                    $usedDataFile, $iterationCount, $requestsJson);
                """;
            insert.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
            insert.Parameters.AddWithValue("$workspaceId", entry.WorkspaceId.ToString("D"));
            insert.Parameters.AddWithValue(
                "$recordedAtUtc",
                entry.RecordedAtUtc.ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$collectionId", entry.CollectionId.ToString("D"));
            insert.Parameters.AddWithValue("$collectionName", entry.CollectionName);
            insert.Parameters.AddWithValue(
                "$environmentId",
                entry.EnvironmentId is { } environmentId
                    ? environmentId.ToString("D")
                    : DBNull.Value);
            insert.Parameters.AddWithValue("$durationMs", entry.DurationMilliseconds);
            insert.Parameters.AddWithValue("$wasCancelled", entry.WasCancelled ? 1 : 0);
            insert.Parameters.AddWithValue("$wasRerun", entry.WasRerun ? 1 : 0);
            insert.Parameters.AddWithValue("$usedDataFile", entry.UsedDataFile ? 1 : 0);
            insert.Parameters.AddWithValue("$iterationCount", entry.IterationCount);
            insert.Parameters.AddWithValue(
                "$requestsJson",
                requestsJson);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var trim = connection.CreateCommand())
        {
            trim.Transaction = transaction;
            trim.CommandText =
                """
                DELETE FROM collection_run_history
                WHERE workspace_id = $workspaceId
                  AND collection_id = $collectionId
                  AND id NOT IN (
                      SELECT id
                      FROM collection_run_history
                      WHERE workspace_id = $workspaceId
                        AND collection_id = $collectionId
                      ORDER BY recorded_at_utc DESC, rowid DESC
                      LIMIT $retentionLimit);
                """;
            trim.Parameters.AddWithValue("$workspaceId", entry.WorkspaceId.ToString("D"));
            trim.Parameters.AddWithValue("$collectionId", entry.CollectionId.ToString("D"));
            trim.Parameters.AddWithValue("$retentionLimit", retentionLimit);
            await trim.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CollectionRunHistoryEntry>> ListAsync(
        Guid workspaceId,
        Guid collectionId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(workspaceId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(collectionId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, workspace_id, recorded_at_utc, collection_id, collection_name,
                   environment_id, duration_ms, was_cancelled, was_rerun,
                   used_data_file, iteration_count, requests_json
            FROM collection_run_history
            WHERE workspace_id = $workspaceId
              AND collection_id = $collectionId
            ORDER BY recorded_at_utc DESC, rowid DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$workspaceId", workspaceId.ToString("D"));
        command.Parameters.AddWithValue("$collectionId", collectionId.ToString("D"));
        command.Parameters.AddWithValue("$take", take);

        var entries = new List<CollectionRunHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var requests = JsonSerializer.Deserialize<CollectionRunHistoryRequest[]>(
                reader.GetString(11),
                JsonOptions) ?? throw new InvalidDataException(
                    "The collection run history result is invalid.");
            var entry = new CollectionRunHistoryEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                WorkspaceId = Guid.Parse(reader.GetString(1)),
                RecordedAtUtc = DateTimeOffset.Parse(
                    reader.GetString(2),
                    CultureInfo.InvariantCulture),
                CollectionId = Guid.Parse(reader.GetString(3)),
                CollectionName = reader.GetString(4),
                EnvironmentId = reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
                DurationMilliseconds = reader.GetDouble(6),
                WasCancelled = reader.GetInt32(7) != 0,
                WasRerun = reader.GetInt32(8) != 0,
                UsedDataFile = reader.GetInt32(9) != 0,
                IterationCount = reader.GetInt32(10),
                Requests = requests,
            };
            CollectionRunHistoryValidator.Validate(entry);
            entries.Add(entry);
        }

        return entries;
    }

    public async Task ClearAsync(
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(workspaceId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(collectionId, Guid.Empty);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM collection_run_history
            WHERE workspace_id = $workspaceId AND collection_id = $collectionId;
            """;
        command.Parameters.AddWithValue("$workspaceId", workspaceId.ToString("D"));
        command.Parameters.AddWithValue("$collectionId", collectionId.ToString("D"));
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
                CREATE TABLE IF NOT EXISTS collection_run_history (
                    id TEXT PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    recorded_at_utc TEXT NOT NULL,
                    collection_id TEXT NOT NULL,
                    collection_name TEXT NOT NULL,
                    environment_id TEXT NULL,
                    duration_ms REAL NOT NULL,
                    was_cancelled INTEGER NOT NULL,
                    was_rerun INTEGER NOT NULL DEFAULT 0,
                    used_data_file INTEGER NOT NULL DEFAULT 0,
                    iteration_count INTEGER NOT NULL,
                    requests_json TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_collection_run_history_scope_recorded
                    ON collection_run_history(
                        workspace_id, collection_id, recorded_at_utc DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            if (!await HasColumnAsync(
                connection,
                "was_rerun",
                cancellationToken))
            {
                await using var migration = connection.CreateCommand();
                migration.CommandText =
                    "ALTER TABLE collection_run_history ADD COLUMN was_rerun INTEGER NOT NULL DEFAULT 0;";
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!await HasColumnAsync(
                connection,
                "used_data_file",
                cancellationToken))
            {
                await using var migration = connection.CreateCommand();
                migration.CommandText =
                    "ALTER TABLE collection_run_history ADD COLUMN used_data_file INTEGER NOT NULL DEFAULT 0;";
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static async Task<bool> HasColumnAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(collection_run_history);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
