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
        /// A call made while another login is running waits for it, and then returns without a second login if the first one succeeded.
        /// </remarks>
        /// <param name="progressReporter">Receives the steps of the login, for example to show them in a UI.</param>
        /// <param name="cancellationToken">Cancels the login, including the wait for the redirect.</param>
        /// <returns><see langword="true"/> when a valid token is available afterwards.</returns>
        Task<bool> StartOAuthFlowAsync(IProgress<AuthProgress>? progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a user is signed in, without opening the login page.
        /// </summary>
        /// <remarks>
        /// When the cached access token has expired, this refreshes it first, so it can send a request to MyAnimeList.
        /// Use it to decide whether to offer a sign-in, for example when the app starts with tokens from a persistent <see cref="IAuthCache"/>.
        /// </remarks>
        /// <param name="cancellationToken">Cancels the check.</param>
        /// <returns><see langword="true"/> when a valid token is available, the same condition under which <see cref="StartOAuthFlowAsync"/> skips the login.</returns>
        Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs the user out by removing the tokens from the <see cref="IAuthCache"/>.
        /// </summary>
        /// <remarks>
        /// MyAnimeList has no endpoint to revoke tokens, so they stay valid on its side until they expire.
        /// Requests made afterwards only reach the endpoints that don't need a signed-in user.
        /// </remarks>
        /// <param name="cancellationToken">Cancels the removal.</param>
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }
}
