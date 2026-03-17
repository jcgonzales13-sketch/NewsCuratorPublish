using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Domain.Entities;

public sealed class ExecutionRun
{
    public long Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Started;
    public TriggerType TriggerType { get; set; }
    public int ItemsCollected { get; set; }
    public int ItemsDeduplicated { get; set; }
    public int ItemsCurated { get; set; }
    public int ItemsApproved { get; set; }
    public int ItemsPublished { get; set; }
    public int ErrorCount { get; set; }
    public string LogSummary { get; set; } = string.Empty;
}
