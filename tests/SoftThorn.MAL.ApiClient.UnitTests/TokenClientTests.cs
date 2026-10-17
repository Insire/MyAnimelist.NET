using Microsoft.Extensions.Caching.Memory;
using SoftThorn.MAL.ApiClient.Authentication;
using System.Net;
using System.Text;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class TokenClientTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

        private readonly StubTokenEndpoint _endpoint = new();
        private readonly AuthCache _authCache = new(new MemoryCache(new MemoryCacheOptions()));
        private readonly TokenClient _sut;

        public TokenClientTests()
        {
            var clientConfiguration = new ClientConfiguration() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" };

            _sut = new TokenClient(new StubHttpClientFactory(_endpoint), clientConfiguration, _authCache, new FixedTimeProvider(Now));
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Return_Null_When_Nothing_Is_Cached()
        {
            var token = await _sut.GetValidTokenAsync(TestContext.Current.CancellationToken);

            Assert.Null(token);
            Assert.Empty(_endpoint.Requests);
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Return_Cached_Token_When_Not_Expired()
        {
            await _authCache.SaveAsync(CreateToken("access", "refresh", Now.AddHours(1)), TestContext.Current.CancellationToken);

            var token = await _sut.GetValidTokenAsync(TestContext.Current.CancellationToken);

            Assert.Equal("access", token?.AccessToken);
            Assert.Empty(_endpoint.Requests);
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Use_Token_Without_Expiry_As_Is()
        {
            await _authCache.SaveAsync(CreateToken("access", "refresh", expiresAt: null), TestContext.Current.CancellationToken);

            var token = await _sut.GetValidTokenAsync(TestContext.Current.CancellationToken);

            Assert.Equal("access", token?.AccessToken);
            Assert.Empty(_endpoint.Requests);
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Refresh_Expired_Token_And_Cache_It()
        {
            await _authCache.SaveAsync(CreateToken("old-access", "old-refresh", Now.AddSeconds(30)), TestContext.Current.CancellationToken);

            var token = await _sut.GetValidTokenAsync(TestContext.Current.CancellationToken);

            Assert.Equal("new-access", token?.AccessToken);
            Assert.Equal(Now.AddSeconds(3600), token?.ExpiresAt);
            Assert.Equal("new-access", (await _authCache.Get(TestContext.Current.CancellationToken))?.AccessToken);

            var request = Assert.Single(_endpoint.Requests);
            Assert.Contains("grant_type=refresh_token", request);
            Assert.Contains("refresh_token=old-refresh", request);
            Assert.Contains("client_id=client-id", request);
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Refresh_Only_Once_For_Concurrent_Callers()
        {
            await _authCache.SaveAsync(CreateToken("old-access", "old-refresh", Now.AddMinutes(-5)), TestContext.Current.CancellationToken);
            _endpoint.Delay = TimeSpan.FromMilliseconds(100);

            var tokens = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _sut.GetValidTokenAsync(TestContext.Current.CancellationToken)));

            Assert.All(tokens, token => Assert.Equal("new-access", token?.AccessToken));
            Assert.Single(_endpoint.Requests);
        }

        [Fact]
        public async Task GetValidTokenAsync_Should_Return_Null_When_Refresh_Fails()
        {
            await _authCache.SaveAsync(CreateToken("old-access", "old-refresh", Now.AddMinutes(-5)), TestContext.Current.CancellationToken);
            _endpoint.StatusCode = HttpStatusCode.BadRequest;

            var token = await _sut.GetValidTokenAsync(TestContext.Current.CancellationToken);

            Assert.Null(token);
        }

        [Fact]
        public async Task ExchangeCodeAsync_Should_Cache_Token_With_Expiry()
        {
            var success = await _sut.ExchangeCodeAsync("code", "verifier", "http://localhost:5000/", TestContext.Current.CancellationToken);

            Assert.True(success);

            var token = await _authCache.Get(TestContext.Current.CancellationToken);
            Assert.Equal("new-access", token?.AccessToken);
            Assert.Equal(Now.AddSeconds(3600), token?.ExpiresAt);

            var request = Assert.Single(_endpoint.Requests);
            Assert.Contains("grant_type=authorization_code", request);
            Assert.Contains("code_verifier=verifier", request);
        }

        private static AuthResponse CreateToken(string accessToken, string refreshToken, DateTimeOffset? expiresAt)
            => new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
            };

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }

        private sealed class StubTokenEndpoint : HttpMessageHandler
        {
            private const string TokenJson = """{"token_type":"Bearer","expires_in":3600,"access_token":"new-access","refresh_token":"new-refresh"}""";

            public List<string> Requests { get; } = [];
            public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
            public TimeSpan Delay { get; set; } = TimeSpan.Zero;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);

                lock (Requests)
                {
                    Requests.Add(body);
                }

                await Task.Delay(Delay, cancellationToken);

                return new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent(TokenJson, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
