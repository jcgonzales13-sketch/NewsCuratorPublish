using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class CurationResultRepository : ICurationResultRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CurationResultRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(CurationResult result, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO CurationResults
            (NewsItemId, RelevanceScore, ConfidenceScore, Category, WhyRelevant, ShouldPublish, AiSummary, KeyPointsJson, PromptVersion, ModelName, PromptPayload, ResponsePayload, CreatedAt)
            VALUES
            (@NewsItemId, @RelevanceScore, @ConfidenceScore, @Category, @WhyRelevant, @ShouldPublish, @AiSummary, @KeyPointsJson, @PromptVersion, @ModelName, @PromptPayload, @ResponsePayload, @CreatedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@NewsItemId", result.NewsItemId);
        command.Parameters.AddWithValue("@RelevanceScore", result.RelevanceScore);
        command.Parameters.AddWithValue("@ConfidenceScore", result.ConfidenceScore);
        command.Parameters.AddWithValue("@Category", result.Category);
        command.Parameters.AddWithValue("@WhyRelevant", result.WhyRelevant);
        command.Parameters.AddWithValue("@ShouldPublish", result.ShouldPublish ? 1 : 0);
        command.Parameters.AddWithValue("@AiSummary", result.AiSummary);
        command.Parameters.AddWithValue("@KeyPointsJson", result.KeyPointsJson);
        command.Parameters.AddWithValue("@PromptVersion", result.PromptVersion);
        command.Parameters.AddWithValue("@ModelName", result.ModelName);
        command.Parameters.AddWithValue("@PromptPayload", result.PromptPayload);
        command.Parameters.AddWithValue("@ResponsePayload", result.ResponsePayload);
        command.Parameters.AddWithValue("@CreatedAt", result.CreatedAt.ToString("O"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<CurationResult?> GetLatestByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CurationResults WHERE NewsItemId = @NewsItemId ORDER BY Id DESC LIMIT 1";
        command.Parameters.AddWithValue("@NewsItemId", newsItemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CurationResult
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            NewsItemId = reader.GetInt64(reader.GetOrdinal("NewsItemId")),
            RelevanceScore = reader.GetDouble(reader.GetOrdinal("RelevanceScore")),
            ConfidenceScore = reader.GetDouble(reader.GetOrdinal("ConfidenceScore")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            WhyRelevant = reader.GetString(reader.GetOrdinal("WhyRelevant")),
            ShouldPublish = reader.GetInt32(reader.GetOrdinal("ShouldPublish")) == 1,
            AiSummary = reader.GetString(reader.GetOrdinal("AiSummary")),
            KeyPointsJson = reader.GetString(reader.GetOrdinal("KeyPointsJson")),
            PromptVersion = reader.GetString(reader.GetOrdinal("PromptVersion")),
            ModelName = reader.GetString(reader.GetOrdinal("ModelName")),
            PromptPayload = reader.GetString(reader.GetOrdinal("PromptPayload")),
            ResponsePayload = reader.GetString(reader.GetOrdinal("ResponsePayload")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
        };
    }
}
