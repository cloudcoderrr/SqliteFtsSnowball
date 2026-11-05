using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using SqliteFtsSnowball.TestKit;

namespace SnowballTests.Mobile.Maui;

/// <summary>
/// Runs the shared <see cref="TestRunner"/> suite, mirroring output to the UI, the platform
/// system log, and a log file, and writing a machine-readable result file that CI collects.
/// </summary>
public static class MobileTestDriver
{
    public const string ResultSentinelPrefix = "SNOWBALL_RESULT";
    public const string ResultFileName = "snowball-result.txt";

    public static async Task<TestOutcome> RunAsync(Action<string>? onLine = null)
    {
        string directory = AppState.LogDirectory ?? FileSystem.Current.AppDataDirectory;
        Directory.CreateDirectory(directory);

        string platform = DeviceInfo.Current.Platform.ToString().ToLowerInvariant();
        string logPath = Path.Combine(directory, $"mobile-{platform}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

        TestOutcome outcome = await Task.Run(() =>
        {
            using var fileLog = new FileTestLog(logPath, mirrorToConsole: false);
            var relay = new InMemoryTestLog(line =>
            {
                AppState.SystemLog(line);
                onLine?.Invoke(line);
            });
            return TestRunner.RunAll(new CompositeTestLog(fileLog, relay));
        });

        string sentinel = $"{ResultSentinelPrefix} total={outcome.Total} passed={outcome.Passed} " +
                          $"failed={outcome.Failed} result={(outcome.Success ? "PASS" : "FAIL")}";
        AppState.SystemLog(sentinel);
        onLine?.Invoke(sentinel);

        try
        {
            File.WriteAllText(
                Path.Combine(directory, ResultFileName),
                sentinel + Environment.NewLine + "log=" + logPath + Environment.NewLine);
        }
        catch
        {
            // best-effort; the system-log sentinel is the primary signal
        }

        return outcome;
    }
}
