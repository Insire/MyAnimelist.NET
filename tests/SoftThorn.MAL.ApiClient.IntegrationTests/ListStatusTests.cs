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
                var added = await sut.Anime.UpdateListStatusAsync(TestAccount.TargetAnimeId, new AnimeListStatusPatch
                {
                    Status = AnimeWatchStatus.Watching,
                    Score = 7,
                    NumWatchedEpisodes = 3,
                    IsRewatching = false,
                    Priority = 1,
                    NumTimesRewatched = 0,
                    RewatchValue = 0,
                    Tags = "integration-test",
                    Comments = Comments,
                }, cancellationToken);

                // Assert
                Assert.Equal("watching", added.Status);
                Assert.Equal(7, added.Score);
                Assert.Equal(3, added.NumEpisodesWatched);
                Assert.False(added.IsRewatching);
                Assert.Equal(1, added.Priority);
                Assert.Equal(["integration-test"], added.Tags!);
                Assert.Equal(Comments, added.Comments);

                // Act: update only the score, the rest has to stay
                var updated = await sut.Anime.UpdateListStatusAsync(TestAccount.TargetAnimeId, new AnimeListStatusPatch { Score = 9 }, cancellationToken);

                // Assert
                Assert.Equal(9, updated.Score);
                Assert.Equal("watching", updated.Status);
                Assert.Equal(3, updated.NumEpisodesWatched);

                var details = await sut.Catalog.GetAnimeAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Equal(9, details.MyListStatus?.Score);

                // Act: remove
                await sut.Anime.DeleteListStatusAsync(TestAccount.TargetAnimeId, cancellationToken);

                // Assert
                details = await sut.Catalog.GetAnimeAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Null(details.MyListStatus);
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
            await sut.Anime.DeleteListStatusAsync(TestAccount.TargetAnimeId, cancellationToken);

            // Assert
            var details = await sut.Catalog.GetAnimeAsync(TestAccount.TargetAnimeId, fields: "my_list_status", cancellationToken: cancellationToken);
            Assert.Null(details.MyListStatus);
        }

        [Fact]
        public async Task GetAnimeAsync_MyListStatus_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Catalog.GetAnimeAsync(TestAccount.SeedAnimeId, fields: MalFields.AnimeDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.MyListStatus);
            await Verify(ModelShape.Of(response.MyListStatus));
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
                var added = await sut.Manga.UpdateListStatusAsync(TestAccount.TargetMangaId, new MangaListStatusPatch
                {
                    Status = MangaReadStatus.Reading,
                    Score = 7,
                    NumVolumesRead = 2,
                    NumChaptersRead = 20,
                    IsRereading = false,
                    Priority = 1,
                    NumTimesReread = 0,
                    RereadValue = 0,
                    Tags = "integration-test",
                    Comments = Comments,
                }, cancellationToken);

                // Assert
                Assert.Equal("reading", added.Status);
                Assert.Equal(7, added.Score);
                Assert.Equal(2, added.NumVolumesRead);
                Assert.Equal(20, added.NumChaptersRead);
                Assert.False(added.IsRereading);
                Assert.Equal(1, added.Priority);
                Assert.Equal(["integration-test"], added.Tags!);
                Assert.Equal(Comments, added.Comments);

                // Act: update only the score, the rest has to stay
                var updated = await sut.Manga.UpdateListStatusAsync(TestAccount.TargetMangaId, new MangaListStatusPatch { Score = 9 }, cancellationToken);

                // Assert
                Assert.Equal(9, updated.Score);
                Assert.Equal("reading", updated.Status);
                Assert.Equal(20, updated.NumChaptersRead);

                var details = await sut.Catalog.GetMangaAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Equal(9, details.MyListStatus?.Score);

                // Act: remove
                await sut.Manga.DeleteListStatusAsync(TestAccount.TargetMangaId, cancellationToken);

                // Assert
                details = await sut.Catalog.GetMangaAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
                Assert.Null(details.MyListStatus);
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
            await sut.Manga.DeleteListStatusAsync(TestAccount.TargetMangaId, cancellationToken);

            // Assert
            var details = await sut.Catalog.GetMangaAsync(TestAccount.TargetMangaId, fields: "my_list_status", cancellationToken: cancellationToken);
            Assert.Null(details.MyListStatus);
        }

        [Fact]
        public async Task GetMangaAsync_MyListStatus_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            await TestAccount.EnsureSeededAsync(sut, TestContext.Current.CancellationToken);

            // Act
            var response = await sut.Catalog.GetMangaAsync(TestAccount.SeedMangaId, fields: MalFields.MangaDetails, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(response.MyListStatus);
            await Verify(ModelShape.Of(response.MyListStatus));
        }
    }
}
