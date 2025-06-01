using System.Text.Json.Serialization;

namespace MyAnimelist.NET.Authentication
{
    public readonly record struct AuthResponse
    {
        [JsonPropertyName("access_token")] required public string AccessToken { get; init; }
        [JsonPropertyName("refresh_token")] required public string RefreshToken { get; init; }
        [JsonPropertyName("expires_in")] required public int ExpiresIn { get; init; }
        [JsonPropertyName("token_type")] required public string TokenType { get; init; }
    }
}