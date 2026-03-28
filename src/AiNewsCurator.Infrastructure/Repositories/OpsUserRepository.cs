using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Persistence;

namespace AiNewsCurator.Infrastructure.Repositories;

public sealed class OpsUserRepository : IOpsUserRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OpsUserRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OpsUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM OpsUsers WHERE Email = @Email LIMIT 1";
        command.Parameters.AddWithValue("@Email", normalizedEmail);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<OpsUser?> FindByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM OpsUsers WHERE Id = @Id LIMIT 1";
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task AddAsync(OpsUser user, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO OpsUsers (Email, DisplayName, IsActive, CreatedAtUtc, UpdatedAtUtc, LastLoginAtUtc)
            VALUES (@Email, @DisplayName, @IsActive, @CreatedAtUtc, @UpdatedAtUtc, @LastLoginAtUtc)
            """;
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAtUtc", user.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAtUtc", user.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@LastLoginAtUtc", user.LastLoginAtUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(OpsUser user, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE OpsUsers
            SET Email = @Email,
                DisplayName = @DisplayName,
                IsActive = @IsActive,
                UpdatedAtUtc = @UpdatedAtUtc,
                LastLoginAtUtc = @LastLoginAtUtc
            WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", user.Id);
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@UpdatedAtUtc", user.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("@LastLoginAtUtc", user.LastLoginAtUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OpsUser Map(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new OpsUser
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            DisplayName = reader.GetNullableString("DisplayName"),
            IsActive = reader.GetInt64(reader.GetOrdinal("IsActive")) == 1,
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAtUtc"))),
            UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAtUtc"))),
            LastLoginAtUtc = reader.GetNullableDateTimeOffset("LastLoginAtUtc")
        };
    }
}
