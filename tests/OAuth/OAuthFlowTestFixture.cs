using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAnimelist.NET.Authentication;

namespace MyAnimelist.NET.Tests.OAuth
{
    public sealed class OAuthFlowTestFixture : IDisposable
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;

        public MyAnimelistService GetMyAnimelistService()
            => _serviceProvider.GetRequiredService<MyAnimelistService>();

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

            _serviceProvider = _services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}