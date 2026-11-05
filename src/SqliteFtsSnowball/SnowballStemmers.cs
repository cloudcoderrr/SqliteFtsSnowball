using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Snowball;

namespace SqliteFtsSnowball
{
    /// <summary>
    /// Describes a single Snowball stemming algorithm (language) that this library supports.
    /// </summary>
    public sealed class SnowballLanguage
    {
        private readonly Func<Stemmer> _factory;

        internal SnowballLanguage(string name, string[] aliases, Func<Stemmer> factory)
        {
            Name = name;
            Aliases = new ReadOnlyCollection<string>(aliases);
            _factory = factory;
        }

        /// <summary>The canonical Snowball name of the language (for example <c>english</c>).</summary>
        public string Name { get; }

        /// <summary>Additional names/codes that resolve to this language (for example <c>en</c>).</summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>Creates a new, stateful <see cref="Stemmer"/> instance for this language.</summary>
        public Stemmer CreateStemmer() => _factory();

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The registry and factory for the Snowball stemmers bundled with this library.
    /// </summary>
    /// <remarks>
    /// All lookups are case-insensitive and treat <c>-</c> and <c>_</c> as equivalent.
    /// Individual <see cref="Stemmer"/> instances are stateful and <b>not</b> thread-safe;
    /// create one per thread (or per SQLite connection).
    /// </remarks>
    public static class SnowballStemmers
    {
        private static readonly SnowballLanguage[] AllLanguages = BuildLanguages();
        private static readonly Dictionary<string, SnowballLanguage> Index = BuildIndex(AllLanguages);
        private static readonly ReadOnlyCollection<string> Names = BuildNames(AllLanguages);

        /// <summary>All supported languages.</summary>
        public static IReadOnlyList<SnowballLanguage> All => AllLanguages;

        /// <summary>The canonical names of all supported languages, sorted alphabetically.</summary>
        public static IReadOnlyCollection<string> SupportedLanguages => Names;

        /// <summary>Returns <see langword="true"/> if the given language name or alias is supported.</summary>
        public static bool IsSupported(string language) =>
            !string.IsNullOrWhiteSpace(language) && Index.ContainsKey(Normalize(language));

        /// <summary>Resolves a language name or alias to its <see cref="SnowballLanguage"/> descriptor.</summary>
        /// <exception cref="ArgumentException">The language is not supported.</exception>
        public static SnowballLanguage Get(string language)
        {
            if (TryGet(language, out SnowballLanguage? result))
            {
                return result!;
            }

            throw new ArgumentException(
                $"Unsupported Snowball language '{language}'. Supported languages: {string.Join(", ", Names)}.",
                nameof(language));
        }

        /// <summary>Attempts to resolve a language name or alias to its descriptor.</summary>
        public static bool TryGet(string language, out SnowballLanguage? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            return Index.TryGetValue(Normalize(language), out result);
        }

        /// <summary>Creates a new stemmer for the given language name or alias.</summary>
        /// <exception cref="ArgumentException">The language is not supported.</exception>
        public static Stemmer Create(string language) => Get(language).CreateStemmer();

        /// <summary>Attempts to create a new stemmer for the given language name or alias.</summary>
        public static bool TryCreate(string language, out Stemmer? stemmer)
        {
            stemmer = null;
            if (!TryGet(language, out SnowballLanguage? lang))
            {
                return false;
            }

            stemmer = lang!.CreateStemmer();
            return true;
        }

        private static string Normalize(string value) =>
            value.Trim().ToLowerInvariant().Replace('-', '_');

        private static Dictionary<string, SnowballLanguage> BuildIndex(SnowballLanguage[] languages)
        {
            var index = new Dictionary<string, SnowballLanguage>(StringComparer.Ordinal);
            foreach (SnowballLanguage language in languages)
            {
                index[Normalize(language.Name)] = language;
                foreach (string alias in language.Aliases)
                {
                    index[Normalize(alias)] = language;
                }
            }

            return index;
        }

        private static ReadOnlyCollection<string> BuildNames(SnowballLanguage[] languages)
        {
            var names = new List<string>(languages.Length);
            foreach (SnowballLanguage language in languages)
            {
                names.Add(language.Name);
            }

            names.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(names);
        }

        private static SnowballLanguage[] BuildLanguages() => new[]
        {
            new SnowballLanguage("arabic", new[] { "ar" }, () => new ArabicStemmer()),
            new SnowballLanguage("armenian", new[] { "hy" }, () => new ArmenianStemmer()),
            new SnowballLanguage("basque", new[] { "eu" }, () => new BasqueStemmer()),
            new SnowballLanguage("catalan", new[] { "ca" }, () => new CatalanStemmer()),
            new SnowballLanguage("czech", new[] { "cs" }, () => new CzechStemmer()),
            new SnowballLanguage("danish", new[] { "da" }, () => new DanishStemmer()),
            new SnowballLanguage("dutch", new[] { "nl" }, () => new DutchStemmer()),
            new SnowballLanguage("dutch_porter", Array.Empty<string>(), () => new Dutch_porterStemmer()),
            new SnowballLanguage("earlymodernenglish", new[] { "enm" }, () => new EarlymodernenglishStemmer()),
            new SnowballLanguage("english", new[] { "en" }, () => new EnglishStemmer()),
            new SnowballLanguage("esperanto", new[] { "eo" }, () => new EsperantoStemmer()),
            new SnowballLanguage("estonian", new[] { "et" }, () => new EstonianStemmer()),
            new SnowballLanguage("finnish", new[] { "fi" }, () => new FinnishStemmer()),
            new SnowballLanguage("french", new[] { "fr" }, () => new FrenchStemmer()),
            new SnowballLanguage("german", new[] { "de" }, () => new GermanStemmer()),
            new SnowballLanguage("greek", new[] { "el" }, () => new GreekStemmer()),
            new SnowballLanguage("hindi", new[] { "hi" }, () => new HindiStemmer()),
            new SnowballLanguage("hungarian", new[] { "hu" }, () => new HungarianStemmer()),
            new SnowballLanguage("indonesian", new[] { "id" }, () => new IndonesianStemmer()),
            new SnowballLanguage("irish", new[] { "ga" }, () => new IrishStemmer()),
            new SnowballLanguage("italian", new[] { "it" }, () => new ItalianStemmer()),
            new SnowballLanguage("lithuanian", new[] { "lt" }, () => new LithuanianStemmer()),
            new SnowballLanguage("nepali", new[] { "ne" }, () => new NepaliStemmer()),
            new SnowballLanguage("norwegian", new[] { "no", "nb" }, () => new NorwegianStemmer()),
            new SnowballLanguage("persian", new[] { "fa" }, () => new PersianStemmer()),
            new SnowballLanguage("polish", new[] { "pl" }, () => new PolishStemmer()),
            new SnowballLanguage("porter", Array.Empty<string>(), () => new PorterStemmer()),
            new SnowballLanguage("portuguese", new[] { "pt" }, () => new PortugueseStemmer()),
            new SnowballLanguage("romanian", new[] { "ro" }, () => new RomanianStemmer()),
            new SnowballLanguage("russian", new[] { "ru" }, () => new RussianStemmer()),
            new SnowballLanguage("serbian", new[] { "sr" }, () => new SerbianStemmer()),
            new SnowballLanguage("sesotho", new[] { "st" }, () => new SesothoStemmer()),
            new SnowballLanguage("spanish", new[] { "es" }, () => new SpanishStemmer()),
            new SnowballLanguage("swedish", new[] { "sv" }, () => new SwedishStemmer()),
            new SnowballLanguage("tamil", new[] { "ta" }, () => new TamilStemmer()),
            new SnowballLanguage("turkish", new[] { "tr" }, () => new TurkishStemmer()),
            new SnowballLanguage("ukrainian", new[] { "uk" }, () => new UkrainianStemmer()),
            new SnowballLanguage("yiddish", new[] { "yi" }, () => new YiddishStemmer()),
        };
    }
}
