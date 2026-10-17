using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalRetryHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        // real timers (FixedTimeProvider only fixes the clock), kept short so the hanging requests time out quickly
        private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(100);

        private readonly StubEndpoint _endpoint = new();
        private readonly List<TimeSpan> _delays = [];
        private readonly HttpClient _sut;
        private double _jitter;

        public MalRetryHandlerTests()
        {
            _sut = CreateClient(jitter: () => _jitter);
        }

        // without a jitter function the handler's own random source is used
        private HttpClient CreateClient(Func<double>? jitter)
        {
            var handler = new MalRetryHandler(new FixedTimeProvider(Now), NullLogger<MalRetryHandler>.Instance)
            {
                InnerHandler = _endpoint,
                Delay = (delay, _) =>
                {
                    _delays.Add(delay);
                    return Task.CompletedTask;
                },
                Jitter = jitter ?? Random.Shared.NextDouble,
                AttemptTimeout = AttemptTimeout,
            };

            return new HttpClient(handler);
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

        [Theory]
        [InlineData(0.0, 500, 1000, 2000)]
        [InlineData(0.5, 750, 1500, 3000)]
        [InlineData(0.999, 999.5, 1999, 3998)]
        public async Task SendAsync_Should_Back_Off_Exponentially_With_Jitter_Without_Retry_After(double jitter, double first, double second, double third)
        {
            _jitter = jitter;
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(4, _endpoint.Calls);
            Assert.Equal([TimeSpan.FromMilliseconds(first), TimeSpan.FromMilliseconds(second), TimeSpan.FromMilliseconds(third)], _delays);
        }

        [Fact]
        public async Task SendAsync_Should_Keep_Random_Backoff_Within_Its_Window()
        {
            using var sut = CreateClient(jitter: null);

            for (var i = 0; i < 50; i++)
            {
                _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
                _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));
                await sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);
            }

            Assert.All(_delays, delay => Assert.InRange(delay, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1)));
            Assert.True(_delays.Distinct().Count() > 1, "the jitter should spread the delays");
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
        public async Task SendAsync_Should_Add_Jitter_On_Top_Of_Retry_After()
        {
            _jitter = 0.5;
            _endpoint.Responses.Enqueue(() => TooManyRequests(new RetryConditionHeaderValue(TimeSpan.FromSeconds(5))));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal([TimeSpan.FromSeconds(5.5)], _delays);
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
        public async Task SendAsync_Should_Retry_Get_That_Timed_Out()
        {
            _endpoint.HangingCalls = 1;
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, _endpoint.Calls);
            Assert.Equal([TimeSpan.FromSeconds(0.5)], _delays);
        }

        [Fact]
        public async Task SendAsync_Should_Throw_Timeout_When_Every_Attempt_Times_Out()
        {
            _endpoint.HangingCalls = int.MaxValue;

            var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken));

            Assert.IsType<TimeoutException>(exception.InnerException);
            Assert.Equal(MalRetryHandler.MaxRetries + 1, _endpoint.Calls);
        }

        [Fact]
        public void Default_Attempt_Timeouts_And_Backoff_Should_Fit_Into_HttpClient_Timeout()
        {
            // otherwise HttpClient.Timeout cancels the whole request before the last attempt can run
            var longestBackoff = Enumerable.Range(0, MalRetryHandler.MaxRetries).Sum(attempt => MalRetryHandler.InitialDelay.TotalSeconds * Math.Pow(2, attempt));
            var worstCase = TimeSpan.FromSeconds(((MalRetryHandler.MaxRetries + 1) * MalRetryHandler.DefaultAttemptTimeout.TotalSeconds) + longestBackoff);

            using var httpClient = new HttpClient();
            Assert.True(worstCase < httpClient.Timeout, $"{worstCase} doesn't fit into {httpClient.Timeout}");
        }

        [Fact]
        public async Task SendAsync_Should_Not_Time_Out_Or_Retry_Patch()
        {
            // a PATCH that timed out may still have reached MAL, so it is left to the caller's cancellation
            _endpoint.HangingCalls = 1;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(AttemptTimeout * 5);

            using var content = new FormUrlEncodedContent([new("score", "9")]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _sut.PatchAsync("https://api.myanimelist.net/v2/anime/1/my_list_status", content, cancellation.Token));

            Assert.Equal(1, _endpoint.Calls);
            Assert.Empty(_delays);
        }

        [Fact]
        public async Task SendAsync_Should_Not_Retry_When_The_Caller_Cancels()
        {
            _endpoint.HangingCalls = 1;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(AttemptTimeout / 4);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", cancellation.Token));

            Assert.Equal(1, _endpoint.Calls);
            Assert.Empty(_delays);
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

        [Theory]
        [InlineData("https://api.myanimelist.net/error.json")]
        [InlineData("/error.json")]
        public async Task SendAsync_Should_Retry_Get_Redirected_To_Error_Page(string location)
        {
            _endpoint.Responses.Enqueue(() => ErrorRedirect(location));
            _endpoint.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(["https://api.myanimelist.net/v2/anime/1", "https://api.myanimelist.net/v2/anime/1"], _endpoint.Uris);
            Assert.Equal([TimeSpan.FromSeconds(0.5)], _delays);
        }

        [Fact]
        public async Task SendAsync_Should_Return_Error_Redirect_After_Max_Retries()
        {
            for (var i = 0; i <= MalRetryHandler.MaxRetries; i++)
            {
                _endpoint.Responses.Enqueue(() => ErrorRedirect("https://api.myanimelist.net/error.json"));
            }

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
            Assert.Equal(MalRetryHandler.MaxRetries + 1, _endpoint.Calls);
        }

        [Fact]
        public async Task SendAsync_Should_Not_Retry_Patch_Redirected_To_Error_Page()
        {
            // like a timeout, the redirect doesn't tell whether MAL applied the PATCH
            _endpoint.Responses.Enqueue(() => ErrorRedirect("https://api.myanimelist.net/error.json"));

            using var content = new FormUrlEncodedContent([new("score", "9")]);
            var response = await _sut.PatchAsync("https://api.myanimelist.net/v2/anime/1/my_list_status", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
            Assert.Equal(1, _endpoint.Calls);
            Assert.Empty(_delays);
        }

        [Fact]
        public async Task SendAsync_Should_Not_Retry_Other_Redirects()
        {
            _endpoint.Responses.Enqueue(() => ErrorRedirect("https://api.myanimelist.net/v2/anime/2"));

            var response = await _sut.GetAsync("https://api.myanimelist.net/v2/anime/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
            Assert.Equal(1, _endpoint.Calls);
        }

        [Fact]
        public void AddMalApiClient_Should_Not_Follow_Redirects()
        {
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();
            var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

            foreach (var name in new[] { MalApiService.HttpClientName, Authentication.TokenClient.HttpClientName })
            {
                var handler = handlerFactory.CreateHandler(name);
                while (handler is DelegatingHandler delegatingHandler)
                {
                    handler = delegatingHandler.InnerHandler!;
                }

                var allowAutoRedirect = handler switch
                {
                    HttpClientHandler httpClientHandler => httpClientHandler.AllowAutoRedirect,
                    SocketsHttpHandler socketsHttpHandler => socketsHttpHandler.AllowAutoRedirect,
                    _ => throw new InvalidOperationException($"Unexpected primary handler {handler.GetType()} for {name}"),
                };

                Assert.False(allowAutoRedirect, name);
            }
        }

        [Fact]
        public void AddMalApiClient_Should_Send_Library_User_Agent()
        {
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            foreach (var name in new[] { MalApiService.HttpClientName, Authentication.TokenClient.HttpClientName })
            {
                var userAgent = httpClientFactory.CreateClient(name).DefaultRequestHeaders.UserAgent.ToString();

                Assert.StartsWith("SoftThorn.MAL.ApiClient/", userAgent);
                Assert.EndsWith("(+https://github.com/Insire/SoftThorn.MAL.ApiClient)", userAgent);
            }
        }

        [Fact]
        public void AddMalApiClient_Should_Keep_The_Applications_User_Agent()
        {
            // the README shows this way for apps to name themselves next to the library
            var services = new ServiceCollection();
            services.ConfigureHttpClientDefaults(builder => builder.ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0")));
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            foreach (var name in new[] { MalApiService.HttpClientName, Authentication.TokenClient.HttpClientName })
            {
                var userAgent = httpClientFactory.CreateClient(name).DefaultRequestHeaders.UserAgent.ToString();

                Assert.StartsWith("MyApp/1.0 SoftThorn.MAL.ApiClient/", userAgent);
            }
        }

        [Fact]
        public void AddMalApiClient_Should_Resolve_IMalApiService()
        {
            // MalApiService is internal, so IMalApiService is registered through a typed client factory
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var service = provider.GetRequiredService<IMalApiService>();

            Assert.NotNull(service.Catalog);
            Assert.NotNull(service.Login);
        }

        [Fact]
        public void IMalApiService_Should_Expose_Only_Public_Interfaces()
        {
            // consumers mock IMalApiService, which only works when everything it returns can be mocked as well
            foreach (var property in typeof(IMalApiService).GetProperties())
            {
                Assert.True(property.PropertyType is { IsInterface: true, IsPublic: true }, property.Name);
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

        // what MAL's edge sends instead of an error status
        private static HttpResponseMessage ErrorRedirect(string location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class StubEndpoint : HttpMessageHandler
        {
            public Queue<Func<HttpResponseMessage>> Responses { get; } = new();

            // the first calls that never answer
            public int HangingCalls { get; set; }

            public List<string> Bodies { get; } = [];

            public List<string> Uris { get; } = [];

            public int Calls { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                Uris.Add(request.RequestUri!.ToString());

                if (request.Content is not null)
                {
                    Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                }

                if (Calls <= HangingCalls)
                {
                    // like MAL when it never answers: only cancellation ends the request
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return Responses.Dequeue()();
            }
        }
    }
}
