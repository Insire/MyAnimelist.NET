using System.Net;

namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// Retries requests that MyAnimeList rejected because of rate limiting (429) or a temporary outage (503),
    /// honoring <c>Retry-After</c> and otherwise backing off exponentially.
    /// </summary>
    internal sealed class MalRetryHandler(TimeProvider timeProvider) : DelegatingHandler
    {
        internal const int MaxRetries = 3;

        internal static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

        // a longer Retry-After than this is returned to the caller instead of blocking the request
        internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = (delay, cancellationToken) => Task.Delay(delay, timeProvider, cancellationToken);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                var response = await base.SendAsync(request, cancellationToken);

                if (attempt == MaxRetries || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                var delay = GetRetryAfter(response) ?? InitialDelay * Math.Pow(2, attempt);
                if (delay > MaxDelay)
                {
                    return response;
                }

                response.Dispose();
                await Delay(delay, cancellationToken);
            }
        }

        private static bool IsTransient(HttpStatusCode statusCode)
            => statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

        private TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;

            if (retryAfter?.Delta is { } delta)
            {
                return delta;
            }

            if (retryAfter?.Date is { } date)
            {
                var delay = date - timeProvider.GetUtcNow();
                return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
            }

            return null;
        }
    }
}
