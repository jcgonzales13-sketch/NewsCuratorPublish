namespace AiNewsCurator.Domain.Entities;

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static OperationResult Ok() => new() { Success = true };

    public static OperationResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
