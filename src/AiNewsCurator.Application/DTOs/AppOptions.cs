namespace AiNewsCurator.Application.DTOs;

public sealed class AppOptions
{
    public string DatabasePath { get; set; } = "data/ainews.db";
    public string PublishMode { get; set; } = "Manual";
    public string OpsAuthMode { get; set; } = "EmailCode";
    public string OpsSessionCookieName { get; set; } = "AiNewsCurator.Ops.Auth";
    public int OpsLoginCodeTtlMinutes { get; set; } = 10;
    public int OpsLoginMaxVerifyAttempts { get; set; } = 5;
    public string? OpsBootstrapEmail { get; set; }
    public string? OpsBootstrapName { get; set; }
    public bool EnableScheduler { get; set; }
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
    public string LinkedInTone { get; set; } = "Editorial";
    public string AttributionFooterLine { get; set; } = "Curated by AI News Curator.";
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpSenderName { get; set; } = "AI News Curator";
    public string? SmtpSenderEmail { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpUseStartTls { get; set; } = true;
}
