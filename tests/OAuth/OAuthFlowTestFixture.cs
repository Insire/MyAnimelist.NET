using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using MyAnimelist.NET.Authentication;
using MyAnimelist.NET.Tests.OAuth;

[assembly: AssemblyFixture(typeof(OAuthFlowTestFixture))]

namespace MyAnimelist.NET.Tests.OAuth
{
    public sealed class OAuthFlowTestFixture : PageTest, IAsyncLifetime, IBrowserLoginHandler
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;
        private readonly MalAuthSettings _malAuthSettings;

        public MyAnimelistService GetMyAnimelistService()
            => _serviceProvider.GetRequiredService<MyAnimelistService>();

        internal AuthTestProgressCollector GetAuthTestProgressCollector()
            => _serviceProvider.GetRequiredService<AuthTestProgressCollector>();

        public OAuthFlowTestFixture()
        {
            var configuration = new ConfigurationBuilder()
#if DEBUG
                .AddUserSecrets<Traits>()
#endif
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
                .AddSingleton<HttpLoggingHandler>()
                .AddHttpClient<MyAnimelistService>(client => client.BaseAddress = new Uri(RegistrationExtensions.API_URI))
                .AddHttpMessageHandler<HttpLoggingHandler>();

            _services.AddMyAnimelist(new ClientConfiguration()
            {
                ClientId = clientConfiguration.ClientId!,
                RedirectUrl = clientConfiguration.RedirectUrl!,
                Port = clientConfiguration.Port!.Value,
            });

            _serviceProvider = _services.BuildServiceProvider();
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            var progress = GetAuthTestProgressCollector();
            var sut = GetMyAnimelistService();

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