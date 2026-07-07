using MyAnimelist.NET.Tests.OAuth;

namespace MyAnimelist.NET.Tests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class FetchTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task Anime_Should_Work_And_Return_Many_Animes()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Anime.AnimeGetAsync("one", limit: 2, offset: 0, fields:
                "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await Verify(response);
        }

        [Fact]
        public async Task RankingAsync_Should_Work_And_Return_Many_Animes()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.MyAnime.RankingAsync(ranking_type: "all", limit: 2, offset: 0, fields:
                "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await Verify(response);
        }
    }
}