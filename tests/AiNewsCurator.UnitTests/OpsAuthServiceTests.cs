using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.UnitTests;

public sealed class OpsAuthServiceTests
{
    [Fact]
    public void EmailNormalizer_Should_Trim_And_Lowercase()
    {
        Assert.Equal("ops@example.com", OpsAuthEmailNormalizer.Normalize(" OPS@Example.com "));
        Assert.Equal(string.Empty, OpsAuthEmailNormalizer.Normalize(" "));
    }

    [Fact]
    public void LoginCodeGenerator_Should_Create_Six_Digit_Code()
    {
        var code = OpsLoginCodeGenerator.GenerateSixDigitCode();

        Assert.Equal(6, code.Length);
        Assert.All(code, character => Assert.InRange(character, '0', '9'));
    }

    [Fact]
    public void CodeHasher_Should_Return_Stable_Hash()
    {
        var first = OpsAuthCodeHasher.Hash("123456");
        var second = OpsAuthCodeHasher.Hash("123456");

        Assert.Equal(first, second);
        Assert.NotEqual(first, OpsAuthCodeHasher.Hash("654321"));
    }

    [Fact]
    public async Task RequestCode_Should_Store_Hashed_Code_And_Send_Email_For_Active_User()
    {
        var userRepository = new FakeOpsUserRepository(
            new OpsUser
            {
                Id = 7,
                Email = "ops@example.com",
                IsActive = true
            });
        var codeRepository = new FakeOpsLoginCodeRepository();
        var emailSender = new FakeEmailSender();
        var service = CreateService(userRepository, codeRepository, emailSender);

        var result = await service.RequestCodeAsync("ops@example.com", "127.0.0.1", "UnitTest", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(codeRepository.StoredCodes);
        Assert.True(codeRepository.InvalidateCalled);
        Assert.NotEmpty(emailSender.Messages);
        Assert.DoesNotContain("123456", codeRepository.StoredCodes[0].CodeHash, StringComparison.Ordinal);
        Assert.Equal("ops@example.com", codeRepository.StoredCodes[0].Email);
    }

    [Fact]
    public async Task RequestCode_Should_Return_Generic_Success_For_Unknown_Email()
    {
        var service = CreateService(new FakeOpsUserRepository(), new FakeOpsLoginCodeRepository(), new FakeEmailSender());

        var result = await service.RequestCodeAsync("unknown@example.com", "127.0.0.1", "UnitTest", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("If the email is authorized, a login code has been sent.", result.Message);
    }

    [Fact]
    public async Task VerifyCode_Should_Mark_Code_As_Used_And_Update_Last_Login()
    {
        var now = new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero);
        var user = new OpsUser
        {
            Id = 3,
            Email = "ops@example.com",
            IsActive = true,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        };
        var codeRepository = new FakeOpsLoginCodeRepository
        {
            LatestCode = new OpsLoginCode
            {
                Id = 9,
                OpsUserId = 3,
                Email = "ops@example.com",
                CodeHash = OpsAuthCodeHasher.Hash("123456"),
                CreatedAtUtc = now.AddMinutes(-1),
                ExpiresAtUtc = now.AddMinutes(9)
            }
        };
        var userRepository = new FakeOpsUserRepository(user);
        var service = CreateService(userRepository, codeRepository, new FakeEmailSender(), now);

        var result = await service.VerifyCodeAsync("ops@example.com", "123456", "127.0.0.1", "UnitTest", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(codeRepository.UpdatedCode);
        Assert.NotNull(codeRepository.UpdatedCode.UsedAtUtc);
        Assert.Equal("127.0.0.1", codeRepository.UpdatedCode.ConsumeIp);
        Assert.Equal(now, userRepository.UpdatedUser?.LastLoginAtUtc);
    }

    [Fact]
    public async Task VerifyCode_Should_Fail_When_Code_Is_Expired()
    {
        var now = new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            new FakeOpsUserRepository(new OpsUser { Id = 1, Email = "ops@example.com", IsActive = true }),
            new FakeOpsLoginCodeRepository
            {
                LatestCode = new OpsLoginCode
                {
                    Id = 5,
                    OpsUserId = 1,
                    Email = "ops@example.com",
                    CodeHash = OpsAuthCodeHasher.Hash("123456"),
                    CreatedAtUtc = now.AddMinutes(-20),
                    ExpiresAtUtc = now.AddMinutes(-1)
                }
            },
            new FakeEmailSender(),
            now);

        var result = await service.VerifyCodeAsync("ops@example.com", "123456", "127.0.0.1", "UnitTest", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired code.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCode_Should_Invalidate_After_Max_Attempts()
    {
        var now = new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero);
        var codeRepository = new FakeOpsLoginCodeRepository
        {
            LatestCode = new OpsLoginCode
            {
                Id = 5,
                OpsUserId = 1,
                Email = "ops@example.com",
                CodeHash = OpsAuthCodeHasher.Hash("123456"),
                CreatedAtUtc = now.AddMinutes(-1),
                ExpiresAtUtc = now.AddMinutes(9),
                AttemptCount = 4
            }
        };
        var service = CreateService(
            new FakeOpsUserRepository(new OpsUser { Id = 1, Email = "ops@example.com", IsActive = true }),
            codeRepository,
            new FakeEmailSender(),
            now);

        var result = await service.VerifyCodeAsync("ops@example.com", "000000", "127.0.0.1", "UnitTest", CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsRateLimited);
        Assert.NotNull(codeRepository.UpdatedCode?.InvalidatedAtUtc);
        Assert.Equal(5, codeRepository.UpdatedCode?.AttemptCount);
    }

    private static OpsAuthService CreateService(
        FakeOpsUserRepository userRepository,
        FakeOpsLoginCodeRepository codeRepository,
        FakeEmailSender emailSender,
        DateTimeOffset? now = null)
    {
        return new OpsAuthService(
            userRepository,
            codeRepository,
            emailSender,
            Options.Create(new AppOptions
            {
                OpsLoginCodeTtlMinutes = 10,
                OpsLoginMaxVerifyAttempts = 5
            }),
            new FixedTimeProvider(now ?? new DateTimeOffset(2026, 03, 28, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<OpsAuthService>.Instance);
    }

    private sealed class FakeOpsUserRepository : IOpsUserRepository
    {
        private readonly List<OpsUser> _users;

        public FakeOpsUserRepository(params OpsUser[] users)
        {
            _users = users.ToList();
        }

        public OpsUser? UpdatedUser { get; private set; }

        public Task<OpsUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)));

        public Task<OpsUser?> FindByIdAsync(long id, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

        public Task AddAsync(OpsUser user, CancellationToken cancellationToken)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OpsUser user, CancellationToken cancellationToken)
        {
            UpdatedUser = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOpsLoginCodeRepository : IOpsLoginCodeRepository
    {
        public bool InvalidateCalled { get; private set; }
        public OpsLoginCode? LatestCode { get; set; }
        public OpsLoginCode? UpdatedCode { get; private set; }
        public List<OpsLoginCode> StoredCodes { get; } = [];

        public Task InvalidateActiveCodesAsync(long opsUserId, CancellationToken cancellationToken)
        {
            InvalidateCalled = true;
            return Task.CompletedTask;
        }

        public Task AddAsync(OpsLoginCode code, CancellationToken cancellationToken)
        {
            StoredCodes.Add(code);
            LatestCode = code;
            return Task.CompletedTask;
        }

        public Task<OpsLoginCode?> GetLatestActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(LatestCode);

        public Task UpdateAsync(OpsLoginCode code, CancellationToken cancellationToken)
        {
            UpdatedCode = code;
            LatestCode = code;
            return Task.CompletedTask;
        }

        public Task<int> CountRecentRequestsByEmailAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<int> CountRecentRequestsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<int> CountRecentVerificationAttemptsByIpAsync(string ipAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
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
