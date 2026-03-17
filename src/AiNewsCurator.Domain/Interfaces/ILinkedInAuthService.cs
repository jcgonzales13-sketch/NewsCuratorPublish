using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ILinkedInAuthService
{
    Task<LinkedInAuthState> GetStatusAsync(CancellationToken cancellationToken);
    Task<string> CreateAuthorizationUrlAsync(CancellationToken cancellationToken);
    Task<LinkedInAuthCallbackResult> HandleCallbackAsync(string code, string state, string? error, string? errorDescription, CancellationToken cancellationToken);
}
