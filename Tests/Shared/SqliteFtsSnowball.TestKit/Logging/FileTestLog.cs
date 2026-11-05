using System.Collections.Concurrent;
using System.Text;

namespace SqliteFtsSnowball.TestKit;

/// <summary>
/// Writes timestamped log lines to a file (and optionally mirrors them to the console and an
/// in-memory buffer so UI apps can display them). Thread-safe and flushes on every write so a
/// crash still leaves a complete log for CI to inspect.
/// </summary>
public sealed class FileTestLog : ITestLog, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly bool _mirrorToConsole;
    private readonly ConcurrentQueue<string>? _buffer;

    public FileTestLog(string path, bool mirrorToConsole = true, bool keepInMemory = false)
    {
        FilePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        _writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
        _mirrorToConsole = mirrorToConsole;
        _buffer = keepInMemory ? new ConcurrentQueue<string>() : null;
    }

    public string FilePath { get; }

    /// <summary>A snapshot of the in-memory buffer (empty unless <c>keepInMemory</c> was set).</summary>
    public IReadOnlyCollection<string> Lines => _buffer?.ToArray() ?? Array.Empty<string>();

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}";
        lock (_gate)
        {
            _writer.WriteLine(line);
        }

        _buffer?.Enqueue(line);
        if (_mirrorToConsole)
        {
            Console.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
