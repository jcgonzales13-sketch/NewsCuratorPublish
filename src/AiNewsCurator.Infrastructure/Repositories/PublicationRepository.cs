using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class PublicationRepository : IPublicationRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PublicationRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(Publication publication, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Publications
            (PostDraftId, Platform, PlatformPostId, PublishedAt, RequestPayload, ResponsePayload, Status, ErrorMessage)
            VALUES
            (@PostDraftId, @Platform, @PlatformPostId, @PublishedAt, @RequestPayload, @ResponsePayload, @Status, @ErrorMessage);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@PostDraftId", publication.PostDraftId);
        command.Parameters.AddWithValue("@Platform", publication.Platform);
        command.Parameters.AddWithValue("@PlatformPostId", (object?)publication.PlatformPostId ?? DBNull.Value);
        command.Parameters.AddWithValue("@PublishedAt", publication.PublishedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestPayload", publication.RequestPayload);
        command.Parameters.AddWithValue("@ResponsePayload", publication.ResponsePayload);
        command.Parameters.AddWithValue("@Status", (int)publication.Status);
        command.Parameters.AddWithValue("@ErrorMessage", (object?)publication.ErrorMessage ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }
}
