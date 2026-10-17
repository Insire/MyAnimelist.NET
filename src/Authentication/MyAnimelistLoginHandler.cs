namespace SoftThorn.MyAnimelistNET.Authentication
{
    internal sealed class MyAnimelistLoginHandler(TokenClient tokenClient) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await tokenClient.GetValidTokenAsync(cancellationToken);

            if (response.HasValue && !string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                request.Headers.Authorization ??= new System.Net.Http.Headers.AuthenticationHeaderValue($"Bearer", response.Value.AccessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}