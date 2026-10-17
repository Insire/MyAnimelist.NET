using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using SoftThorn.MAL.ApiClient.Authentication;
using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;

[assembly: AssemblyFixture(typeof(OAuthFlowTestFixture))]

namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    public sealed class OAuthFlowTestFixture : PageTest, IAsyncLifetime, IBrowserLoginHandler
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;
        private readonly MalAuthSettings _malAuthSettings;

        public MalApiService GetMalApiService()
            => _serviceProvider.GetRequiredService<MalApiService>();

        internal AuthTestProgressCollector GetAuthTestProgressCollector()
            => _serviceProvider.GetRequiredService<AuthTestProgressCollector>();

        public OAuthFlowTestFixture()
        {
            // user secrets in every configuration, since the Cake build runs the tests in Release; CI has no secrets file and uses the environment variables, which win
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<OAuthFlowTestFixture>()
                .AddEnvironmentVariables()
                .Build();

            var sectionName = nameof(ClientConfiguration);
            var clientConfiguration = new ClientConfigurationOptions();

            var section = configuration.GetSection(sectionName);
            section.Bind(clientConfiguration);

            sectionName = nameof(MalAuthSettings);
            var malAuthSettings = new MalAuthSettings();

            section = configuration.GetSection(sectionName);
            section.Bind(malAuthSettings);

            _malAuthSettings = malAuthSettings;

            _services = new ServiceCollection();

            _services
                .AddSingleton<AuthTestProgressCollector>()
                .AddSingleton<IBrowserLoginHandler>(this)
                .AddTransient<HttpLoggingHandler>()
                .AddHttpClient<MalApiService>(client => client.BaseAddress = new Uri(RegistrationExtensions.API_URI))
                .AddHttpMessageHandler<HttpLoggingHandler>();

            _services.AddMalApiClient(new ClientConfiguration()
            {
                ClientId = clientConfiguration.ClientId!,
                RedirectUrl = clientConfiguration.RedirectUrl!,
            });

            _serviceProvider = _services.BuildServiceProvider();
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            var progress = GetAuthTestProgressCollector();
            var sut = GetMalApiService();

            await sut.Login.StartOAuthFlowAsync(progress, TestContext.Current.CancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();

            await base.DisposeAsync();
        }

        public async ValueTask Handle(string authUrl)
        {
            await Page.GotoAsync(authUrl);

            await Page.Locator("#loginUserName").FillAsync(_malAuthSettings.UserName);
            await Page.Locator("#login-password").FillAsync(_malAuthSettings.Password);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Allow" }).ClickAsync();
        }
    }
}