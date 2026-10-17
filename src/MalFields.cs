namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// Values for the <c>fields</c> parameter that request every field the models of this library map.
    /// </summary>
    /// <remarks>
    /// Without <c>fields</c>, MyAnimeList only returns the id, title and main picture of each entry.
    /// Pass one of these values, or a shorter comma-separated list of the fields you need, which keeps the responses smaller.
    /// The integration tests request these values, so they stay in step with the models.
    /// </remarks>
    public static class MalFields
    {
        private const string Work = "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,rank,popularity,num_list_users,num_scoring_users,nsfw,genres,created_at,updated_at,media_type,status";

        // without the nested selection MyAnimeList only sends status, score, progress, is_re* and updated_at
        private const string AnimeListStatusFields = "{start_date,finish_date,priority,num_times_rewatched,rewatch_value,tags,comments}";

        private const string MangaListStatusFields = "{start_date,finish_date,priority,num_times_reread,reread_value,tags,comments}";

        /// <summary>
        /// The fields of an anime in searches, rankings, seasons, suggestions and user lists,
        /// including the signed-in user's list entry (<c>my_list_status</c>).
        /// </summary>
        public const string Anime = Work + ",my_list_status" + AnimeListStatusFields + ",num_episodes,start_season,broadcast,source,average_episode_duration,rating,studios";

        /// <summary>
        /// The fields of <see cref="Anime"/>, plus the ones only <see cref="ICatalogClient.GetAnimeAsync"/> returns:
        /// pictures, background, related works, recommendations, statistics, theme songs and videos.
        /// </summary>
        public const string AnimeDetails = Anime + ",pictures,background,related_anime,related_manga,recommendations,statistics,opening_themes,ending_themes,videos";

        /// <summary>
        /// The fields of a manga in searches, rankings and user lists, including the signed-in user's list entry (<c>my_list_status</c>).
        /// </summary>
        public const string Manga = Work + ",my_list_status" + MangaListStatusFields + ",num_volumes,num_chapters,authors{first_name,last_name}";

        /// <summary>
        /// The fields of <see cref="Manga"/>, plus the ones only <see cref="ICatalogClient.GetMangaAsync"/> returns:
        /// pictures, background, related works, recommendations and serializations.
        /// </summary>
        public const string MangaDetails = Manga + ",pictures,background,related_anime,related_manga,recommendations,serialization{name}";

        /// <summary>
        /// The fields for <see cref="IUsersClient.GetAnimeListAsync"/>: those of <see cref="Anime"/>, plus each entry's status on that user's list (<c>list_status</c>).
        /// </summary>
        public const string UserAnimeList = Anime + ",list_status" + AnimeListStatusFields;

        /// <summary>
        /// The fields for <see cref="IUsersClient.GetMangaListAsync"/>: those of <see cref="Manga"/>, plus each entry's status on that user's list (<c>list_status</c>).
        /// </summary>
        public const string UserMangaList = Manga + ",list_status" + MangaListStatusFields;

        /// <summary>
        /// The fields for <see cref="IUsersClient.GetUserAsync"/>.
        /// </summary>
        public const string User = "id,name,picture,gender,birthday,location,joined_at,anime_statistics,time_zone,is_supporter";
    }
}
