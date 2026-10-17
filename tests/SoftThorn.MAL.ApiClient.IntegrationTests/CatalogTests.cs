using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class CatalogTests(OAuthFlowTestFixture fixture)
    {
        // Cowboy Bebop and Monster: finished long ago, so apart from the counters their data is stable
        private const int CowboyBebopAnimeId = 1;
        private const int MonsterMangaId = 1;

        [Fact]
        public async Task SearchAnimeAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.SearchAnimeAsync("cowboy bebop", limit: 5, fields: MalFields.Anime, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent));
        }

        [Fact]
        public async Task GetAnimeAsync_Should_Work_Without_Signed_In_User()
        {
            // Arrange: MAL accepts the Client ID instead of a token on its public endpoints
            var sut = fixture.GetSignedOutMalApiService();

            // Act
            var response = await sut.Catalog.GetAnimeAsync(CowboyBebopAnimeId, fields: "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CowboyBebopAnimeId, response.Id);
            Assert.Equal("Cowboy Bebop", response.Title);
        }

        [Fact]
        public async Task GetAnimeAsync_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.GetAnimeAsync(CowboyBebopAnimeId, fields: MalFields.AnimeDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CowboyBebopAnimeId, response.Id);
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task SearchMangaAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.SearchMangaAsync("monster", limit: 5, fields: MalFields.Manga, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response, ModelShape.AccountDependent));
        }

        [Fact]
        public async Task GetMangaAsync_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.GetMangaAsync(MonsterMangaId, fields: MalFields.MangaDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(MonsterMangaId, response.Id);
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task GetUserAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.GetUserAsync("@me", fields: MalFields.User, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: only the structure, so the test account's details stay out of the repository
            Assert.NotNull(response.Id);
            await Verify(ModelShape.Of(response));
        }
    }
}
