using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class UsersTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task GetAnimeListAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Users.GetAnimeListAsync("@me", sort: UserAnimeListSort.AnimeTitle, limit: 5, fields: MalFields.UserAnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(response.Data!, entry => entry.Node?.Id == TestAccount.SeedAnimeId && entry.ListStatus?.Status != null);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task GetMangaListAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Users.GetMangaListAsync("@me", sort: UserMangaListSort.MangaTitle, limit: 5, fields: MalFields.UserMangaList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(response.Data!, entry => entry.Node?.Id == TestAccount.SeedMangaId && entry.ListStatus?.Status != null);
            await Verify(ModelShape.Of(response));
        }
    }
}
