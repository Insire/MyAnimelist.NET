namespace SoftThorn.MyAnimelistNET
{
    public class AnimeList
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<Data>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    public class AnimeListForRanking
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<Data2>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    public class MangaList
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<Data3>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    public class MangaListForRanking
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<Data4>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    public class UserAnimeList
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<UserAnimeListEdge>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }

    public class UserMangaList
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<UserMangaListEdge>? Data { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paging")]
        public Paging Paging { get; set; } = default!;
    }
}