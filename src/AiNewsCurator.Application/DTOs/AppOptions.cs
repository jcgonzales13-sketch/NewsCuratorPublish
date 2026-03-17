namespace AiNewsCurator.Application.DTOs;

public sealed class AppOptions
{
    public string DatabasePath { get; set; } = "data/ainews.db";
    public string PublishMode { get; set; } = "Manual";
    public int RunHourLocal { get; set; } = 8;
    public int RunMinuteLocal { get; set; } = 0;
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public string InternalApiKey { get; set; } = "changeme";
    public string? AiProvider { get; set; }
    public string? AiApiKey { get; set; }
    public string AiModelName { get; set; } = "gpt-4o-mini";
    public string? AiBaseUrl { get; set; }
    public string? LinkedInClientId { get; set; }
    public string? LinkedInClientSecret { get; set; }
    public string? LinkedInRedirectUri { get; set; }
    public string? LinkedInAccessToken { get; set; }
    public string? LinkedInMemberUrn { get; set; }
    public int MaxNewsPerRun { get; set; } = 50;
    public int MaxCandidatesForAi { get; set; } = 10;
    public double RelevanceThreshold { get; set; } = 0.75;
    public double ConfidenceThreshold { get; set; } = 0.80;
    public int DuplicateLookbackDays { get; set; } = 14;
    public int PostLookbackDays { get; set; } = 15;
    public int NewsWindowHours { get; set; } = 48;
}
