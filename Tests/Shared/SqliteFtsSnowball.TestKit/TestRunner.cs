using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace SqliteFtsSnowball.TestKit;

/// <summary>The entry point that runs every suite, logs an environment banner, and summarizes.</summary>
public static class TestRunner
{
    public static TestOutcome RunAll(ITestLog log, TestOptions? options = null)
    {
        options ??= new TestOptions();
        var outcome = new TestOutcome();
        var stopwatch = Stopwatch.StartNew();

        LogEnvironment(log, options);

        if (options.RunStemming)
        {
            StemmingTestSuite.Run(log, options, outcome);
        }

        if (options.RunFtsScenarios)
        {
            FtsScenarioTestSuite.Run(log, options, outcome);
        }

        if (options.RunTokenizerTests)
        {
            TokenizerBehaviorTestSuite.Run(log, options, outcome);
        }

        stopwatch.Stop();
        outcome.Duration = stopwatch.Elapsed;
        LogSummary(log, outcome);
        return outcome;
    }

    private static void LogEnvironment(ITestLog log, TestOptions options)
    {
        log.Section("SqliteFtsSnowball test run");
        log.Info("UTC time        : " + DateTime.UtcNow.ToString("O"));
        log.Info("OS              : " + RuntimeInformation.OSDescription);
        log.Info("OS arch         : " + RuntimeInformation.OSArchitecture);
        log.Info("Process arch    : " + RuntimeInformation.ProcessArchitecture);
        log.Info("Framework       : " + RuntimeInformation.FrameworkDescription);
        log.Info("Database mode   : " + (options.UseFileDatabase ? "file" : "in-memory"));

        string? manifest = TestDataProvider.ReadManifest();
        if (manifest is not null)
        {
            log.Info("Test data       : " + manifest.Replace("\r", string.Empty).Replace("\n", " ").Trim());
        }

        log.Info("Stemmer languages: " + SnowballStemmers.SupportedLanguages.Count + " (" + string.Join(", ", SnowballStemmers.SupportedLanguages) + ")");

        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            string? version = SqliteHelper.ScalarString(connection, "SELECT sqlite_version()");
            string? fts5 = SqliteHelper.ScalarString(connection, "SELECT CASE WHEN sqlite_compileoption_used('ENABLE_FTS5') THEN 'yes' ELSE 'no' END");
            log.Info("SQLite version  : " + version + " (FTS5=" + fts5 + ")");
        }
        catch (Exception ex)
        {
            log.Warn("Could not query SQLite version: " + ex.Message);
        }
    }

    private static void LogSummary(ITestLog log, TestOutcome outcome)
    {
        log.Section("Summary");
        foreach (TestCaseResult failure in outcome.Failures)
        {
            log.Error($"FAILED {failure.Suite} / {failure.Name}: {failure.Message}");
        }

        log.Info($"Total {outcome.Total}, passed {outcome.Passed}, failed {outcome.Failed}, duration {outcome.Duration.TotalSeconds:F1}s");
        log.Info("RESULT: " + (outcome.Success ? "PASS" : "FAIL"));
    }
}
