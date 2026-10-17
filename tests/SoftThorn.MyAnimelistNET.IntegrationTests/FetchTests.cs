using SoftThorn.MyAnimelistNET.IntegrationTests.OAuth;

namespace SoftThorn.MyAnimelistNET.IntegrationTests
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
            var response = await sut.Catalog.AnimeGetAsync("one", limit: 2, offset: 1, fields:
                "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Count > 0);
            Assert.NotNull(response.Paging);
            Assert.NotNull(response.Paging.Next);
            Assert.NotNull(response.Paging.Previous);
        }

        [Fact]
        public async Task RankingAsync_Should_Work_And_Return_Many_Animes()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Anime.RankingAsync(ranking_type: "all", limit: 2, offset: 1, fields:
                "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Data);
            Assert.True(response.Data.Count > 0);
            Assert.NotNull(response.Paging);
            Assert.NotNull(response.Paging.Next);
            Assert.NotNull(response.Paging.Previous);
        }
    }
}