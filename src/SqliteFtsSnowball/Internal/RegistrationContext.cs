#if NET6_0_OR_GREATER
using System;

namespace SqliteFtsSnowball.Internal
{
    /// <summary>
    /// Per-registration state passed to FTS5 as the tokenizer "context". It holds the
    /// default options for the registration and the unmanaged copy of the tokenizer name
    /// (freed when FTS5 destroys the tokenizer).
    /// </summary>
    internal sealed class RegistrationContext
    {
        public RegistrationContext(SnowballTokenizerOptions options, IntPtr namePtr)
        {
            Options = options;
            NamePtr = namePtr;
        }

        public SnowballTokenizerOptions Options { get; }

        public IntPtr NamePtr { get; }
    }
}
#endif
