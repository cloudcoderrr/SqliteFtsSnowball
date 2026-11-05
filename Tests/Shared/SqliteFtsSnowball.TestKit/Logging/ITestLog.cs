namespace SqliteFtsSnowball.TestKit;

/// <summary>A sink for structured test log output.</summary>
public interface ITestLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);
}

/// <summary>Convenience helpers over <see cref="ITestLog"/>.</summary>
public static class TestLogExtensions
{
    public static void Section(this ITestLog log, string title)
    {
        log.Info(string.Empty);
        log.Info("==== " + title + " ====");
    }

    public static void Result(this ITestLog log, bool passed, string message)
    {
        if (passed)
        {
            log.Info("[PASS] " + message);
        }
        else
        {
            log.Error("[FAIL] " + message);
        }
    }
}
