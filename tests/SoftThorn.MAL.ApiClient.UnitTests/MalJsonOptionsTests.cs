using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalJsonOptionsTests
    {
        private readonly RecordingLogger _logger = new();
        private readonly MalJsonOptions _sut;

        public MalJsonOptionsTests()
        {
            _sut = new MalJsonOptions(_logger);
        }

        [Theory]
        [InlineData("winter")]
        [InlineData("spring")]
        [InlineData("summer")]
        [InlineData("fall")]
        public void Deserialize_Should_Not_Warn_About_Known_Seasons(string season)
        {
            var result = JsonSerializer.Deserialize<AnimeSeason>($$"""{"year":2024,"season":"{{season}}"}""", _sut.Options);

            Assert.Equal(season, result?.Season);
            Assert.Empty(_logger.Warnings);
        }

        [Fact]
        public void Deserialize_Should_Keep_And_Warn_About_Unknown_Season_Once()
        {
            // a page repeats the season in every entry, so the same value is reported only once
            var result = JsonSerializer.Deserialize<AnimeSeason[]>("""[{"year":2024,"season":"autumn"},{"year":2025,"season":"autumn"}]""", _sut.Options);

            Assert.All(result!, season => Assert.Equal("autumn", season.Season));
            var warning = Assert.Single(_logger.Warnings);
            Assert.Contains("'autumn'", warning);
            Assert.Contains("AnimeSeason.season", warning);
            Assert.Contains("winter, spring, summer, fall", warning);
        }

        [Fact]
        public void Options_Should_Read_Numbers_From_Strings()
        {
            // MalJsonSerializerSettings.Update still applies: MAL sends statistics.status.* as strings
            var result = JsonSerializer.Deserialize<AnimeStatusStatistics>("""{"watching":"12"}""", _sut.Options);

            Assert.Equal(12, result?.Watching);
        }

        [Fact]
        public async Task Clients_Should_Use_The_Options_Of_Their_Service_Provider()
        {
            using var httpClient = new HttpClient(new StubHandler("""{"data":[],"paging":{},"season":{"year":2024,"season":"autumn"}}"""))
            {
                BaseAddress = new Uri(RegistrationExtensions.API_URI),
            };
            var client = new AnimeClient(httpClient, _sut.Options);

            var result = await client.GetSeasonalAsync(2024, Season.Fall, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("autumn", result.Season?.Season);
            Assert.Single(_logger.Warnings);
        }

        private sealed class StubHandler(string json) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }

        private sealed class RecordingLogger : ILogger<MalJsonOptions>
        {
            public List<string> Warnings { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
