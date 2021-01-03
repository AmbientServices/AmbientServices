﻿using System;
using System.Linq;

namespace AmbientServices
{
    /// <summary>
    /// A static class that extends <see cref="System.TimeSpan"/>.
    /// </summary>
    public static class TimeSpanExtensions
    {
        private static readonly char[] Alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] Numeric = "-0123456789. \t".ToCharArray();
        /// <summary>
        /// Attempts to parse a string as a timespan.
        /// </summary>
        /// <param name="span">The candidate string.</param>
        /// <returns>A <see cref="TimeSpan"/>, if one could be parsed, or <b>null</b> if not.</returns>
        //        [System.Diagnostics.DebuggerStepThrough]
        public static TimeSpan? TryParseTimeSpan(this string span)
        {
            if (string.IsNullOrEmpty(span)) return null;
            // is a span specified and does it contain a : and does it parse correctly as a timespan?
            TimeSpan timeSpan;
            if (span.Contains(':') && TimeSpan.TryParse(span, out timeSpan))
            {
                // use that timespan
                return timeSpan;
            }
            // split out the numeric part
            double units;
            if (!double.TryParse(span.TrimEnd(Alpha), out units))
            {
                return null;
            }
            // figure out the unit type
            string unitType = span.TrimStart(Numeric);
            // handle "M" specially because it's months as opposed to minutes
            if (unitType == "m") unitType = "MINUTES";
            switch (unitType.ToUpperInvariant())
            {
                case "Y":
                case "YEAR":
                case "YEARS":
                    return TimeSpan.FromDays(365.25 * units);
                case "M":
                case "MONTH":
                case "MONTHS":
                    return TimeSpan.FromDays(30.4375 * units);
                case "D":
                case "DAY":
                case "DAYS":
                    return TimeSpan.FromDays(units);
                case "H":
                case "HOUR":
                case "HOURS":
                    return TimeSpan.FromHours(units);
                case "MINUTE":
                case "MINUTES":
                    return TimeSpan.FromMinutes(units);
                case "S":
                case "SECOND":
                case "SECONDS":
                    return TimeSpan.FromSeconds(units);
                case "MS":
                case "MILLISECOND":
                case "MILLISECONDS":
                    return TimeSpan.FromMilliseconds(units);
                default:
                    return TimeSpan.FromTicks((long)units);
            }
        }
        private static bool MatchUnits(double total, int count)
        {
            if (total < 2.0)
            {
                return false;
            }
            return total >= count;
        }
        private static string UnitString(string prefix, double count, string postfix)
        {
            int intPart = (int)count;
            int firstDecimal = ((int)(count * 10.0) % 10);
            if (intPart < 10 && firstDecimal != 0)
            {
                return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
            }
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        private static string UnitStringWithPlural(string prefix, double count, string postfix)
        {
            int intPart = (int)count;
            int firstDecimal = ((int)(count * 10.0) % 10);
            if (intPart < 10 && firstDecimal != 0)
            {
                return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
            }
            // else nothing past the decimal
            if (intPart == 1) postfix = postfix + "s";
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        /// <summary>
        /// Gets a short string representing the specified timespan.
        /// </summary>
        /// <param name="duration">The <see cref="TimeSpan"/> whose string representation is to be generated</param>
        /// <returns>An easily human-readable string representing the time span with a postfix character indicating the units (ms, s, m, h, d).</returns>
        public static string ToShortHumanReadableString(this TimeSpan duration)
        {
            string sign;
            TimeSpan absTimeSpan;
            if (duration.Ticks < 0)
            {
                sign = "-";
                absTimeSpan = new TimeSpan(-duration.Ticks);
            }
            else
            {
                sign = "";
                absTimeSpan = new TimeSpan(duration.Ticks);
            }
            if (MatchUnits(absTimeSpan.TotalDays, 731))
            {
                return UnitString(sign, absTimeSpan.TotalDays / 365.25, "Y");
            }
            if (MatchUnits(absTimeSpan.TotalDays, 61))
            {
                return UnitString(sign, absTimeSpan.TotalDays / 30.4375, "M");
            }
            if (MatchUnits(absTimeSpan.TotalHours, 48))
            {
                return UnitString(sign, absTimeSpan.TotalDays, "D");
            }
            if (MatchUnits(absTimeSpan.TotalMinutes, 120))
            {
                return UnitString(sign, absTimeSpan.TotalHours, "h");
            }
            if (MatchUnits(absTimeSpan.TotalSeconds, 120))
            {
                return UnitString(sign, absTimeSpan.TotalMinutes, "m");
            }
            if (MatchUnits(absTimeSpan.TotalMilliseconds, 2000))
            {
                return UnitString(sign, absTimeSpan.TotalSeconds, "s");
            }
            return UnitString(sign, absTimeSpan.TotalMilliseconds, "ms");
        }
        /// <summary>
        /// Gets a long string representing the specified timespan.
        /// </summary>
        /// <param name="duration">The <see cref="TimeSpan"/> whose string is to be generated.</param>
        /// <returns>An easily human-readable string representing the time span with a postfix string indicating the units (millisecond, second, minute, hour, day).</returns>
        public static string ToLongHumanReadableString(this TimeSpan duration)
        {
            string sign;
            TimeSpan absTimeSpan;
            if (duration.Ticks < 0)
            {
                sign = "-";
                absTimeSpan = new TimeSpan(-duration.Ticks);
            }
            else
            {
                sign = "";
                absTimeSpan = new TimeSpan(duration.Ticks);
            }
            if (MatchUnits(absTimeSpan.TotalDays, 731))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalDays / 365.25, " Year");
            }
            if (MatchUnits(absTimeSpan.TotalDays, 61))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalDays / 30.4375, " Month");
            }
            if (MatchUnits(absTimeSpan.TotalHours, 48))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalDays, " Day");
            }
            if (MatchUnits(absTimeSpan.TotalMinutes, 120))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalHours, " hour");
            }
            if (MatchUnits(absTimeSpan.TotalSeconds, 120))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalMinutes, " minute");
            }
            if (MatchUnits(absTimeSpan.TotalMilliseconds, 2000))
            {
                return UnitStringWithPlural(sign, absTimeSpan.TotalSeconds, " second");
            }
            return UnitStringWithPlural(sign, absTimeSpan.TotalMilliseconds, " millisecond");
        }
    }
}