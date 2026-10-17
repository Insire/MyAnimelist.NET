namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    /// <summary>
    /// The <c>fields</c> query values that request every property our models map, so the snapshots cover all of them.
    /// </summary>
    internal static class Fields
    {
        private const string Work = "id,title,main_picture,alternative_titles,start_date,end_date,synopsis,mean,rank,popularity,num_list_users,num_scoring_users,nsfw,genres,created_at,updated_at,media_type,status,my_list_status";

        public const string AnimeList = Work + ",num_episodes,start_season,broadcast,source,average_episode_duration,rating,studios";

        public const string AnimeDetails = AnimeList + ",pictures,background,related_anime,related_manga,recommendations,statistics";

        public const string MangaList = Work + ",num_volumes,num_chapters,authors{first_name,last_name}";

        public const string MangaDetails = MangaList + ",pictures,background,related_anime,related_manga,recommendations,serialization{name}";

        // without the nested selection MAL only sends status, score, progress, is_re* and updated_at
        private const string AnimeListStatusFields = "{start_date,finish_date,priority,num_times_rewatched,rewatch_value,tags,comments}";

        private const string MangaListStatusFields = "{start_date,finish_date,priority,num_times_reread,reread_value,tags,comments}";

        public const string AnimeListStatus = "my_list_status" + AnimeListStatusFields;

        public const string MangaListStatus = "my_list_status" + MangaListStatusFields;

        // the user list endpoints send the entry's status as list_status next to the node
        public const string UserAnimeList = AnimeList + ",list_status" + AnimeListStatusFields;

        public const string UserMangaList = MangaList + ",list_status" + MangaListStatusFields;

        public const string User ="id,name,picture,gender,birthday,location,joined_at,anime_statistics,time_zone,is_supporter";
    }
}
