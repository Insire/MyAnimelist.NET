using MyAnimelist.NET.IntegrationTests.OAuth;
using MyAnimelist.NET.IntegrationTests.Snapshots;

namespace MyAnimelist.NET.IntegrationTests
{
    // the list endpoints have no fields parameter, so MAL only returns id, title and main_picture of each node
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class UsersTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task AnimelistAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Users.AnimelistAsync("@me", sort: "anime_title", limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.Data);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task MangalistAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Users.MangalistAsync("@me", sort: "manga_title", limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.Data);
            await Verify(ModelShape.Of(response));
        }
    }
}
