using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAnimelist.NET.Authentication;
using MyAnimelist.NET.Tests.OAuth;

[assembly: AssemblyFixture(typeof(OAuthFlowTestFixture))]

namespace MyAnimelist.NET.Tests.OAuth
{
    public sealed class OAuthFlowTestFixture : IAsyncLifetime
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;

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

            _services = new ServiceCollection();
            _services.AddMyAnimelist(new ClientConfiguration()
            {
                ClientId = clientConfiguration.ClientId!,
                RedirectUrl = clientConfiguration.RedirectUrl!,
                Port = clientConfiguration.Port!.Value,
            });

            _services
                .AddSingleton<AuthTestProgressCollector>()
                .AddSingleton<HttpLoggingHandler>()
                .AddHttpClient<MyAnimelistService>(client => client.BaseAddress = new Uri(RegistrationExtensions.API_URI))
                .AddHttpMessageHandler<HttpLoggingHandler>();

            _serviceProvider = _services.BuildServiceProvider();
        }

        public async ValueTask InitializeAsync()
        {
            var progress = GetAuthTestProgressCollector();
            var sut = GetMyAnimelistService();

            await sut.Login.StartOAuthFlowAsync(progress, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}