using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class UsersTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task AnimelistAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Users.AnimelistAsync("@me", sort: "anime_title", limit: 5, fields: Fields.UserAnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(response.Data!, entry => entry.Node?.Id == TestAccount.SeedAnimeId && entry.List_status?.Status != null);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task MangalistAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Users.MangalistAsync("@me", sort: "manga_title", limit: 5, fields: Fields.UserMangaList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(response.Data!, entry => entry.Node?.Id == TestAccount.SeedMangaId && entry.List_status?.Status != null);
            await Verify(ModelShape.Of(response));
        }
    }
}
