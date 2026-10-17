namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// Stores the signed-in user's tokens.
    /// </summary>
    /// <remarks>
    /// The default implementation keeps them in memory, so the user has to sign in again after a restart.
    /// Register your own implementation before calling
    /// <see cref="RegistrationExtensions.AddMalApiClient(Microsoft.Extensions.DependencyInjection.IServiceCollection, ClientConfiguration)"/>
    /// to keep them somewhere else, for example in the operating system's credential store.
    /// The library calls it from several requests at once, so it has to be thread-safe.
    /// </remarks>
    public interface IAuthCache
    {
        /// <summary>
        /// Returns the stored tokens.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The tokens last passed to <see cref="SaveAsync"/>, or <see langword="null"/> when there are none.</returns>
        Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores new tokens, replacing the ones stored before.
        /// </summary>
        /// <remarks>
        /// Keep the tokens after <see cref="AuthResponse.ExpiresAt"/>: the refresh token is still needed to get a new access token.
        /// </remarks>
        /// <param name="authResponse">The tokens to store.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the stored tokens, so <see cref="GetAsync"/> returns <see langword="null"/> afterwards.
        /// </summary>
        /// <remarks>
        /// Called by <see cref="ILoginClient.SignOutAsync"/>, and when MyAnimeList rejects the refresh token.
        /// </remarks>
        /// <param name="cancellationToken">Cancels the removal.</param>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
