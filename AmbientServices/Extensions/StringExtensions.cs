using AmbientServices.Utilities;
using System;
using System.Globalization;
using System.Linq;

namespace AmbientServices.Extensions;

/// <summary>
/// A static partial class that extends <see cref="string"/>.
/// </summary>
/// <remarks>
/// <pitch>Natural string comparison (&quot;a99b&quot; before &quot;a100b&quot;) for sorting user-visible names that embed numbers, plus small shims that give older target frameworks the newer <see cref="string"/> overloads with explicit ordinal semantics.</pitch>
/// <pledge>
/// Natural comparison orders embedded numeric sequences by numeric value rather than character order: comma-grouped digits are treated as a single number, floating-point and negative numbers are supported, and sequences of numbers separated by single dashes or periods (dates, versions) are treated as runs of separate positive numbers.  Null and empty strings sort first and equal to each other.
/// Every comparison in the class is invariant-culture or ordinal — no member's result is affected by the thread's current culture.
/// </pledge>
/// <plan>
/// Natural comparison is regex normalization followed by a single ordinary compare: both strings are rewritten by <see cref="AmbientServices.Utilities.StringUtilities"/> so that every numeric token is zero-padded to the longest digit run present in either string and prefixed with a sign marker that makes negatives order before positives, then the normalized strings are compared once using the invariant culture.  The token-finding regexes are compiled and shared.  The framework-conditional members simply forward to whichever ordinal overload the target framework provides.
/// </plan>
/// </remarks>
public static partial class StringExtensions
{
    private static readonly System.Text.RegularExpressions.Regex DigitSequenceRegex = new(@"\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex CommaSeparatedDigits = new(@"(?<csn>\d+(?:,\d+)+)", System.Text.RegularExpressions.RegexOptions.Compiled);
    /// <summary>
    /// Compares two strings naturally, so that numeric sequences embedded in the strings are sorted numerically instead of based on the characters.
    /// For example, a regular string sort would sort "a100b" before "a99b", but a natural string sort would not.
    /// Numeric sequences sort numerically as if they were zero-padded.
    /// Floating-point numbers and negatives are supported, but sequences of numbers separated by single dashes are treated as positive.
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
    /// Numeric sequences sort numerically as if they were zero-padded.
    /// Floating-point numbers and negatives are supported, but sequences of numbers separated by single dashes are treated as positive.
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
        if (source == null) throw new ArgumentNullException(nameof(source));
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
#if !NET8_0_OR_GREATER
    /// <summary>
    /// Checks to see if the string starts with the specified character, using standard ordinal comparison.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <param name="value">The character to look for.</param>
    /// <returns></returns>
    public static bool StartsWith(this string str, char value)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
        return str.StartsWith(value.ToString(), StringComparison.Ordinal);
    }
    /// <summary>
    /// Checks to see if the string ends with the specified character, using standard ordinal comparison.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <param name="value">The character to look for.</param>
    /// <returns></returns>
    public static bool EndsWith(this string str, char value)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
        return str.EndsWith(value.ToString(), StringComparison.Ordinal);
    }
#endif
    /// <summary>
    /// Gets the index of the first occurrence of a character in a string using ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="c">The character to search for.</param>
    /// <returns>The index of the first occurrence of the character in the string, or -1 if the character is not found.</returns>
    public static int IndexOfOrdinal(this string str, char c)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return str.IndexOf(c, StringComparison.Ordinal);
#else
    return str.IndexOf(c);
#endif
    }
    /// <summary>
    /// Checks if a string contains a character using ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="c">The character to search for.</param>
    /// <returns>true if the character is found in the string, false otherwise.</returns>
    public static bool ContainsOrdinal(this string str, char c)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return str.Contains(c, StringComparison.Ordinal);
#else
        return str.Contains(c);
#endif
    }
    /// <summary>
    /// Replaces all occurrences of a substring within a string using ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="find">The string to find.</param>
    /// <param name="replacement">The string to put in in place of <paramref name="find"/>.</param>
    /// <returns>The resulting string with all occurrences of <paramref name="find"/> replaced by <paramref name="replacement"/>.</returns>
    public static string ReplaceOrdinal(this string str, string find, string replacement)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return str.Replace(find, replacement, StringComparison.Ordinal);
#else
        return str.Replace(find, replacement);
#endif
    }
}
