using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoftThorn.MyAnimelistNET.Authentication;

namespace SoftThorn.MyAnimelistNET
{
    public static class RegistrationExtensions
    {
        public const string API_URI = "https://api.myanimelist.net/v2/";

        public static IServiceCollection AddMyAnimelist(this IServiceCollection services, ClientConfiguration clientConfiguration)
        {
            services.TryAddSingleton<IAuthCache, AuthCache>();
            services.TryAddSingleton<IBrowserLoginHandler, BrowserLoginHandler>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<TokenClient>();
            services.AddHttpClient(TokenClient.HttpClientName);
            services.AddSingleton<MyAnimelistLoginHandler>();
            services.AddSingleton<MyAnimelistService>();
            services.AddSingleton(clientConfiguration);

            services.AddHttpClient<MyAnimelistService>(client => client.BaseAddress = new Uri(API_URI))
                .AddHttpMessageHandler<MyAnimelistLoginHandler>();

            services.AddMemoryCache();

            return services;
        }
    }
}