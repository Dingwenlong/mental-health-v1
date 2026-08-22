using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MentalHealth.IntegrationTests.Auth;

public sealed record TestLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message);

public sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<TestLogEntry> entries = new();

    public IReadOnlyList<TestLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, entries);

    public void Clear()
    {
        while (entries.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string category,
        ConcurrentQueue<TestLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new TestLogEntry(
                category,
                logLevel,
                eventId,
                formatter(state, exception)));
        }
    }
}
