using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoftThorn.MAL.ApiClient.Authentication;
using System.Net.Http.Headers;
using System.Reflection;

namespace SoftThorn.MAL.ApiClient
{
    public static class RegistrationExtensions
    {
        public const string API_URI = "https://api.myanimelist.net/v2/";

        public static IServiceCollection AddMalApiClient(this IServiceCollection services, ClientConfiguration clientConfiguration)
        {
            services.TryAddSingleton<IAuthCache, AuthCache>();
            services.TryAddSingleton<IBrowserLoginHandler, BrowserLoginHandler>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<TokenClient>();
            // IHttpClientFactory rebuilds handler chains every few minutes and sets InnerHandler on each handler,
            // which throws for a handler that was already used, so handlers must be transient
            services.AddTransient<MalRetryHandler>();
            services.AddHttpClient(TokenClient.HttpClientName, AddUserAgent)
                .ConfigurePrimaryHttpMessageHandler(DisableRedirects)
                .AddHttpMessageHandler<MalRetryHandler>();
            services.AddTransient<MalLoginHandler>();
            services.AddSingleton<MalApiService>();
            services.AddSingleton(clientConfiguration);

            services.AddHttpClient<MalApiService>(client =>
                {
                    client.BaseAddress = new Uri(API_URI);
                    AddUserAgent(client);
                })
                .ConfigurePrimaryHttpMessageHandler(DisableRedirects)
                .AddHttpMessageHandler<MalLoginHandler>()
                .AddHttpMessageHandler<MalRetryHandler>();

            services.AddMemoryCache();

            return services;
        }

        // MAL's API License Agreement (3(a)(x)) forbids concealing the identity of an application, so every request names the library
        internal static readonly ProductInfoHeaderValue[] UserAgent =
        [
            new("SoftThorn.MAL.ApiClient", typeof(RegistrationExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"),
            new("(+https://github.com/Insire/SoftThorn.MAL.ApiClient)"),
        ];

        // MAL's API doesn't redirect, except to its error page (MalRetryHandler.ErrorPagePath), which never answers.
        // Following that redirect would also send every retry there, since the handler rewrites the request's URI.
        private static void DisableRedirects(HttpMessageHandler handler, IServiceProvider _)
        {
            switch (handler)
            {
                case HttpClientHandler httpClientHandler:
                    httpClientHandler.AllowAutoRedirect = false;
                    break;

                case SocketsHttpHandler socketsHttpHandler:
                    socketsHttpHandler.AllowAutoRedirect = false;
                    break;
            }
        }

        private static void AddUserAgent(HttpClient client)
        {
            foreach (var product in UserAgent)
            {
                client.DefaultRequestHeaders.UserAgent.Add(product);
            }
        }
    }
}