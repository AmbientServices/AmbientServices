using System;

namespace AmbientServices;

/// <summary>
/// A static class that extends <see cref="System.DateTime"/>.
/// </summary>
public static class WindowScope
{
    /// <summary>
    /// Gets a timestamp for the specified <see cref="DateTime"/>, with a resolution appropriate for the units near the specified <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime"/> to get an window identifier for.</param>
    /// <param name="resolution">A <see cref="TimeSpan"/> indicating what type of resolution is needed.</param>
    /// <returns>A string containing a timespan that will be distinguishable from timestamps for other <see cref="DateTime"/>s plus or minus the specified <see cref="TimeSpan"/>.</returns>
    public static string WindowId(DateTime dateTime, TimeSpan resolution)
    {
        string timestamp = dateTime.ToString("u", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('Z').Replace(' ', '_');
        if (resolution > TimeSpan.FromDays(365))
        {
            timestamp = timestamp.Substring(0, 7);
        }
        else if (resolution > TimeSpan.FromDays(30))
        {
            timestamp = timestamp.Substring(5, 5);
        }
        else if (resolution > TimeSpan.FromDays(1))
        {
            timestamp = timestamp.Substring(8, 5);
        }
        else if (resolution > TimeSpan.FromHours(1))
        {
            timestamp = timestamp.Substring(11, 5);
        }
        else if (resolution > TimeSpan.FromMinutes(1))
        {
            timestamp = timestamp.Substring(11, 8);
        }
        else if (resolution > TimeSpan.FromSeconds(1))
        {
            timestamp = string.Concat(timestamp.Substring(14), ".", dateTime.Millisecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            timestamp = string.Concat(timestamp.Substring(17), ".", dateTime.Millisecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return timestamp;
    }
    private static string UnitString(string prefix, double count, string postfix)
    {
        double rounded = Math.Round(count, 1);
        int intPart = (int)rounded;
        int firstDecimal = (int)((rounded * 10) - (intPart * 10));
        if (intPart < 10 && firstDecimal != 0)
        {
            return prefix + intPart.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + firstDecimal.ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
        }
        return prefix + Math.Round(count, 0).ToString(System.Globalization.CultureInfo.InvariantCulture) + postfix;
    }
    /// <summary>
    /// Gets a short string representing the specified timespan.
    /// </summary>
    /// <param name="duration">The <see cref="TimeSpan"/> whose string representation is to be generated</param>
    /// <returns>An easily human-readable string representing the time span with a postfix character indicating the units (ms, s, m, h, d).</returns>
    public static string WindowSize(TimeSpan duration)
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
        if (absTimeSpan.TotalDays > 1827)
        {
            return UnitString(sign, absTimeSpan.TotalDays / 365.25, "Y");
        }
        if (absTimeSpan.TotalDays > 160)
        {
            return UnitString(sign, absTimeSpan.TotalDays / 30.5, "M");
        }
        if (absTimeSpan.TotalDays > 5)
        {
            return UnitString(sign, absTimeSpan.TotalDays, "D");
        }
        if (absTimeSpan.TotalHours > 5)
        {
            return UnitString(sign, absTimeSpan.TotalHours, "h");
        }
        if (absTimeSpan.TotalMinutes > 5)
        {
            return UnitString(sign, absTimeSpan.TotalMinutes, "m");
        }
        if (absTimeSpan.TotalSeconds > 5)
        {
            return UnitString(sign, absTimeSpan.TotalSeconds, "s");
        }
        return UnitString(sign, absTimeSpan.TotalMilliseconds, "ms");
    }
}
