using SoftThorn.MAL.ApiClient.Authentication;

namespace SoftThorn.MAL.ApiClient
{
    // the implementation behind IMalApiService, which is all that consumers see
    internal sealed class MalApiService : IMalApiService
    {
        // the name of the HttpClient that the API clients use, as IHttpClientFactory's typed clients name it
        internal const string HttpClientName = nameof(MalApiService);

        public ICatalogClient Catalog { get; }

        public IAnimeClient Anime { get; }

        public IForumClient Forum { get; }

        public IMangaClient Manga { get; }

        public IUsersClient Users { get; }

        public ILoginClient Login { get; }

        public MalApiService(HttpClient httpClient, ClientConfiguration clientConfiguration, IBrowserLoginHandler loginHandler, TokenClient tokenClient)
        {
            Login = new LoginClient(clientConfiguration, loginHandler, tokenClient);
            Catalog = new CatalogClient(httpClient);
            Anime = new AnimeClient(httpClient);
            Forum = new ForumClient(httpClient);
            Manga = new MangaClient(httpClient);
            Users = new UsersClient(httpClient);
        }
    }
}
