using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class AnimeTests(OAuthFlowTestFixture fixture)
    {
        [Theory]
        [InlineData("all")]
        [InlineData("airing")]
        [InlineData("upcoming")]
        [InlineData("movie")]
        [InlineData("bypopularity")]
        public async Task RankingAsync_Should_Return_Expected_Structure(string rankingType)
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.RankingAsync(rankingType, limit: 5, fields: Fields.AnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response)).UseParameters(rankingType);
        }

        [Fact]
        public async Task SeasonAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.SeasonAsync(2020, "fall", sort: "anime_num_list_users", limit: 5, fields: Fields.AnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task SuggestionsAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Anime.SuggestionsAsync(limit: 5, fields: Fields.AnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: suggestions depend on the account, so they may be empty
            await Verify(ModelShape.Of(response));
        }
    }
}
