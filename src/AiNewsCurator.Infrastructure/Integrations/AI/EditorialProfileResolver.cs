using System.Text.Json;
using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Infrastructure.Integrations.AI;

internal static class EditorialProfileResolver
{
    public static EditorialProfile Resolve(Source? source, NewsItem newsItem)
    {
        var sourceFingerprint = string.Join(
            " ",
            source?.Name ?? string.Empty,
            source?.Url ?? string.Empty,
            source?.TagsJson ?? string.Empty,
            source?.IncludeKeywordsJson ?? string.Empty).ToLowerInvariant();

        var newsFingerprint = string.Join(
            " ",
            newsItem.Title,
            newsItem.RawSummary ?? string.Empty,
            newsItem.RawContent ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(sourceFingerprint, ".net", "dotnet", "csharp", "c#", "asp.net", "blazor", "visual studio", "rider", "resharper", "roslyn") ||
            ContainsAny(newsFingerprint, ".net", "dotnet", "c#", "asp.net", "blazor", "entity framework", "ef core", "nuget", "roslyn", "visual studio", "rider", "resharper"))
        {
            return new EditorialProfile
            {
                Name = "dotnet",
                Audience = ".NET and C# software teams",
                SourceLabel = source?.Name ?? ".NET source",
                PromptInstruction =
                    "This item comes from a .NET / C# source. Evaluate it with a developer-news lens. " +
                    "Treat runtime, SDK, ASP.NET Core, Blazor, Entity Framework, language, compiler, tooling, IDE, package, and workflow changes as potentially relevant even when they are not directly about AI. " +
                    "Favor concrete developer workflow impact, release engineering implications, migration implications, framework adoption signals, and tooling productivity gains.",
                HeuristicKeywords =
                [
                    ".net", "dotnet", "c#", "asp.net", "asp.net core", "blazor", "entity framework", "ef core",
                    "runtime", "sdk", "roslyn", "nuget", "visual studio", "rider", "resharper", "developer", "compiler"
                ],
                HeuristicWhyRelevant =
                    "This story matters to software teams because changes in .NET tooling, runtime, frameworks, or language behavior can quickly affect delivery speed, migration decisions, and developer workflows.",
                HeuristicHook =
                    "Developer platform shifts like this often matter before they look dramatic in the market.",
                HeuristicWhyItMatters =
                    "This directly affects developer workflows, framework adoption, or release execution for engineering teams. That makes it relevant well beyond the headline feature itself.",
                HeuristicTakeaway =
                    "The bigger signal is that advantage in the .NET ecosystem is increasingly shaped by lower workflow friction, better tooling, and faster delivery loops."
            };
        }

        return new EditorialProfile
        {
            Name = "ai",
            Audience = "technology and business professionals tracking AI",
            SourceLabel = source?.Name ?? "Original reporting",
            PromptInstruction =
                "Evaluate this as AI-focused editorial coverage. Favor operational AI impact, workflow execution, product shifts, market signal, and practical consequences for teams adopting AI.",
            HeuristicKeywords =
            [
                "ai", "artificial intelligence", "inteligencia artificial", "llm", "model",
                "agent", "openai", "anthropic", "google", "microsoft", "nvidia", "regulation"
            ],
            HeuristicWhyRelevant =
                "This story is relevant to technology and business professionals because it signals a meaningful AI development with practical implications.",
            HeuristicHook =
                "The bigger story here is how quickly AI products are moving toward real execution inside everyday workflows.",
            HeuristicWhyItMatters =
                "This moves AI closer to workflow execution instead of just assistance. That matters because teams can judge task completion and operational automation in real environments.",
            HeuristicTakeaway =
                "The real shift is that AI is becoming an execution layer inside workflows, not just a conversational layer on top of them."
        };
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class EditorialProfile
{
    public string Name { get; init; } = "ai";
    public string Audience { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string PromptInstruction { get; init; } = string.Empty;
    public IReadOnlyList<string> HeuristicKeywords { get; init; } = [];
    public string HeuristicWhyRelevant { get; init; } = string.Empty;
    public string HeuristicHook { get; init; } = string.Empty;
    public string HeuristicWhyItMatters { get; init; } = string.Empty;
    public string HeuristicTakeaway { get; init; } = string.Empty;
}
