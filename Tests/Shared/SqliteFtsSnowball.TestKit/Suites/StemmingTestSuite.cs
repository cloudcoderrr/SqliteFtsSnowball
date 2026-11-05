using System.Diagnostics;

namespace SqliteFtsSnowball.TestKit;

/// <summary>
/// Verifies every bundled Snowball stemmer against the official snowball-data vectors
/// embedded in this assembly (word -> expected stem). This is the independent correctness
/// baseline for the stemming algorithms.
/// </summary>
public static class StemmingTestSuite
{
    public static void Run(ITestLog log, TestOptions options, TestOutcome outcome)
    {
        log.Section("Stemming vectors (" + TestDataProvider.StemmingLanguages.Count + " languages)");

        long totalPairs = 0;
        long totalMismatch = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (string language in TestDataProvider.StemmingLanguages)
        {
            (int count, int mismatch) = RunLanguage(language, log, options, outcome);
            totalPairs += count;
            totalMismatch += mismatch;
        }

        stopwatch.Stop();
        log.Info($"Stemming totals: {totalPairs - totalMismatch}/{totalPairs} correct in {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>Verifies a single language's vectors. Returns (checked, mismatched).</summary>
    public static (int Count, int Mismatch) RunLanguage(string language, ITestLog log, TestOptions options, TestOutcome outcome)
    {
        if (!SnowballStemmers.TryGet(language, out SnowballLanguage? info))
        {
            outcome.Add(new TestCaseResult("Stemming", language, false, "language not supported by the library"));
            log.Result(false, $"{language}: NOT SUPPORTED by SnowballStemmers");
            return (0, 1);
        }

        var stemmer = info!.CreateStemmer();
        int count = 0;
        int mismatch = 0;
        int logged = 0;

        foreach (StemmingPair pair in TestDataProvider.ReadStemmingPairs(language))
        {
            if (options.MaxStemmingPairsPerLanguage > 0 && count >= options.MaxStemmingPairsPerLanguage)
            {
                break;
            }

            count++;
            string actual = stemmer.Stem(pair.Word);
            if (!string.Equals(actual, pair.ExpectedStem, StringComparison.Ordinal))
            {
                mismatch++;
                if (logged < options.MaxLoggedFailuresPerSuite)
                {
                    logged++;
                    log.Error($"  {language}: stem('{pair.Word}') = '{actual}', expected '{pair.ExpectedStem}'");
                }
            }
        }

        outcome.Add(new TestCaseResult("Stemming", language, mismatch == 0, $"{count - mismatch}/{count} correct"));
        log.Result(mismatch == 0, $"{language,-20} {count - mismatch}/{count} correct");
        return (count, mismatch);
    }
}
