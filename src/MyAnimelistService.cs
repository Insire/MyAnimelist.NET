using MyAnimelist.NET.Authentication;

namespace MyAnimelist.NET
{
    public sealed class MyAnimelistService
    {
        public Client Catalog { get; }

        public AnimeClient Anime { get; }

        public ForumClient Forum { get; }

        public MangaClient Manga { get; }

        public UsersClient Users { get; }

        public LoginClient Login { get; }

        public MyAnimelistService(HttpClient httpClient, ClientConfiguration clientConfiguration, IBrowserLoginHandler loginHandler, TokenClient tokenClient)
        {
            Login = new LoginClient(clientConfiguration, loginHandler, tokenClient);
            Catalog = new Client(httpClient);
            Anime = new AnimeClient(httpClient);
            Forum = new ForumClient(httpClient);
            Manga = new MangaClient(httpClient);
            Users = new UsersClient(httpClient);
        }
    }
}