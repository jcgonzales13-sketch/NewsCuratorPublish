using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface ILinkedInPublisher
{
    Task<LinkedInPublishResult> PublishTextPostAsync(PostDraft draft, CancellationToken cancellationToken);
    Task<OperationResult> ValidateCredentialsAsync(CancellationToken cancellationToken);
    Task<OperationResult> RefreshAccessAsync(CancellationToken cancellationToken);
}
