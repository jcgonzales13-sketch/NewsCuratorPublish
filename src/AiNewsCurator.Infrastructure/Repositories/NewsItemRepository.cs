using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Net;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class NewsItemRepository : INewsItemRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public NewsItemRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await GetSingleAsync("SELECT * FROM NewsItems WHERE Id = @Id", ("@Id", id), cancellationToken);
    }

    public async Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken)
    {
        return await GetSingleAsync("SELECT * FROM NewsItems WHERE CanonicalUrl = @CanonicalUrl", ("@CanonicalUrl", canonicalUrl), cancellationToken);
    }

    public async Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ni.*
            FROM NewsItems ni
            INNER JOIN PostDrafts pd ON pd.NewsItemId = ni.Id
            WHERE ni.CanonicalUrl = @CanonicalUrl AND pd.Status = @PublishedStatus
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@CanonicalUrl", canonicalUrl);
        command.Parameters.AddWithValue("@PublishedStatus", (int)DraftStatus.Published);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM NewsItems
            WHERE (TitleHash = @TitleHash OR ContentHash = @ContentHash)
              AND PublishedAt >= @PublishedAfter
            """;
        command.Parameters.AddWithValue("@TitleHash", titleHash);
        command.Parameters.AddWithValue("@ContentHash", contentHash);
        command.Parameters.AddWithValue("@PublishedAfter", DateTimeOffset.UtcNow.AddDays(-lookbackDays).ToString("O"));

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 1;
    }

    public async Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO NewsItems
            (SourceId, ExternalId, Title, Url, CanonicalUrl, ImageUrl, ImageOrigin, Author, PublishedAt, Language, RawSummary, RawContent, ContentHash, TitleHash, Status, CreatedAt, UpdatedAt)
            VALUES
            (@SourceId, @ExternalId, @Title, @Url, @CanonicalUrl, @ImageUrl, @ImageOrigin, @Author, @PublishedAt, @Language, @RawSummary, @RawContent, @ContentHash, @TitleHash, @Status, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@SourceId", newsItem.SourceId);
        command.Parameters.AddWithValue("@ExternalId", (object?)newsItem.ExternalId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Title", newsItem.Title);
        command.Parameters.AddWithValue("@Url", newsItem.Url);
        command.Parameters.AddWithValue("@CanonicalUrl", newsItem.CanonicalUrl);
        command.Parameters.AddWithValue("@ImageUrl", (object?)newsItem.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@ImageOrigin", (object?)newsItem.ImageOrigin ?? DBNull.Value);
        command.Parameters.AddWithValue("@Author", (object?)newsItem.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("@PublishedAt", newsItem.PublishedAt.ToString("O"));
        command.Parameters.AddWithValue("@Language", newsItem.Language);
        command.Parameters.AddWithValue("@RawSummary", (object?)newsItem.RawSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("@RawContent", (object?)newsItem.RawContent ?? DBNull.Value);
        command.Parameters.AddWithValue("@ContentHash", newsItem.ContentHash);
        command.Parameters.AddWithValue("@TitleHash", newsItem.TitleHash);
        command.Parameters.AddWithValue("@Status", (int)newsItem.Status);
        command.Parameters.AddWithValue("@CreatedAt", newsItem.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", newsItem.UpdatedAt.ToString("O"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateStatusAsync(long id, NewsItemStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE NewsItems SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        command.Parameters.AddWithValue("@Status", (int)status);
        command.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE NewsItems SET ImageUrl = @ImageUrl, ImageOrigin = @ImageOrigin, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        command.Parameters.AddWithValue("@ImageUrl", imageUrl);
        command.Parameters.AddWithValue("@ImageOrigin", imageOrigin);
        command.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT * FROM NewsItems
            WHERE Status = @Status AND PublishedAt >= @PublishedAfter
            ORDER BY PublishedAt DESC
            LIMIT @MaxItems
            """;
        command.Parameters.AddWithValue("@Status", (int)NewsItemStatus.Collected);
        command.Parameters.AddWithValue("@PublishedAfter", publishedAfter.ToString("O"));
        command.Parameters.AddWithValue("@MaxItems", maxItems);

        var items = new List<NewsItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<NewsItem>> GetRecentAsync(int maxItems, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT * FROM NewsItems
            ORDER BY PublishedAt DESC, Id DESC
            LIMIT @MaxItems
            """;
        command.Parameters.AddWithValue("@MaxItems", maxItems);

        var items = new List<NewsItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT * FROM NewsItems
            WHERE ImageUrl IS NULL OR TRIM(ImageUrl) = ''
            ORDER BY PublishedAt DESC, Id DESC
            """;

        var items = new List<NewsItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT Id, Title, RawSummary, RawContent FROM NewsItems";

        var items = new List<(long Id, string Title, string? RawSummary, string? RawContent)>();
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add((
                    reader.GetInt64(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("Title")),
                    reader.GetNullableString("RawSummary"),
                    reader.GetNullableString("RawContent")));
            }
        }

        var updatedCount = 0;
        foreach (var item in items)
        {
            var normalizedTitle = Decode(item.Title);
            var normalizedSummary = Decode(item.RawSummary);
            var normalizedContent = Decode(item.RawContent);

            if (normalizedTitle == item.Title &&
                normalizedSummary == item.RawSummary &&
                normalizedContent == item.RawContent)
            {
                continue;
            }

            await using var update = connection.CreateCommand();
            update.CommandText =
                """
                UPDATE NewsItems
                SET Title = @Title,
                    RawSummary = @RawSummary,
                    RawContent = @RawContent,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                """;
            update.Parameters.AddWithValue("@Title", normalizedTitle);
            update.Parameters.AddWithValue("@RawSummary", (object?)normalizedSummary ?? DBNull.Value);
            update.Parameters.AddWithValue("@RawContent", (object?)normalizedContent ?? DBNull.Value);
            update.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("@Id", item.Id);

            await update.ExecuteNonQueryAsync(cancellationToken);
            updatedCount++;
        }

        return updatedCount;
    }

    private async Task<NewsItem?> GetSingleAsync(string sql, (string Name, object Value) parameter, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static NewsItem Map(SqliteDataReader reader)
    {
        return new NewsItem
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            SourceId = reader.GetInt64(reader.GetOrdinal("SourceId")),
            ExternalId = reader.GetNullableString("ExternalId"),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Url = reader.GetString(reader.GetOrdinal("Url")),
            CanonicalUrl = reader.GetString(reader.GetOrdinal("CanonicalUrl")),
            ImageUrl = reader.GetNullableString("ImageUrl"),
            ImageOrigin = reader.GetNullableString("ImageOrigin"),
            Author = reader.GetNullableString("Author"),
            PublishedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PublishedAt"))),
            Language = reader.GetString(reader.GetOrdinal("Language")),
            RawSummary = reader.GetNullableString("RawSummary"),
            RawContent = reader.GetNullableString("RawContent"),
            ContentHash = reader.GetString(reader.GetOrdinal("ContentHash")),
            TitleHash = reader.GetString(reader.GetOrdinal("TitleHash")),
            Status = (NewsItemStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")))
        };
    }

    private static string? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return WebUtility.HtmlDecode(value).Trim();
    }
}
