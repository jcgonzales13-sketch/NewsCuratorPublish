using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class SourceRepository : ISourceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SourceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Sources ORDER BY Priority DESC, Id ASC";

        var items = new List<Source>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Sources WHERE IsActive = 1 ORDER BY Priority DESC, Id ASC";

        var items = new List<Source>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Sources WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<long> InsertAsync(Source source, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Sources
            (Name, Type, Url, Language, IsActive, Priority, MaxItemsPerRun, IncludeKeywordsJson, ExcludeKeywordsJson, TagsJson, CreatedAt, UpdatedAt)
            VALUES
            (@Name, @Type, @Url, @Language, @IsActive, @Priority, @MaxItemsPerRun, @IncludeKeywordsJson, @ExcludeKeywordsJson, @TagsJson, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@Name", source.Name);
        command.Parameters.AddWithValue("@Type", (int)source.Type);
        command.Parameters.AddWithValue("@Url", source.Url);
        command.Parameters.AddWithValue("@Language", source.Language);
        command.Parameters.AddWithValue("@IsActive", source.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@Priority", source.Priority);
        command.Parameters.AddWithValue("@MaxItemsPerRun", source.MaxItemsPerRun);
        command.Parameters.AddWithValue("@IncludeKeywordsJson", source.IncludeKeywordsJson);
        command.Parameters.AddWithValue("@ExcludeKeywordsJson", source.ExcludeKeywordsJson);
        command.Parameters.AddWithValue("@TagsJson", source.TagsJson);
        command.Parameters.AddWithValue("@CreatedAt", source.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", source.UpdatedAt.ToString("O"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateAsync(Source source, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Sources
            SET Name = @Name,
                Type = @Type,
                Url = @Url,
                Language = @Language,
                IsActive = @IsActive,
                Priority = @Priority,
                MaxItemsPerRun = @MaxItemsPerRun,
                IncludeKeywordsJson = @IncludeKeywordsJson,
                ExcludeKeywordsJson = @ExcludeKeywordsJson,
                TagsJson = @TagsJson,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", source.Id);
        command.Parameters.AddWithValue("@Name", source.Name);
        command.Parameters.AddWithValue("@Type", (int)source.Type);
        command.Parameters.AddWithValue("@Url", source.Url);
        command.Parameters.AddWithValue("@Language", source.Language);
        command.Parameters.AddWithValue("@IsActive", source.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@Priority", source.Priority);
        command.Parameters.AddWithValue("@MaxItemsPerRun", source.MaxItemsPerRun);
        command.Parameters.AddWithValue("@IncludeKeywordsJson", source.IncludeKeywordsJson);
        command.Parameters.AddWithValue("@ExcludeKeywordsJson", source.ExcludeKeywordsJson);
        command.Parameters.AddWithValue("@TagsJson", source.TagsJson);
        command.Parameters.AddWithValue("@UpdatedAt", source.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Sources";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        if (count > 0)
        {
            return;
        }

        var defaults = new[]
        {
            new Source
            {
                Name = "OpenAI News",
                Type = SourceType.Rss,
                Url = "https://openai.com/news/rss.xml",
                Language = "en",
                IsActive = true,
                Priority = 10,
                MaxItemsPerRun = 10,
                IncludeKeywordsJson = "[\"ai\",\"model\",\"gpt\"]",
                ExcludeKeywordsJson = "[]",
                TagsJson = "[\"llm\",\"official\"]"
            },
            new Source
            {
                Name = "MIT AI News",
                Type = SourceType.Rss,
                Url = "https://news.mit.edu/rss/topic/artificial-intelligence2",
                Language = "en",
                IsActive = true,
                Priority = 8,
                MaxItemsPerRun = 10,
                IncludeKeywordsJson = "[\"ai\",\"artificial intelligence\"]",
                ExcludeKeywordsJson = "[]",
                TagsJson = "[\"research\"]"
            }
        };

        foreach (var source in defaults)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO Sources
                (Name, Type, Url, Language, IsActive, Priority, MaxItemsPerRun, IncludeKeywordsJson, ExcludeKeywordsJson, TagsJson, CreatedAt, UpdatedAt)
                VALUES
                (@Name, @Type, @Url, @Language, @IsActive, @Priority, @MaxItemsPerRun, @IncludeKeywordsJson, @ExcludeKeywordsJson, @TagsJson, @CreatedAt, @UpdatedAt)
                """;
            insert.Parameters.AddWithValue("@Name", source.Name);
            insert.Parameters.AddWithValue("@Type", (int)source.Type);
            insert.Parameters.AddWithValue("@Url", source.Url);
            insert.Parameters.AddWithValue("@Language", source.Language);
            insert.Parameters.AddWithValue("@IsActive", source.IsActive ? 1 : 0);
            insert.Parameters.AddWithValue("@Priority", source.Priority);
            insert.Parameters.AddWithValue("@MaxItemsPerRun", source.MaxItemsPerRun);
            insert.Parameters.AddWithValue("@IncludeKeywordsJson", source.IncludeKeywordsJson);
            insert.Parameters.AddWithValue("@ExcludeKeywordsJson", source.ExcludeKeywordsJson);
            insert.Parameters.AddWithValue("@TagsJson", source.TagsJson);
            insert.Parameters.AddWithValue("@CreatedAt", DateTimeOffset.UtcNow.ToString("O"));
            insert.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static Source Map(SqliteDataReader reader)
    {
        return new Source
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Type = (SourceType)reader.GetInt32(reader.GetOrdinal("Type")),
            Url = reader.GetString(reader.GetOrdinal("Url")),
            Language = reader.GetString(reader.GetOrdinal("Language")),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            Priority = reader.GetInt32(reader.GetOrdinal("Priority")),
            MaxItemsPerRun = reader.GetInt32(reader.GetOrdinal("MaxItemsPerRun")),
            IncludeKeywordsJson = reader.GetString(reader.GetOrdinal("IncludeKeywordsJson")),
            ExcludeKeywordsJson = reader.GetString(reader.GetOrdinal("ExcludeKeywordsJson")),
            TagsJson = reader.GetString(reader.GetOrdinal("TagsJson")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")))
        };
    }
}
