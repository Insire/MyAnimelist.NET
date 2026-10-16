using Microsoft.Extensions.Caching.Memory;

namespace MyAnimelist.NET.Authentication
{
    public sealed class AuthCache : IAuthCache
    {
        private const string MemoryCache_KEY = "MyAnimelist.NET_TokenCache_Key";

        private readonly IMemoryCache _memoryCache;

        public AuthCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        // kept past the access token's expiry, since the refresh token is still needed to get a new one
        public Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default)
        {
            _memoryCache.Set(MemoryCache_KEY, authResponse);

            return Task.CompletedTask;
        }

        public Task<AuthResponse?> Get(CancellationToken cancellationToken = default)
        {
            var token = _memoryCache.Get<AuthResponse?>(MemoryCache_KEY);

            return Task.FromResult(token);
        }
    }
}