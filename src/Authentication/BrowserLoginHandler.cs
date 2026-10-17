using System.Diagnostics;

namespace SoftThorn.MAL.ApiClient.Authentication
{
    // the default IBrowserLoginHandler: opens the login page in the user's default browser
    internal sealed class BrowserLoginHandler : IBrowserLoginHandler
    {
        public ValueTask HandleAsync(string authUrl, CancellationToken cancellationToken = default)
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            return ValueTask.CompletedTask;
        }
    }
}
