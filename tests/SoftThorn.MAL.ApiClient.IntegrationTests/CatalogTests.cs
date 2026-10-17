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
        public async Task SearchAnimeAsync_Should_Return_Paging_Links()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Catalog.SearchAnimeAsync("one", limit: 2, offset: 1, fields: "title", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data);
            Assert.NotNull(response.Paging.Next);
            Assert.NotNull(response.Paging.Previous);
        }

        [Fact]
        public async Task SearchAnimeAsync_Should_Include_Nsfw_Entries_Only_When_Asked()
        {
            // Arrange: MAL leaves out entries it rates gray or black unless nsfw=true; this search has both kinds
            var sut = fixture.GetMalApiService();
            var cancellationToken = TestContext.Current.CancellationToken;

            // Act
            var safe = await sut.Catalog.SearchAnimeAsync("kanojo", limit: 100, fields: "nsfw", cancellationToken: cancellationToken);
            var all = await sut.Catalog.SearchAnimeAsync("kanojo", limit: 100, fields: "nsfw", nsfw: true, cancellationToken: cancellationToken);

            // Assert
            Assert.All(safe.Data, entry => Assert.Equal("white", entry.Node.Nsfw));
            Assert.Contains(all.Data, entry => entry.Node.Nsfw != "white");
        }

        [Fact]
        public async Task SearchAnimeAsync_Should_Throw_ApiException_With_MyAnimeLists_Error()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act: MAL rejects search terms shorter than 3 characters
            var exception = await Assert.ThrowsAsync<ApiException>(() => sut.Catalog.SearchAnimeAsync("ab", cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(400, exception.StatusCode);
            Assert.Equal("bad_request", exception.ErrorCode);
            Assert.Equal("invalid q", exception.ErrorMessage);
        }
    }
}
