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

    public async Task<Publication?> GetLatestByDraftIdAsync(long postDraftId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Publications WHERE PostDraftId = @PostDraftId ORDER BY Id DESC LIMIT 1";
        command.Parameters.AddWithValue("@PostDraftId", postDraftId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Publication
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            PostDraftId = reader.GetInt64(reader.GetOrdinal("PostDraftId")),
            Platform = reader.GetString(reader.GetOrdinal("Platform")),
            PlatformPostId = reader.IsDBNull(reader.GetOrdinal("PlatformPostId")) ? null : reader.GetString(reader.GetOrdinal("PlatformPostId")),
            PublishedAt = reader.IsDBNull(reader.GetOrdinal("PublishedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PublishedAt"))),
            RequestPayload = reader.GetString(reader.GetOrdinal("RequestPayload")),
            ResponsePayload = reader.GetString(reader.GetOrdinal("ResponsePayload")),
            Status = (AiNewsCurator.Domain.Enums.PublicationStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage"))
        };
    }

    public async Task<IReadOnlyList<Publication>> GetByDraftIdAsync(long postDraftId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Publications WHERE PostDraftId = @PostDraftId ORDER BY Id DESC";
        command.Parameters.AddWithValue("@PostDraftId", postDraftId);

        var items = new List<Publication>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new Publication
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                PostDraftId = reader.GetInt64(reader.GetOrdinal("PostDraftId")),
                Platform = reader.GetString(reader.GetOrdinal("Platform")),
                PlatformPostId = reader.IsDBNull(reader.GetOrdinal("PlatformPostId")) ? null : reader.GetString(reader.GetOrdinal("PlatformPostId")),
                PublishedAt = reader.IsDBNull(reader.GetOrdinal("PublishedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PublishedAt"))),
                RequestPayload = reader.GetString(reader.GetOrdinal("RequestPayload")),
                ResponsePayload = reader.GetString(reader.GetOrdinal("ResponsePayload")),
                Status = (AiNewsCurator.Domain.Enums.PublicationStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage"))
            });
        }

        return items;
    }
}
