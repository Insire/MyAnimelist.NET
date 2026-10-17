using System.Diagnostics;
using System.Net.Http.Headers;

namespace SoftThorn.MyAnimelistNET.IntegrationTests
{
    public sealed class HttpLoggingHandler : DelegatingHandler
    {
        private static void WriteLine(string msg)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(msg);
            Debug.WriteLine(msg);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var req = request;
            var id = Guid.NewGuid().ToString();
            var msg = $"[{id} -  Request]";
            var pathAndQuery = req.RequestUri?.PathAndQuery ?? "";
            var scheme = req.RequestUri?.Scheme ?? "";
            var host = req.RequestUri?.Host ?? "";

            WriteLine($"{msg}========Start==========");
            WriteLine($"{msg} {req.Method} {pathAndQuery} {scheme}/{req.Version}");
            WriteLine($"{msg} Host: {scheme}://{host}");

            foreach (var header in req.Headers)
            {
                WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (req.Content != null)
            {
                foreach (var header in req.Content.Headers)
                {
                    WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
                }

                if (req.Content is StringContent || IsTextBasedContentType(req.Headers) || IsTextBasedContentType(req.Content.Headers))
                {
                    var result = await req.Content.ReadAsStringAsync(cancellationToken);

                    WriteLine($"{msg} Content:");
                    WriteLine($"{msg} {string.Join("", result.Cast<char>().Take(255))}...");
                }
            }

            var start = DateTime.Now;

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var end = DateTime.Now;

            WriteLine($"{msg} Duration: {end - start}");
            WriteLine($"{msg}==========End==========");

            msg = $"[{id} - Response]";
            WriteLine($"{msg}=========Start=========");

            var resp = response;

            WriteLine($"{msg} {scheme.ToUpper()}/{resp.Version} {(int)resp.StatusCode} {resp.ReasonPhrase}");

            foreach (var header in resp.Headers)
            {
                WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (resp.Content != null)
            {
                foreach (var header in resp.Content.Headers)
                {
                    WriteLine($"{msg} {header.Key}: {string.Join(", ", header.Value)}");
                }

                if (resp.Content is StringContent || IsTextBasedContentType(resp.Headers) || IsTextBasedContentType(resp.Content.Headers))
                {
                    start = DateTime.Now;
                    var result = await resp.Content.ReadAsStringAsync(cancellationToken);
                    end = DateTime.Now;

                    WriteLine($"{msg} Content:");
                    if (result.Length == 0)
                    {
                        WriteLine($"{msg} <NO CONTENT>");
                    }
                    else
                    {
                        WriteLine($"{msg} {string.Join("", result.Cast<char>().Take(1024))}...");
                    }

                    WriteLine($"{msg} Duration: {end - start}");
                }
            }

            WriteLine($"{msg}==========End==========");
            return response;
        }

        private readonly string[] _types = new[] { "html", "text", "xml", "json", "txt", "x-www-form-urlencoded" };

        private bool IsTextBasedContentType(HttpHeaders headers)
        {
            if (!headers.TryGetValues("Content-Type", out var values))
            {
                return false;
            }

            var header = string.Join(" ", values).ToLowerInvariant();

            return _types.Any(t => header.Contains(t));
        }
    }
}