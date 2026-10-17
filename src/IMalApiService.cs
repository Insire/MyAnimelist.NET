using SoftThorn.MAL.ApiClient.Authentication;

namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// The entry point to the MyAnimeList API v2: groups the endpoints into one client per area.
    /// </summary>
    /// <remarks>
    /// Register it with <see cref="RegistrationExtensions.AddMalApiClient"/> and resolve it from the service provider.
    /// Requests are authorized with the signed-in user's access token after signing in with <see cref="Login"/>.
    /// Without a signed-in user they send the Client ID instead, which MyAnimeList accepts for the public endpoints
    /// (search, details, rankings, seasons, forum and public lists); the user's own lists and suggestions need a sign-in.
    /// Without <c>fields</c>, MyAnimeList only returns a few fields of each entry; <see cref="MalFields"/> has values that request every field the models map.
    /// Every call throws an <see cref="ApiException"/> when MyAnimeList answers with an error.
    /// All members are interfaces, so tests of code that uses the API can replace any of them with a mock.
    /// </remarks>
    public interface IMalApiService
    {
        /// <summary>
        /// Anime and manga search and details, and user profiles.
        /// </summary>
        ICatalogClient Catalog { get; }

        /// <summary>
        /// Anime rankings, seasonal anime, suggestions, and the signed-in user's anime list entries.
        /// </summary>
        IAnimeClient Anime { get; }

        /// <summary>
        /// Forum boards and topics.
        /// </summary>
        IForumClient Forum { get; }

        /// <summary>
        /// Manga rankings and the signed-in user's manga list entries.
        /// </summary>
        IMangaClient Manga { get; }

        /// <summary>
        /// A user's anime list and manga list.
        /// </summary>
        IUsersClient Users { get; }

        /// <summary>
        /// The OAuth2 sign-in.
        /// </summary>
        ILoginClient Login { get; }
    }
}
