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

        TestOutcome? outcome = null;
        string? error = null;
        try
        {
            outcome = await Task.Run(() =>
            {
                using var fileLog = new FileTestLog(logPath, mirrorToConsole: false);
                var relay = new InMemoryTestLog(line =>
                {
                    AppState.SystemLog(line);
                    onLine?.Invoke(line);
                });
                return TestRunner.RunAll(new CompositeTestLog(fileLog, relay));
            });
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            AppState.SystemLog("TEST RUN EXCEPTION: " + error);
            onLine?.Invoke("TEST RUN EXCEPTION: " + error);
            try
            {
                File.AppendAllText(logPath, Environment.NewLine + "TEST RUN EXCEPTION:" + Environment.NewLine + error + Environment.NewLine);
            }
            catch
            {
                // ignore secondary logging failures
            }
        }

        bool success = outcome?.Success ?? false;
        int total = outcome?.Total ?? 0;
        int passed = outcome?.Passed ?? 0;
        int failed = outcome?.Failed ?? (error is null ? 0 : 1);

        string sentinel = $"{ResultSentinelPrefix} total={total} passed={passed} " +
                          $"failed={failed} result={(success ? "PASS" : "FAIL")}";
        AppState.SystemLog(sentinel);
        onLine?.Invoke(sentinel);

        try
        {
            string body = sentinel + Environment.NewLine + "log=" + logPath + Environment.NewLine;
            if (error is not null)
            {
                body += "error=" + error + Environment.NewLine;
            }

            File.WriteAllText(Path.Combine(directory, ResultFileName), body);
        }
        catch
        {
            // best-effort; the system-log sentinel is the primary signal
        }

        return outcome ?? CreateFailedOutcome(error ?? "unknown error");
    }

    private static TestOutcome CreateFailedOutcome(string message)
    {
        var outcome = new TestOutcome();
        outcome.Add(new TestCaseResult("Driver", "test run", false, message));
        return outcome;
    }
}
