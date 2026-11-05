#if NET6_0_OR_GREATER
using System.Globalization;
using System.Text;

namespace SqliteFtsSnowball.Internal
{
    /// <summary>
    /// Unicode-aware helpers for splitting text into tokens and normalizing them.
    /// </summary>
    internal static class TextSegmentation
    {
        /// <summary>
        /// Returns <see langword="true"/> if the rune is part of a token: a letter, a
        /// combining mark (so diacritics stay attached to their base letter), or a number.
        /// Everything else is treated as a token separator.
        /// </summary>
        public static bool IsTokenRune(System.Text.Rune rune)
        {
            switch (System.Text.Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Removes Unicode combining marks (diacritics) from a string.</summary>
        public static string RemoveDiacritics(string value)
        {
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
#endif
