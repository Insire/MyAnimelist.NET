using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyAnimelist.NET.Authentication;

namespace MyAnimelist.NET
{
    public static class RegistrationExtensions
    {
        public static IServiceCollection AddMyAnimelist(this IServiceCollection services, ClientConfiguration clientConfiguration)
        {
            services.TryAddSingleton<IAuthCache, AuthCache>();
            services.AddSingleton<MyAnimelistService>();
            services.AddSingleton(clientConfiguration);
            services.AddHttpClient();
            services.AddMemoryCache();

            return services;
        }
    }
}