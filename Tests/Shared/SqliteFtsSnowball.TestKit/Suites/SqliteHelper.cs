using System.Text;
using Microsoft.Data.Sqlite;

namespace SqliteFtsSnowball.TestKit;

/// <summary>Small helpers for driving SQLite FTS5 with the Snowball tokenizer registered.</summary>
internal static class SqliteHelper
{
    public static SqliteConnection OpenWithTokenizer(TestOptions options, out string? dbPath)
    {
        dbPath = null;
        string connectionString;
        if (options.UseFileDatabase)
        {
            string dir = options.WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "snowball-tests");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "fts-" + Guid.NewGuid().ToString("N") + ".db");
            connectionString = "Data Source=" + dbPath;
        }
        else
        {
            connectionString = "Data Source=:memory:";
        }

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        SnowballTokenizer.Register(connection.Handle!, SnowballTokenizer.DefaultName);
        return connection;
    }

    public static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static string? ScalarString(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }

    public static void CreateFtsTable(SqliteConnection connection, string table, IReadOnlyList<string> columns, string tokenize)
    {
        var builder = new StringBuilder();
        builder.Append("CREATE VIRTUAL TABLE ").Append(Quote(table)).Append(" USING fts5(");
        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Quote(columns[i]));
        }

        builder.Append(", tokenize = ").Append(QuoteLiteral(tokenize)).Append(')');
        Execute(connection, builder.ToString());
    }

    public static void InsertDocument(SqliteConnection connection, string table, IReadOnlyList<string> columns, long id, IReadOnlyList<string> values)
    {
        var builder = new StringBuilder();
        builder.Append("INSERT INTO ").Append(Quote(table)).Append("(rowid");
        foreach (string column in columns)
        {
            builder.Append(", ").Append(Quote(column));
        }

        builder.Append(") VALUES ($id");
        for (int i = 0; i < columns.Count; i++)
        {
            builder.Append(", $v").Append(i);
        }

        builder.Append(')');

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = builder.ToString();
        command.Parameters.AddWithValue("$id", id);
        for (int i = 0; i < columns.Count; i++)
        {
            command.Parameters.AddWithValue("$v" + i, i < values.Count ? values[i] : string.Empty);
        }

        command.ExecuteNonQuery();
    }

    public static List<long> Match(SqliteConnection connection, string table, string matchExpression)
    {
        var rowids = new List<long>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT rowid FROM {Quote(table)} WHERE {Quote(table)} MATCH $m ORDER BY rowid";
        command.Parameters.AddWithValue("$m", matchExpression);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rowids.Add(reader.GetInt64(0));
        }

        return rowids;
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string QuoteLiteral(string literal) => "'" + literal.Replace("'", "''") + "'";
}
