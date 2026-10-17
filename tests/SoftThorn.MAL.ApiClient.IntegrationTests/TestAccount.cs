namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    /// <summary>
    /// The state the tests keep on the dedicated test account. The seed works are always on its lists, so list snapshots
    /// don't depend on what happens to be on the account; the target works are added and removed by <see cref="ListStatusTests"/>.
    /// Seeds and targets differ so tests running in parallel never touch the same entry.
    /// </summary>
    internal static class TestAccount
    {
        public const int SeedAnimeId = 30; // Neon Genesis Evangelion

        public const int SeedMangaId = 2; // Berserk

        public const int TargetAnimeId = 6; // Trigun

        public const int TargetMangaId = 13; // One Piece

        private const string SeedComments = "Seeded by the SoftThorn.MAL.ApiClient integration tests";

        public static async Task EnsureSeededAsync(IMalApiService sut, CancellationToken cancellationToken)
        {
            // priority, tags and comments make MAL return every my_list_status field the snapshots cover
            await sut.Anime.UpdateListStatusAsync(SeedAnimeId, new AnimeListStatusPatch
            {
                Status = AnimeWatchStatus.Completed,
                Score = 8,
                Priority = 1,
                Tags = ["integration-test"],
                Comments = SeedComments,
            }, cancellationToken);

            await sut.Manga.UpdateListStatusAsync(SeedMangaId, new MangaListStatusPatch
            {
                Status = MangaReadStatus.Reading,
                Score = 8,
                NumChaptersRead = 10,
                Priority = 1,
                Tags = ["integration-test"],
                Comments = SeedComments,
            }, cancellationToken);
        }

        public static async Task RemoveAnimeAsync(IMalApiService sut, int animeId, CancellationToken cancellationToken)
        {
            try
            {
                await sut.Anime.DeleteListStatusAsync(animeId, cancellationToken);
            }
            catch (ApiException exception) when (exception.StatusCode == 404)
            {
                // not on the list; MAL's spec documents a 404, although the live API answers 200 (see ListStatusTests)
            }
        }

        public static async Task RemoveMangaAsync(IMalApiService sut, int mangaId, CancellationToken cancellationToken)
        {
            try
            {
                await sut.Manga.DeleteListStatusAsync(mangaId, cancellationToken);
            }
            catch (ApiException exception) when (exception.StatusCode == 404)
            {
                // not on the list
            }
        }
    }
}
