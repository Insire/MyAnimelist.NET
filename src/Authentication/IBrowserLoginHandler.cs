namespace SoftThorn.MyAnimelistNET.Authentication
{
    public interface IBrowserLoginHandler
    {
        ValueTask Handle(string authUrl);
    }
}