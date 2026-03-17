using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class ExecutionRunRepository : IExecutionRunRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ExecutionRunRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(ExecutionRun run, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ExecutionRuns
            (StartedAt, FinishedAt, Status, TriggerType, ItemsCollected, ItemsDeduplicated, ItemsCurated, ItemsApproved, ItemsPublished, ErrorCount, LogSummary)
            VALUES
            (@StartedAt, @FinishedAt, @Status, @TriggerType, @ItemsCollected, @ItemsDeduplicated, @ItemsCurated, @ItemsApproved, @ItemsPublished, @ErrorCount, @LogSummary);
            SELECT last_insert_rowid();
            """;
        Bind(command, run, includeIdentity: false);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateAsync(ExecutionRun run, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ExecutionRuns
            SET FinishedAt = @FinishedAt,
                Status = @Status,
                TriggerType = @TriggerType,
                ItemsCollected = @ItemsCollected,
                ItemsDeduplicated = @ItemsDeduplicated,
                ItemsCurated = @ItemsCurated,
                ItemsApproved = @ItemsApproved,
                ItemsPublished = @ItemsPublished,
                ErrorCount = @ErrorCount,
                LogSummary = @LogSummary
            WHERE Id = @Id
            """;
        Bind(command, run, includeIdentity: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionRun>> GetRecentAsync(int maxItems, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ExecutionRuns ORDER BY StartedAt DESC LIMIT @MaxItems";
        command.Parameters.AddWithValue("@MaxItems", maxItems);

        var items = new List<ExecutionRun>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ExecutionRun
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                StartedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
                FinishedAt = reader.GetNullableDateTimeOffset("FinishedAt"),
                Status = (RunStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                TriggerType = (TriggerType)reader.GetInt32(reader.GetOrdinal("TriggerType")),
                ItemsCollected = reader.GetInt32(reader.GetOrdinal("ItemsCollected")),
                ItemsDeduplicated = reader.GetInt32(reader.GetOrdinal("ItemsDeduplicated")),
                ItemsCurated = reader.GetInt32(reader.GetOrdinal("ItemsCurated")),
                ItemsApproved = reader.GetInt32(reader.GetOrdinal("ItemsApproved")),
                ItemsPublished = reader.GetInt32(reader.GetOrdinal("ItemsPublished")),
                ErrorCount = reader.GetInt32(reader.GetOrdinal("ErrorCount")),
                LogSummary = reader.GetString(reader.GetOrdinal("LogSummary"))
            });
        }

        return items;
    }

    private static void Bind(SqliteCommand command, ExecutionRun run, bool includeIdentity)
    {
        if (includeIdentity)
        {
            command.Parameters.AddWithValue("@Id", run.Id);
        }

        command.Parameters.AddWithValue("@StartedAt", run.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@FinishedAt", run.FinishedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)run.Status);
        command.Parameters.AddWithValue("@TriggerType", (int)run.TriggerType);
        command.Parameters.AddWithValue("@ItemsCollected", run.ItemsCollected);
        command.Parameters.AddWithValue("@ItemsDeduplicated", run.ItemsDeduplicated);
        command.Parameters.AddWithValue("@ItemsCurated", run.ItemsCurated);
        command.Parameters.AddWithValue("@ItemsApproved", run.ItemsApproved);
        command.Parameters.AddWithValue("@ItemsPublished", run.ItemsPublished);
        command.Parameters.AddWithValue("@ErrorCount", run.ErrorCount);
        command.Parameters.AddWithValue("@LogSummary", run.LogSummary);
    }
}
