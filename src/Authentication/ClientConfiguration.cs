namespace SoftThorn.MyAnimelistNET.Authentication
{
    public sealed record class ClientConfiguration
    {
        required public string ClientId { get; init; }
        required public string RedirectUrl { get; init; }
    }
}