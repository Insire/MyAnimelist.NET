using System.Text.Json.Serialization;

// The paged list models, written by hand because the spec builds them from an allOf with the base schema List, which
// NSwag turns into awkward types (the .nswag config excludes them from generation). They follow the generated models:
// MAL always sends data and paging, so they aren't nullable, and unmapped fields land in AdditionalProperties.
namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// A page of anime search results or seasonal anime.
    /// </summary>
    public sealed class AnimeList
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The anime on this page.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<AnimeListEdge> Data { get; set; } = [];

        /// <summary>
        /// The season of the page's anime, or <see langword="null"/> for search results and suggestions.
        /// </summary>
        /// <remarks>
        /// MyAnimeList sends it only for <see cref="IAnimeClient.GetSeasonalAsync"/>, and its spec doesn't declare it.
        /// </remarks>
        [JsonPropertyName("season")]
        public AnimeSeason? Season { get; set; }

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }

    /// <summary>
    /// A page of an anime ranking.
    /// </summary>
    public sealed class AnimeListForRanking
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The anime on this page, each with its place in the ranking.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<AnimeRankingEdge> Data { get; set; } = [];

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }

    /// <summary>
    /// A page of manga search results.
    /// </summary>
    public sealed class MangaList
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The manga on this page.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<MangaListEdge> Data { get; set; } = [];

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }

    /// <summary>
    /// A page of a manga ranking.
    /// </summary>
    public sealed class MangaListForRanking
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The manga on this page, each with its place in the ranking.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<MangaRankingEdge> Data { get; set; } = [];

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }

    /// <summary>
    /// A page of a user's anime list.
    /// </summary>
    public sealed class UserAnimeList
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The list entries on this page.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<UserAnimeListEdge> Data { get; set; } = [];

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }

    /// <summary>
    /// A page of a user's manga list.
    /// </summary>
    public sealed class UserMangaList
    {
        private IDictionary<string, object>? _additionalProperties;

        /// <summary>
        /// The list entries on this page.
        /// </summary>
        [JsonPropertyName("data")]
        public IReadOnlyList<UserMangaListEdge> Data { get; set; } = [];

        /// <summary>
        /// The links to the previous and next page.
        /// </summary>
        [JsonPropertyName("paging")]
        public Paging Paging { get; set; } = new();

        /// <summary>
        /// The fields of the JSON object that no other property maps, for example fields MyAnimeList added after this library was released.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get => _additionalProperties ??= new Dictionary<string, object>();
            set => _additionalProperties = value;
        }
    }
}
