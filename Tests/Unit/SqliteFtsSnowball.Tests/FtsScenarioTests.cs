using SqliteFtsSnowball.TestKit;
using Xunit;

namespace SqliteFtsSnowball.Tests;

public class FtsScenarioTests
{
    public static IEnumerable<object[]> Scenarios =>
        TestDataProvider.ScenarioNames().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario_queries_return_expected_rows(string name)
    {
        FtsScenario? scenario = TestDataProvider.GetScenario(name);
        Assert.NotNull(scenario);

        var outcome = new TestOutcome();
        var log = new InMemoryTestLog();
        FtsScenarioTestSuite.RunScenario(scenario!, log, new TestOptions(), outcome);

        Assert.True(
            outcome.Success,
            $"Scenario '{name}' failures:\n" +
            string.Join("\n", outcome.Failures.Select(f => $"  {f.Name}: {f.Message}")));
    }
}
