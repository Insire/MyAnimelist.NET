using System.Globalization;
using System.Net;

namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// Retries requests that MyAnimeList rejected because of rate limiting (429) or a temporary outage (503).
    /// Without <c>Retry-After</c> it backs off exponentially with jitter (0.5-1s, 1-2s, 2-4s), so clients that were
    /// limited together don't retry in lockstep. With <c>Retry-After</c> it waits at least that long, plus up to 1s of jitter.
    /// Idempotent requests also get a per-attempt timeout, because MAL occasionally never answers a request;
    /// such an attempt is cancelled and retried instead of blocking until <see cref="HttpClient.Timeout"/>.
    /// </summary>
    internal sealed class MalRetryHandler(TimeProvider timeProvider) : DelegatingHandler
    {
        internal const int MaxRetries = 3;

        internal static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

        // a longer Retry-After than this is returned to the caller instead of blocking the request
        internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        // all attempts plus the backoff (4 x 20s + at most 7s) fit into HttpClient's default timeout of 100s
        internal static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(20);

        internal TimeSpan AttemptTimeout { get; init; } = DefaultAttemptTimeout;

        internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = (delay, cancellationToken) => Task.Delay(delay, timeProvider, cancellationToken);

        // a value in [0, 1) that spreads the waits; replaceable so tests are deterministic
        internal Func<double> Jitter { get; init; } = Random.Shared.NextDouble;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // a POST or PATCH that timed out may still have reached MAL (a refresh token may be used up), so only these are retried
            var isIdempotent = IsIdempotent(request.Method);

            for (var attempt = 0; ; attempt++)
            {
                var response = isIdempotent
                    ? await SendWithAttemptTimeoutAsync(request, cancellationToken)
                    : await base.SendAsync(request, cancellationToken);

                if (response is null)
                {
                    if (attempt == MaxRetries)
                    {
                        var message = string.Format(CultureInfo.InvariantCulture, "MyAnimeList didn't answer {0} {1} within {2}s, {3} times in a row.", request.Method, request.RequestUri, AttemptTimeout.TotalSeconds, MaxRetries + 1);
                        throw new TaskCanceledException(message, new TimeoutException(message));
                    }

                    await Delay(GetBackoff(attempt), cancellationToken);
                    continue;
                }

                if (attempt == MaxRetries || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                var retryAfter = GetRetryAfter(response);
                if (retryAfter > MaxDelay)
                {
                    return response;
                }

                var delay = retryAfter is { } minimum
                    ? minimum + (InitialDelay * Jitter())
                    : GetBackoff(attempt);

                response.Dispose();
                await Delay(delay, cancellationToken);
            }
        }

        // returns null when the attempt timed out; the caller's own cancellation still throws
        private async Task<HttpResponseMessage?> SendWithAttemptTimeoutAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var attemptTimeout = new CancellationTokenSource(AttemptTimeout, timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptTimeout.Token);

            try
            {
                return await base.SendAsync(request, linked.Token);
            }
            catch (OperationCanceledException) when (attemptTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        // "equal jitter": half of the exponential delay is fixed, the other half random, so a retry never comes right back
        private TimeSpan GetBackoff(int attempt)
        {
            var exponential = InitialDelay * Math.Pow(2, attempt);
            return (exponential / 2) + (exponential / 2 * Jitter());
        }

        private static bool IsIdempotent(HttpMethod method)
            => method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options || method == HttpMethod.Put || method == HttpMethod.Delete;

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
