namespace AiNewsCurator.Application.DTOs;

public sealed class DailyRunResult
{
    public long RunId { get; init; }
    public int ItemsCollected { get; init; }
    public int ItemsDeduplicated { get; init; }
    public int ItemsCurated { get; init; }
    public int ItemsApproved { get; init; }
    public int ItemsPublished { get; init; }
    public int ErrorCount { get; init; }
    public string Status { get; init; } = string.Empty;
}
