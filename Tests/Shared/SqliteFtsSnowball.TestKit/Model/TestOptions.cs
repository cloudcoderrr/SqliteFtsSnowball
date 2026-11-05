namespace SqliteFtsSnowball.TestKit;

/// <summary>Controls which test suites run and how.</summary>
public sealed class TestOptions
{
    /// <summary>Maximum stemming pairs to check per language (0 = all available).</summary>
    public int MaxStemmingPairsPerLanguage { get; set; }

    /// <summary>When true, use an on-disk database file rather than an in-memory database.</summary>
    public bool UseFileDatabase { get; set; }

    /// <summary>Run the direct Snowball stemming vector suite.</summary>
    public bool RunStemming { get; set; } = true;

    /// <summary>Run the end-to-end FTS5 search scenario suite.</summary>
    public bool RunFtsScenarios { get; set; } = true;

    /// <summary>Run the focused tokenizer-behavior suite.</summary>
    public bool RunTokenizerTests { get; set; } = true;

    /// <summary>How many individual stemming mismatches to log per language before truncating.</summary>
    public int MaxLoggedFailuresPerSuite { get; set; } = 25;

    /// <summary>Optional working directory for temporary database files.</summary>
    public string? WorkingDirectory { get; set; }
}
