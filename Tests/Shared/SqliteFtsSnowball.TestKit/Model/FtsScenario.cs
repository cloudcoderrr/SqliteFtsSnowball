using System.Text.Json.Serialization;

namespace SqliteFtsSnowball.TestKit;

/// <summary>An end-to-end FTS5 search scenario loaded from <c>Tests/Data/fts/*.json</c>.</summary>
public sealed class FtsScenario
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The FTS5 <c>tokenize=</c> argument, e.g. <c>snowball english</c>.</summary>
    public string Tokenize { get; set; } = "snowball english";

    public List<string> Columns { get; set; } = new();

    public List<FtsDocument> Documents { get; set; } = new();

    public List<FtsQuery> Queries { get; set; } = new();
}

public sealed class FtsDocument
{
    public long Id { get; set; }

    public List<string> Values { get; set; } = new();
}

public sealed class FtsQuery
{
    /// <summary>The FTS5 MATCH expression.</summary>
    public string Match { get; set; } = string.Empty;

    /// <summary>The rowids expected to match, in any order.</summary>
    public List<long> Expected { get; set; } = new();

    /// <summary>Optional human-readable description of what the query verifies.</summary>
    public string? Note { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(FtsScenario))]
internal sealed partial class FtsJsonContext : JsonSerializerContext
{
}
