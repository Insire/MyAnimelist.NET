using System.Text.Json.Serialization;

namespace SoftThorn.MAL.ApiClient.Authentication
{
    public readonly record struct AuthResponse
    {
        [JsonPropertyName("access_token")] required public string AccessToken { get; init; }
        [JsonPropertyName("refresh_token")] required public string RefreshToken { get; init; }
        [JsonPropertyName("expires_in")] required public int ExpiresIn { get; init; }
        [JsonPropertyName("token_type")] required public string TokenType { get; init; }

        /// <summary>
        /// When the access token expires. MyAnimeList doesn't send this; it is set when the token is received.
        /// </summary>
        [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; init; }
    }
}