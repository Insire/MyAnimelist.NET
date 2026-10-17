namespace SoftThorn.MyAnimelistNET.Authentication
{
    public interface IAuthCache
    {
        Task<AuthResponse?> Get(CancellationToken cancellationToken = default);

        Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default);
    }
}