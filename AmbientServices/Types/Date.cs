using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A class that represents just a date (no time, no timezone).  Can represent Gregorian dates from 0001.01.01 to a little after 9999.12.31
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [Serializable]
#pragma warning disable CA1716  // if Microsoft implements this and doesn't implement it functionally equivalent to this, they did it wrong
    public struct Date : IComparable, IFormattable, IConvertible, ISerializable, IComparable<Date>, IEquatable<Date>
#pragma warning restore CA1716
    {
        /// <summary>
        /// The number of days since 0001.01.01 in the Gregorian Calendar.  
        /// Should never exceed <see cref="DaysTo10000"/> (3,652,059).
        /// </summary>
        private int _daysSinceCalendarStart;

        // number of days in a non-leap year
        private const int YearsPerLeapCycle = 400;
        // number of days in a non-leap year
        private const int DaysPerNonLeapYear = 365;
        // number of days in a leap year
        private const int DaysPerLeapYear = 366;
        // number of days in 4 years (1461)
        private const int DaysPerQuadrayear = DaysPerNonLeapYear * 4 + 1;
        // number of days in most centuries (36524)  (every fourth century will have an extra leap day)
        private const int DaysPerCentury = DaysPerQuadrayear * 25 - 1;
        // number of days in a leap cycle (146097)
        private const int DaysPerLeapCycle = DaysPerCentury * 4 + 1;
        // number of System.DateTime ticks in a day 864,000,000,000
        private const long DateTimeTicksPerDay = 10000L * 1000 * 60 * 60 * 24;
        // number of days from 0001.01.01 to 9999.12.31 (3652059)
        private const int DaysTo10000 = DaysPerLeapCycle * 25 - DaysPerLeapYear;
        // number of days to the beginning of each month in a non leap year
        private static readonly int[] DaysToMonthStartNonLeapYear = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
        // number of days to the beginning of each month in a leap year
        private static readonly int[] DaysToMonthStartLeapYear = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };
        // number of cumulative days since the leap cycle start (by year within the leap cycle)  NOTE: assume that the start of the cycle is the first year *after* the year that's divisible by 400, so 2001, for example
        private static readonly int[] OneBasedCumulativeDaysSinceLeapCycleStartByYear = {
                0,365,730,1095,1461,1826,2191,2556,2922,3287,3652,4017,4383,4748,5113,5478,5844,6209,6574,6939,7305,7670,8035,8400,8766,9131,9496,9861,10227,10592,10957,11322,11688,12053,12418,12783,13149,13514,13879,14244,14610,14975,15340,15705,16071,16436,16801,17166,17532,17897,18262,18627,18993,19358,19723,20088,20454,20819,21184,21549,21915,22280,22645,23010,23376,23741,24106,24471,24837,25202,25567,25932,26298,26663,27028,27393,27759,28124,28489,28854,29220,29585,29950,30315,30681,31046,31411,31776,32142,32507,32872,33237,33603,33968,34333,34698,35064,35429,35794,36159,
                36524,36889,37254,37619,37985,38350,38715,39080,39446,39811,40176,40541,40907,41272,41637,42002,42368,42733,43098,43463,43829,44194,44559,44924,45290,45655,46020,46385,46751,47116,47481,47846,48212,48577,48942,49307,49673,50038,50403,50768,51134,51499,51864,52229,52595,52960,53325,53690,54056,54421,54786,55151,55517,55882,56247,56612,56978,57343,57708,58073,58439,58804,59169,59534,59900,60265,60630,60995,61361,61726,62091,62456,62822,63187,63552,63917,64283,64648,65013,65378,65744,66109,66474,66839,67205,67570,67935,68300,68666,69031,69396,69761,70127,70492,70857,71222,71588,71953,72318,72683,
                73048,73413,73778,74143,74509,74874,75239,75604,75970,76335,76700,77065,77431,77796,78161,78526,78892,79257,79622,79987,80353,80718,81083,81448,81814,82179,82544,82909,83275,83640,84005,84370,84736,85101,85466,85831,86197,86562,86927,87292,87658,88023,88388,88753,89119,89484,89849,90214,90580,90945,91310,91675,92041,92406,92771,93136,93502,93867,94232,94597,94963,95328,95693,96058,96424,96789,97154,97519,97885,98250,98615,98980,99346,99711,100076,100441,100807,101172,101537,101902,102268,102633,102998,103363,103729,104094,104459,104824,105190,105555,105920,106285,106651,107016,107381,107746,108112,108477,108842,109207,
                109572,109937,110302,110667,111033,111398,111763,112128,112494,112859,113224,113589,113955,114320,114685,115050,115416,115781,116146,116511,116877,117242,117607,117972,118338,118703,119068,119433,119799,120164,120529,120894,121260,121625,121990,122355,122721,123086,123451,123816,124182,124547,124912,125277,125643,126008,126373,126738,127104,127469,127834,128199,128565,128930,129295,129660,130026,130391,130756,131121,131487,131852,132217,132582,132948,133313,133678,134043,134409,134774,135139,135504,135870,136235,136600,136965,137331,137696,138061,138426,138792,139157,139522,139887,140253,140618,140983,141348,141714,142079,142444,142809,143175,143540,143905,144270,144636,145001,145366,145731,
            };
        // whether or not a given year in the leap cycle is a leap year (first entry is for years just after the 400-year leap year, so for example, 2001)
        private static readonly bool[] OneBasedIsLeapCycleYearLeapYear = {
                false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,false, // the century years that are not divisible by 400 are NOT leap years
                false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,false,
                false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,false,
                false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true,false,false,false,true, // the 400-year century years ARE leap years
            };
        // a name for the days field for serialization
        private const string DaysField = "days";

        /// <summary>
        /// Represents the largest possible value of <see cref="Date"/>. This field is read-only.
        /// </summary>
        public static readonly Date MaxValue = new Date(DaysTo10000 - 1);
        /// <summary>
        /// Represents the smallest possible value of <see cref="Date"/>. This field is read-only.
        /// </summary>
        public static readonly Date MinValue = new Date(0);

        /// <summary>
        /// Constructs a new <see cref="Date"/> using the number of days since the calendar start.
        /// </summary>
        /// <param name="daysSinceCalendarStart"></param>
        public Date(int daysSinceCalendarStart)
        {
            _daysSinceCalendarStart = daysSinceCalendarStart;
        }

        /// <summary>
        /// Gets the number of days since the calendar start.  (Kind of like Ticks for Date).
        /// </summary>
        public int DaysSinceCalendarStart { get { return _daysSinceCalendarStart; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="Date"/> structure to the specified year, month, and day (all one-based).
        /// </summary>
        /// <param name="year">The year (1 through 9999).</param>
        /// <param name="month">The month (1 through 12).</param>
        /// <param name="day">The day (1 through the number of days in month).</param>
        public Date(int year, int month, int day)
        {
            if (year < 1 || year > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "The specified year is not in the valid range of 1-9999");
            }
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), "The specified month is not in the valid range of 1-12");
            }
            // get the day chart for the year
            int[] days = IsLeapYear(year) ? DaysToMonthStartLeapYear : DaysToMonthStartNonLeapYear;
            if (day < 1 || day > days[month] - days[month - 1])
            {
                throw new ArgumentOutOfRangeException(nameof(day), "The specified day is not in the valid range of 1-" + (days[month] - days[month - 1]).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            int y = year - 1;
            int n = y * 365 + y / 4 - y / 100 + y / YearsPerLeapCycle + days[month - 1] + day - 1;
            _daysSinceCalendarStart = n;
        }

        /// <summary>
        /// Deserializes a previously serialized <see cref="Date"/> using the provided <see cref="SerializationInfo"/> and <see cref="StreamingContext"/>.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> containing the serialized form of the data.</param>
        /// <param name="context">The <see cref="StreamingContext"/> indicating how the serialization occurred.</param>
        [ExcludeFromCoverage]   // this is only here to provide an implementation that is as close as possible to System.Date.   However, BinaryFormatter has been deprecated so we won't bother testing it
        private Date(SerializationInfo info, StreamingContext context)
        {   // 
            // Note that this function is only invoked by the .NET Serialization system, and it's not clear at all how to exercise some code paths
            // 
            if (info == null) throw new ArgumentNullException(nameof(info));

            bool foundDays = false;
            int serializedDays = 0;

            SerializationInfoEnumerator enumerator = info.GetEnumerator();
            while (enumerator.MoveNext())
            {
                switch (enumerator.Name)
                {
                    case DaysField:
                        serializedDays = Convert.ToInt32(enumerator.Value, CultureInfo.InvariantCulture);
                        foundDays = true;
                        break;
                    default:
                        // Ignore other fields for forward compatability.
                        break;
                }
            }
            if (foundDays)
            {
                if (serializedDays > MaxValue.DaysSinceCalendarStart || serializedDays < MinValue.DaysSinceCalendarStart)
                {
                    throw new SerializationException("The Date.days information is outside of the allowable range!");
                }
                _daysSinceCalendarStart = serializedDays;
            }
            else
            {
                throw new SerializationException("The serialization data is missing the Date.days information!");
            }
        }

        /// <summary>
        /// Returns whether or not the specified year is a leap year.
        /// </summary>
        /// <param name="year">The year to check.</param>
        /// <returns><b>true</b> if year is a leap year; otherwise, <b>false</b>.</returns>
        public static bool IsLeapYear(int year)
        {
            if (year < 1 || year > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "The specified year is not in the valid range of 1-9999");
            }
            return OneBasedIsLeapCycleYearLeapYear[(year - 1) % YearsPerLeapCycle];
        }

        /// <summary>
        /// Subtracts a specified date from another specified date and returns a time interval.
        /// </summary>
        /// <param name="d1">A <see cref="Date"/> (the minuend).</param>
        /// <param name="d2">A <see cref="Date"/> (the subtrahend).</param>
        /// <returns>A <see cref="TimeSpan"/> that is the time interval between <paramref name="d1"/> and <paramref name="d2"/>; that is, <paramref name="d1"/> minus <paramref name="d2"/>.</returns>
        public static TimeSpan operator -(Date d1, Date d2)
        {
            return TimeSpan.FromDays(d1._daysSinceCalendarStart - d2._daysSinceCalendarStart);
        }
        /// <summary>
        /// Subtracts a specified time interval from a specified date and returns a new date.  If the time interval has something other than day parts, it is truncated to the nearest number of days.
        /// </summary>
        /// <param name="d">A <see cref="Date"/>.</param>
        /// <param name="t">A <see cref="TimeSpan"/> to subtract.</param>
        /// <returns><see cref="Date"/> whose value is the value of <paramref name="d"/> minus the value of <paramref name="t"/>.</returns>
        public static Date operator -(Date d, TimeSpan t)
        {
            return new Date(d._daysSinceCalendarStart - (int)t.TotalDays);
        }
        /// <summary>
        /// Determines whether two specified instances of <see cref="Date"/> are not equal.
        /// </summary>
        /// <param name="d1">A <see cref="Date"/>.</param>
        /// <param name="d2">A <see cref="Date"/>.</param>
        /// <returns><b>false</b> if <paramref name="d1"/> and <paramref name="d2"/> do not represent the same date and time; otherwise, <b>false</b>.</returns>
        public static bool operator !=(Date d1, Date d2)
        {
            return d1._daysSinceCalendarStart != d2._daysSinceCalendarStart;
        }
        /// <summary>
        /// Adds a specified time interval to a specified date and returns a new date.  If the time interval has something other than day parts, it is truncated to the nearest number of days.
        /// </summary>
        /// <param name="d">A <see cref="Date"/>.</param>
        /// <param name="t">A <see cref="TimeSpan"/> to add.</param>
        /// <returns><see cref="Date"/> whose value is the value of <paramref name="d"/> minus the value of <paramref name="t"/>.</returns>
        public static Date operator +(Date d, TimeSpan t)
        {
            return new Date(d._daysSinceCalendarStart + (int)t.TotalDays);
        }
        /// <summary>
        /// Determines whether one specified Date is less than another specified Date.
        /// </summary>
        /// <param name="t1">A <see cref="Date"/>.</param>
        /// <param name="t2">A <see cref="Date"/>.</param>
        /// <returns><b>true</b> if <paramref name="t1"/> is less than <paramref name="t2"/>; otherwise, <b>false</b>.</returns>
        public static bool operator <(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart < t2._daysSinceCalendarStart;
        }
        /// <summary>
        /// Determines whether one specified Date is less or equal to another specified Date.
        /// </summary>
        /// <param name="t1">A <see cref="Date"/>.</param>
        /// <param name="t2">A <see cref="Date"/>.</param>
        /// <returns><b>true</b> if <paramref name="t1"/> is less than or equal to <paramref name="t2"/>; otherwise, <b>false</b>.</returns>
        public static bool operator <=(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart <= t2._daysSinceCalendarStart;
        }
        /// <summary>
        /// Determines whether one specified Date is equal to another specified Date.
        /// </summary>
        /// <param name="t1">A <see cref="Date"/>.</param>
        /// <param name="t2">A <see cref="Date"/>.</param>
        /// <returns><b>true</b> if <paramref name="t1"/> is equal to <paramref name="t2"/>; otherwise, <b>false</b>.</returns>
        public static bool operator ==(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart == t2._daysSinceCalendarStart;
        }
        /// <summary>
        /// Determines whether one specified Date is greater than another specified Date.
        /// </summary>
        /// <param name="t1">A <see cref="Date"/>.</param>
        /// <param name="t2">A <see cref="Date"/>.</param>
        /// <returns><b>true</b> if <paramref name="t1"/> is greater than <paramref name="t2"/>; otherwise, <b>false</b>.</returns>
        public static bool operator >(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart > t2._daysSinceCalendarStart;
        }
        /// <summary>
        /// Determines whether one specified Date is greater or equal to another specified Date.
        /// </summary>
        /// <param name="t1">A <see cref="Date"/>.</param>
        /// <param name="t2">A <see cref="Date"/>.</param>
        /// <returns><b>true</b> if <paramref name="t1"/> is greater than or equal to <paramref name="t2"/>; otherwise, <b>false</b>.</returns>
        public static bool operator >=(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart >= t2._daysSinceCalendarStart;
        }

        private enum ParsePart
        {
            Year,
            DayOfYear,
            Month,
            DayOfMonth,
        }

        private static int GetDatePart(int daysSinceCalendarStart, ParsePart part)
        {
            // number of leap cycles since 0001.01.01
            int leapCycle = daysSinceCalendarStart / DaysPerLeapCycle;
            // day number within leap cycle
            int leapCycleLeftoverDays = daysSinceCalendarStart % DaysPerLeapCycle;
            // rather than slowly scanning the array, make a guess (erring to the lesser side) for the year within the leap cycle
            int yearWithinLeapCycle = leapCycleLeftoverDays / DaysPerLeapYear;
            // now scan forward in the array to determine the exact year within the leap cycle
            while (yearWithinLeapCycle + 1 < OneBasedCumulativeDaysSinceLeapCycleStartByYear.Length && leapCycleLeftoverDays >= OneBasedCumulativeDaysSinceLeapCycleStartByYear[yearWithinLeapCycle + 1]) ++yearWithinLeapCycle;
            // did the caller ask for the year?
            if (part == ParsePart.Year)
            {
                // return the year (one-based)
                return leapCycle * YearsPerLeapCycle + yearWithinLeapCycle + 1;
            }
            // get the day number within the year
            int dayOfYear = leapCycleLeftoverDays - OneBasedCumulativeDaysSinceLeapCycleStartByYear[yearWithinLeapCycle];
            // did the caller ask for the day of the year?
            if (part == ParsePart.DayOfYear)
            {
                // return the day of the year (one-based)
                return dayOfYear + 1;
            }
            // is this day in a leap year?
            bool leapYear = OneBasedIsLeapCycleYearLeapYear[yearWithinLeapCycle];
            // get the day list for this year
            int[] days = leapYear ? DaysToMonthStartLeapYear : DaysToMonthStartNonLeapYear;
            // rather than just slowly scanning the array, make a guess (erring to the lesser side) for the month within the year
            int month = (dayOfYear >> 5) + 1;
            // adjust the month if needed
            while (dayOfYear >= days[month]) ++month;
            // did the caller ask for the month?
            if (part == ParsePart.Month)
            {
                // return the month (one-based)
                return month;
            }
            // the caller better have asked for the day of the month--otherwise, what did they ask for?
            System.Diagnostics.Debug.Assert(part == ParsePart.DayOfMonth);
            // return the day-of-month (one-based)
            return dayOfYear - days[month - 1] + 1;
        }

        /// <summary>
        /// Gets the one-based year component of the date represented by this instance.
        /// </summary>
        /// <returns>The year, between 1 and 9999.</returns>
        public int Year { get { return GetDatePart(_daysSinceCalendarStart, ParsePart.Year); } }
        /// <summary>
        /// Gets the one-based month component of the date represented by this instance.
        /// </summary>
        /// <returns>The month component, expressed as a value between 1 and 12.</returns>
        public int Month { get { return GetDatePart(_daysSinceCalendarStart, ParsePart.Month); } }
        /// <summary>
        /// Gets the one-based day of the month represented by this instance.
        /// </summary>
        /// <returns>The day component, expressed as a value between 1 and 31.</returns>
        public int Day { get { return GetDatePart(_daysSinceCalendarStart, ParsePart.DayOfMonth); } }
        /// <summary>
        /// Gets the day of the week represented by this instance.
        /// </summary>
        /// <returns>A <see cref="DayOfWeek"/> enumerated constant that indicates the day of the week of this <see cref="Date"/> value.</returns>
        public DayOfWeek DayOfWeek { get { return (DayOfWeek)((_daysSinceCalendarStart + 1) % 7); } }
        /// <summary>
        /// Gets the one-based day of the year represented by this instance.
        /// </summary>
        /// <returns>The day of the year, expressed as a value between 1 and 366.</returns>
        public int DayOfYear { get { return GetDatePart(_daysSinceCalendarStart, ParsePart.DayOfYear); } }

        /// <summary>
        /// Adds the number of whole days in the specified <see cref="TimeSpan"/> to the value of this instance.
        /// </summary>
        /// <param name="timespan">A System.TimeSpan object that represents a positive or negative time interval.</param>
        /// <returns>A <see cref="Date"/> whose value is the sum of the date represented by this instance and the number of whole days represented by <paramref name="timespan"/>.</returns>
        public Date Add(TimeSpan timespan)
        {
            return new Date(_daysSinceCalendarStart + (int)timespan.TotalDays);
        }
        /// <summary>
        /// Adds the specified number of days to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole days. The value parameter can be negative or positive.</param>
        /// <returns>A <see cref="Date"/> whose value is the sum of the date represented by this instance and the number of days represented by <paramref name="value"/>.</returns>
        public Date AddDays(int value)
        {
            return new Date(_daysSinceCalendarStart + value);
        }
        /// <summary>
        /// Adds the specified number of months to the value of this instance.
        /// Note that the behavior near the last day of the month may be unexpected, as if you're adding one month to the 29th of January on a non-leap year, you'd get an invalid date.
        /// Such dates are adjusted to be the last valid day of the month for the target month.
        /// </summary>
        /// <param name="months">A number of months. The months parameter can be negative or positive.</param>
        /// <returns>A <see cref="Date"/> whose value is the sum of the date and time represented by this instance and months.</returns>
        public Date AddMonths(int months)
        {
            int year = Year;
            int month = Month + months;
            while (month > 12)
            {
                month -= 12;
                ++year;
            }
            while (month < 1)
            {
                month += 12;
                --year;
            }
            // special case: adjust the last day of the month if it's out of range (the 31st of February, for example)
            int day = Day;
            int[] days = IsLeapYear(year) ? DaysToMonthStartLeapYear : DaysToMonthStartNonLeapYear;
            if (day > days[month] - days[month - 1])
            {
                day = days[month] - days[month - 1];
            }
            return new Date(year, month, day);
        }
        /// <summary>
        /// Adds the specified number of years to the value of this instance.
        /// </summary>
        /// <param name="years">A number of years. The value parameter can be negative or positive.</param>
        /// <returns>A <see cref="Date"/> whose value is the sum of the date and time represented by this instance and the number of years represented by value.</returns>
        public Date AddYears(int years)
        {
            return new Date(Year + years, Month, Day);
        }
        /// <summary>
        /// Compares two instances of Date and returns an integer that indicates whether the first instance is earlier than, the same as, or later than the second instance.
        /// </summary>
        /// <param name="t1">The first <see cref="Date"/>.</param>
        /// <param name="t2">The second <see cref="Date"/>.</param>
        /// <returns>A signed number indicating the relative values of <paramref name="t1"/> and <paramref name="t2"/>.</returns>
        /// <value>Condition Less than zero <paramref name="t1"/> is earlier than <paramref name="t2"/>. Zero <paramref name="t1"/> is the same as <paramref name="t2"/>. Greater than zero <paramref name="t1"/> is later than <paramref name="t2"/>.</value>
        public static int Compare(Date t1, Date t2)
        {
            return t1._daysSinceCalendarStart.CompareTo(t2._daysSinceCalendarStart);
        }

        #region IComparable<Date> Members

        /// <summary>
        /// Compares the value of this instance to a specified <see cref="Date"/> value and returns an integer that indicates whether this instance is earlier than, the same as, or later than the specified <see cref="Date"/> value.
        /// </summary>
        /// <param name="obj">
        /// A <see cref="Date"/> object to compare.
        /// </param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and the value
        /// parameter.
        /// </returns>
        /// <value>
        /// Less than zero This instance is earlier than
        /// value. Zero This instance is the same as value. Greater than zero This instance
        /// is later than value.
        /// </value>
        public int CompareTo(Date obj)
        {
            return _daysSinceCalendarStart.CompareTo(obj._daysSinceCalendarStart);
        }

        #endregion


        #region IComparable Members

        /// <summary>
        /// Compares the value of this instance to a specified object that contains a specified <see cref="Date"/> value, and returns an integer that indicates whether this instance is earlier than, the same as, or later than the specified <see cref="Date"/> value.
        /// </summary>
        /// <param name="obj">A boxed <see cref="Date"/> object to compare, or null.</param>
        /// <returns>A signed number indicating the relative values of this instance and value.</returns>
        /// <value>Less than zero This instance is earlier than value. Zero This instance is the same as value. Greater than zero This instance is later than value, or value is null.</value>
        public int CompareTo(object? obj)
        {
            if (obj is Date)
            {
                obj = ((Date)obj)._daysSinceCalendarStart;
            }
            return _daysSinceCalendarStart.CompareTo(obj);
        }

        #endregion
        /// <summary>
        /// Returns the number of days in the specified month and year.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month (a number ranging from 1 to 12).</param>
        /// <returns>The number of days in month for the specified year.  For example, if month equals 2 for February, the return value is 28 or 29 depending upon whether year is a leap year.</returns>
        public static int DaysInMonth(int year, int month)
        {
            return DateTime.DaysInMonth(year, month);
        }

        #region IEquatable<Date> Members
        /// <summary>
        /// Returns a value indicating whether this instance is equal to the specified <see cref="Date"/> instance. </summary>
        /// <param name="other">A <see cref="Date"/> instance to compare to this instance.</param>
        /// <returns><b>true</b> if the value parameter equals the value of this instance; otherwise, <b>false</b>.</returns>
        public bool Equals(Date other)
        {
            return _daysSinceCalendarStart.Equals(other._daysSinceCalendarStart);
        }
        #endregion

        /// <summary>
        /// Returns a value indicating whether this instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">An object to compare to this instance.</param>
        /// <returns><b>true</b> if value is an instance of <see cref="Date"/> and equals the value of this instance; otherwise, <b>false</b>.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is Date)
            {
                obj = ((Date)obj)._daysSinceCalendarStart;
            }
            return _daysSinceCalendarStart.Equals(obj);
        }
        /// <summary>
        /// Returns a value indicating whether two instances of <see cref="Date"/> are equal.
        /// </summary>
        /// <param name="t1">The first <see cref="Date"/> instance.</param>
        /// <param name="t2">The second <see cref="Date"/> instance.</param>
        /// <returns><b>true</b> if the two <see cref="Date"/> values are equal; otherwise, <b>false</b>.</returns>
        public static bool Equals(Date t1, Date t2)
        {
            return DateTime.Equals(t1._daysSinceCalendarStart, t2._daysSinceCalendarStart);
        }
        /// <summary>
        /// Converts a <see cref="System.DateTime"/> to a <see cref="Date"/>.
        /// </summary>
        /// <param name="dateTime">The <see cref="System.DateTime"/> to convert from.</param>
        /// <returns>A <see cref="Date"/>.</returns>
        public static Date FromDateTime(DateTime dateTime)
        {
            return new Date((int)(dateTime - DateTime.MinValue).TotalDays);
        }
        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return _daysSinceCalendarStart.GetHashCode();
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/> equivalent.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <returns>A <see cref="Date"/> equivalent to the date contained in <paramref name="s"/>.</returns>
        public static Date Parse(string s)
        {
            return FromDateTime(DateTime.Parse(s, System.Globalization.CultureInfo.CurrentCulture));
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/>
        /// equivalent using the specified culture-specific format information.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="provider">An object that supplies culture-specific format information about <paramref name="s"/>.</param>
        /// <returns>A <see cref="Date"/> equivalent to the date and time contained in <paramref name="s"/> as specified by provider.</returns>
        public static Date Parse(string s, IFormatProvider provider)
        {
            return FromDateTime(DateTime.Parse(s, provider));
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/>
        /// equivalent using the specified culture-specific format information and formatting
        /// style.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information about <paramref name="s"/>.</param>
        /// <param name="styles"> A bitwise combination of the enumeration values that indicates the style elements that can be present in <paramref name="s"/> for the parse operation to succeed and that defines how to interpret the parsed date in relation to the current time zone or the current date. A typical value to specify is <see cref="System.Globalization.DateTimeStyles.None"/>.</param>
        /// <returns>A <see cref="Date"/> equivalent to the date contained in s as specified by provider and styles.</returns>
        public static Date Parse(string s, IFormatProvider provider, System.Globalization.DateTimeStyles styles)
        {
            return FromDateTime(DateTime.Parse(s, provider, styles));
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/>
        /// equivalent using the specified format and culture-specific format information.
        /// The format of the string representation must match the specified format exactly.
        /// </summary>
        /// <param name="s">A string that contains a date to convert.</param>
        /// <param name="format">A format specifier that defines the required format of <paramref name="s"/>.</param>
        /// <param name="provider">An object that supplies culture-specific format information about <paramref name="s"/>.</param>
        /// <returns>A <see cref="Date"/> equivalent to the date contained in s as specified by format and provider.</returns>
        public static Date ParseExact(string s, string format, IFormatProvider provider)
        {
            return FromDateTime(DateTime.ParseExact(s, format, provider));
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/>
        /// equivalent using the specified format, culture-specific format information,
        /// and style. The format of the string representation must match the specified
        /// format exactly or an exception is thrown.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="format">A format specifier that defines the required format of <paramref name="s"/>.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information about <paramref name="s"/>.</param>
        /// <param name="style">A bitwise combination of the enumeration values that provides additional information about <paramref name="s"/>, about style elements that may be present in <paramref name="s"/>, or about the conversion from <paramref name="s"/> to a <see cref="Date"/> value. A typical value to specify is <see cref="System.Globalization.DateTimeStyles.None"/>. </param>
        /// <returns>A <see cref="Date"/> equivalent to the date contained in s as specified by format, provider, and style. </returns>
        public static Date ParseExact(string s, string format, IFormatProvider provider, System.Globalization.DateTimeStyles style)
        {
            return FromDateTime(DateTime.ParseExact(s, format, provider, style));
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/>
        /// equivalent using the specified array of formats, culture-specific format
        /// information, and style. The format of the string representation must match
        /// at least one of the specified formats exactly or an exception is thrown.
        /// </summary>
        /// <param name="s">A string containing one or more dates and times to convert.</param>
        /// <param name="formats">An array of allowable formats of <paramref name="s"/>.</param>
        /// <param name="provider">An <see cref="IFormatProvider"/> that supplies culture-specific format information about <paramref name="s"/>.</param>
        /// <param name="style">A bitwise combination of System.Globalization.DateTimeStyles values that indicates the permitted format of <paramref name="s"/>. A typical value to specify is <see cref="System.Globalization.DateTimeStyles.None"/>.</param>
        /// <returns>A <see cref="Date"/> equivalent to the date contained in s as specified by formats, provider, and style.</returns>
        public static Date ParseExact(string s, string[] formats, IFormatProvider provider, System.Globalization.DateTimeStyles style)
        {
            return FromDateTime(DateTime.ParseExact(s, formats, provider, style));
        }
        /// <summary>
        /// Subtracts the specified date from this instance.
        /// </summary>
        /// <param name="value">An instance of <see cref="Date"/>.</param>
        /// <returns>A <see cref="TimeSpan"/> interval equal to the date represented by this instance minus the date represented by value.  Will always be in whole days.</returns>
        public TimeSpan Subtract(Date value)
        {
            return TimeSpan.FromDays(_daysSinceCalendarStart - value._daysSinceCalendarStart);
        }
        /// <summary>
        /// Subtracts the specified duration from this instance.
        /// </summary>
        /// <param name="value">An instance of <see cref="TimeSpan"/>.</param>
        /// <returns>A <see cref="Date"/> equal to the date and time represented by this instance minus the time interval represented by value.</returns>
        public Date Subtract(TimeSpan value)
        {
            return new Date(_daysSinceCalendarStart - (int)value.TotalDays);
        }
        /// <summary>
        /// Converts the <see cref="Date"/> to a <see cref="DateTime"/>.
        /// </summary>
        /// <returns>A <see cref="DateTime"/>.</returns>
        public DateTime ToDateTime()
        {
            return new DateTime(_daysSinceCalendarStart * DateTimeTicksPerDay);
        }
        /// <summary>
        /// Converts the value of the current <see cref="Date"/> object to its equivalent long date string representation.
        /// </summary>
        /// <returns>A string that contains the long date string representation of the current <see cref="Date"/> object.</returns>
        public string ToLongDateString()
        {
            return ToDateTime().ToLongDateString();
        }
        /// <summary>
        /// Converts the value of the current System.DateTime object to its equivalent short date string representation.
        /// </summary>
        /// <returns>A string that contains the short date string representation of the current <see cref="Date"/> object.</returns>
        public string ToShortDateString()
        {
            return ToDateTime().ToShortDateString();
        }

        /// <summary>
        /// Converts the value of the current <see cref="Date"/> object to its equivalent string representation.
        /// </summary>
        /// <returns>A string representation of the value of the current <see cref="Date"/> object.</returns>
        public override string ToString()
        {
            return ToDateTime().ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        }
        /// <summary>
        /// Converts the value of the current <see cref="Date"/> object to its equivalent string representation using the specified culture-specific format information.
        /// </summary>
        /// <param name="provider">An <see cref="IFormatProvider"/> that supplies culture-specific formatting information.</param>
        /// <returns>A string representation of value of the current <see cref="Date"/> object as specified by provider.</returns>
        public string ToString(IFormatProvider provider)
        {
            return ToString("d", provider);
        }
        /// <summary>
        /// Converts the value of the current <see cref="Date"/> object to its equivalent string representation using the specified format.
        /// </summary>
        /// <param name="format">A <see cref="Date"/> format string.</param>
        /// <returns>A string representation of value of the current <see cref="Date"/> object as specified by format.</returns>
        public string ToString(string format)
        {
            return ToString(format, System.Threading.Thread.CurrentThread.CurrentCulture);
        }
        #region IFormattable Members
        /// <summary>
        /// Converts the value of the current <see cref="Date"/> object to its equivalent string representation using the specified format and culture-specific format information.
        /// </summary>
        /// <param name="format">A DateTime format string.</param>
        /// <param name="formatProvider">An <see cref="IFormatProvider"/> that supplies culture-specific formatting information.</param>
        /// <returns>A string representation of value of the current <see cref="Date"/> object as specified by format and provider.</returns>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (format.Length == 1)
            {
                if (format != "d" && format != "D" && format != "m" && format != "o" && format != "s" && format != "u" && format != "y")
                {
                    throw new FormatException("Invalid Date Format String: " + format);
                }
            }
            string ret = ToDateTime().ToString(format, formatProvider);
            if (format == "o" || format == "s" || format == "u")
            {
                ret = ret.Substring(0, 10);
            }
            return ret;
        }
        #endregion

        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/> equivalent and returns a value that indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="result">When this method returns, contains the <see cref="Date"/> value equivalent to the date contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Date.MinValue"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter is <b>null</b>, is an empty string, or does not contain a valid string representation of a date. This parameter is passed uninitialized.</param>
        /// <returns><b>true</b> if the s parameter was converted successfully; otherwise, <b>false</b>.</returns>
        public static bool TryParse(string s, out Date result)
        {
            DateTime d = new DateTime();
            bool ret = DateTime.TryParse(s, out d);
            if (ret)
            {
                result = FromDateTime(d);
                return true;
            }
            else
            {
                result = new Date(0);
                return false;
            }
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/> equivalent using the specified culture-specific format information and formatting style, and returns a value that indicates whether the conversion succeeded.</summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information about <paramref name="s"/>.</param>
        /// <param name="styles">A bitwise combination of enumeration values that defines how to interpret the parsed date in relation to the current time zone or the current date. A typical value to specify is <see cref="System.Globalization.DateTimeStyles.None"/>.</param>
        /// <param name="result">When this method returns, contains the <see cref="Date"/> value equivalent to the date contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Date.MinValue"/> if the conversion failed. The conversion fails if the s parameter is <b>null</b>, is an empty string, or does not contain a valid string representation of a date. This parameter is passed uninitialized.</param>
        /// <returns><b>true</b> if <paramref name="s"/> was converted successfully; otherwise, <b>false</b>.</returns>
        public static bool TryParse(string s, IFormatProvider provider, System.Globalization.DateTimeStyles styles, out Date result)
        {
            DateTime d = new DateTime();
            bool ret = DateTime.TryParse(s, provider, styles, out d);
            if (ret)
            {
                result = FromDateTime(d);
                return true;
            }
            else
            {
                result = new Date(0);
                return false;
            }
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/> equivalent using the specified format, culture-specific format information, and style. The format of the string representation must match the specified format exactly. The method returns a value that indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="s">A string containing a date to convert.</param>
        /// <param name="format">The required format of s.</param>
        /// <param name="provider">An <see cref="IFormatProvider"/> object that supplies culture-specific formatting information about <paramref name="s"/>.</param>
        /// <param name="style">A bitwise combination of one or more enumeration values that indicate the permitted format of <paramref name="s"/>.</param>
        /// <param name="result">When this method returns, contains the <see cref="Date"/> value equivalent to the date contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Date.MinValue"/> if the conversion failed. The conversion fails if either the <paramref name="s"/> or <paramref name="format"/> is <b>null</b>, is an empty string, or does not contain a date that correspond to the pattern specified in format. This parameter is passed uninitialized.</param>
        /// <returns><b>true</b> if <paramref name="s"/> was converted successfully; otherwise, <b>false</b>.</returns>
        public static bool TryParseExact(string s, string format, IFormatProvider provider, System.Globalization.DateTimeStyles style, out Date result)
        {
            DateTime d = new DateTime();
            bool ret = DateTime.TryParseExact(s, format, provider, style, out d);
            if (ret)
            {
                result = FromDateTime(d);
                return true;
            }
            else
            {
                result = new Date(0);
                return false;
            }
        }
        /// <summary>
        /// Converts the specified string representation of a date to its <see cref="Date"/> equivalent using the specified array of formats, culture-specific format information, and style. The format of the string representation must match at least one of the specified formats exactly. The method returns a value that indicates whether the conversion succeeded.</summary>
        /// <param name="s">A string containing one or more dates and times to convert.</param>
        /// <param name="formats">An array of allowable formats of <paramref name="s"/>.</param>
        /// <param name="provider">An object that supplies culture-specific format information about <paramref name="s"/>.</param>
        /// <param name="style">A bitwise combination of enumeration values that indicates the permitted format of <paramref name="s"/>. A typical value to specify is <see cref="System.Globalization.DateTimeStyles.None"/>.</param>
        /// <param name="result">When this method returns, contains the <see cref="Date"/> value equivalent to the date contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Date.MinValue"/> if the conversion failed. The conversion fails if <paramref name="s"/> or <paramref name="formats"/> is <b>null</b>, <paramref name="s"/> or an element of formats is an empty string, or the format of <paramref name="s"/> is not exactly as specified by at least one of the format patterns in <paramref name="formats"/>. This parameter is passed uninitialized.</param>
        /// <returns><b>true</b> if the <paramref name="s"/> parameter was converted successfully; otherwise, <b>false</b>.</returns>
        public static bool TryParseExact(string s, string[] formats, IFormatProvider provider, System.Globalization.DateTimeStyles style, out Date result)
        {
            DateTime d = new DateTime();
            bool ret = DateTime.TryParseExact(s, formats, provider, style, out d);
            if (ret)
            {
                result = FromDateTime(d);
                return true;
            }
            else
            {
                result = new Date(0);
                return false;
            }
        }
        /// <summary>
        /// Gets the current <see cref="Date"/> (in the local timezone).
        /// </summary>
        public static Date LocalToday
        {
            get
            {
                return FromDateTime(DateTime.Now);
            }
        }
        /// <summary>
        /// Gets the current <see cref="Date"/> in the UTC timezone.
        /// </summary>
        public static Date UtcToday
        {
            get
            {
                return FromDateTime(DateTime.UtcNow);
            }
        }

        #region IConvertible Members

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.Int32;
        }

        bool IConvertible.ToBoolean(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToBoolean(provider);
        }

        byte IConvertible.ToByte(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToByte(provider);
        }

        char IConvertible.ToChar(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToChar(provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToDateTime(provider);
        }

        decimal IConvertible.ToDecimal(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToDecimal(provider);
        }

        double IConvertible.ToDouble(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToDouble(provider);
        }

        short IConvertible.ToInt16(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToInt16(provider);
        }

        int IConvertible.ToInt32(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToInt32(provider);
        }

        long IConvertible.ToInt64(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToInt64(provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToSByte(provider);
        }

        float IConvertible.ToSingle(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToSingle(provider);
        }

        string IConvertible.ToString(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToString(provider);
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToType(conversionType, provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToUInt16(provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToUInt32(provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider? provider)
        {
            return ((IConvertible)_daysSinceCalendarStart).ToUInt64(provider);
        }

        #endregion

        #region ISerializable Members

        [ExcludeFromCoverage]   // this is only here to provide an implementation that is as close as possible to System.Date.   However, BinaryFormatter has been deprecated so we won't bother testing it
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(DaysField, _daysSinceCalendarStart);
        }

        #endregion
    }
}
