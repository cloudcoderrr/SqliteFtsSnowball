using SqliteFtsSnowball.TestKit;
using Xunit;

namespace SqliteFtsSnowball.Tests;

public class TokenizerTests
{
    [Fact]
    public void Tokenizer_behaviors_are_correct()
    {
        var outcome = new TestOutcome();
        var log = new InMemoryTestLog();
        TokenizerBehaviorTestSuite.Run(log, new TestOptions(), outcome);

        Assert.True(
            outcome.Success,
            "Tokenizer behavior failures:\n" +
            string.Join("\n", outcome.Failures.Select(f => $"  {f.Name}: {f.Message}")));
    }

    [Fact]
    public void Register_with_null_handle_throws()
    {
        Assert.Throws<ArgumentException>(() => SnowballTokenizer.Register(IntPtr.Zero));
    }

    [Fact]
    public void Unsupported_language_throws()
    {
        Assert.Throws<ArgumentException>(() => SnowballStemmers.Create("klingon"));
    }

    [Theory]
    [InlineData("english", "running", "run")]
    [InlineData("en", "running", "run")]
    [InlineData("french", "continuellement", "continuel")]
    [InlineData("german", "spielen", "spiel")]
    public void Factory_resolves_aliases_and_stems(string language, string word, string expected)
    {
        Assert.Equal(expected, SnowballStemmers.Create(language).Stem(word));
    }

    [Fact]
    public void All_advertised_languages_are_constructible()
    {
        foreach (string language in SnowballStemmers.SupportedLanguages)
        {
            Assert.NotNull(SnowballStemmers.Create(language));
        }
    }
}
