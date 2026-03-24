using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class PostDraftRepository : IPostDraftRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PostDraftRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO PostDrafts
            (NewsItemId, TitleSuggestion, PostText, Tone, Cta, Status, ValidationErrorsJson, CreatedAt, ApprovedAt, ApprovedBy)
            VALUES
            (@NewsItemId, @TitleSuggestion, @PostText, @Tone, @Cta, @Status, @ValidationErrorsJson, @CreatedAt, @ApprovedAt, @ApprovedBy);
            SELECT last_insert_rowid();
            """;
        Bind(command, draft, includeIdentity: false);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<PostDraft?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PostDrafts WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<PostDraft>> GetByNewsItemIdAsync(long newsItemId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PostDrafts WHERE NewsItemId = @NewsItemId ORDER BY CreatedAt DESC, Id DESC";
        command.Parameters.AddWithValue("@NewsItemId", newsItemId);

        var items = new List<PostDraft>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<PostDraft>> GetAllEditableAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PostDrafts WHERE Status <> @Published ORDER BY CreatedAt DESC, Id DESC";
        command.Parameters.AddWithValue("@Published", (int)DraftStatus.Published);

        var items = new List<PostDraft>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<PostDraft>> GetPendingApprovalAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PostDrafts WHERE Status IN (@PendingApproval, @Approved) ORDER BY CreatedAt DESC";
        command.Parameters.AddWithValue("@PendingApproval", (int)DraftStatus.PendingApproval);
        command.Parameters.AddWithValue("@Approved", (int)DraftStatus.Approved);

        var items = new List<PostDraft>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task UpdateAsync(PostDraft draft, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE PostDrafts
            SET TitleSuggestion = @TitleSuggestion,
                PostText = @PostText,
                Tone = @Tone,
                Cta = @Cta,
                Status = @Status,
                ValidationErrorsJson = @ValidationErrorsJson,
                ApprovedAt = @ApprovedAt,
                ApprovedBy = @ApprovedBy
            WHERE Id = @Id
            """;
        Bind(command, draft, includeIdentity: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Bind(SqliteCommand command, PostDraft draft, bool includeIdentity)
    {
        if (includeIdentity)
        {
            command.Parameters.AddWithValue("@Id", draft.Id);
        }

        command.Parameters.AddWithValue("@NewsItemId", draft.NewsItemId);
        command.Parameters.AddWithValue("@TitleSuggestion", (object?)draft.TitleSuggestion ?? DBNull.Value);
        command.Parameters.AddWithValue("@PostText", draft.PostText);
        command.Parameters.AddWithValue("@Tone", draft.Tone);
        command.Parameters.AddWithValue("@Cta", (object?)draft.Cta ?? DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)draft.Status);
        command.Parameters.AddWithValue("@ValidationErrorsJson", draft.ValidationErrorsJson);
        command.Parameters.AddWithValue("@CreatedAt", draft.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@ApprovedAt", draft.ApprovedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ApprovedBy", (object?)draft.ApprovedBy ?? DBNull.Value);
    }

    private static PostDraft Map(SqliteDataReader reader)
    {
        return new PostDraft
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            NewsItemId = reader.GetInt64(reader.GetOrdinal("NewsItemId")),
            TitleSuggestion = reader.GetNullableString("TitleSuggestion"),
            PostText = reader.GetString(reader.GetOrdinal("PostText")),
            Tone = reader.GetString(reader.GetOrdinal("Tone")),
            Cta = reader.GetNullableString("Cta"),
            Status = (DraftStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            ValidationErrorsJson = reader.GetString(reader.GetOrdinal("ValidationErrorsJson")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            ApprovedAt = reader.GetNullableDateTimeOffset("ApprovedAt"),
            ApprovedBy = reader.GetNullableString("ApprovedBy")
        };
    }
}
