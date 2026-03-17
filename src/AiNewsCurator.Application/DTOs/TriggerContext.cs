using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.Application.DTOs;

public sealed class TriggerContext
{
    public TriggerType TriggerType { get; init; }
    public string InitiatedBy { get; init; } = "system";
}
