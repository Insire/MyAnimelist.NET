namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// A page of anime search results or seasonal anime.
    /// </summary>
    public sealed class AnimeList
    {
        /// <summary>
        /// The anime on this page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<AnimeListEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    /// <summary>
    /// A page of an anime ranking.
    /// </summary>
    public sealed class AnimeListForRanking
    {
        /// <summary>
        /// The anime on this page, each with its place in the ranking.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<AnimeRankingEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    /// <summary>
    /// A page of manga search results.
    /// </summary>
    public sealed class MangaList
    {
        /// <summary>
        /// The manga on this page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<MangaListEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    /// <summary>
    /// A page of a manga ranking.
    /// </summary>
    public sealed class MangaListForRanking
    {
        /// <summary>
        /// The manga on this page, each with its place in the ranking.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<MangaRankingEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    /// <summary>
    /// A page of a user's anime list.
    /// </summary>
    public sealed class UserAnimeList
    {
        /// <summary>
        /// The list entries on this page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<UserAnimeListEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    /// <summary>
    /// A page of a user's manga list.
    /// </summary>
    public sealed class UserMangaList
    {
        /// <summary>
        /// The list entries on this page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<UserMangaListEdge>? Data { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }
}
