namespace AiNewsCurator.Application.Services;

public static class OpsAuthEmailNormalizer
{
    public static string Normalize(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
