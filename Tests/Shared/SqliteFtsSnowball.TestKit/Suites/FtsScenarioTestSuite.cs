namespace SqliteFtsSnowball.TestKit;

/// <summary>
/// Runs the embedded end-to-end FTS5 search scenarios: for each scenario it builds an FTS5
/// table with the Snowball tokenizer, inserts the documents, and asserts that each MATCH query
/// returns exactly the expected rowids.
/// </summary>
public static class FtsScenarioTestSuite
{
    public static void Run(ITestLog log, TestOptions options, TestOutcome outcome)
    {
        IReadOnlyList<FtsScenario> scenarios = TestDataProvider.ReadFtsScenarios();
        log.Section("FTS5 search scenarios (" + scenarios.Count + ")");

        foreach (FtsScenario scenario in scenarios)
        {
            RunScenario(scenario, log, options, outcome);
        }
    }

    /// <summary>Builds the table, inserts documents, and verifies every query for one scenario.</summary>
    public static void RunScenario(FtsScenario scenario, ITestLog log, TestOptions options, TestOutcome outcome)
    {
        try
        {
            using Microsoft.Data.Sqlite.SqliteConnection connection = SqliteHelper.OpenWithTokenizer(options, out _);
            SqliteHelper.CreateFtsTable(connection, "docs", scenario.Columns, scenario.Tokenize);
            foreach (FtsDocument document in scenario.Documents)
            {
                SqliteHelper.InsertDocument(connection, "docs", scenario.Columns, document.Id, document.Values);
            }

            log.Info($"scenario '{scenario.Name}' tokenize='{scenario.Tokenize}' columns=[{string.Join(",", scenario.Columns)}] docs={scenario.Documents.Count}");

            foreach (FtsQuery query in scenario.Queries)
            {
                List<long> got = SqliteHelper.Match(connection, "docs", query.Match);
                List<long> expected = query.Expected.OrderBy(x => x).ToList();
                got.Sort();
                bool ok = expected.SequenceEqual(got);
                string detail = $"MATCH '{query.Match}' -> [{string.Join(",", got)}] expected [{string.Join(",", expected)}]"
                    + (query.Note is null ? string.Empty : "  // " + query.Note);
                outcome.Add(new TestCaseResult("Fts:" + scenario.Name, query.Match, ok, detail));
                log.Result(ok, scenario.Name + ": " + detail);
            }
        }
        catch (Exception ex)
        {
            outcome.Add(new TestCaseResult("Fts:" + scenario.Name, "(scenario setup)", false, ex.Message));
            log.Error($"scenario '{scenario.Name}' threw: {ex}");
        }
    }
}
