using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoftThorn.MAL.ApiClient.Authentication;
using System.Net.Http.Headers;
using System.Reflection;

namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// Registers the MyAnimeList API client with an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class RegistrationExtensions
    {
        internal const string API_URI = "https://api.myanimelist.net/v2/";

        /// <summary>
        /// Registers <see cref="IMalApiService"/> and the services it needs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="IAuthCache"/>, <see cref="IBrowserLoginHandler"/> and <see cref="TimeProvider"/> are only registered
        /// when the collection has no registration for them yet, so register your own implementations before calling this.
        /// By default tokens are kept in memory and the login page opens in the default browser.
        /// </para>
        /// <para>
        /// The <see cref="HttpClient"/>s are created through <see cref="IHttpClientFactory"/>. They identify the library in the
        /// <c>User-Agent</c> header, don't follow redirects, and retry requests that MyAnimeList rate-limited or didn't answer.
        /// </para>
        /// </remarks>
        /// <param name="services">The collection to add the services to.</param>
        /// <param name="clientConfiguration">The Client ID and redirect URL registered with MyAnimeList.</param>
        /// <returns><paramref name="services"/>, so calls can be chained.</returns>
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
            services.AddSingleton(clientConfiguration);

            // a factory instead of AddHttpClient<IMalApiService, MalApiService>, because typed clients need a public type and constructor
            services.AddHttpClient(MalApiService.HttpClientName, client =>
                {
                    client.BaseAddress = new Uri(API_URI);
                    AddUserAgent(client);
                })
                .ConfigurePrimaryHttpMessageHandler(DisableRedirects)
                .AddHttpMessageHandler<MalLoginHandler>()
                .AddHttpMessageHandler<MalRetryHandler>()
                .AddTypedClient<IMalApiService>((httpClient, provider) => new MalApiService(
                    httpClient,
                    provider.GetRequiredService<ClientConfiguration>(),
                    provider.GetRequiredService<IBrowserLoginHandler>(),
                    provider.GetRequiredService<TokenClient>()));

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