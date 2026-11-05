#if NET6_0_OR_GREATER
using System;
using System.Buffers;
using System.Text;
using Snowball;

namespace SqliteFtsSnowball.Internal
{
    /// <summary>
    /// A single FTS5 tokenizer instance (one per FTS5 table on a connection). It splits text
    /// into tokens, case-folds them, optionally removes diacritics, applies the configured
    /// Snowball stemmer, and forwards each resulting token to FTS5 with the original byte
    /// offsets of the source token (so snippets/highlights remain accurate).
    /// </summary>
    /// <remarks>
    /// Instances are stateful (they reuse a <see cref="Stemmer"/> and a buffer) and are not
    /// thread-safe. FTS5 only calls a tokenizer instance from the owning connection, whose
    /// access SQLite already serializes, so this is safe in normal use.
    /// </remarks>
    internal sealed unsafe class TokenizerInstance
    {
        private const int SQLITE_OK = 0;

        private readonly Stemmer? _stemmer;
        private readonly bool _removeDiacritics;
        private readonly int _minLength;
        private readonly int _maxLength; // 0 = unlimited
        private readonly StringBuilder _word = new(64);

        public TokenizerInstance(Stemmer? stemmer, bool removeDiacritics, int minLength, int maxLength)
        {
            _stemmer = stemmer;
            _removeDiacritics = removeDiacritics;
            _minLength = minLength < 1 ? 1 : minLength;
            _maxLength = maxLength < 0 ? 0 : maxLength;
        }

        public int Tokenize(IntPtr pContext, int flags, IntPtr pText, int nText, IntPtr xToken)
        {
            _ = flags; // documents and queries are tokenized identically
            if (pText == IntPtr.Zero || nText <= 0)
            {
                return SQLITE_OK;
            }

            var text = new ReadOnlySpan<byte>((void*)pText, nText);
            int index = 0;
            int tokenStart = -1;
            _word.Clear();

            while (index < text.Length)
            {
                OperationStatus status = System.Text.Rune.DecodeFromUtf8(text.Slice(index), out System.Text.Rune rune, out int consumed);
                bool isToken = status == OperationStatus.Done && TextSegmentation.IsTokenRune(rune);

                if (isToken)
                {
                    if (tokenStart < 0)
                    {
                        tokenStart = index;
                        _word.Clear();
                    }

                    AppendCaseFolded(_word, rune);
                }
                else if (tokenStart >= 0)
                {
                    int rc = Emit(pContext, xToken, tokenStart, index);
                    if (rc != SQLITE_OK)
                    {
                        return rc;
                    }

                    tokenStart = -1;
                }

                index += consumed <= 0 ? 1 : consumed;
            }

            if (tokenStart >= 0)
            {
                int rc = Emit(pContext, xToken, tokenStart, text.Length);
                if (rc != SQLITE_OK)
                {
                    return rc;
                }
            }

            return SQLITE_OK;
        }

        private static void AppendCaseFolded(StringBuilder builder, System.Text.Rune rune)
        {
            System.Text.Rune lower = System.Text.Rune.ToLowerInvariant(rune);
            Span<char> utf16 = stackalloc char[2];
            int count = lower.EncodeToUtf16(utf16);
            builder.Append(utf16.Slice(0, count));
        }

        private int Emit(IntPtr pContext, IntPtr xToken, int startByte, int endByte)
        {
            if (_word.Length == 0)
            {
                return SQLITE_OK;
            }

            string word = _word.ToString();
            if (word.Length < _minLength || (_maxLength > 0 && word.Length > _maxLength))
            {
                return SQLITE_OK;
            }

            if (_removeDiacritics)
            {
                word = TextSegmentation.RemoveDiacritics(word);
                if (word.Length == 0)
                {
                    return SQLITE_OK;
                }
            }

            string token = word;
            if (_stemmer != null)
            {
                string stem = _stemmer.Stem(word);
                if (!string.IsNullOrEmpty(stem))
                {
                    token = stem;
                }
            }

            int maxBytes = Encoding.UTF8.GetMaxByteCount(token.Length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                int byteCount = Encoding.UTF8.GetBytes(token, 0, token.Length, buffer, 0);
                fixed (byte* tokenPtr = buffer)
                {
                    var invoke = (delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr, int, int, int, int>)xToken;
                    return invoke(pContext, 0, (IntPtr)tokenPtr, byteCount, startByte, endByte);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
#endif
