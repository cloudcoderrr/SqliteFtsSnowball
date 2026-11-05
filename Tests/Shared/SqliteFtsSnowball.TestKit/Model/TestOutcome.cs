namespace SqliteFtsSnowball.TestKit;

/// <summary>The result of a single logical test case.</summary>
public sealed class TestCaseResult
{
    public TestCaseResult(string suite, string name, bool passed, string? message = null)
    {
        Suite = suite;
        Name = name;
        Passed = passed;
        Message = message;
    }

    public string Suite { get; }

    public string Name { get; }

    public bool Passed { get; }

    public string? Message { get; }
}

/// <summary>The aggregated outcome of a test run.</summary>
public sealed class TestOutcome
{
    private readonly List<TestCaseResult> _results = new();

    public IReadOnlyList<TestCaseResult> Results => _results;

    public int Total { get; private set; }

    public int Passed { get; private set; }

    public int Failed { get; private set; }

    public TimeSpan Duration { get; set; }

    public bool Success => Failed == 0 && Total > 0;

    public void Add(TestCaseResult result)
    {
        _results.Add(result);
        Total++;
        if (result.Passed)
        {
            Passed++;
        }
        else
        {
            Failed++;
        }
    }

    public IEnumerable<TestCaseResult> Failures => _results.Where(r => !r.Passed);
}
