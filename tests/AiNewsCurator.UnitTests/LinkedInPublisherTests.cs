using System.Net;
using System.Text;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Integrations.LinkedIn;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.UnitTests;

public sealed class LinkedInPublisherTests
{
    [Fact]
    public async Task RefreshAccessAsync_Should_Update_Stored_Access_Token()
    {
        var settings = new InMemorySettingsRepository();
        await settings.UpsertAsync("linkedin.refresh_token", "refresh-token-1", CancellationToken.None);
        await settings.UpsertAsync("linkedin.member_urn", "urn:li:person:member-1", CancellationToken.None);

        var publisher = CreatePublisher(
            settings,
            request =>
            {
                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.AbsoluteUri == "https://www.linkedin.com/oauth/v2/accessToken")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"access_token\":\"fresh-access-token\",\"refresh_token\":\"refresh-token-2\"}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var result = await publisher.RefreshAccessAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("fresh-access-token", (await settings.GetAsync("linkedin.access_token", CancellationToken.None))?.Value);
        Assert.Equal("refresh-token-2", (await settings.GetAsync("linkedin.refresh_token", CancellationToken.None))?.Value);
        Assert.NotNull(await settings.GetAsync("linkedin.token_updated_at", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_Should_Refresh_Expired_Access_Token_And_Retry()
    {
        var settings = new InMemorySettingsRepository();
        await settings.UpsertAsync("linkedin.access_token", "expired-access-token", CancellationToken.None);
        await settings.UpsertAsync("linkedin.refresh_token", "refresh-token-1", CancellationToken.None);
        await settings.UpsertAsync("linkedin.member_urn", "urn:li:person:member-1", CancellationToken.None);

        var userInfoAttempts = 0;
        var publisher = CreatePublisher(
            settings,
            request =>
            {
                if (request.Method == HttpMethod.Get &&
                    request.RequestUri?.AbsoluteUri == "https://api.linkedin.com/v2/userinfo")
                {
                    userInfoAttempts++;
                    var bearer = request.Headers.Authorization?.Parameter;
                    if (userInfoAttempts == 1 && bearer == "expired-access-token")
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }

                    if (bearer == "fresh-access-token")
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"sub\":\"member-1\"}", Encoding.UTF8, "application/json")
                        };
                    }
                }

                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.AbsoluteUri == "https://www.linkedin.com/oauth/v2/accessToken")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"access_token\":\"fresh-access-token\",\"refresh_token\":\"refresh-token-2\"}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var result = await publisher.ValidateCredentialsAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, userInfoAttempts);
        Assert.Equal("fresh-access-token", (await settings.GetAsync("linkedin.access_token", CancellationToken.None))?.Value);
    }

    private static LinkedInPublisher CreatePublisher(
        ISettingsRepository settingsRepository,
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var options = Options.Create(new AppOptions
        {
            LinkedInClientId = "client-id",
            LinkedInClientSecret = "client-secret"
        });

        return new LinkedInPublisher(
            new StubHttpClientFactory(handler),
            settingsRepository,
            new StubNewsItemRepository(),
            options,
            NullLogger<LinkedInPublisher>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(new StubHttpMessageHandler(_handler), disposeHandler: true);
            if (name == "linkedin-auth")
            {
                client.BaseAddress = new Uri("https://www.linkedin.com");
            }

            return client;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, Setting> _values = new(StringComparer.OrdinalIgnoreCase);
        private long _idSequence = 1;

        public Task<Setting?> GetAsync(string key, CancellationToken cancellationToken)
        {
            _values.TryGetValue(key, out var setting);
            return Task.FromResult(setting);
        }

        public Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
        {
            if (_values.TryGetValue(key, out var existing))
            {
                existing.Value = value;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _values[key] = new Setting
                {
                    Id = _idSequence++,
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubNewsItemRepository : INewsItemRepository
    {
        public Task<long> InsertAsync(NewsItem newsItem, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NewsItem?> GetByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NewsItem?> GetPublishedByCanonicalUrlAsync(string canonicalUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsRecentSimilarAsync(string titleHash, string contentHash, int lookbackDays, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NewsItem>> GetCandidatesForCurationAsync(int maxItems, DateTimeOffset publishedAfter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateStatusAsync(long newsItemId, Domain.Enums.NewsItemStatus status, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateImageAsync(long id, string imageUrl, string imageOrigin, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NewsItem?> GetByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<NewsItem?>(null);
        public Task<IReadOnlyList<NewsItem>> GetRecentAsync(int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NewsItem>> GetWithoutImageAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> NormalizeStoredContentAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
