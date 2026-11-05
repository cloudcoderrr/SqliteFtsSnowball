using Microsoft.Data.Sqlite;

namespace SqliteFtsSnowball.TestKit;

/// <summary>
/// Focused checks for specific tokenizer behaviors: case folding, stemming on both the index
/// and query side, snippet offset fidelity, the <c>none</c> (no-stem) mode, diacritic removal,
/// per-column matching, numbers, prefix/phrase queries, and non-Latin scripts.
/// </summary>
public static class TokenizerBehaviorTestSuite
{
    public static void Run(ITestLog log, TestOptions options, TestOutcome outcome)
    {
        log.Section("Tokenizer behavior");
        using SqliteConnection connection = SqliteHelper.OpenWithTokenizer(options, out _);

        int tableSeq = 0;
        string NewTable(string tokenize, string[] columns, params (long id, string[] values)[] docs)
        {
            string table = "t" + tableSeq++;
            SqliteHelper.CreateFtsTable(connection, table, columns, tokenize);
            foreach ((long id, string[] values) in docs)
            {
                SqliteHelper.InsertDocument(connection, table, columns, id, values);
            }

            return table;
        }

        bool Matches(string table, string match) => SqliteHelper.Match(connection, table, match).Count > 0;

        void Check(string name, bool passed, string detail)
        {
            outcome.Add(new TestCaseResult("Tokenizer", name, passed, detail));
            log.Result(passed, $"{name}: {detail}");
        }

        // sqlite_version is logged by the runner; prove FTS5 is present here too.
        try
        {
            // 1. Stemming applies on both sides + case folding.
            string t1 = NewTable("snowball english", new[] { "body" }, (1, new[] { "The RUNNERS were Running quickly" }));
            Check("case-fold + stem (index)", Matches(t1, "run"), "MATCH 'run' finds 'Running'");
            Check("case-fold query", Matches(t1, "RUN"), "MATCH 'RUN' finds 'Running'");
            Check("stem quickly->quick", Matches(t1, "quick"), "MATCH 'quick' finds 'quickly'");
            Check("stem runners->runner", Matches(t1, "runner"), "MATCH 'runner' finds 'RUNNERS'");

            // 2. Snippet keeps original-text offsets.
            using (SqliteCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT snippet(\"{t1}\", 0, '[', ']', '', 10) FROM \"{t1}\" WHERE \"{t1}\" MATCH 'run'";
                string snippet = cmd.ExecuteScalar() as string ?? string.Empty;
                Check("snippet offsets", snippet.Contains("[Running]"), $"snippet='{snippet}'");
            }

            // 3. 'none' language => no stemming.
            string t2 = NewTable("snowball none", new[] { "body" }, (1, new[] { "running" }));
            Check("none: exact matches", Matches(t2, "running"), "MATCH 'running' finds 'running'");
            Check("none: no stemming", !Matches(t2, "run"), "MATCH 'run' does NOT find 'running'");

            // 4. Diacritic removal option.
            string t3 = NewTable("snowball none remove_diacritics 1", new[] { "body" }, (1, new[] { "caf\u00e9 r\u00e9sum\u00e9" }));
            Check("remove_diacritics", Matches(t3, "cafe") && Matches(t3, "resume"), "ASCII query matches accented text");
            string t3b = NewTable("snowball none", new[] { "body" }, (1, new[] { "caf\u00e9" }));
            Check("diacritics preserved by default", !Matches(t3b, "cafe"), "default keeps 'caf\u00e9' distinct from 'cafe'");

            // 5. Per-column matching.
            string t4 = NewTable("snowball english", new[] { "title", "body" }, (1, new[] { "Running", "leather shoes" }));
            Check("column filter positive", Matches(t4, "title:run"), "title:run matches title");
            Check("column filter negative", !Matches(t4, "body:run"), "body:run does not match body");
            Check("column filter other", Matches(t4, "body:shoe"), "body:shoe matches 'shoes'");

            // 6. Numbers are tokenized.
            string t5 = NewTable("snowball english", new[] { "body" }, (1, new[] { "Order 12345 shipped" }));
            Check("numbers tokenized", Matches(t5, "12345"), "MATCH '12345' finds the number");

            // 7. Prefix and phrase queries.
            string t6 = NewTable("snowball none", new[] { "body" }, (1, new[] { "quick brown fox" }));
            Check("prefix query", Matches(t6, "bro*"), "MATCH 'bro*' finds 'brown'");
            Check("phrase query positive", Matches(t6, "\"quick brown\""), "phrase 'quick brown' matches");
            Check("phrase query negative", !Matches(t6, "\"brown quick\""), "reversed phrase does not match");

            // 8. Non-Latin scripts (Russian, Greek, Arabic) tokenize and stem.
            CheckScript(connection, NewTable, Matches, Check, "russian", "\u043a\u043d\u0438\u0433\u0438");   // книги
            CheckScript(connection, NewTable, Matches, Check, "greek", "\u03b1\u03bd\u03b8\u03c1\u03ce\u03c0\u03bf\u03c5\u03c2"); // ανθρώπους
            CheckScript(connection, NewTable, Matches, Check, "arabic", "\u0627\u0644\u0643\u062a\u0627\u0628"); // الكتاب
        }
        catch (Exception ex)
        {
            Check("tokenizer suite", false, "threw: " + ex);
        }
    }

    private static void CheckScript(
        SqliteConnection connection,
        Func<string, string[], (long, string[])[], string> newTable,
        Func<string, string, bool> matches,
        Action<string, bool, string> check,
        string language,
        string word)
    {
        // Insert a word, then query with its (validated) stem: the tokenizer must match it.
        string stem = SnowballStemmers.Create(language).Stem(word);
        string table = newTable($"snowball {language}", new[] { "body" }, new (long, string[])[] { (1, new[] { word }) });
        check($"script:{language}", matches(table, stem), $"insert '{word}', MATCH stem '{stem}'");
    }
}
