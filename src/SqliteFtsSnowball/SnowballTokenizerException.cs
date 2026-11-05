using System;

namespace SqliteFtsSnowball
{
    /// <summary>
    /// The exception thrown when the Snowball FTS5 tokenizer cannot be registered with,
    /// or fails while running inside, SQLite.
    /// </summary>
    public sealed class SnowballTokenizerException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="SnowballTokenizerException"/> class.</summary>
        public SnowballTokenizerException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="SnowballTokenizerException"/> class.</summary>
        public SnowballTokenizerException(string message, Exception innerException) : base(message, innerException) { }
    }
}
