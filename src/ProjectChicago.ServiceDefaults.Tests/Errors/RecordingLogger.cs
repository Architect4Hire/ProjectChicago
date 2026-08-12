using Microsoft.Extensions.Logging;

namespace ProjectChicago.ServiceDefaults.Tests.Errors;

public sealed record LoggedEntry(LogLevel Level, Exception? Exception, string Message);

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LoggedEntry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LoggedEntry(logLevel, exception, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
