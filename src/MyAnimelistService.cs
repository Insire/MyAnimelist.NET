using MyAnimelist.NET.Authentication;

namespace MyAnimelist.NET
{
    public sealed class MyAnimelistService
    {
        public Client Anime { get; }

        public AnimeClient MyAnime { get; }

        public ForumClient Forum { get; }

        public MangaClient Manga { get; }

        public UsersClient Users { get; }

        public LoginClient Login { get; }

        public MyAnimelistService(HttpClient httpClient, ClientConfiguration clientConfiguration, IAuthCache authCache)
        {
            Login = new LoginClient(httpClient, clientConfiguration, authCache);
            Anime = new Client(httpClient);
            MyAnime = new AnimeClient(httpClient);
            Forum = new ForumClient(httpClient);
            Manga = new MangaClient(httpClient);
            Users = new UsersClient(httpClient);
        }
    }
}