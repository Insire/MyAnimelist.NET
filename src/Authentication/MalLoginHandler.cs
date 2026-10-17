namespace SoftThorn.MAL.ApiClient.Authentication
{
    internal sealed class MalLoginHandler(TokenClient tokenClient, ClientConfiguration clientConfiguration) : DelegatingHandler
    {
        // MAL authorizes requests without a signed-in user by the Client ID; only the public endpoints accept that
        internal const string ClientIdHeaderName = "X-MAL-CLIENT-ID";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await tokenClient.GetValidTokenAsync(cancellationToken);

            if (response.HasValue && !string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                request.Headers.Authorization ??= new System.Net.Http.Headers.AuthenticationHeaderValue($"Bearer", response.Value.AccessToken);
            }
            else if (request.Headers.Authorization is null && !request.Headers.Contains(ClientIdHeaderName))
            {
                request.Headers.Add(ClientIdHeaderName, clientConfiguration.ClientId);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
