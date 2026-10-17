namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    // the bindable (nullable) counterpart of ClientConfiguration, for the ClientConfiguration section of the user secrets
    internal sealed class ClientConfigurationOptions
    {
        public string? ClientId { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
