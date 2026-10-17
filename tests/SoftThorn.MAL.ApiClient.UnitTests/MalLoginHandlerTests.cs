using Microsoft.Extensions.Logging.Abstractions;
using SoftThorn.MAL.ApiClient.Authentication;
using System.Net;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalLoginHandlerTests
    {
        private readonly AuthCache _authCache = new();
        private readonly RecordingEndpoint _endpoint = new();
        private readonly HttpMessageInvoker _sut;

        public MalLoginHandlerTests()
        {
            var clientConfiguration = new ClientConfiguration() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" };
            var tokenClient = new TokenClient(new NoHttpClientFactory(), clientConfiguration, _authCache, TimeProvider.System, NullLogger<TokenClient>.Instance);

            _sut = new HttpMessageInvoker(new MalLoginHandler(tokenClient, clientConfiguration) { InnerHandler = _endpoint });
        }

        [Fact]
        public async Task SendAsync_Should_Send_Client_Id_Without_Signed_In_User()
        {
            // MAL accepts the Client ID instead of a token on its public endpoints
            await _sut.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.myanimelist.net/v2/anime/1"), TestContext.Current.CancellationToken);

            var request = Assert.Single(_endpoint.Requests);
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(["client-id"], request.Headers.GetValues(MalLoginHandler.ClientIdHeaderName));
        }

        [Fact]
        public async Task SendAsync_Should_Send_Only_The_Token_With_Signed_In_User()
        {
            await _authCache.SaveAsync(new AuthResponse
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                ExpiresIn = 3600,
                TokenType = "Bearer",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            }, TestContext.Current.CancellationToken);

            await _sut.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.myanimelist.net/v2/anime/1"), TestContext.Current.CancellationToken);

            var request = Assert.Single(_endpoint.Requests);
            Assert.Equal("Bearer access", request.Headers.Authorization?.ToString());
            Assert.False(request.Headers.Contains(MalLoginHandler.ClientIdHeaderName));
        }

        // the token is valid or missing in these tests, so nothing is refreshed
        private sealed class NoHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => throw new InvalidOperationException("The token endpoint shouldn't be called");
        }

        private sealed class RecordingEndpoint : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
