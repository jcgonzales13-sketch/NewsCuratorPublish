using Microsoft.Extensions.Configuration;

namespace AiNewsCurator.Application.Configuration;

public static class AppOptionEnvironmentMapper
{
    public static IConfigurationBuilder AddMappedEnvironmentVariables(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddEnvironmentVariables();

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DatabasePath"] = Environment.GetEnvironmentVariable("DATABASE_PATH"),
            ["PublishMode"] = Environment.GetEnvironmentVariable("PUBLISH_MODE"),
            ["OpsAuthMode"] = Environment.GetEnvironmentVariable("OPS_AUTH_MODE"),
            ["OpsSessionCookieName"] = Environment.GetEnvironmentVariable("OPS_SESSION_COOKIE_NAME"),
            ["OpsLoginCodeTtlMinutes"] = Environment.GetEnvironmentVariable("OPS_LOGIN_CODE_TTL_MINUTES"),
            ["OpsLoginMaxVerifyAttempts"] = Environment.GetEnvironmentVariable("OPS_LOGIN_MAX_VERIFY_ATTEMPTS"),
            ["OpsBootstrapEmail"] = Environment.GetEnvironmentVariable("OPS_BOOTSTRAP_EMAIL"),
            ["OpsBootstrapName"] = Environment.GetEnvironmentVariable("OPS_BOOTSTRAP_NAME"),
            ["EnableScheduler"] = Environment.GetEnvironmentVariable("ENABLE_SCHEDULER"),
            ["RunHourLocal"] = Environment.GetEnvironmentVariable("RUN_HOUR_LOCAL"),
            ["RunMinuteLocal"] = Environment.GetEnvironmentVariable("RUN_MINUTE_LOCAL"),
            ["Timezone"] = Environment.GetEnvironmentVariable("TIMEZONE"),
            ["InternalApiKey"] = Environment.GetEnvironmentVariable("INTERNAL_API_KEY"),
            ["LinkedInClientId"] = Environment.GetEnvironmentVariable("LINKEDIN_CLIENT_ID"),
            ["LinkedInClientSecret"] = Environment.GetEnvironmentVariable("LINKEDIN_CLIENT_SECRET"),
            ["LinkedInRedirectUri"] = Environment.GetEnvironmentVariable("LINKEDIN_REDIRECT_URI"),
            ["LinkedInAccessToken"] = Environment.GetEnvironmentVariable("LINKEDIN_ACCESS_TOKEN"),
            ["LinkedInMemberUrn"] = Environment.GetEnvironmentVariable("LINKEDIN_MEMBER_URN"),
            ["AiProvider"] = Environment.GetEnvironmentVariable("AI_PROVIDER"),
            ["AiApiKey"] = Environment.GetEnvironmentVariable("AI_API_KEY"),
            ["AiModelName"] = Environment.GetEnvironmentVariable("AI_MODEL_NAME"),
            ["AiBaseUrl"] = Environment.GetEnvironmentVariable("AI_BASE_URL"),
            ["MaxNewsPerRun"] = Environment.GetEnvironmentVariable("MAX_NEWS_PER_RUN"),
            ["MaxCandidatesForAi"] = Environment.GetEnvironmentVariable("MAX_CANDIDATES_FOR_AI"),
            ["RelevanceThreshold"] = Environment.GetEnvironmentVariable("RELEVANCE_THRESHOLD"),
            ["ConfidenceThreshold"] = Environment.GetEnvironmentVariable("CONFIDENCE_THRESHOLD"),
            ["DuplicateLookbackDays"] = Environment.GetEnvironmentVariable("DUPLICATE_LOOKBACK_DAYS"),
            ["PostLookbackDays"] = Environment.GetEnvironmentVariable("POST_LOOKBACK_DAYS"),
            ["NewsWindowHours"] = Environment.GetEnvironmentVariable("NEWS_WINDOW_HOURS"),
            ["LinkedInTone"] = Environment.GetEnvironmentVariable("LINKEDIN_TONE"),
            ["AttributionFooterLine"] = Environment.GetEnvironmentVariable("ATTRIBUTION_FOOTER_LINE"),
            ["SmtpHost"] = Environment.GetEnvironmentVariable("SMTP_HOST"),
            ["SmtpPort"] = Environment.GetEnvironmentVariable("SMTP_PORT"),
            ["SmtpSenderName"] = Environment.GetEnvironmentVariable("SMTP_SENDER_NAME"),
            ["SmtpSenderEmail"] = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL"),
            ["SmtpUsername"] = Environment.GetEnvironmentVariable("SMTP_USERNAME"),
            ["SmtpPassword"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
            ["SmtpUseStartTls"] = Environment.GetEnvironmentVariable("SMTP_USE_STARTTLS")
        };

        configurationBuilder.AddInMemoryCollection(values.Where(item => !string.IsNullOrWhiteSpace(item.Value))!);
        return configurationBuilder;
    }
}
