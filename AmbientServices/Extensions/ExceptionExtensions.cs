using AmbientServices.Utilities;
using System;
using System.Text;

namespace AmbientServices.Extensions
{
    /// <summary>
    /// A static class that extends <see cref="System.Exception"/>.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Gets a filtered version of the exception string, with irrelevant stack frames and file paths stripped out.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> for which we are to get a string.</param>
        /// <returns>A filtered string for the exception.</returns>
        public static string ToFilteredString(this Exception exception)
        {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(exception);
#else
            if (exception is null) throw new ArgumentNullException(nameof(exception));
#endif
            StringBuilder stack = new();
            ExceptionUtilities.BuildFilteredString(exception, stack);
            return stack.ToString();
        }
        /// <summary>
        /// Gets the type name for the exception, which is literally the type name with any trailing &quot;Exception&quot; removed.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> whose type name is to be determined.</param>
        /// <returns>The type name for the exception.</returns>
        public static string TypeName(this Exception e)
        {
#if NET5_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(e);
#else
            if (e is null) throw new ArgumentNullException(nameof(e));
#endif
            string typeName = e.GetType().Name;
            return typeName.EndsWith("Exception", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 9) : typeName;
        }
    }
}
