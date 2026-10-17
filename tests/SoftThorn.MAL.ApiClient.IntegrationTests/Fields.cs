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

        public const string User = "id,name,picture,gender,birthday,location,joined_at,anime_statistics,time_zone,is_supporter";
    }
}
