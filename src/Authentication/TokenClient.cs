using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// Requests tokens from the MyAnimeList OAuth endpoint, stores them in the <see cref="IAuthCache"/> and refreshes them once they expire.
    /// </summary>
    internal sealed partial class TokenClient
    {
        internal const string HttpClientName = "SoftThorn.MAL.ApiClient.OAuth";

        internal const string BaseUri = "https://myanimelist.net/v1/oauth2/";

        // refresh a bit before MyAnimeList expires the token, so requests in flight don't use an expired token
        private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthCache _authCache;
        private readonly TimeProvider _timeProvider;
        private readonly string _clientId;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private readonly ILogger<TokenClient> _logger;

        public TokenClient(IHttpClientFactory httpClientFactory, ClientConfiguration clientConfiguration, IAuthCache authCache, TimeProvider timeProvider, ILogger<TokenClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _authCache = authCache;
            _timeProvider = timeProvider;
            _logger = logger;
            _clientId = clientConfiguration.ClientId;
        }

        /// <summary>
        /// Exchanges the authorization code from the login redirect for a token.
        /// </summary>
        /// <returns><see langword="true"/> when a token was received and cached.</returns>
        public async Task<bool> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default)
        {
            var (token, _) = await RequestTokenAsync(
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
            var token = await _authCache.GetAsync(cancellationToken);
            if (token is null || !IsExpired(token.Value))
            {
                return token;
            }

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // another caller may have refreshed the token while we were waiting
                token = await _authCache.GetAsync(cancellationToken);
                if (token is null || !IsExpired(token.Value))
                {
                    return token;
                }

                var (refreshed, statusCode) = await RequestTokenAsync(
                [
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", token.Value.RefreshToken)
                ], cancellationToken);

                // 400 (invalid_grant) or 401 (invalid_client): the refresh token is revoked or used up and will never work again.
                // Without clearing it, every following request would send another refresh to MAL. Other failures (5xx) may pass.
                if (refreshed is null && statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    LogRefreshRejected((int)statusCode);
                    await _authCache.ClearAsync(cancellationToken);
                }
                else if (refreshed is null)
                {
                    // MalLoginHandler then sends the Client ID, which endpoints that need a user reject with 401
                    LogRefreshFailed((int)statusCode);
                }

                return refreshed;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Removes the cached token, waiting for a refresh in progress so it can't store a new token afterwards.
        /// </summary>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                await _authCache.ClearAsync(cancellationToken);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "MyAnimeList rejected the refresh token with {StatusCode}; the tokens were removed, so the user has to sign in again")]
        private partial void LogRefreshRejected(int statusCode);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Refreshing the access token failed with {StatusCode}; the tokens were kept for the next attempt, and until then requests are sent with the Client ID, so endpoints that need a signed-in user fail")]
        private partial void LogRefreshFailed(int statusCode);

        // tokens without an expiry date (e.g. from a custom cache that doesn't store it) are used as they are
        private bool IsExpired(AuthResponse token)
            => token.ExpiresAt is { } expiresAt && expiresAt - ExpirationMargin <= _timeProvider.GetUtcNow();

        private async Task<(AuthResponse? Token, HttpStatusCode StatusCode)> RequestTokenAsync(IEnumerable<KeyValuePair<string, string>> parameters, CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(parameters);
            using var response = await httpClient.PostAsync(BaseUri + "token", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            token = token with { ExpiresAt = _timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) };

            await _authCache.SaveAsync(token, cancellationToken);

            return (token, response.StatusCode);
        }
    }
}
