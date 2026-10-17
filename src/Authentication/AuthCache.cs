namespace SoftThorn.MAL.ApiClient.Authentication
{
    // the default IAuthCache: keeps the token in memory only, so it is gone after a restart
    internal sealed class AuthCache : IAuthCache
    {
        // AuthResponse is a struct that is too large to read or write atomically
        private readonly Lock _lock = new();
        private AuthResponse? _token;

        // kept past the access token's expiry, since the refresh token is still needed to get a new one
        public Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _token = authResponse;
            }

            return Task.CompletedTask;
        }

        public Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_token);
            }
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _token = null;
            }

            return Task.CompletedTask;
        }
    }
}
