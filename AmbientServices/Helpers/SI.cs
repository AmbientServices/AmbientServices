using System;

namespace AmbientServices.Utilities
{
    /// <summary>
    /// A static class to hold units and unit conversions for the International System of Units (SI).
    /// </summary>
    public static class SI
    {
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Yocto = .001 * .001 * .001 * .001 * .001 * .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Zepto = .001 * .001 * .001 * .001 * .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Atto = .001 * .001 * .001 * .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Femto = .001 * .001 * .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Pico = .001 * .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Nano = .001 * .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Micro = .001 * .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Milli = .001;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Centi = .01;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Deci = .1;

        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const int Kilo = 1000;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const int Mega = 1000 * 1000;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const int Giga = 1000 * 1000 * 1000;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const long Tera = 1000L * 1000L * 1000L * 1000L;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const long Peta = 1000L * 1000L * 1000L * 1000L * 1000L;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const long Exa = 1000L * 1000L * 1000L * 1000L * 1000L * 1000L;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Zetta = 1000.0 * 1000L * 1000L * 1000L * 1000L * 1000L * 1000L;
        /// <summary>
        /// A multiplier equivalent to the SI prefix of the corresponding name.
        /// </summary>
        public const double Yotta = 1000.0 * 1000L * 1000L * 1000L * 1000L * 1000L * 1000L * 1000L;

        private static readonly string[] sSmallSiPrefixes = { "milli", "micro", "nano", "pico", "femto", "atto", "zepto", "yocto" };
        private static readonly string[] sShortSmallSiPrefixes = { "m", "μ", "n", "p", "f", "a", "z", "y" };
        private static readonly string[] sLargeSiPrefixes = { "kilo", "mega", "giga", "tera", "peta", "exa", "zetta", "yotta" };
        private static readonly string[] sShortLargeSiPrefixes = { "k", "M", "G", "T", "P", "E", "Z", "Y" };     // NOTE here that the proper SI prefix for kilo is a lower-case k, not upper-case like all the others.  This is to distinguish Kilo- from Kelvin, which is an unfortunate inconsistency.
        private const int YottaPrefixIndex = 7;
        private const int YoctoPrefixIndex = 7;
        /// <summary>
        /// Gets a string containing an abbreviated version of a number using the International System of Units (SI).
        /// SI Units are exact decimal units, mostly powers of 1000.
        /// </summary>
        /// <param name="number">The number to output.</param>
        /// <param name="maxCharacters">The maximum number of characters to use to represent the numeric part (if possible), including the decimal point.  Defaults to 4 (three significant digits).  When less than 4, the number may not be able to be represented in the specified number of characters (57 in 1 character, for example), in which case, it will use the minimum possible number of characters.</param>
        /// <param name="postfix">A postfix string (for example, "B").</param>
        /// <param name="longName">Whether or not to use the long version of the Si prefix (kilo, mega, etc.)</param>
        /// <param name="positiveSign">Whether or not to includ a positive sign on positive numbers.</param>
        /// <param name="culture">The <see cref="System.Globalization.CultureInfo"/> to use to format the number, defaults to the current thread culture.</param>
        /// <returns>The SI representation of the number, for example for 5342432, the return value might be "5.34MB".</returns>
        public static string ToSi(this double number, int maxCharacters = 4, string? postfix = null, bool longName = false, bool positiveSign = false, System.Globalization.CultureInfo? culture = null)
        {
            if (culture == null) culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            if (postfix == null) postfix = string.Empty;
            if (double.IsNaN(number)) return "NaN";
            if (double.IsNegativeInfinity(number)) return "-INF";
            if (double.IsPositiveInfinity(number)) return positiveSign ? "+INF" : "INF";
            // small non-zero number?
            if (number != 0.0 && number > -0.9995 && number < 0.9995)
            {
                if (number == -double.Epsilon) return "-EPS";
                if (number == double.Epsilon) return positiveSign ? "+EPS" : "EPS";
                string[] prefixes = longName ? sSmallSiPrefixes : sShortSmallSiPrefixes;
                string extraYoctos = "";
                while (number < Yocto && number > -1.0 * Yocto)
                {
                    extraYoctos += prefixes[YoctoPrefixIndex];
                    number /= Yocto;
                }
                int magnitude;
                for (magnitude = 0; magnitude < sSmallSiPrefixes.Length && number > -0.9995 && number < 0.9995; ++magnitude)
                {
                    number *= 1000.0;
                }
                // handle a weird floating point rounding situation where we end up with 999.5 here
                if (number <= -999.5 || number >= 999.5)
                {
                    // undo the last multiplication and round (this should correct for the problem)
                    number = Math.Round(number / 1000.0, 15);
                    --magnitude;
                }
                System.Diagnostics.Debug.Assert(number > -999.5 && number < 999.5);
                int digitsAfterDecimal = Math.Max(0, maxCharacters - (int)Math.Log10(Math.Abs(number)) - 2);     // Log10 give us the number of digits *before* the decimal point minus one, but we have to compensate for the decimal point itself as well
                return ((positiveSign && number > 0.0) ? "+" : "") + number.ToString("N0" + digitsAfterDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture), culture.NumberFormat) + (longName ? (" ") : "") + prefixes[magnitude - 1] + extraYoctos + postfix;
            }
            // large number?
            else if (number <= -999.5 || number >= 999.5)
            {
                if (number == double.MinValue) return "-MAX";
                if (number == double.MaxValue) return positiveSign ? "+MAX" : "MAX";
                string[] prefixes = longName ? sLargeSiPrefixes : sShortLargeSiPrefixes;
                string extraYottas = "";
                while (number >= 1000.0 * Yotta || number <= -1000.0 * Yotta)
                {
                    extraYottas += prefixes[YottaPrefixIndex];
                    number /= Yotta;
                }
                int magnitude;
                for (magnitude = 0; magnitude < sLargeSiPrefixes.Length && (number <= -999.5 || number >= 999.5); ++magnitude)
                {
                    number /= 1000.0;
                }
                // note that the rounding issue in the corresponding algorithm above doesn't happen when we're going this direction because we're dividing here and will naturally end up with the smaller number (which is what we want because a number that's too large will result in three digits to the LEFT of the decimal point, which is what we're tyring to avoid)
                System.Diagnostics.Debug.Assert(number > -999.5 && number < 999.5);
                int digitsAfterDecimal = Math.Max(0, maxCharacters - (int)Math.Log10(Math.Abs(number)) - 2);     // Log10 give us the number of digits *before* the decimal point minus one, but we have to compensate for the decimal point itself as well
                return ((positiveSign && number > 0.0) ? "+" : "") + number.ToString("N0" + digitsAfterDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture), culture.NumberFormat) + (longName ? (" ") : "") + (longName ? sLargeSiPrefixes : sShortLargeSiPrefixes)[magnitude - 1] + extraYottas + postfix;
            }
            else // just a normal number!
            {
                System.Diagnostics.Debug.Assert(number > -999.5 && number < 999.5);
                int digitsAfterDecimal =  Math.Max(0, maxCharacters - ((number == 0) ? 0 : (int)Math.Log10(Math.Abs(number))) - 2);     // Log10 give us the number of digits *before* the decimal point minus one, but we have to compensate for the decimal point itself as well
                return ((positiveSign && number > 0.0) ? "+" : "") + number.ToString("N0" + digitsAfterDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture), culture.NumberFormat) + (longName ? " " : "") + postfix;
            }
        }
        /// <summary>
        /// Gets a string containing an abbreviated version of a number using the International System of Units (SI).
        /// SI Units are exact decimal units, mostly powers of 1000.
        /// </summary>
        /// <param name="number">The number to output.</param>
        /// <param name="maxCharacters">The maximum number of characters to use to represent the numeric part (if possible), including the decimal point.  Defaults to 4 (three significant digits).  When less than 4, the number may not be able to be represented in the specified number of characters (57 in 1 character, for example), in which case, it will use the minimum possible number of characters.</param>
        /// <param name="postfix">A postfix string (for example, "B").</param>
        /// <param name="longName">Whether or not to use the long version of the Si prefix (kilo, mega, etc.)</param>
        /// <param name="positiveSign">Whether or not to includ a positive sign on positive numbers.</param>
        /// <param name="culture">The <see cref="System.Globalization.CultureInfo"/> to use to format the number, defaults to the current thread culture.</param>
        /// <returns>The SI representation of the number, for example for 5342432, the return value might be "5.34MB".</returns>
        public static string ToSi(this float number, int maxCharacters = 4, string? postfix = null, bool longName = false, bool positiveSign = false, System.Globalization.CultureInfo? culture = null)
        {
            return ToSi((double)number, maxCharacters, postfix, longName, positiveSign, culture);
        }
    }
}
