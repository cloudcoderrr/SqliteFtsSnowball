# SqliteFtsSnowball

[![build-nuget](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/build-nuget.yml/badge.svg)](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/build-nuget.yml)
[![test-desktop](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/test-desktop.yml/badge.svg)](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/test-desktop.yml)
[![test-mobile](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/test-mobile.yml/badge.svg)](https://github.com/cloudcoderrr/SqliteFtsSnowball/actions/workflows/test-mobile.yml)

A production-ready **SQLite FTS5 custom tokenizer** that applies **Snowball** (`libstemmer`)
stemming for **38 languages**, plus a direct stemming API. It lets full-text search match
different inflected forms of a word (e.g. searching `run` finds *running*, *runs*).

The package is **pure managed code** — the Snowball stemmers are the official C# distribution,
and the FTS5 tokenizer is registered against whatever SQLite your app already ships (via
[SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw), i.e. Microsoft.Data.Sqlite or
sqlite-net). That means a single assembly runs everywhere, including AOT targets.

## Supported platforms

Works anywhere SQLitePCLRaw works:

| Scenario | Platforms |
| --- | --- |
| Desktop / server | Windows (Win32 & WinUI 3), Linux, macOS — x64 & arm64 |
| Cross-platform UI | Avalonia, .NET MAUI |
| Mobile | iOS, Android, Mac Catalyst (via .NET MAUI) |
| Target frameworks | `netstandard2.0`, `net8.0`, `net10.0` |

> On `netstandard2.0` the stemming API is available; the FTS5 tokenizer registration requires
> .NET 6 or later (it uses `[UnmanagedCallersOnly]`).

## Install

```shell
dotnet add package SqliteFtsSnowball
```

You also need a SQLite provider with **FTS5** enabled. The recommended, security-patched choice:

```shell
dotnet add package Microsoft.Data.Sqlite
dotnet add package SQLitePCLRaw.bundle_e_sqlite3 --version 3.0.5
```

## Quick start (Microsoft.Data.Sqlite)

```csharp
using Microsoft.Data.Sqlite;
using SqliteFtsSnowball;

using var connection = new SqliteConnection("Data Source=app.db");
connection.Open();

// Register once per connection, before using FTS5 tables that reference "snowball".
SnowballTokenizer.Register(connection.Handle!);

using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        CREATE VIRTUAL TABLE docs USING fts5(title, body,
            tokenize = 'snowball english');";
    cmd.ExecuteNonQuery();
}

// Insert …
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "INSERT INTO docs(title, body) VALUES ($t, $b)";
    cmd.Parameters.AddWithValue("$t", "Running club");
    cmd.Parameters.AddWithValue("$b", "The runners were running quickly.");
    cmd.ExecuteNonQuery();
}

// … and search. "run" matches "running".
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT title FROM docs WHERE docs MATCH 'run'";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        Console.WriteLine(reader.GetString(0));
}
```

### sqlite-net

```csharp
var db = new SQLiteConnection("app.db");
SnowballTokenizer.Register(db.Handle);
```

## Choosing a language per table

The tokenizer name is `snowball`; the language and options are selected per FTS5 table through
the `tokenize=` arguments:

```sql
CREATE VIRTUAL TABLE fr USING fts5(body, tokenize = 'snowball french');
CREATE VIRTUAL TABLE ru USING fts5(body, tokenize = 'snowball russian');

-- no stemming, just Unicode tokenization + case-folding:
CREATE VIRTUAL TABLE raw USING fts5(body, tokenize = 'snowball none');

-- fold diacritics (é -> e) before indexing:
CREATE VIRTUAL TABLE de USING fts5(body, tokenize = 'snowball german remove_diacritics 1');
```

Recognized arguments: a bare leading language name (or `language <name>`),
`remove_diacritics 0|1`, `min_token_length N`, `max_token_length N`, `stem 0|1`.

## Direct stemming API

```csharp
using SqliteFtsSnowball;

var stemmer = SnowballStemmers.Create("english"); // or "en"
stemmer.Stem("running");   // "run"

foreach (var name in SnowballStemmers.SupportedLanguages)
    Console.WriteLine(name);
```

## Supported languages

`arabic, armenian, basque, catalan, czech, danish, dutch, dutch_porter, earlymodernenglish,
english, esperanto, estonian, finnish, french, german, greek, hindi, hungarian, indonesian,
irish, italian, lithuanian, nepali, norwegian, persian, polish, porter, portuguese, romanian,
russian, serbian, sesotho, spanish, swedish, tamil, turkish, ukrainian, yiddish`
(common ISO codes such as `en`, `fr`, `ru` are accepted as aliases).

## How it works

- **Stemmers**: the official `libstemmer_csharp` sources are downloaded and compiled into the
  assembly (see [`scripts/fetch-snowball.ps1`](scripts/fetch-snowball.ps1) and
  [`patches/`](patches)). They are verified against the authoritative `snowball-data` test
  vectors for every language.
- **Tokenizer**: on registration the library obtains the `fts5_api` (via
  `sqlite3_bind_pointer`) and registers a tokenizer whose callbacks are managed
  `[UnmanagedCallersOnly]` functions. Text is split on Unicode boundaries, case-folded,
  optionally de-accented, and each token is stemmed. Original byte offsets are preserved so
  `snippet()` / `highlight()` keep working.

## Building from source

```shell
pwsh ./scripts/fetch-snowball.ps1      # download + patch the Snowball C# sources
dotnet build src/SqliteFtsSnowball/SqliteFtsSnowball.csproj -c Release
```

## Testing

```shell
pwsh ./scripts/fetch-snowball.ps1
dotnet test Tests/Unit/SqliteFtsSnowball.Tests -f net10.0
```

Ground-truth test data lives in [`Tests/Data`](Tests/Data) (regenerate with
[`scripts/fetch-testdata.ps1`](scripts/fetch-testdata.ps1)). It contains the authoritative
`snowball-data` word → stem vectors for every language plus hand-authored FTS5 search
scenarios. The shared [`SqliteFtsSnowball.TestKit`](Tests/Shared/SqliteFtsSnowball.TestKit)
runs three suites — stemming vectors, end-to-end FTS5 search scenarios, and focused tokenizer
behavior — and writes a detailed log file.

The same TestKit is driven by three front-ends:

| Project | Runs on |
| --- | --- |
| [`SqliteFtsSnowball.Tests`](Tests/Unit/SqliteFtsSnowball.Tests) (xUnit) | CI on all desktop OSes |
| [`SnowballTests.Desktop.Avalonia`](Tests/Desktop/SnowballTests.Desktop.Avalonia) | Windows, Linux, macOS (interactive UI, or `--ci`) |
| [`SnowballTests.Mobile.Maui`](Tests/Mobile/SnowballTests.Mobile.Maui) | iOS, Android, Mac Catalyst |

## Repository layout

```
src/SqliteFtsSnowball/            the NuGet library (stemmers + FTS5 tokenizer)
scripts/fetch-snowball.ps1        downloads + patches the Snowball C# sources
scripts/fetch-testdata.ps1        regenerates the ground-truth test data
patches/libstemmer_csharp/        unified-diff patches applied to the Snowball sources
Tests/Data/                       committed ground-truth test data
Tests/Shared/…TestKit/            shared test logic + logging (embeds Tests/Data)
Tests/Unit/…Tests/                xUnit tests
Tests/Desktop/…Avalonia/          Avalonia desktop test app
Tests/Mobile/…Maui/               .NET MAUI mobile test app
.github/workflows/                build + desktop + mobile CI
```

## Continuous integration

- **build-nuget** — fetches/patches Snowball, packs the NuGet, and on a `v*` tag publishes it
  to the GitHub Releases page.
- **test-desktop** — xUnit + the Avalonia app (headless) on Windows, Linux and macOS, on both
  **x64 and arm64**, for `net8.0` and `net10.0`.
- **test-mobile** — the MAUI app on an **Android emulator**, an **iOS Simulator**, and **Mac
  Catalyst**. Logs from every run are uploaded as build artifacts.

## License

[GPL-3.0-or-later](LICENSE). Bundled Snowball stemmer sources are BSD-3-Clause — see
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).