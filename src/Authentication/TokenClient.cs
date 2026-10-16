using System.Net.Http.Json;

namespace MyAnimelist.NET.Authentication
{
    /// <summary>
    /// Requests tokens from the MyAnimeList OAuth endpoint, stores them in the <see cref="IAuthCache"/> and refreshes them once they expire.
    /// </summary>
    public sealed class TokenClient
    {
        public const string HttpClientName = "MyAnimelist.NET.OAuth";

        internal const string BaseUri = "https://myanimelist.net/v1/oauth2/";

        // refresh a bit before MyAnimeList expires the token, so requests in flight don't use an expired token
        private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthCache _authCache;
        private readonly TimeProvider _timeProvider;
        private readonly string _clientId;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public TokenClient(IHttpClientFactory httpClientFactory, ClientConfiguration clientConfiguration, IAuthCache authCache, TimeProvider timeProvider)
        {
            _httpClientFactory = httpClientFactory;
            _authCache = authCache;
            _timeProvider = timeProvider;
            _clientId = clientConfiguration.ClientId;
        }

        /// <summary>
        /// Exchanges the authorization code from the login redirect for a token.
        /// </summary>
        /// <returns><see langword="true"/> when a token was received and cached.</returns>
        public async Task<bool> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default)
        {
            var token = await RequestTokenAsync(
            [
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", redirectUri)
            ], cancellationToken);

            return token is not null;
        }

        /// <summary>
        /// Returns the cached token, refreshing it first when it has expired.
        /// </summary>
        /// <returns>A usable token, or <see langword="null"/> when there is none or the refresh failed.</returns>
        public async Task<AuthResponse?> GetValidTokenAsync(CancellationToken cancellationToken = default)
        {
            var token = await _authCache.Get(cancellationToken);
            if (token is null || !IsExpired(token.Value))
            {
                return token;
            }

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // another caller may have refreshed the token while we were waiting
                token = await _authCache.Get(cancellationToken);
                if (token is null || !IsExpired(token.Value))
                {
                    return token;
                }

                return await RequestTokenAsync(
                [
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", token.Value.RefreshToken)
                ], cancellationToken);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        // tokens without an expiry date (e.g. from a custom cache that doesn't store it) are used as they are
        private bool IsExpired(AuthResponse token)
            => token.ExpiresAt is { } expiresAt && expiresAt - ExpirationMargin <= _timeProvider.GetUtcNow();

        private async Task<AuthResponse?> RequestTokenAsync(IEnumerable<KeyValuePair<string, string>> parameters, CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(parameters);
            using var response = await httpClient.PostAsync(BaseUri + "token", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            token = token with { ExpiresAt = _timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) };

            await _authCache.SaveAsync(token, cancellationToken);

            return token;
        }
    }
}
