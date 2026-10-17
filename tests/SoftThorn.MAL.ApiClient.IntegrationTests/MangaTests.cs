using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class MangaTests(OAuthFlowTestFixture fixture)
    {
        [Theory]
        [InlineData("all")]
        [InlineData("manga")]
        [InlineData("lightnovels")]
        [InlineData("bypopularity")]
        public async Task RankingAsync_Should_Return_Expected_Structure(string rankingType)
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Manga.RankingAsync(rankingType, limit: 5, fields: Fields.MangaList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response)).UseParameters(rankingType);
        }
    }
}
