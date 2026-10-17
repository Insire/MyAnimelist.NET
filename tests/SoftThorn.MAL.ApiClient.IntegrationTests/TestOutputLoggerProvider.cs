using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    /// <summary>
    /// Writes log entries to the output of the test that is running, which ends up in the test results (trx); nothing goes to a file.
    /// Entries written outside of a test, such as during the login in the assembly fixture, are kept in memory
    /// and written to the output of the next test that logs, or added to the fixture's exception by <see cref="TakePending"/>.
    /// </summary>
    internal sealed class TestOutputLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _pending = new();

        public ILogger CreateLogger(string categoryName)
            => new TestOutputLogger(this, categoryName);

        /// <summary>
        /// Removes and returns the entries that were logged outside of a test.
        /// </summary>
        public string TakePending()
        {
            var entries = new List<string>();
            while (_pending.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            return string.Join(Environment.NewLine, entries);
        }

        public void Dispose()
        {
        }

        private void Write(string entry)
        {
            if (TestContext.Current.TestOutputHelper is not { } output)
            {
                _pending.Enqueue(entry);
                return;
            }

            try
            {
                if (TakePending() is { Length: > 0 } pending)
                {
                    output.WriteLine("Logged before this test:");
                    output.WriteLine(pending);
                    output.WriteLine(string.Empty);
                }

                output.WriteLine(entry);
            }
            catch (InvalidOperationException)
            {
                // the test has already finished, e.g. for a background request that outlived it
                _pending.Enqueue(entry);
            }
        }

        private sealed class TestOutputLogger(TestOutputLoggerProvider provider, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var entry = $"{DateTimeOffset.Now:HH:mm:ss.fff} [{logLevel}] {categoryName}: {formatter(state, exception)}";
                provider.Write(exception is null ? entry : entry + Environment.NewLine + exception);
            }
        }
    }
}
