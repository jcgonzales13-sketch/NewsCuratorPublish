using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Infrastructure.Persistence;
using AiNewsCurator.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.IntegrationTests;

public sealed class OpsAuthIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ainews-ops-auth-{Guid.NewGuid():N}.db");
    private SqliteConnectionFactory _connectionFactory = default!;

    public async Task InitializeAsync()
    {
        _connectionFactory = new SqliteConnectionFactory(Options.Create(new AppOptions { DatabasePath = _databasePath }));
        var initializer = new SqliteDatabaseInitializer(_connectionFactory, NullLogger<SqliteDatabaseInitializer>.Instance);
        await initializer.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task RequestCode_For_Any_Email_Should_Store_Hashed_Code()
    {
        var now = new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero);
        var emailSender = new CapturingEmailSender();
        var service = CreateService(emailSender, now);

        var result = await service.RequestCodeAsync("ops@example.com", "127.0.0.1", "IntegrationTest", CancellationToken.None);
        var rowCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsLoginCodes");
        var storedHash = await ExecuteStringScalarAsync("SELECT CodeHash FROM OpsLoginCodes LIMIT 1");
        var userCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsUsers WHERE Email = 'ops@example.com'");

        Assert.True(result.Accepted);
        Assert.Equal(1, rowCount);
        Assert.Equal(1, userCount);
        Assert.NotNull(storedHash);
        Assert.DoesNotContain(emailSender.LastCode!, storedHash!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCode_For_Unknown_Email_Should_Create_User_And_Store_Code()
    {
        var service = CreateService(new CapturingEmailSender(), new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero));

        var result = await service.RequestCodeAsync("unknown@example.com", "127.0.0.1", "IntegrationTest", CancellationToken.None);
        var rowCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsLoginCodes");
        var userCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsUsers WHERE Email = 'unknown@example.com'");

        Assert.True(result.Accepted);
        Assert.Equal(1, rowCount);
        Assert.Equal(1, userCount);
    }

    [Fact]
    public async Task Requesting_A_Second_Code_Should_Invalidate_The_First()
    {
        var firstTime = new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero);
        await SeedOpsUserAsync("ops@example.com");
        var emailSender = new CapturingEmailSender();

        await CreateService(emailSender, firstTime).RequestCodeAsync("ops@example.com", "127.0.0.1", "IntegrationTest", CancellationToken.None);
        await CreateService(emailSender, firstTime.AddMinutes(2)).RequestCodeAsync("ops@example.com", "127.0.0.1", "IntegrationTest", CancellationToken.None);

        var invalidatedCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsLoginCodes WHERE InvalidatedAtUtc IS NOT NULL");
        var activeCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM OpsLoginCodes WHERE UsedAtUtc IS NULL AND InvalidatedAtUtc IS NULL");

        Assert.Equal(1, invalidatedCount);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Verify_Expired_Code_Should_Fail()
    {
        await SeedOpsUserAsync("ops@example.com");
        await InsertCodeAsync(
            new OpsLoginCode
            {
                OpsUserId = 1,
                Email = "ops@example.com",
                CodeHash = OpsAuthCodeHasher.Hash("123456"),
                CreatedAtUtc = new DateTimeOffset(2026, 03, 28, 11, 40, 0, TimeSpan.Zero),
                ExpiresAtUtc = new DateTimeOffset(2026, 03, 28, 11, 50, 0, TimeSpan.Zero)
            });

        var result = await CreateService(new CapturingEmailSender(), new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero))
            .VerifyCodeAsync("ops@example.com", "123456", "127.0.0.1", "IntegrationTest", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired code.", result.ErrorMessage);
    }

    [Fact]
    public async Task Verify_Reused_Code_Should_Fail()
    {
        await SeedOpsUserAsync("ops@example.com");
        await InsertCodeAsync(
            new OpsLoginCode
            {
                OpsUserId = 1,
                Email = "ops@example.com",
                CodeHash = OpsAuthCodeHasher.Hash("123456"),
                CreatedAtUtc = new DateTimeOffset(2026, 03, 28, 11, 55, 0, TimeSpan.Zero),
                ExpiresAtUtc = new DateTimeOffset(2026, 03, 28, 12, 05, 0, TimeSpan.Zero),
                UsedAtUtc = new DateTimeOffset(2026, 03, 28, 11, 56, 0, TimeSpan.Zero)
            });

        var result = await CreateService(new CapturingEmailSender(), new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero))
            .VerifyCodeAsync("ops@example.com", "123456", "127.0.0.1", "IntegrationTest", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired code.", result.ErrorMessage);
    }

    private OpsAuthService CreateService(CapturingEmailSender emailSender, DateTimeOffset now)
    {
        return new OpsAuthService(
            new OpsUserRepository(_connectionFactory),
            new OpsLoginCodeRepository(_connectionFactory),
            emailSender,
            Options.Create(new AppOptions
            {
                DatabasePath = _databasePath,
                OpsLoginCodeTtlMinutes = 10,
                OpsLoginMaxVerifyAttempts = 5
            }),
            new FixedTimeProvider(now),
            NullLogger<OpsAuthService>.Instance);
    }

    private async Task SeedOpsUserAsync(string email)
    {
        var repository = new OpsUserRepository(_connectionFactory);
        await repository.AddAsync(
            new OpsUser
            {
                Email = email,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            CancellationToken.None);
    }

    private async Task InsertCodeAsync(OpsLoginCode code)
    {
        var repository = new OpsLoginCodeRepository(_connectionFactory);
        await repository.AddAsync(code, CancellationToken.None);
    }

    private async Task<int> ExecuteScalarAsync(string sql)
    {
        await using var connection = await _connectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(CancellationToken.None));
    }

    private async Task<string?> ExecuteStringScalarAsync(string sql)
    {
        await using var connection = await _connectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(CancellationToken.None) as string;
    }

    private sealed class CapturingEmailSender : Domain.Interfaces.IEmailSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            var marker = "Your one-time login code is:";
            var index = message.TextBody.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                var tail = message.TextBody[(index + marker.Length)..].Trim();
                LastCode = tail
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
