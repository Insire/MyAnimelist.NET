using System.Text.Json.Serialization;

namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// The tokens MyAnimeList issued for the signed-in user, as stored in the <see cref="IAuthCache"/>.
    /// </summary>
    /// <remarks>
    /// A custom <see cref="IAuthCache"/> should store every property, including <see cref="ExpiresAt"/>.
    /// The JSON property names match MyAnimeList's token response, so the value can be stored with <c>System.Text.Json</c> as it is.
    /// </remarks>
    public readonly record struct AuthResponse
    {
        /// <summary>
        /// The access token that is sent as the <c>Bearer</c> token with every API request.
        /// </summary>
        [JsonPropertyName("access_token")] required public string AccessToken { get; init; }

        /// <summary>
        /// The refresh token, used to get a new access token once <see cref="AccessToken"/> has expired.
        /// </summary>
        [JsonPropertyName("refresh_token")] required public string RefreshToken { get; init; }

        /// <summary>
        /// How many seconds the access token was valid for when it was issued.
        /// </summary>
        [JsonPropertyName("expires_in")] required public int ExpiresIn { get; init; }

        /// <summary>
        /// The token type, <c>Bearer</c>.
        /// </summary>
        [JsonPropertyName("token_type")] required public string TokenType { get; init; }

        /// <summary>
        /// When the access token expires. MyAnimeList doesn't send this; it is set when the token is received.
        /// A token without it is used until the API rejects it, because the library can't tell when to refresh it.
        /// </summary>
        [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; init; }
    }
}
