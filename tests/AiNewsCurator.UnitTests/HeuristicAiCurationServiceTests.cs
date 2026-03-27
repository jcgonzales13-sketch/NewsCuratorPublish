using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Integrations.AI;

namespace AiNewsCurator.UnitTests;

public sealed class HeuristicAiCurationServiceTests
{
    [Fact]
    public async Task EvaluateNewsAsync_Should_Use_DotNet_Profile_For_DotNet_Source()
    {
        var sourceRepository = new StubSourceRepository(new Source
        {
            Id = 7,
            Name = ".NET Blog",
            Type = SourceType.Rss,
            Url = "https://devblogs.microsoft.com/dotnet/feed/",
            Language = "en",
            IsActive = true,
            Priority = 9,
            MaxItemsPerRun = 10,
            IncludeKeywordsJson = "[\".net\",\"dotnet\",\"c#\"]",
            ExcludeKeywordsJson = "[]",
            TagsJson = "[\"dotnet\",\"csharp\",\"official\"]"
        });

        var service = new HeuristicAiCurationService(sourceRepository);
        var newsItem = new NewsItem
        {
            SourceId = 7,
            Title = "ASP.NET Core improves hot reload and build diagnostics",
            Url = "https://example.com/news/aspnet-hot-reload",
            CanonicalUrl = "https://example.com/news/aspnet-hot-reload",
            PublishedAt = DateTimeOffset.UtcNow,
            Language = "en",
            RawSummary = "The release improves hot reload and diagnostics for ASP.NET Core developers.",
            RawContent = "The release improves hot reload and diagnostics for ASP.NET Core developers."
        };

        var result = await service.EvaluateNewsAsync(newsItem, CancellationToken.None);

        Assert.True(result.IsRelevant);
        Assert.Equal("Developer Tools", result.Category);
        Assert.Contains("software teams", result.WhyRelevant, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("developer workflows", result.LinkedInDraft, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubSourceRepository : ISourceRepository
    {
        private readonly Source _source;

        public StubSourceRepository(Source source)
        {
            _source = source;
        }

        public Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([_source]);
        public Task<IReadOnlyList<Source>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Source>>([_source]);
        public Task<Source?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<Source?>(id == _source.Id ? _source : null);
        public Task<long> InsertAsync(Source source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Source source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SeedDefaultsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
