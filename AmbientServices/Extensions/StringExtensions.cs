using AmbientServices.Utilities;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AmbientServices.Extensions
{
    /// <summary>
    /// A static partial class that extends <see cref="string"/>.
    /// </summary>
    internal static partial class StringExtensions
    {
        private static readonly System.Text.RegularExpressions.Regex DigitSequenceRegex = new(@"\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex CommaSeparatedDigits = new(@"(?<csn>\d+(?:,\d+)+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        /// <summary>
        /// Compares two strings naturally, so that numeric sequences embedded in the strings are sorted numerically instead of based on the characters.
        /// For example, a regular string sort would sort "a100b" before "a99b", but a natural string sort would not.
        /// Numeric sequences with more leading zeros sort after those with fewer (or no) leading zeros.
        /// Floating-point numbers and negatives are supported, but sequences od numbers separated by single dashes are treated as positive.
        /// </summary>
        /// <param name="a">The first string to compare.</param>
        /// <param name="b">The second string to compare.</param>
        /// <returns>&gt;0 if the first string should be sorted after the second one, &lt;0 if the second string should be sorted after, and zero (0) if the strings are the same.</returns>
        public static int CompareNaturalInvariant(this string a, string b)
        {
            return RegexCompareNaturalInvariant(a, b, false);
        }

        /// <summary>
        /// Compares two strings naturally, so that numeric sequences embedded in the strings are sorted numerically instead of based on the characters.
        /// For example, a regular string sort would sort "a100b" before "a99b", but a natural string sort would not.
        /// Numeric sequences with more leading zeros sort after those with fewer (or no) leading zeros.
        /// Floating-point numbers and negatives are supported, but sequences od numbers separated by single dashes are treated as positive.
        /// </summary>
        /// <param name="a">The first string to compare.</param>
        /// <param name="b">The second string to compare.</param>
        /// <param name="ignoreCase">Whether to ignore casing when comparing.</param>
        /// <returns>&gt;0 if the first string should be sorted after the second one, &lt;0 if the second string should be sorted after, and zero (0) if the strings are the same.</returns>
        public static int CompareNaturalInvariant(this string a, string b, bool ignoreCase)
        {
            return RegexCompareNaturalInvariant(a, b, ignoreCase);
        }
        /// <summary>
        /// Compares two strings using a 'natural' compare algorithm which compares embedded numbers numerically rather than alphabetically.
        /// </summary>
        /// <param name="a">The first string to compare.</param>
        /// <param name="b">The second string to compare.</param>
        /// <param name="ignoreCase">Whether to ignore casing when comparing.</param>
        /// <returns>0 if the strings are the same, &gt;0 if the first string is 'greater' than the second, or &lt;0 if the first string is 'less' than the second one.</returns>
        public static int RegexCompareNaturalInvariant(this string a, string b, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(a))
            {
                if (string.IsNullOrEmpty(b)) return 0;
                return -1;
            }
            else if (string.IsNullOrEmpty(b))
            {
                return 1;
            }
            // first replace any sequences of comma-separated digits with just the digits
            string normA = CommaSeparatedDigits.Replace(a, m => m.Value.Replace(",", "", StringComparison.Ordinal));
            string normB = CommaSeparatedDigits.Replace(b, m => m.Value.Replace(",", "", StringComparison.Ordinal));
            // next count the longest digit sequence in either string
            int maxDigitsA = DigitSequenceRegex.Matches(normA + ".0").Cast<System.Text.RegularExpressions.Match>().Select(digitChunk => digitChunk.Value.Length).Max();
            int maxDigitsB = DigitSequenceRegex.Matches(normB + ".0").Cast<System.Text.RegularExpressions.Match>().Select(digitChunk => digitChunk.Value.Length).Max();
            int maxDigits = Math.Max(maxDigitsA, maxDigitsB);
            // next expand each digit sequence to that length and transform the string into one that will sorts the way we want it to
            normA = StringUtilities.NormalizeStringWithNumberSequences(normA, maxDigits);
            normB = StringUtilities.NormalizeStringWithNumberSequences(normB, maxDigits);
            // now compare the strings
            return CultureInfo.InvariantCulture.CompareInfo.Compare(normA, normB, ignoreCase ? CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal);
        }

#if !NETCOREAPP3_1 && !NET5_0_OR_GREATER
#pragma warning disable CA1801  // these functions specifically make the code behave the old pre net5.0 way.
        /// <summary>
        /// Replaces matching parts of the string with another string.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="find">The string to find.</param>
        /// <param name="target">The string to put in place of <paramref name="find"/>.</param>
        /// <param name="compare">A <see cref="StringComparison"/> indicating how to perform the search.</param>
        /// <returns><paramref name="source"/> string with instances of <paramref name="find"/> replaced with <paramref name="target"/>.</returns>
        public static string Replace(this string source, string find, string target, StringComparison compare)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Replace(find, target, StringComparison.Ordinal);
        }
        /// <summary>
        /// Checks to see if a string contains the specified character.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="find">The character to look for.</param>
        /// <param name="compare">A <see cref="StringComparison"/> indicating how to perform the search.</param>
        /// <returns>Whether or not <paramref name="source"/> contains <paramref name="find"/>.</returns>
        public static bool Contains(this string source, char find, StringComparison compare)
        {
            return source.Contains(find, StringComparison.Ordinal);
        }
        /// <summary>
        /// Checks to see if a string contains a specified string.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="find">The string to look for.</param>
        /// <param name="compare">A <see cref="StringComparison"/> indicating how to perform the search.</param>
        /// <returns>Whether or not <paramref name="source"/> contains <paramref name="find"/>.</returns>
        public static bool Contains(this string source, string find, StringComparison compare)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Contains(find, StringComparison.Ordinal);
        }
        /// <summary>
        /// Gets a 32-bit hash code for the specified string.
        /// </summary>
        /// <param name="source">The string.</param>
        /// <param name="compare">A <see cref="StringComparison"/> indicating whether to ignore case and such when making the hash code.</param>
        /// <returns>A 32-bit hash code for <paramref name="source"/>.</returns>
        public static int GetHashCode(this string source, StringComparison compare)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.GetHashCode(StringComparison.Ordinal);
        }
#pragma warning restore CA1801
#endif
    }
}
