using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    /// <summary>
    /// Logs every API request and response with headers and (shortened) text content.
    /// It is the outermost handler, so it sees each call once, before <see cref="Authentication.MalLoginHandler"/> adds the token;
    /// the retries of a call show up in the log of <c>System.Net.Http.HttpClient.*.ClientHandler</c> and <c>MalRetryHandler</c>.
    /// </summary>
    public sealed class HttpLoggingHandler(ILogger<HttpLoggingHandler> logger) : DelegatingHandler
    {
        private const int MaxContentLength = 1024;

        private static readonly string[] TextContentTypes = ["html", "text", "xml", "json", "txt", "x-www-form-urlencoded"];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            var test = TestContext.Current.Test?.TestDisplayName ?? "(no test)";

            var message = new StringBuilder();
            message.AppendLine($"{request.Method} {request.RequestUri} HTTP/{request.Version}");
            AppendHeaders(message, request.Headers);
            await AppendContentAsync(message, request.Content, cancellationToken);
            logger.LogInformation("[{RequestId}] Request of {Test}\n{Request}", id, test, message.ToString().TrimEnd());

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "[{RequestId}] {Method} {Uri} of {Test} failed after {Duration:0}ms", id, request.Method, request.RequestUri, test, stopwatch.Elapsed.TotalMilliseconds);
                throw;
            }

            message.Clear();
            message.AppendLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
            AppendHeaders(message, response.Headers);
            await AppendContentAsync(message, response.Content, cancellationToken);
            logger.LogInformation("[{RequestId}] Response of {Test} after {Duration:0}ms\n{Response}", id, test, stopwatch.Elapsed.TotalMilliseconds, message.ToString().TrimEnd());

            return response;
        }

        private static void AppendHeaders(StringBuilder message, HttpHeaders headers)
        {
            foreach (var header in headers)
            {
                message.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        private static async Task AppendContentAsync(StringBuilder message, HttpContent? content, CancellationToken cancellationToken)
        {
            if (content is null)
            {
                return;
            }

            AppendHeaders(message, content.Headers);

            if (content is not StringContent && !IsTextBasedContentType(content.Headers))
            {
                return;
            }

            // buffers the content, so the generated client can still read it
            var text = await content.ReadAsStringAsync(cancellationToken);
            message.AppendLine();
            message.AppendLine(text.Length switch
            {
                0 => "<NO CONTENT>",
                > MaxContentLength => $"{text[..MaxContentLength]}... ({text.Length} characters)",
                _ => text,
            });
        }

        private static bool IsTextBasedContentType(HttpHeaders headers)
        {
            if (!headers.TryGetValues("Content-Type", out var values))
            {
                return false;
            }

            var header = string.Join(" ", values).ToLowerInvariant();

            return TextContentTypes.Any(header.Contains);
        }
    }
}
