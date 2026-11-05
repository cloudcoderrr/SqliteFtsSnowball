using System;
using System.Linq;
using Avalonia;

namespace SnowballTests.Desktop.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppState.CiMode = args.Contains("--ci")
            || string.Equals(Environment.GetEnvironmentVariable("SNOWBALL_CI"), "1", StringComparison.Ordinal);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

/// <summary>Process-wide flags shared between the entry point and the window.</summary>
internal static class AppState
{
    /// <summary>When true, the app runs the full suite on launch and then exits with a status code.</summary>
    public static bool CiMode { get; set; }
}
