using System.Runtime.InteropServices;
using SqliteFtsSnowball.TestKit;
using Xunit;

namespace SqliteFtsSnowball.Tests;

public class FullSuiteTests
{
    [Fact]
    public void Full_suite_passes_and_writes_a_log_file()
    {
        string directory = Environment.GetEnvironmentVariable("SNOWBALL_LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "test-logs");
        Directory.CreateDirectory(directory);

        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string fileName = $"desktop-xunit-{arch}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log";
        string path = Path.Combine(directory, fileName);

        using var log = new FileTestLog(path, mirrorToConsole: false);
        TestOutcome outcome = TestRunner.RunAll(log, new TestOptions());

        Assert.True(
            outcome.Success,
            $"Full suite failed (see {path}). Failures:\n" +
            string.Join("\n", outcome.Failures.Select(f => $"  {f.Suite}/{f.Name}: {f.Message}")));
        Assert.True(File.Exists(path), "Expected a log file to be written.");
    }
}
