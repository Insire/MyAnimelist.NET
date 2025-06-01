using Microsoft.Extensions.Caching.Memory;

namespace MyAnimelist.NET.Authentication
{
    public sealed class AuthCache : IAuthCache
    {
        private const string MemoryCache_KEY = "MyAnimelist.NET_TokenCache_Key";

        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _options;

        public AuthCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
            _options = new MemoryCacheEntryOptions();
        }

        public async Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default)
        {
            await _memoryCache.GetOrCreateAsync(MemoryCache_KEY, entry =>
            {
                entry.Value = authResponse;

                return Task.FromResult(authResponse);
            }, _options);
        }

        public Task<AuthResponse?> Get(CancellationToken cancellationToken = default)
        {
            var token = _memoryCache.Get<AuthResponse?>(MemoryCache_KEY);

            return Task.FromResult(token);
        }
    }
}