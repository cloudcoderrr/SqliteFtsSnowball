namespace SqliteFtsSnowball
{
    /// <summary>
    /// Options that control how the Snowball FTS5 tokenizer analyzes text.
    /// </summary>
    /// <remarks>
    /// These values are used as the <em>defaults</em> for a registration. They can be
    /// overridden per FTS5 table through the <c>tokenize=</c> arguments, for example:
    /// <code>
    /// CREATE VIRTUAL TABLE docs USING fts5(body, tokenize = 'snowball french remove_diacritics 0');
    /// </code>
    /// Recognized table arguments: a bare leading language name, <c>language &lt;name&gt;</c>,
    /// <c>remove_diacritics 0|1</c>, <c>min_token_length N</c>, <c>max_token_length N</c>,
    /// and <c>stem 0|1</c>.
    /// </remarks>
    public sealed class SnowballTokenizerOptions
    {
        /// <summary>
        /// The Snowball language used for stemming (canonical name such as <c>english</c>,
        /// <c>french</c>, <c>russian</c>, or an alias such as <c>en</c>). Use <c>none</c>
        /// (or set <see cref="StemmingEnabled"/> to <see langword="false"/>) to tokenize
        /// without stemming. Defaults to <c>english</c>.
        /// </summary>
        public string Language { get; set; } = "english";

        /// <summary>
        /// When <see langword="true"/>, Unicode combining marks (diacritics) are removed
        /// from tokens before stemming. Defaults to <see langword="false"/> because most
        /// Snowball algorithms rely on diacritics (e.g. French, Spanish, Portuguese).
        /// </summary>
        public bool RemoveDiacritics { get; set; }

        /// <summary>
        /// Tokens shorter than this number of UTF-16 code units are skipped. Minimum 1.
        /// Defaults to 1 (only empty tokens are skipped).
        /// </summary>
        public int MinTokenLength { get; set; } = 1;

        /// <summary>
        /// Tokens longer than this number of UTF-16 code units are skipped.
        /// A value of 0 (the default) means "no maximum".
        /// </summary>
        public int MaxTokenLength { get; set; }

        /// <summary>
        /// When <see langword="false"/>, tokens are emitted (case-folded) without stemming.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool StemmingEnabled { get; set; } = true;
    }
}
