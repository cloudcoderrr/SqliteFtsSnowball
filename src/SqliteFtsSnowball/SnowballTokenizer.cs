using System;

namespace SqliteFtsSnowball
{
    /// <summary>
    /// Registers the Snowball stemming tokenizer with SQLite's FTS5 extension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration is per database <em>connection</em>: call <see cref="Register(IntPtr, string, SnowballTokenizerOptions)"/>
    /// (or an overload) once on each open connection, before creating or querying FTS5
    /// tables that use the tokenizer.
    /// </para>
    /// <para>
    /// After registration, create tables with <c>tokenize = 'snowball &lt;language&gt; ...'</c>, e.g.
    /// <code>
    /// CREATE VIRTUAL TABLE docs USING fts5(title, body, tokenize = 'snowball english');
    /// </code>
    /// </para>
    /// <para>
    /// Registration requires .NET 6 or later. On <c>netstandard2.0</c> only the stemming
    /// API (<see cref="SnowballStemmers"/>) is available.
    /// </para>
    /// </remarks>
    public static class SnowballTokenizer
    {
        /// <summary>The default FTS5 tokenizer name (<c>snowball</c>).</summary>
        public const string DefaultName = "snowball";

        /// <summary>
        /// The native SQLite library that P/Invoke calls are resolved against. Defaults to
        /// <c>e_sqlite3</c> (the library shipped by <c>SQLitePCLRaw.bundle_e_sqlite3</c>, which
        /// Microsoft.Data.Sqlite and sqlite-net use). Set this before registering if your app
        /// uses a different SQLite build (for example <c>winsqlite3</c> or <c>sqlite3</c>).
        /// </summary>
        public static string SqliteLibraryName
        {
            get =>
#if NET6_0_OR_GREATER
                Internal.Fts5Interop.SqliteLibraryName;
#else
                "e_sqlite3";
#endif
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The SQLite library name must be non-empty.", nameof(value));
                }
#if NET6_0_OR_GREATER
                Internal.Fts5Interop.SqliteLibraryName = value;
#endif
            }
        }

        /// <summary>
        /// Registers the tokenizer on a SQLite connection identified by its raw
        /// <c>sqlite3*</c> handle.
        /// </summary>
        /// <param name="db">The native <c>sqlite3*</c> connection handle.</param>
        /// <param name="name">The FTS5 tokenizer name to register (default <c>snowball</c>).</param>
        /// <param name="options">Default tokenizer options; may be overridden per table.</param>
        public static void Register(IntPtr db, string name = DefaultName, SnowballTokenizerOptions? options = null)
        {
            if (db == IntPtr.Zero)
            {
                throw new ArgumentException("The SQLite database handle is null (IntPtr.Zero).", nameof(db));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The tokenizer name must be non-empty.", nameof(name));
            }

#if NET6_0_OR_GREATER
            Internal.Fts5Interop.Register(db, name, options ?? new SnowballTokenizerOptions());
#else
            throw new PlatformNotSupportedException(
                "Registering the FTS5 Snowball tokenizer requires .NET 6 or later. " +
                "The stemming API (SnowballStemmers) is available on all target frameworks.");
#endif
        }

        /// <summary>
        /// Registers the tokenizer using a SQLitePCLRaw <see cref="SQLitePCL.sqlite3"/> handle.
        /// This is the handle exposed by Microsoft.Data.Sqlite (<c>SqliteConnection.Handle</c>)
        /// and sqlite-net (<c>SQLiteConnection.Handle</c>).
        /// </summary>
        public static void Register(SQLitePCL.sqlite3 db, string name = DefaultName, SnowballTokenizerOptions? options = null)
        {
            if (db is null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (db.IsInvalid || db.IsClosed)
            {
                throw new ArgumentException("The SQLite connection handle is invalid or closed.", nameof(db));
            }

            Register(db.DangerousGetHandle(), name, options);
        }
    }
}
