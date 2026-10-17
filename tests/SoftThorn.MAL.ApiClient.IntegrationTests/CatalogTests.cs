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
        public async Task AnimeGetAsync_Search_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.AnimeGetAsync("cowboy bebop", limit: 5, fields: Fields.AnimeList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task AnimeGetAsync_Details_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.AnimeGetAsync(CowboyBebopAnimeId, fields: Fields.AnimeDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CowboyBebopAnimeId, response.Id);
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task MangaGetAsync_Search_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.MangaGetAsync("monster", limit: 5, fields: Fields.MangaList, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task MangaGetAsync_Details_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.MangaGetAsync(MonsterMangaId, fields: Fields.MangaDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(MonsterMangaId, response.Id);
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task UsersAsync_Me_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.UsersAsync("@me", fields: Fields.User, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: only the structure, so the test account's details stay out of the repository
            Assert.NotNull(response.Id);
            await Verify(ModelShape.Of(response));
        }
    }
}
