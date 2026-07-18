namespace MyAnimelist.NET.Authentication
{
    public interface IBrowserLoginHandler
    {
        ValueTask Handle(string authUrl);
    }
}