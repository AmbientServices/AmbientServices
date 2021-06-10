using System;

namespace AmbientServices.Utility
{
    /// <summary>
    /// A static class that extends <see cref="DateTime"/>.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Gets the <see cref="Date"/> part of a <see cref="DateTime"/>.
        /// </summary>
        /// <param name="datetime">The <see cref="DateTime"/> to get the <see cref="Date"/> part for.</param>
        /// <returns>A <see cref="Date"/> with no time portion.</returns>
        public static Date GetDate(this DateTime datetime)
        {
            return Date.FromDateTime(DateTime.SpecifyKind(datetime, DateTimeKind.Unspecified));
        }
    }
}
