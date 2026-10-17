using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalRetryHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        private readonly StubEndpoint _endpoint = new();
        private readonly List<TimeSpan> _delays = [];
        private readonly HttpClient _sut;

        public MalRetryHandlerTests()
        {
            var handler = new MalRetryHandler(new FixedTimeProvider(Now))
            {
                InnerHandler = _endpoint,
                Delay = (delay, _) =>
                {
                    _delays.Add(delay);
                    return Task.CompletedTask;
                },
            };

            _sut = new HttpClient(handler);
        }

        [Fact]
        public async Task SendAsync_Should_Not_Retry_Successful_Responses()
        {
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, _endpoint.Calls);
            Assert.Empty(_delays);
        }

        [Fact]
        public async Task SendAsync_Should_Not_Retry_Client_Errors()
        {
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.NotFound));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(1, _endpoint.Calls);
        }

        [Fact]
        public async Task SendAsync_Should_Back_Off_Exponentially_Without_Retry_After()
        {
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, _endpoint.Calls);
            Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], _delays);
        }

        [Fact]
        public async Task SendAsync_Should_Honor_Retry_After_Delta_And_Date()
        {
            _endpoint.Responses.Enqueue(() => TooManyRequests(new RetryConditionHeaderValue(TimeSpan.FromSeconds(5))));
            _endpoint.Responses.Enqueue(() => TooManyRequests(new RetryConditionHeaderValue(Now.AddSeconds(10))));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], _delays);
        }

        [Fact]
        public async Task SendAsync_Should_Return_Response_When_Retry_After_Is_Too_Long()
        {
            _endpoint.Responses.Enqueue(() => TooManyRequests(new RetryConditionHeaderValue(TimeSpan.FromMinutes(5))));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(1, _endpoint.Calls);
            Assert.Empty(_delays);
        }

        [Fact]
        public async Task SendAsync_Should_Give_Up_After_Max_Retries()
        {
            for (var i = 0; i <= MalRetryHandler.MaxRetries + 1; i++)
            {
                _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(MalRetryHandler.MaxRetries + 1, _endpoint.Calls);
        }

        [Fact]
        public async Task SendAsync_Should_Resend_Request_Content()
        {
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            using var content = new FormUrlEncodedContent([new("status", "watching")]);
            await _sut.PatchAsync("https://api.myanimelist.net/v2/anime/1/my_list_status", content, TestContext.Current.CancellationToken);

            Assert.Equal(["status=watching", "status=watching"], _endpoint.Bodies);
        }

        [Fact]
        public void AddMalApiClient_Should_Send_Library_User_Agent()
        {
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            foreach (var name in new[] { typeof(MalApiService).Name, Authentication.TokenClient.HttpClientName })
            {
                var userAgent = httpClientFactory.CreateClient(name).DefaultRequestHeaders.UserAgent.ToString();

                Assert.StartsWith("SoftThorn.MAL.ApiClient/", userAgent);
                Assert.EndsWith("(+https://github.com/Insire/SoftThorn.MAL.ApiClient)", userAgent);
            }
        }

        [Theory]
        [InlineData(typeof(MalRetryHandler))]
        [InlineData(typeof(Authentication.MalLoginHandler))]
        public void AddMalApiClient_Should_Register_Message_Handlers_As_Transient(Type handlerType)
        {
            // a reused DelegatingHandler throws once IHttpClientFactory rebuilds the handler chain after its lifetime
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            var descriptor = Assert.Single(services, service => service.ServiceType == handlerType);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        private static HttpResponseMessage TooManyRequests(RetryConditionHeaderValue retryAfter)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = retryAfter;
            return response;
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class StubEndpoint : HttpMessageHandler
        {
            public Queue<Func<HttpResponseMessage>> Responses { get; } = new();

            public List<string> Bodies { get; } = [];

            public int Calls { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;

                if (request.Content is not null)
                {
                    Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                }

                return Responses.Dequeue()();
            }
        }
    }
}
