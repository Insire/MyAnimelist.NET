using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class AnimeTests(OAuthFlowTestFixture fixture)
    {
        [Theory]
        [InlineData(AnimeRankingType.All)]
        [InlineData(AnimeRankingType.Airing)]
        [InlineData(AnimeRankingType.Upcoming)]
        [InlineData(AnimeRankingType.Movie)]
        [InlineData(AnimeRankingType.ByPopularity)]
        public async Task GetRankingAsync_Should_Return_Expected_Structure(AnimeRankingType rankingType)
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.GetRankingAsync(rankingType, limit: 5, fields: MalFields.Anime, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent)).UseParameters(rankingType);
        }

        [Fact]
        public async Task GetRankingAsync_Should_Accept_Every_Ranking_Type()
        {
            // Arrange: MAL answers 400 for a ranking type it doesn't know, so this checks every value the enum sends
            var sut = fixture.GetMalApiService();

            foreach (var rankingType in Enum.GetValues<AnimeRankingType>())
            {
                // Act
                var response = await sut.Anime.GetRankingAsync(rankingType, limit: 1, fields: "id", cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotEmpty(response.Data!);
            }
        }

        [Fact]
        public async Task GetSeasonalAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.GetSeasonalAsync(2020, Season.Fall, sort: SeasonalAnimeSort.NumListUsers, limit: 5, fields: MalFields.Anime, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent));
        }

        [Fact]
        public async Task GetSuggestionsAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.GetSuggestionsAsync(limit: 5, fields: MalFields.Anime, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: suggestions depend on the account, so they may be empty
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent));
        }
    }
}
