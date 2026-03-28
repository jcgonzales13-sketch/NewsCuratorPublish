using System.Security.Cryptography;
using System.Text;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Application.Services;

public sealed class OpsAuthService : IOpsAuthService
{
    private const string GenericRequestCodeMessage = "If the email is authorized, a login code has been sent.";
    private readonly IOpsUserRepository _opsUserRepository;
    private readonly IOpsLoginCodeRepository _opsLoginCodeRepository;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OpsAuthService> _logger;

    public OpsAuthService(
        IOpsUserRepository opsUserRepository,
        IOpsLoginCodeRepository opsLoginCodeRepository,
        IEmailSender emailSender,
        IOptions<AppOptions> options,
        TimeProvider timeProvider,
        ILogger<OpsAuthService> logger)
    {
        _opsUserRepository = opsUserRepository;
        _opsLoginCodeRepository = opsLoginCodeRepository;
        _emailSender = emailSender;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<OpsRequestCodeResult> RequestCodeAsync(string email, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var normalizedEmail = OpsAuthEmailNormalizer.Normalize(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new OpsRequestCodeResult { Accepted = true, Message = GenericRequestCodeMessage };
        }

        var user = await _opsUserRepository.FindByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Ops login code request ignored for unknown or inactive email {Email}.", normalizedEmail);
            return new OpsRequestCodeResult { Accepted = true, Message = GenericRequestCodeMessage };
        }

        var now = _timeProvider.GetUtcNow();
        if (await IsRequestRateLimitedAsync(normalizedEmail, ipAddress, now, cancellationToken))
        {
            _logger.LogWarning("Ops login code request rate limit triggered for email {Email} from IP {IpAddress}.", normalizedEmail, ipAddress);
            return new OpsRequestCodeResult
            {
                Accepted = false,
                IsRateLimited = true,
                Message = "Too many attempts. Please wait and try again."
            };
        }

        var rawCode = OpsLoginCodeGenerator.GenerateSixDigitCode();
        var loginCode = new OpsLoginCode
        {
            OpsUserId = user.Id,
            Email = normalizedEmail,
            CodeHash = OpsAuthCodeHasher.Hash(rawCode),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_options.OpsLoginCodeTtlMinutes),
            RequestIp = Truncate(ipAddress, 128),
            RequestUserAgent = Truncate(userAgent, 512),
            AttemptCount = 0
        };

        await _opsLoginCodeRepository.InvalidateActiveCodesAsync(user.Id, cancellationToken);
        await _opsLoginCodeRepository.AddAsync(loginCode, cancellationToken);

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    ToEmail = user.Email,
                    ToName = user.DisplayName,
                    Subject = "Your AI News Curator login code",
                    TextBody = $"""
Your one-time login code is: {rawCode}

This code expires in {_options.OpsLoginCodeTtlMinutes} minutes.
If you did not request this code, you can ignore this email.
""",
                    HtmlBody = $"""
<p>Your one-time login code is:</p>
<h2>{rawCode}</h2>
<p>This code expires in {_options.OpsLoginCodeTtlMinutes} minutes.</p>
<p>If you did not request this code, you can ignore this email.</p>
"""
                },
                cancellationToken);

            _logger.LogInformation("Ops login code sent for email {Email} to ops user {OpsUserId}.", normalizedEmail, user.Id);
            return new OpsRequestCodeResult { Accepted = true, Message = GenericRequestCodeMessage };
        }
        catch (Exception ex)
        {
            loginCode.InvalidatedAtUtc = now;
            await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);
            _logger.LogError(ex, "Ops login email send failed for email {Email}.", normalizedEmail);
            return new OpsRequestCodeResult
            {
                Accepted = false,
                IsSystemError = true,
                Message = "Unable to send email right now."
            };
        }
    }

    public async Task<OpsAuthResult> VerifyCodeAsync(string email, string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var normalizedEmail = OpsAuthEmailNormalizer.Normalize(email);
        var trimmedCode = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || trimmedCode.Length != 6 || !trimmedCode.All(char.IsDigit))
        {
            return new OpsAuthResult { ErrorMessage = "Invalid or expired code." };
        }

        var now = _timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            var failedAttemptsByIp = await _opsLoginCodeRepository.CountRecentVerificationAttemptsByIpAsync(ipAddress, now.AddHours(-1), cancellationToken);
            if (failedAttemptsByIp >= 10)
            {
                _logger.LogWarning("Ops login verify rate limit triggered for IP {IpAddress}.", ipAddress);
                return new OpsAuthResult
                {
                    IsRateLimited = true,
                    ErrorMessage = "Too many attempts. Please wait and try again."
                };
            }
        }

        var loginCode = await _opsLoginCodeRepository.GetLatestActiveByEmailAsync(normalizedEmail, cancellationToken);
        if (loginCode is null || loginCode.ExpiresAtUtc <= now)
        {
            if (loginCode is not null && loginCode.ExpiresAtUtc <= now)
            {
                loginCode.InvalidatedAtUtc = now;
                await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);
            }

            _logger.LogInformation("Ops login code rejected for email {Email}: no active code.", normalizedEmail);
            return new OpsAuthResult { ErrorMessage = "Invalid or expired code." };
        }

        if (loginCode.AttemptCount >= _options.OpsLoginMaxVerifyAttempts)
        {
            loginCode.InvalidatedAtUtc = now;
            await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);
            _logger.LogWarning("Ops login code verify rate limit triggered for email {Email}.", normalizedEmail);
            return new OpsAuthResult
            {
                IsRateLimited = true,
                ErrorMessage = "Too many attempts. Please wait and try again."
            };
        }

        loginCode.AttemptCount += 1;
        var providedHash = OpsAuthCodeHasher.Hash(trimmedCode);
        var hashMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedHash),
            Encoding.UTF8.GetBytes(loginCode.CodeHash));

        if (!hashMatches)
        {
            if (loginCode.AttemptCount >= _options.OpsLoginMaxVerifyAttempts)
            {
                loginCode.InvalidatedAtUtc = now;
            }

            await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);
            _logger.LogInformation("Ops login code rejected for email {Email}: invalid code.", normalizedEmail);

            return new OpsAuthResult
            {
                IsRateLimited = loginCode.AttemptCount >= _options.OpsLoginMaxVerifyAttempts,
                ErrorMessage = loginCode.AttemptCount >= _options.OpsLoginMaxVerifyAttempts
                    ? "Too many attempts. Please wait and try again."
                    : "Invalid or expired code."
            };
        }

        var user = await _opsUserRepository.FindByIdAsync(loginCode.OpsUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            loginCode.InvalidatedAtUtc = now;
            await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);
            _logger.LogInformation("Ops login code rejected for email {Email}: user unavailable.", normalizedEmail);
            return new OpsAuthResult { ErrorMessage = "Invalid or expired code." };
        }

        loginCode.UsedAtUtc = now;
        loginCode.ConsumeIp = Truncate(ipAddress, 128);
        loginCode.ConsumeUserAgent = Truncate(userAgent, 512);
        await _opsLoginCodeRepository.UpdateAsync(loginCode, cancellationToken);

        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        await _opsUserRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Ops login code verified for email {Email} and ops user {OpsUserId}.", normalizedEmail, user.Id);
        return new OpsAuthResult { Success = true, User = user };
    }

    private async Task<bool> IsRequestRateLimitedAsync(string normalizedEmail, string? ipAddress, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var emailRequestsLastMinute = await _opsLoginCodeRepository.CountRecentRequestsByEmailAsync(normalizedEmail, now.AddMinutes(-1), cancellationToken);
        if (emailRequestsLastMinute >= 1)
        {
            return true;
        }

        var emailRequestsLastHour = await _opsLoginCodeRepository.CountRecentRequestsByEmailAsync(normalizedEmail, now.AddHours(-1), cancellationToken);
        if (emailRequestsLastHour >= 5)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            var ipRequestsLastHour = await _opsLoginCodeRepository.CountRecentRequestsByIpAsync(ipAddress, now.AddHours(-1), cancellationToken);
            if (ipRequestsLastHour >= 20)
            {
                return true;
            }
        }

        return false;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
