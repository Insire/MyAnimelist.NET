namespace MyAnimelist.NET.Authentication
{
    public sealed record class ClientConfigurationOptions
    {
        public string? ClientId { get; set; }
        public string? RedirectUrl { get; set; }
        public int? Port { get; set; }
    }
}