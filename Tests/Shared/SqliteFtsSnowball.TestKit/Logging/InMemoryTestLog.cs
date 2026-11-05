using System.Collections.Concurrent;

namespace SqliteFtsSnowball.TestKit;

/// <summary>An in-memory log, handy for UI apps and for assertions about log contents.</summary>
public sealed class InMemoryTestLog : ITestLog
{
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly Action<string>? _onLine;

    public InMemoryTestLog(Action<string>? onLine = null) => _onLine = onLine;

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public void Info(string message) => Add("INFO", message);

    public void Warn(string message) => Add("WARN", message);

    public void Error(string message) => Add("ERROR", message);

    private void Add(string level, string message)
    {
        string line = $"[{level}] {message}";
        _lines.Enqueue(line);
        _onLine?.Invoke(line);
    }
}

/// <summary>Fans a single log stream out to several sinks.</summary>
public sealed class CompositeTestLog : ITestLog
{
    private readonly IReadOnlyList<ITestLog> _sinks;

    public CompositeTestLog(params ITestLog[] sinks) => _sinks = sinks;

    public void Info(string message)
    {
        foreach (ITestLog sink in _sinks)
        {
            sink.Info(message);
        }
    }

    public void Warn(string message)
    {
        foreach (ITestLog sink in _sinks)
        {
            sink.Warn(message);
        }
    }

    public void Error(string message)
    {
        foreach (ITestLog sink in _sinks)
        {
            sink.Error(message);
        }
    }
}
