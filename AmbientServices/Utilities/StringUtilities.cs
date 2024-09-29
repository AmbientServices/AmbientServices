using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AmbientServices.Utilities;

/// <summary>
/// A static partial class that extends <see cref="string"/>.
/// </summary>
internal static partial class StringUtilities
{
    private static readonly char[] DecimalPointCharArray = ".,".ToCharArray();
    private static readonly char[] NumberSeparatorCharArray = ".,-".ToCharArray();
    private static readonly System.Text.RegularExpressions.Regex NumberRegex = new(
        @"(?<ps>(?:-?\d+)\.(?:(?:\d+)\.)+(?:\d+))" +    // finds a sequence of numbers separated by periods, such as 2020.5.7, and prevents them from being detected as real numbers (they should be treated as a sequence of whole numbers)
        @"|(?<ds>(?:-?\d+)-(?:(?:\d+)-)+(?:\d+))" +     // finds a sequence of numbers separated by dashes, such as 2020-5-7, and prevents them from being detected as negative numbers (they should be treated as a sequence of whole numbers)
        @"|(?<nr>(?<![0-9])(?:-(?:\d*)\.\d+))" +        // finds a negative real
        @"|(?<ni>(?<![0-9])(?:-(?:\d+)))" +             // finds a negative integer
        @"|(?<pr>(?<![-.,]\d*)(?:(?:\d*)\.\d+))" +      // finds a positive real
        @"|(?<pi>(?<![-.,]\d*)(?:\d+))",                // finds a positive integer
        System.Text.RegularExpressions.RegexOptions.Compiled);
    internal static string NormalizeStringWithNumberSequences(string str, int maxDigits)
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
                        ? NegativePartTransform("1", numberParts[0].PadLeft(maxDigits, '0')) + "." + string.Join(".", numberParts.Skip(1).Select(s => "4" + s.PadLeft(maxDigits, '0')))
                        : string.Join(".", numberParts.Select(s => "4" + s.PadLeft(maxDigits, '0')));
                    case 2: // ds: dash sequence
                        numberParts = m.Value.Split(NumberSeparatorCharArray);
                        return (numberParts[0].Length == 0 && m.Value[0] == '-')
                        ? NegativePartTransform("1", numberParts[0].PadLeft(maxDigits, '0')) + "-" + string.Join("-", numberParts.Skip(1).Select(s => "4" + s.PadLeft(maxDigits, '0')))
                        : string.Join("-", numberParts.Select(s => "4" + s.PadLeft(maxDigits, '0')));
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
        StringBuilder builder = new(prefix);
        for (int off = 0; off < str.Length; ++off)
        {
            char c = str[off];
            System.Diagnostics.Debug.Assert(c >= '0' && c <= '9');
            builder.Append((char)('0' + (9 - (c - '0'))));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Gets the index of the first occurrence of a character in a string using ordinal comparison.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="c">The character to search for.</param>
    /// <returns>The index of the first occurrence of the character in the string, or -1 if the character is not found.</returns>
    public static int IndexOfOrdinal(this string str, char c)
    {
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
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return str.Contains(c, StringComparison.Ordinal);
#else
        return str.Contains(c);
#endif
    }
}
