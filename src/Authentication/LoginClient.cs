using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace MyAnimelist.NET.Authentication
{
    public sealed class LoginClient
    {
        private const string ResponseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to Aniki.</p><script>window.close();</script></body></html>";
        private const string BaseUri = "https://myanimelist.net/v1/oauth2/";

        private readonly HttpClient _httpClient;
        private readonly IAuthCache _authCache;
        private readonly string _clientId;
        private readonly string _redirectUri;

        public LoginClient(HttpClient httpClient, ClientConfiguration clientConfiguration, IAuthCache authCache)
        {
            _httpClient = httpClient;
            _authCache = authCache;
            _clientId = clientConfiguration.ClientId;
            _redirectUri = clientConfiguration.RedirectUrl;
        }

        public async Task StartOAuthFlowAsync(IProgress<AuthProgress>? progressReporter = null, CancellationToken cancellationToken = default)
        {
            var codeVerifier = GenerateCodeVerifier(progressReporter);

            using var httpListener = new HttpListener();
            httpListener.Prefixes.Clear();
            httpListener.Prefixes.Add(_redirectUri);
            httpListener.Start();

            OpenBrowser(codeVerifier, progressReporter);
            var code = await GetCodeFromBrowserAuth(httpListener, progressReporter, cancellationToken);

            if (!string.IsNullOrEmpty(code))
            {
                await ExchangeCodeForTokenAsync(code, codeVerifier, cancellationToken);
                progressReporter?.Report(new AuthProgress("Authentication successful...", 1));
            }
            else
            {
                progressReporter?.Report(new AuthProgress("Authentication failed or cancelled.", 1));
            }
        }

        private async Task ExchangeCodeForTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken)
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            ]);

            var response = await _httpClient.PostAsync(BaseUri + "token", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            await _authCache.SaveAsync(authResponse, cancellationToken);
        }

        private static async Task<string?> GetCodeFromBrowserAuth(HttpListener httpListener, IProgress<AuthProgress>? progressReporter = null, CancellationToken cancellationToken = default)
        {
            progressReporter?.Report(new AuthProgress("Waiting for authentication in browser...", 0.5));

            var context = await httpListener.GetContextAsync();
            var result = context.Request.QueryString["code"];

            var buffer = Encoding.UTF8.GetBytes(ResponseString);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            context.Response.Close();
            httpListener.Stop();

            return result;
        }

        private void OpenBrowser(string codeVerifier, IProgress<AuthProgress>? progressReporter = null)
        {
            progressReporter?.Report(new AuthProgress("Redirecting to MyAnimeList...", 0.3));

            var authUrl = BaseUri + "authorize?response_type=code" +
                             $"&client_id={_clientId}" +
                             $"&code_challenge={codeVerifier}" +
                             $"&code_challenge_method=plain" +
                             $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                             $"&state={Guid.NewGuid()}";

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
        }

        private static string GenerateCodeVerifier(IProgress<AuthProgress>? progressReporter = null)
        {
            progressReporter?.Report(new AuthProgress("Preparing OAuth flow", 0.1));

            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);

            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
            output = output.Replace('+', '-');
            output = output.Replace('/', '_');
            output = output.TrimEnd('=');
            return output;
        }
    }
}