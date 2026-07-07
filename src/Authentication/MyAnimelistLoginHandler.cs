namespace MyAnimelist.NET.Authentication
{
    internal sealed class MyAnimelistLoginHandler(IAuthCache authCache, ClientConfiguration clientConfiguration) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await authCache.Get(cancellationToken);

            if (response.HasValue && !string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                request.Headers.Authorization ??= new System.Net.Http.Headers.AuthenticationHeaderValue($"Bearer", response.Value.AccessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}