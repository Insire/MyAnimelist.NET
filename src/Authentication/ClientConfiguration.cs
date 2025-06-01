namespace MyAnimelist.NET.Authentication
{
    public sealed record class ClientConfiguration
    {
        required public string ClientId { get; init; }
        required public string RedirectUrl { get; init; }
        required public int Port { get; init; }
    }
}