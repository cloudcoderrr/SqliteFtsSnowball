using SqliteFtsSnowball.TestKit;
using Xunit;

namespace SqliteFtsSnowball.Tests;

public class StemmingTests
{
    public static IEnumerable<object[]> Languages =>
        TestDataProvider.StemmingLanguages.Select(language => new object[] { language });

    [Theory]
    [MemberData(nameof(Languages))]
    public void Stemmer_matches_official_snowball_vectors(string language)
    {
        var outcome = new TestOutcome();
        var log = new InMemoryTestLog();

        StemmingTestSuite.RunLanguage(language, log, new TestOptions(), outcome);

        Assert.True(
            outcome.Success,
            $"'{language}' stemming mismatches:\n" +
            string.Join("\n", log.Lines.Where(l => l.Contains("stem(", StringComparison.Ordinal))));
    }
}
