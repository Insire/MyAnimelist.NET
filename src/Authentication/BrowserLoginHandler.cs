using System.Diagnostics;

namespace SoftThorn.MyAnimelistNET.Authentication
{
    public sealed class BrowserLoginHandler : IBrowserLoginHandler
    {
        public ValueTask Handle(string authUrl)
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            return ValueTask.CompletedTask;
        }
    }
}