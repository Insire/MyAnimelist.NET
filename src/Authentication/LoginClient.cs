using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MyAnimelist.NET.Authentication
{
    public sealed class LoginClient
    {
        private const string ResponseString = "<html><head><title>Auth Success</title></head><body><h1>Authentication successful!</h1><p>You can close this browser tab/window and return to the application.</p><script>window.close();</script></body></html>";
        private const string FailedResponseString = "<html><head><title>Auth Failed</title></head><body><h1>Authentication failed.</h1><p>You can close this browser tab/window and return to the application.</p></body></html>";
        private readonly IBrowserLoginHandler _loginHandler;
        private readonly TokenClient _tokenClient;
        private readonly string _clientId;
        private readonly string _redirectUri;

        public LoginClient(ClientConfiguration clientConfiguration, IBrowserLoginHandler loginHandler, TokenClient tokenClient)
        {
            _loginHandler = loginHandler;
            _tokenClient = tokenClient;
            _clientId = clientConfiguration.ClientId;
            _redirectUri = clientConfiguration.RedirectUrl;
        }

        /// <summary>
        /// Signs the user in, unless a valid token is already cached or an expired one can be refreshed.
        /// </summary>
        /// <returns><see langword="true"/> when a valid token is available afterwards.</returns>
        public async Task<bool> StartOAuthFlowAsync(IProgress<AuthProgress>? progressReporter = null, CancellationToken cancellationToken = default)
        {
            var response = await _tokenClient.GetValidTokenAsync(cancellationToken);
            if (response is not null)
            {
                return true;
            }

            var codeVerifier = GenerateCodeVerifier(progressReporter);
            var state = Guid.NewGuid().ToString("N");

            using var httpListener = new HttpListener();
            httpListener.Prefixes.Clear();
            httpListener.Prefixes.Add(_redirectUri);
            httpListener.Start();

            // the login handler (e.g. browser automation) may only complete once the redirect was answered,
            // so wait for the redirect and the handler concurrently and surface handler failures instead of hanging
            var contextTask = httpListener.GetContextAsync().WaitAsync(cancellationToken);
            var loginTask = OpenBrowser(codeVerifier, state, progressReporter).AsTask();

            progressReporter?.Report(new AuthProgress("Waiting for authentication in browser...", 0.5));

            var completedTask = await Task.WhenAny(contextTask, loginTask);
            if (completedTask == loginTask)
            {
                await loginTask; // rethrows handler failures
            }

            var code = await GetCodeFromBrowserAuth(await contextTask, state, cancellationToken);
            httpListener.Stop();

            await loginTask;

            if (!string.IsNullOrEmpty(code) && await _tokenClient.ExchangeCodeAsync(code, codeVerifier, _redirectUri, cancellationToken))
            {
                progressReporter?.Report(new AuthProgress("Authentication successful...", 1));
                return true;
            }

            progressReporter?.Report(new AuthProgress("Authentication failed or cancelled.", 1));
            return false;
        }

        private static async Task<string?> GetCodeFromBrowserAuth(HttpListenerContext context, string expectedState, CancellationToken cancellationToken)
        {
            var query = context.Request.QueryString;
            var isValid = query["state"] == expectedState && string.IsNullOrEmpty(query["error"]);

            var buffer = Encoding.UTF8.GetBytes(isValid ? ResponseString : FailedResponseString);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            context.Response.Close();

            return isValid ? query["code"] : null;
        }

        private ValueTask OpenBrowser(string codeVerifier, string state, IProgress<AuthProgress>? progressReporter = null)
        {
            progressReporter?.Report(new AuthProgress("Redirecting to MyAnimeList...", 0.3));

            var authUrl = TokenClient.BaseUri + "authorize?response_type=code" +
                             $"&client_id={_clientId}" +
                             $"&code_challenge={codeVerifier}" +
                             $"&code_challenge_method=plain" +
                             $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                             $"&state={state}";

            return _loginHandler.Handle(authUrl);
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
            var builder = new StringBuilder(output.TrimEnd('='));

            builder.Replace('+', '-');
            builder.Replace('/', '_');

            return builder.ToString();
        }
    }
}