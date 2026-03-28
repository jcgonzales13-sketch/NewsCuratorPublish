using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class OpsLoginCodeRepository : IOpsLoginCodeRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OpsLoginCodeRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InvalidateActiveCodesAsync(long opsUserId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE OpsLoginCodes
            SET InvalidatedAtUtc = @InvalidatedAtUtc
            WHERE OpsUserId = @OpsUserId
              AND UsedAtUtc IS NULL
              AND InvalidatedAtUtc IS NULL
            """;
        command.Parameters.AddWithValue("@OpsUserId", opsUserId);
        command.Parameters.AddWithValue("@InvalidatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddAsync(OpsLoginCode code, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO OpsLoginCodes
            (OpsUserId, Email, CodeHash, CreatedAtUtc, ExpiresAtUtc, UsedAtUtc, InvalidatedAtUtc, RequestIp, RequestUserAgent, ConsumeIp, ConsumeUserAgent, AttemptCount)
            VALUES
            (@OpsUserId, @Email, @CodeHash, @CreatedAtUtc, @ExpiresAtUtc, @UsedAtUtc, @InvalidatedAtUtc, @RequestIp, @RequestUserAgent, @ConsumeIp, @ConsumeUserAgent, @AttemptCount)
            """;
        command.Parameters.AddWithValue("@OpsUserId", code.OpsUserId);
        command.Parameters.AddWithValue("@Email", code.Email);
        command.Parameters.AddWithValue("@CodeHash", code.CodeHash);
        command.Parameters.AddWithValue("@CreatedAtUtc", code.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@ExpiresAtUtc", code.ExpiresAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@UsedAtUtc", code.UsedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@InvalidatedAtUtc", code.InvalidatedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RequestIp", (object?)code.RequestIp ?? DBNull.Value);
        command.Parameters.AddWithValue("@RequestUserAgent", (object?)code.RequestUserAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("@ConsumeIp", (object?)code.ConsumeIp ?? DBNull.Value);
        command.Parameters.AddWithValue("@ConsumeUserAgent", (object?)code.ConsumeUserAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("@AttemptCount", code.AttemptCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OpsLoginCode?> GetLatestActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT *
            FROM OpsLoginCodes
            WHERE Email = @Email
              AND UsedAtUtc IS NULL
              AND InvalidatedAtUtc IS NULL
            ORDER BY CreatedAtUtc DESC, Id DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@Email", normalizedEmail);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task UpdateAsync(OpsLoginCode code, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE OpsLoginCodes
            SET CodeHash = @CodeHash,
                ExpiresAtUtc = @ExpiresAtUtc,
                UsedAtUtc = @UsedAtUtc,
                InvalidatedAtUtc = @InvalidatedAtUtc,
                ConsumeIp = @ConsumeIp,
                ConsumeUserAgent = @ConsumeUserAgent,
                AttemptCount = @AttemptCount
            WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", code.Id);
        command.Parameters.AddWithValue("@CodeHash", code.CodeHash);
        command.Parameters.AddWithValue("@ExpiresAtUtc", code.ExpiresAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@UsedAtUtc", code.UsedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@InvalidatedAtUtc", code.InvalidatedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ConsumeIp", (object?)code.ConsumeIp ?? DBNull.Value);
        command.Parameters.AddWithValue("@ConsumeUserAgent", (object?)code.ConsumeUserAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("@AttemptCount", code.AttemptCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<int> CountRecentRequestsByEmailAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        => CountAsync(
            "SELECT COUNT(*) FROM OpsLoginCodes WHERE Email = @Value AND CreatedAtUtc >= @SinceUtc",
            normalizedEmail,
            sinceUtc,
            cancellationToken);

    public Task<int> CountRecentRequestsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        => CountAsync(
            "SELECT COUNT(*) FROM OpsLoginCodes WHERE RequestIp = @Value AND CreatedAtUtc >= @SinceUtc",
            ipAddress,
            sinceUtc,
            cancellationToken);

    public async Task<int> CountRecentVerificationAttemptsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COALESCE(SUM(AttemptCount), 0)
            FROM OpsLoginCodes
            WHERE RequestIp = @IpAddress
              AND CreatedAtUtc >= @SinceUtc
            """;
        command.Parameters.AddWithValue("@IpAddress", ipAddress);
        command.Parameters.AddWithValue("@SinceUtc", sinceUtc.ToString("O"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<int> CountAsync(string sql, string value, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Value", value);
        command.Parameters.AddWithValue("@SinceUtc", sinceUtc.ToString("O"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static OpsLoginCode Map(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new OpsLoginCode
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            OpsUserId = reader.GetInt64(reader.GetOrdinal("OpsUserId")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CodeHash = reader.GetString(reader.GetOrdinal("CodeHash")),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAtUtc"))),
            ExpiresAtUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ExpiresAtUtc"))),
            UsedAtUtc = reader.GetNullableDateTimeOffset("UsedAtUtc"),
            InvalidatedAtUtc = reader.GetNullableDateTimeOffset("InvalidatedAtUtc"),
            RequestIp = reader.GetNullableString("RequestIp"),
            RequestUserAgent = reader.GetNullableString("RequestUserAgent"),
            ConsumeIp = reader.GetNullableString("ConsumeIp"),
            ConsumeUserAgent = reader.GetNullableString("ConsumeUserAgent"),
            AttemptCount = reader.GetInt32(reader.GetOrdinal("AttemptCount"))
        };
    }
}
