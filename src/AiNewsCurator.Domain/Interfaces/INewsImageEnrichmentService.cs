namespace AiNewsCurator.Domain.Interfaces;

public interface INewsImageEnrichmentService
{
    Task<int> BackfillMissingImagesAsync(CancellationToken cancellationToken);
}
