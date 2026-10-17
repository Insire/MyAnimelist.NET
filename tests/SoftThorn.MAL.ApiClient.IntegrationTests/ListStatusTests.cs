using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    // these tests change the dedicated test account's lists, see TestAccount
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class ListStatusTests(OAuthFlowTestFixture fixture)
    {
        private const string Comments = "Added by the SoftThorn.MAL.ApiClient integration tests";

        [Fact]
        public async Task AnimeListStatus_Should_Be_Added_Updated_And_Removed()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            var cancellationToken = TestContext.Current.CancellationToken;
            await TestAccount.RemoveAnimeAsync(sut, TestAccount.TargetAnimeId, cancellationToken);

            try
            {
                // Act: add with every field
                var added = await sut.Anime.My_list_statusPatchAsync(TestAccount.TargetAnimeId, new AnimeListStatusPatch
                {
                    Status = "watching",
                    Score = 7,
                    Num_watched_episodes = 3,
                    Is_rewatching = false,
                    Priority = 1,
                    Num_times_rewatched = 0,
                    Rewatch_value = 0,
                    Tags = "integration-test",
                    Comments = Comments,
                }, cancellationToken);

                // Assert
                Assert.Equal("watching", added.Status);
                Assert.Equal(7, added.Score);
                Assert.Equal(3, added.Num_episodes_watched);
                Assert.False(added.Is_rewatching);
                Assert.Equal(1, added.Priority);
                Assert.Equal(["integration-test"], added.Tags!);
                Assert.Equal(Comments, added.Comments);

                // Act: update only the score, the rest has to stay
                var updated = await sut.Anime.My_list_statusPatchAsync(TestAccount.TargetAnimeId, new AnimeListStatusPatch { Score = 9 }, cancellationToken);

                // Assert
                Assert.Equal(9, updated.Score);
                Assert.Equal("watching", updated.Status);
                Assert.Equal(3, updated.Num_episodes_watched);

                var details = await sut.Catalog.AnimeGetAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Equal(9, details.My_list_status?.Score);

                // Act: remove
                await sut.Anime.My_list_statusDeleteAsync(TestAccount.TargetAnimeId, cancellationToken);

                // Assert
                details = await sut.Catalog.AnimeGetAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Null(details.My_list_status);
            }
            finally
            {
                await TestAccount.RemoveAnimeAsync(sut, TestAccount.TargetAnimeId, CancellationToken.None);
            }
        }

        [Fact]
        public async Task AnimeListStatus_Delete_Should_Succeed_When_Not_On_List()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            var cancellationToken = TestContext.Current.CancellationToken;
            await TestAccount.RemoveAnimeAsync(sut, TestAccount.TargetAnimeId, cancellationToken);

            // Act: MAL's spec promises 404 here, but the live API answers 200
            await sut.Anime.My_list_statusDeleteAsync(TestAccount.TargetAnimeId, cancellationToken);

            // Assert
            var details = await sut.Catalog.AnimeGetAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
            Assert.Null(details.My_list_status);
        }

        [Fact]
        public async Task AnimeGetAsync_MyListStatus_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Catalog.AnimeGetAsync(TestAccount.SeedAnimeId, fields: Fields.AnimeListStatus, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.My_list_status);
            await Verify(ModelShape.Of(response.My_list_status));
        }

        [Fact]
        public async Task MangaListStatus_Should_Be_Added_Updated_And_Removed()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            var cancellationToken = TestContext.Current.CancellationToken;
            await TestAccount.RemoveMangaAsync(sut, TestAccount.TargetMangaId, cancellationToken);

            try
            {
                // Act: add with every field
                var added = await sut.Manga.My_list_statusPatchAsync(TestAccount.TargetMangaId, new MangaListStatusPatch
                {
                    Status = "reading",
                    Score = 7,
                    Num_volumes_read = 2,
                    Num_chapters_read = 20,
                    Is_rereading = false,
                    Priority = 1,
                    Num_times_reread = 0,
                    Reread_value = 0,
                    Tags = "integration-test",
                    Comments = Comments,
                }, cancellationToken);

                // Assert
                Assert.Equal("reading", added.Status);
                Assert.Equal(7, added.Score);
                Assert.Equal(2, added.Num_volumes_read);
                Assert.Equal(20, added.Num_chapters_read);
                Assert.False(added.Is_rereading);
                Assert.Equal(1, added.Priority);
                Assert.Equal(["integration-test"], added.Tags!);
                Assert.Equal(Comments, added.Comments);

                // Act: update only the score, the rest has to stay
                var updated = await sut.Manga.My_list_statusPatchAsync(TestAccount.TargetMangaId, new MangaListStatusPatch { Score = 9 }, cancellationToken);

                // Assert
                Assert.Equal(9, updated.Score);
                Assert.Equal("reading", updated.Status);
                Assert.Equal(20, updated.Num_chapters_read);

                var details = await sut.Catalog.MangaGetAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Equal(9, details.My_list_status?.Score);

                // Act: remove
                await sut.Manga.My_list_statusDeleteAsync(TestAccount.TargetMangaId, cancellationToken);

                // Assert
                details = await sut.Catalog.MangaGetAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Null(details.My_list_status);
            }
            finally
            {
                await TestAccount.RemoveMangaAsync(sut, TestAccount.TargetMangaId, CancellationToken.None);
            }
        }

        [Fact]
        public async Task MangaListStatus_Delete_Should_Succeed_When_Not_On_List()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            var cancellationToken = TestContext.Current.CancellationToken;
            await TestAccount.RemoveMangaAsync(sut, TestAccount.TargetMangaId, cancellationToken);

            // Act: MAL's spec promises 404 here, but the live API answers 200
            await sut.Manga.My_list_statusDeleteAsync(TestAccount.TargetMangaId, cancellationToken);

            // Assert
            var details = await sut.Catalog.MangaGetAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
            Assert.Null(details.My_list_status);
        }

        [Fact]
        public async Task MangaGetAsync_MyListStatus_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Catalog.MangaGetAsync(TestAccount.SeedMangaId, fields: Fields.MangaListStatus, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.My_list_status);
            await Verify(ModelShape.Of(response.My_list_status));
        }
    }
}
