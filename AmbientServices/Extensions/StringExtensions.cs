using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A static partial class that extends <see cref="System.String"/>.
    /// </summary>
    internal static partial class StringExtensions
    {
        private static readonly char[] DecimalPointCharArray = ".,".ToCharArray();
        private static readonly char[] NumberSeparatorCharArray = ".,-".ToCharArray();
        private static readonly System.Text.RegularExpressions.Regex DigitSequenceRegex = new System.Text.RegularExpressions.Regex(@"\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex CommaSeparatedDigits = new System.Text.RegularExpressions.Regex(@"(?<csn>\d+(?:,\d+)+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex NumberRegex = new System.Text.RegularExpressions.Regex(
            @"(?<ps>(?:-?\d+)\.(?:(?:\d+)\.)+(?:\d+))" +    // finds a sequence of numbers separated by periods, such as 2020.5.7, and prevents them from being detected as real numbers (they should be treated as a sequence of whole numbers)
            @"|(?<ds>(?:-?\d+)-(?:(?:\d+)-)+(?:\d+))" +     // finds a sequence of numbers separated by dashes, such as 2020-5-7, and prevents them from being detected as negative numbers (they should be treated as a sequence of whole numbers)
            @"|(?<nr>(?<![0-9])(?:-(?:\d*)\.\d+))" +        // finds a negative real
            @"|(?<ni>(?<![0-9])(?:-(?:\d+)))" +             // finds a negative integer
            @"|(?<pr>(?<![-.,]\d*)(?:(?:\d*)\.\d+))" +      // finds a positive real
            @"|(?<pi>(?<![-.,]\d*)(?:\d+))",                // finds a positive integer
            System.Text.RegularExpressions.RegexOptions.Compiled);
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
            normA = NormalizeStringWithNumberSequences(normA, maxDigits);
            normB = NormalizeStringWithNumberSequences(normB, maxDigits);
            // now compare the strings
            return CultureInfo.InvariantCulture.CompareInfo.Compare(normA, normB, ignoreCase ? CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal);
        }

        private static string NormalizeStringWithNumberSequences(string str, int maxDigits)
        {
            str = NumberRegex.Replace(str,
                delegate (System.Text.RegularExpressions.Match m)
                {
                    int matchGroup = 1;
                    for (; matchGroup < m.Groups.Count; ++matchGroup)
                    {
                        if (m.Groups[matchGroup].Captures.Count > 0) break;
                    }
                    // I don't think this should ever happen, but just in case...
                    if (matchGroup >= m.Groups.Count) return m.Value;
                    int prefixIndex = m.Index - 1;
                    int decimalPointIndex;
                    string wholePart;
                    string fractionPart;
                    string[] numberParts;
                    // Note that the use of 1 and 4 here is to be sure that negatives sort before positives.  1 is like a sideways minus sign and 4 is like a plus sign as well.
                    switch (matchGroup)
                    {
                        case 1: // ps: period sequence
                            numberParts = m.Value.Split(NumberSeparatorCharArray);
                            return (numberParts[0].Length == 0 && m.Value[0] == '-')
                            ? NegativePartTransform("1", numberParts[0].PadLeft(maxDigits, '0')) + "." + String.Join(".", numberParts.Skip(1).Select(s => "4" + s.PadLeft(maxDigits, '0')))
                            : String.Join(".", numberParts.Select(s => "4" + s.PadLeft(maxDigits, '0')));
                        case 2: // ds: dash sequence
                            numberParts = m.Value.Split(NumberSeparatorCharArray);
                            return (numberParts[0].Length == 0 && m.Value[0] == '-')
                            ? NegativePartTransform("1", numberParts[0].PadLeft(maxDigits, '0')) + "-" + String.Join("-", numberParts.Skip(1).Select(s => "4" + s.PadLeft(maxDigits, '0')))
                            : String.Join("-", numberParts.Select(s => "4" + s.PadLeft(maxDigits, '0')));
                        case 3: // nr: negative real
                            decimalPointIndex = m.Value.IndexOfAny(DecimalPointCharArray, 1);
                            System.Diagnostics.Debug.Assert(decimalPointIndex > 0);
                            wholePart = NegativePartTransform("1", m.Value.Substring(1, decimalPointIndex - 1).PadLeft(maxDigits, '0'));
                            fractionPart = NegativePartTransform("4", m.Value.Substring(decimalPointIndex + 1, m.Value.Length - decimalPointIndex - 1).PadRight(maxDigits, '0'));
                            return wholePart + fractionPart;
                        case 4: // ni: negative integer
                            return NegativePartTransform("1", m.Value.Substring(1).PadLeft(maxDigits, '0'));
                        case 5: // pr: positive real
                            decimalPointIndex = m.Value.IndexOfAny(DecimalPointCharArray, 0);
                            System.Diagnostics.Debug.Assert(decimalPointIndex >= 0);
                            wholePart = "4" + m.Value.Substring(0, decimalPointIndex).PadLeft(maxDigits, '0');
                            fractionPart = "4" + m.Value.Substring(decimalPointIndex + 1, m.Value.Length - decimalPointIndex - 1).PadRight(maxDigits, '0');
                            return wholePart + fractionPart;
                        case 6: // pi: positive integer
                            return "4" + m.Value.PadLeft(maxDigits, '0');
                        default:
                            // this should also never happen, but just in case...
                            throw new InvalidOperationException("The match group number was not expected--the regex must have changed without a corresponding change in the code!");
                    }
                });
            return str;
        }

        private static string NegativePartTransform(string prefix, string str)
        {
            System.Diagnostics.Debug.Assert(str[0] != '-');
            // use 1 as the 'negative prefix' because it should sort before the 'positive prefix' of 4
            StringBuilder builder = new StringBuilder(prefix);
            for (int off = 0; off < str.Length; ++off)
            {
                char c = str[off];
                System.Diagnostics.Debug.Assert(c >= '0' && c <= '9');
                builder.Append((char)('0' + (9 - (c - '0'))));
            }
            return builder.ToString();
        }

#if !NET5_0_OR_GREATER
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
            return source.Replace(find, target);
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
            return source.Contains(find);
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
            return source.Contains(find);
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
            return source.GetHashCode();
        }
#pragma warning restore CA1801
#endif
    }
}
