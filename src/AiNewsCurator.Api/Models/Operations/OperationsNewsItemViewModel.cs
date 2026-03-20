using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Models.Operations;

public sealed class OperationsNewsItemViewModel
{
    public required NewsItem NewsItem { get; init; }
    public CurationResult? LatestCuration { get; init; }
}
