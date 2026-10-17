namespace SoftThorn.MAL.ApiClient.Authentication
{
    public interface IBrowserLoginHandler
    {
        ValueTask Handle(string authUrl);
    }
}