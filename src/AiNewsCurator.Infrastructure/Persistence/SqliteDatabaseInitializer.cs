using AiNewsCurator.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiNewsCurator.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteDatabaseInitializer> _logger;

    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory, ILogger<SqliteDatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Sources (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Url TEXT NOT NULL,
                Language TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                MaxItemsPerRun INTEGER NOT NULL,
                IncludeKeywordsJson TEXT NOT NULL,
                ExcludeKeywordsJson TEXT NOT NULL,
                TagsJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS NewsItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceId INTEGER NOT NULL,
                ExternalId TEXT NULL,
                Title TEXT NOT NULL,
                Url TEXT NOT NULL,
                CanonicalUrl TEXT NOT NULL,
                Author TEXT NULL,
                PublishedAt TEXT NOT NULL,
                Language TEXT NOT NULL,
                RawSummary TEXT NULL,
                RawContent TEXT NULL,
                ContentHash TEXT NOT NULL,
                TitleHash TEXT NOT NULL,
                Status INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (SourceId) REFERENCES Sources(Id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_NewsItems_CanonicalUrl ON NewsItems(CanonicalUrl);

            CREATE TABLE IF NOT EXISTS CurationResults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NewsItemId INTEGER NOT NULL,
                RelevanceScore REAL NOT NULL,
                ConfidenceScore REAL NOT NULL,
                Category TEXT NOT NULL,
                WhyRelevant TEXT NOT NULL,
                ShouldPublish INTEGER NOT NULL,
                AiSummary TEXT NOT NULL,
                KeyPointsJson TEXT NOT NULL,
                PromptVersion TEXT NOT NULL,
                ModelName TEXT NOT NULL,
                PromptPayload TEXT NOT NULL,
                ResponsePayload TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (NewsItemId) REFERENCES NewsItems(Id)
            );

            CREATE TABLE IF NOT EXISTS PostDrafts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NewsItemId INTEGER NOT NULL,
                TitleSuggestion TEXT NULL,
                PostText TEXT NOT NULL,
                Tone TEXT NOT NULL,
                Cta TEXT NULL,
                Status INTEGER NOT NULL,
                ValidationErrorsJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                ApprovedAt TEXT NULL,
                ApprovedBy TEXT NULL,
                FOREIGN KEY (NewsItemId) REFERENCES NewsItems(Id)
            );

            CREATE TABLE IF NOT EXISTS Publications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PostDraftId INTEGER NOT NULL,
                Platform TEXT NOT NULL,
                PlatformPostId TEXT NULL,
                PublishedAt TEXT NULL,
                RequestPayload TEXT NOT NULL,
                ResponsePayload TEXT NOT NULL,
                Status INTEGER NOT NULL,
                ErrorMessage TEXT NULL,
                FOREIGN KEY (PostDraftId) REFERENCES PostDrafts(Id)
            );

            CREATE TABLE IF NOT EXISTS ExecutionRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                Status INTEGER NOT NULL,
                TriggerType INTEGER NOT NULL,
                ItemsCollected INTEGER NOT NULL,
                ItemsDeduplicated INTEGER NOT NULL,
                ItemsCurated INTEGER NOT NULL,
                ItemsApproved INTEGER NOT NULL,
                ItemsPublished INTEGER NOT NULL,
                ErrorCount INTEGER NOT NULL,
                LogSummary TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL UNIQUE,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("SQLite schema is ready.");
    }
}
