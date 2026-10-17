namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// Shows MyAnimeList's login page to the user.
    /// </summary>
    /// <remarks>
    /// The default implementation opens the page in the user's default browser.
    /// Register your own implementation before calling
    /// <see cref="RegistrationExtensions.AddMalApiClient(Microsoft.Extensions.DependencyInjection.IServiceCollection, ClientConfiguration)"/>
    /// to show it in an embedded web view instead, or to automate it.
    /// </remarks>
    public interface IBrowserLoginHandler
    {
        /// <summary>
        /// Opens the login page.
        /// </summary>
        /// <remarks>
        /// The login waits for this call and for the redirect to <see cref="ClientConfiguration.RedirectUrl"/> at the same time,
        /// so the returned task may complete before or after the user has signed in. A faulted task ends the login with its exception.
        /// </remarks>
        /// <param name="authUrl">The address of MyAnimeList's authorization page for this login.</param>
        /// <param name="cancellationToken">
        /// Cancelled when the caller of <see cref="ILoginClient.StartOAuthFlowAsync"/> gives up on the login, for example to close an embedded web view.
        /// </param>
        ValueTask HandleAsync(string authUrl, CancellationToken cancellationToken = default);
    }
}
