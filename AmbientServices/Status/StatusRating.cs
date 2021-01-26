namespace AmbientServices
{
    /// <summary>
    /// An enumeration of the possible ranges for status ratings.
    /// </summary>
    public enum StatusRatingRange
    {
        /// <summary>
        /// A range indicating that a system status test has not yet determined the status.
        /// </summary>
        Pending,
        /// <summary>
        /// A range indicating that a system has failed.
        /// </summary>
        Fail,
        /// <summary>
        /// A range indicating that a system is alerting.
        /// </summary>
        Alert,
        /// <summary>
        /// A range indicating that a system is okay.
        /// </summary>
        Okay,
        /// <summary>
        /// A range indicating that a system is better than okay.
        /// </summary>
        Superlative,
    }
    /// <summary>
    /// An enumeration of threshold status rating values.
    /// These values are a rough determination of whether or not a system is functioning and if there is any information the operations staff should be alerted to.  
    /// Performance is only considered if it is so bad that requests are failing.
    /// Worse states are numerically less than better states.
    /// </summary>
    /// <remarks>
    /// When a status rating is exactly equal to the specified value, it means that the corresponding system has just barely crossed the threshold from the next higher (better) state into that state.
    /// Negative values indicate how badly the system is failing.
    /// Values above 2.0 indicate that the system is "Superlative", ie. better than Okay.
    /// For example, a value of 0.5 indicates that the system is alerting and is about half way towards failing but not yet failing;
    /// a value of 1.5 indicates that the system is okay, but halfway towards alerting (for example, a lack of redundancy, or nearing the alert threshold for disk space available).
    /// When comparing status ratings, keep in mind that <see cref="StatusRating.Pending"/> has the value <see cref="System.Single.NaN"/>, which will not compare to any other value.
    /// If you need to check for pending statuses, either add <see cref="System.Single.IsNaN(float)"/> logic or use the opposite logic and a not.
    /// </remarks>
    public static class StatusRating
    {
        /// <summary>
        /// The status check has not been completed yet, even though the status system is running.
        /// Note that this value is <see cref="System.Single.NaN"/> and therefore will not compare even to itself with the == operator.
        /// Use <see cref="System.Single.IsNaN(float)"/> to check to see if an assigned rating has this value.
        /// The reason for this is that this is an explicitly-assigned state, but isn't necessarily better or worse than any other value.
        /// Using any other possible value would result in some use cases not working as intended.
        /// </summary>
        public const float Pending = float.NaN;
        /// <summary>
        /// The system has completely failed.  This constant defines the bottom of the bottom range.  
        /// Although values less than this may be assigned, they are meaningless to the framework.
        /// Values lower than this will count as being in the same range as those between this one and the next higher constant (<see cref="Fail"/>).
        /// </summary>
        public const float Catastrophic = -1.0f;
        /// <summary>
        /// The value for when a system is just barely failing (and therefore also has an alert).
        /// </summary>
        public const float Fail = 0.0f;
        /// <summary>
        /// The value for when a system has not yet failed, but has just entered a state that might need work.
        /// </summary>
        public const float Alert = 1.0f;
        /// <summary>
        /// The value for when a system is almost superlative, but still just barely only okay.
        /// </summary>
        public const float Okay = 2.0f;
        /// <summary>
        /// The system is superlative and there are no alerts.  This constant defines the top of the top range.  
        /// Although values higher than this may be assigned, they are meaningless to the framework.
        /// Values higher than this will count as being in the same range as those between this one and the next lower constant (<see cref="Okay"/>).
        /// </summary>
        public const float Superlative = 3.0f;

        private static readonly float[] RangeValues = new float[] { float.NaN, Catastrophic, Fail, Alert, Okay, Superlative };
        private static readonly string[] RangeNames = new string[] { "Pending", "Fail", "Alert", "Okay", "Superlative", };
        private static readonly string[] RangeSymbols = new string[] { "⌛", "🛑", "⚠️", "🟢", "💙", };
        private static readonly string[] RangeForegroundColors = new string[] { "grey", "red", "#ffdf00", "green", "blue", };
        private static readonly string[] RangeBackgroundColors = new string[] { "#bfbfbf", "#ffdfdf", "#fff7df", "#efefef", "#dfefdf", "#dfdfef", };
        //private static readonly int[,] RangeRgbForegroundColorParts = new int[,] { { 0xff, 0, 0 }, { 0x7f, 0, 0 }, { 0xff, 0xdf, 0 }, { 0, 0x7f, 0 }, { 0, 0, 0xff }, };
        //private static readonly int[,] RangeRgbBackgroundColorParts = new int[,] { { 0xff, 0xdf, 0xdf }, { 0xef, 0xdf, 0xdf }, { 0xff, 0xf7, 0xdf }, { 0xdf, 0xef, 0xdf }, { 0xdf, 0xdf, 0xef }, };
        internal const string StyleDefinition = @"
    .pending-range{background-color:#bfbfbf;color:grey;}
    .fail-range{background-color:#ffdfdf;color:red;}
    .alert-range{background-color:#fff7df;color:#ffdf00;}
    .okay-range{background-color:#dfefdf;color:green;}
    .superlative-range{background-color:#dfdfef;color:blue;}";

        /// <summary>
        /// The number of ranges (as determined by the possible values of a float and the rating categories above.
        /// </summary>
        public const int Ranges = 5;

        /// <summary>
        /// Gets the single-character symbol for the specified status rating range.
        /// </summary>
        /// <param name="ratingRange">A <see cref="StatusRatingRange"/> indicating the range, the same that would be returned by <see cref="FindRange"/> for values in the specified range.</param>
        /// <returns>A single character representing the specified rating range.</returns>
        public static string GetRangeSymbol(StatusRatingRange ratingRange)
        {
            return RangeSymbols[(int)ratingRange];
        }
        /// <summary>
        /// Gets the <see cref="float"/> for the lower bound for the specified range.  
        /// The returned value is the upper bound for the previous range, and passing this value to <see cref="FindRange"/> will return the next lower range (unless it's already the lowest range).
        /// </summary>
        /// <param name="ratingRange">The <see cref="StatusRatingRange"/> to get the lower bound for.</param>
        /// <returns>The lower bound for the specified range.</returns>
        public static float GetRangeLowerBound(StatusRatingRange ratingRange)
        {
            return (ratingRange == 0) ? float.NaN : RangeValues[(int)ratingRange];
        }
        /// <summary>
        /// Gets the <see cref="float"/> for the upper bound for the specified range.  
        /// The returned value is the lower bound for the next range.
        /// Passing this value to <see cref="FindRange"/> will return the specified range.
        /// </summary>
        /// <param name="ratingRange">The <see cref="StatusRatingRange"/> to get the upper bound for.</param>
        /// <returns>The upper bound for the specified range.</returns>
        public static float GetRangeUpperBound(StatusRatingRange ratingRange)
        {
            return (ratingRange == 0) ? float.NaN : RangeValues[(int)ratingRange + 1];
        }
        /// <summary>
        /// Finds the range offset for the specified rating.
        /// </summary>
        /// <param name="rating">The rating whose offset is to be determined.</param>
        /// <returns>A <see cref="StatusRatingRange"/> indicating the range of the specified rating.</returns>
        public static StatusRatingRange FindRange(float rating)
        {
            if (float.IsNaN(rating)) return StatusRatingRange.Pending;
            if (rating <= Fail) return StatusRatingRange.Fail;
            if (rating <= Alert) return StatusRatingRange.Alert;
            if (rating <= Okay) return StatusRatingRange.Okay;
            return StatusRatingRange.Superlative;
        }
        private static int GetRatingRgbForegroundColorValue(float rating)
        {
            // pending?
            if (float.IsNaN(rating)) return 0x808080;

            float AdjustPortion(float portion)
            {
                return 0.2f + 0.6f * portion;
            }
            if (rating <= Catastrophic) return 0xff0000;
            if (rating > Superlative) return 0x0000ff;
            float portionTowardsNextStatus;
            byte rVal;
            byte gVal;
            byte bVal;
            if (rating <= Fail)
            {
                portionTowardsNextStatus = AdjustPortion(rating - Catastrophic);
                return (((byte)(0xff - 0x80 * portionTowardsNextStatus)) << 16);
            }
            else if (rating <= Alert)
            {
                portionTowardsNextStatus = AdjustPortion(rating - Fail);
                return (((byte)(0x7f + 0x80 * portionTowardsNextStatus)) << 16) | (((byte)(0xdf * portionTowardsNextStatus)) << 8);
            }
            else if (rating <= Okay)
            {
                portionTowardsNextStatus = AdjustPortion(rating - Alert);
                rVal = ((byte)(0xff - 0xff * portionTowardsNextStatus));
                gVal = ((byte)(0xdf - 0x60 * portionTowardsNextStatus));
                bVal = 0;
                return (rVal << 16) | (gVal << 8) | bVal;

            }
            portionTowardsNextStatus = AdjustPortion(rating - Okay);
            rVal = 0;
            gVal = ((byte)(0x7f - 0x7f * portionTowardsNextStatus));
            bVal = ((byte)(0x00 + 0xff * portionTowardsNextStatus));
            return (rVal << 16) | (gVal << 8) | bVal;
        }
        /// <summary>
        /// Returns a string indicating the name of the range represented by the raw status rating.
        /// </summary>
        /// <param name="rating">The raw rating number, which may have any possible value.</param>
        /// <returns>A string containing the name of the range the specified rating value falls into.</returns>
        public static string GetRangeName(float rating)
        {
            return GetRangeName(FindRange(rating));
        }
        /// <summary>
        /// Returns a string indicating the name of the specified range.
        /// </summary>
        /// <param name="ratingRange">A <see cref="StatusRatingRange"/> indicating the range, presumably returned by <see cref="FindRange"/>.</param>
        /// <returns>A string containing the name of the specified range.</returns>
        public static string GetRangeName(StatusRatingRange ratingRange)
        {
            return RangeNames[(int)ratingRange];
        }
        /// <summary>
        /// Returns a foreground color associated with the specified rating.
        /// </summary>
        /// <param name="rating">The raw rating number, which may have any possible value.</param>
        /// <returns>A string identifying the color associated with the rating.</returns>
        public static string GetRangeForegroundColor(float rating)
        {
            return GetRangeForegroundColor(FindRange(rating));
        }
        /// <summary>
        /// Returns a foreground color associated with the specified rating range.
        /// </summary>
        /// <param name="ratingRange">A <see cref="StatusRatingRange"/> indicating the range, presumably returned by <see cref="FindRange"/>.</param>
        /// <returns>A string identifying the color associated with the rating range.</returns>
        public static string GetRangeForegroundColor(StatusRatingRange ratingRange)
        {
            return RangeForegroundColors[(int)ratingRange];
        }
        /// <summary>
        /// Returns a background color associated with the specified rating.
        /// </summary>
        /// <param name="rating">The raw rating number, which may have any possible value.</param>
        /// <returns>A string identifying the color associated with the rating.</returns>
        public static string GetRangeBackgroundColor(float rating)
        {
            return GetRangeBackgroundColor(FindRange(rating));
        }
        /// <summary>
        /// Returns a background color for the specified rating range.
        /// </summary>
        /// <param name="ratingRange">A <see cref="StatusRatingRange"/> indicating the range, presumably returned by <see cref="FindRange"/>.</param>
        /// <returns>A string identifying the color associated with the rating range.</returns>
        public static string GetRangeBackgroundColor(StatusRatingRange ratingRange)
        {
            return RangeBackgroundColors[(int)ratingRange];
        }
        /// <summary>
        /// Returns an RGB background color associated with the specified rating.
        /// </summary>
        /// <param name="rating">The raw rating number, which may have any possible value.</param>
        /// <returns>A string identifying the hexadecimal RGB color associated with the rating.</returns>
        public static string GetRatingRgbForegroundColor(float rating)
        {
            return "#" + GetRatingRgbForegroundColorValue(rating).ToString("x6", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
