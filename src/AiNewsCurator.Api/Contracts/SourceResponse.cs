using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Api.Contracts;

public sealed class SourceResponse
{
    public required Source Source { get; init; }
    public required string EditorialProfile { get; init; }
    public required string EditorialProfileLabel { get; init; }
}
