namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// Signs the user in to MyAnimeList with OAuth2 (authorization code flow with PKCE).
    /// </summary>
    /// <remarks>
    /// Get it through <see cref="IMalApiService.Login"/>. The tokens are stored in the registered <see cref="IAuthCache"/>,
    /// and every API request sends them, refreshing the access token shortly before it expires.
    /// </remarks>
    public interface ILoginClient
    {
        /// <summary>
        /// Signs the user in, unless a valid token is already cached or an expired one can be refreshed.
        /// </summary>
        /// <remarks>
        /// Opens MyAnimeList's login page through the registered <see cref="IBrowserLoginHandler"/> and waits until MyAnimeList
        /// redirects to <see cref="ClientConfiguration.RedirectUrl"/>. The wait has no time limit of its own; use <paramref name="cancellationToken"/> to give up.
        /// </remarks>
        /// <param name="progressReporter">Receives the steps of the login, for example to show them in a UI.</param>
        /// <param name="cancellationToken">Cancels the login, including the wait for the redirect.</param>
        /// <returns><see langword="true"/> when a valid token is available afterwards.</returns>
        Task<bool> StartOAuthFlowAsync(IProgress<AuthProgress>? progressReporter = null, CancellationToken cancellationToken = default);
    }
}
