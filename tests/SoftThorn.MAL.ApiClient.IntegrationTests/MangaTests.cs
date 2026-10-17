using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class MangaTests(OAuthFlowTestFixture fixture)
    {
        [Theory]
        [InlineData(MangaRankingType.All)]
        [InlineData(MangaRankingType.Manga)]
        [InlineData(MangaRankingType.LightNovels)]
        [InlineData(MangaRankingType.ByPopularity)]
        public async Task GetRankingAsync_Should_Return_Expected_Structure(MangaRankingType rankingType)
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Manga.GetRankingAsync(rankingType, limit: 5, fields: MalFields.Manga, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent)).UseParameters(rankingType);
        }

        [Fact]
        public async Task GetRankingAsync_Should_Accept_Every_Ranking_Type()
        {
            // Arrange: MAL answers 400 for a ranking type it doesn't know, so this checks every value the enum sends
            var sut = fixture.GetMalApiService();

            foreach (var rankingType in Enum.GetValues<MangaRankingType>())
            {
                // Act
                var response = await sut.Manga.GetRankingAsync(rankingType, limit: 1, fields: "id", cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.NotEmpty(response.Data!);
            }
        }
    }
}
