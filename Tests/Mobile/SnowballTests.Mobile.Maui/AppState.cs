using System;

namespace SnowballTests.Mobile.Maui;

/// <summary>
/// Process-wide hooks that the platform entry points configure so the shared test driver can
/// write to the right place, emit to the platform system log, detect CI mode, and exit.
/// </summary>
public static class AppState
{
    /// <summary>When true, the app runs the suite on launch and then terminates with a status code.</summary>
    public static bool CiMode { get; set; }

    /// <summary>Directory for log/result files (platform-specific; CI pulls from here).</summary>
    public static string? LogDirectory { get; set; }

    /// <summary>Writes a line to the platform system log (logcat on Android, stdout elsewhere).</summary>
    public static Action<string> SystemLog { get; set; } = Console.WriteLine;

    /// <summary>Terminates the process with the given exit code.</summary>
    public static Action<int> ExitProcess { get; set; } = Environment.Exit;
}
