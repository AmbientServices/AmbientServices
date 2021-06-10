using System;
using System.Text;

namespace AmbientServices.Utility
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
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            StringBuilder stack = new StringBuilder();
            BuildFilteredString(exception, stack);
            return stack.ToString();
        }

        internal static void BuildFilteredString(Exception exception, StringBuilder output)
        {
            Exception? innerException = exception.InnerException;
            if (innerException != null) BuildFilteredString(innerException, output);

            if (output.Length > 0) output.AppendLine();

            output.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "[{0}] {1}", exception.GetType().Name, exception.Message);
            output.AppendLine();
            output.Append(new FilteredStackTrace(exception));
        }
        /// <summary>
        /// Gets the type name for the exception, which is literally the type name with any trailing &quot;Exception&quot; removed.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> whose type name is to be determined.</param>
        /// <returns>The type name for the exception.</returns>
        public static string TypeName(this Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            string typeName = e.GetType().Name;
            return typeName.EndsWith("Exception", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 9) : typeName;
        }
    }
}
