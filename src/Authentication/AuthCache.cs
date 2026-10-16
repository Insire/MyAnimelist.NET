using Microsoft.Extensions.Caching.Memory;

namespace MyAnimelist.NET.Authentication
{
    public sealed class AuthCache : IAuthCache
    {
        private const string MemoryCache_KEY = "MyAnimelist.NET_TokenCache_Key";

        // drop the token a bit before MyAnimeList does, so requests in flight don't use an expired token
        private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _memoryCache;

        public AuthCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default)
        {
            var lifetime = TimeSpan.FromSeconds(authResponse.ExpiresIn) - ExpirationMargin;
            if (lifetime <= TimeSpan.Zero)
            {
                _memoryCache.Remove(MemoryCache_KEY);
                return Task.CompletedTask;
            }

            _memoryCache.Set(MemoryCache_KEY, authResponse, lifetime);

            return Task.CompletedTask;
        }

        public Task<AuthResponse?> Get(CancellationToken cancellationToken = default)
        {
            var token = _memoryCache.Get<AuthResponse?>(MemoryCache_KEY);

            return Task.FromResult(token);
        }
    }
}