using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SqliteFtsSnowball.TestKit;

/// <summary>A single word / expected-stem pair.</summary>
public readonly record struct StemmingPair(string Word, string ExpectedStem);

/// <summary>
/// Loads the embedded ground-truth test data (stemming vectors and FTS scenarios) that is
/// compiled into the TestKit assembly, so the suite runs identically on every platform.
/// </summary>
public static class TestDataProvider
{
    private const string StemmingPrefix = "snowball.stemming.";
    private const string FtsPrefix = "snowball.fts.";
    private const string ManifestName = "snowball.manifest.json";

    private static readonly Assembly Assembly = typeof(TestDataProvider).Assembly;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The languages that have an embedded stemming vector file.</summary>
    public static IReadOnlyList<string> StemmingLanguages { get; } = DiscoverStemmingLanguages();

    /// <summary>Reads the embedded manifest JSON (pinned commit, counts, …).</summary>
    public static string? ReadManifest() => ReadResource(ManifestName);

    /// <summary>Enumerates the word / expected-stem pairs for a language.</summary>
    public static IEnumerable<StemmingPair> ReadStemmingPairs(string language)
    {
        string resource = StemmingPrefix + language + ".tsv";
        string? content = ReadResource(resource);
        if (content is null)
        {
            yield break;
        }

        foreach (string raw in SplitLines(content))
        {
            int tab = raw.IndexOf('\t');
            if (tab <= 0)
            {
                continue;
            }

            yield return new StemmingPair(raw.Substring(0, tab), raw.Substring(tab + 1));
        }
    }

    /// <summary>Loads all embedded FTS scenarios, ordered by name.</summary>
    public static IReadOnlyList<FtsScenario> ReadFtsScenarios()
    {
        var scenarios = new List<FtsScenario>();
        foreach (string name in Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(FtsPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string? json = ReadResource(name);
            if (json is null)
            {
                continue;
            }

            FtsScenario? scenario = JsonSerializer.Deserialize<FtsScenario>(json, JsonOptions);
            if (scenario is not null)
            {
                scenarios.Add(scenario);
            }
        }

        scenarios.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return scenarios;
    }

    /// <summary>The names of all embedded FTS scenarios, ordered.</summary>
    public static IReadOnlyList<string> ScenarioNames()
    {
        var names = ReadFtsScenarios().Select(s => s.Name).ToList();
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Loads a single FTS scenario by name (case-sensitive), or null if not found.</summary>
    public static FtsScenario? GetScenario(string name) =>
        ReadFtsScenarios().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    private static IReadOnlyList<string> DiscoverStemmingLanguages()
    {
        var languages = new List<string>();
        foreach (string name in Assembly.GetManifestResourceNames())
        {
            if (name.StartsWith(StemmingPrefix, StringComparison.Ordinal) && name.EndsWith(".tsv", StringComparison.Ordinal))
            {
                languages.Add(name.Substring(StemmingPrefix.Length, name.Length - StemmingPrefix.Length - ".tsv".Length));
            }
        }

        languages.Sort(StringComparer.Ordinal);
        return languages;
    }

    private static string? ReadResource(string name)
    {
        using Stream? stream = Assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitLines(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }
}
