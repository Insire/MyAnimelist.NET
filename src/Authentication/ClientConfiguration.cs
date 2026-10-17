namespace SoftThorn.MAL.ApiClient.Authentication
{
    /// <summary>
    /// The settings of the API client registered with MyAnimeList, passed to
    /// <see cref="RegistrationExtensions.AddMalApiClient(Microsoft.Extensions.DependencyInjection.IServiceCollection, ClientConfiguration)"/>.
    /// </summary>
    public sealed record class ClientConfiguration
    {
        /// <summary>
        /// The Client ID that MyAnimeList shows for the registered client at <c>https://myanimelist.net/apiconfig</c>.
        /// </summary>
        required public string ClientId { get; init; }

        /// <summary>
        /// The App Redirect URL registered for the client, for example <c>http://localhost:5000/callback/</c>.
        /// </summary>
        /// <remarks>
        /// It has to match the registered URL exactly, port included, and end with <c>/</c>,
        /// because the login listens on it with an <see cref="System.Net.HttpListener"/>.
        /// </remarks>
        required public string RedirectUrl { get; init; }
    }
}
