namespace SoftThorn.MAL.ApiClient.Authentication
{
    public sealed record class ClientConfigurationOptions
    {
        public string? ClientId { get; set; }
        public string? RedirectUrl { get; set; }
    }
}