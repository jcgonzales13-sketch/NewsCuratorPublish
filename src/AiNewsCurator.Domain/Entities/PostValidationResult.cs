namespace AiNewsCurator.Domain.Entities;

public sealed class PostValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
}
