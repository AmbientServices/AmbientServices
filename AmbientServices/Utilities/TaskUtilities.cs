using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace AmbientServices.Utilities
{
    /// <summary>
    /// A static class that contains extensions for <see cref="Task"/>.
    /// </summary>
    public static class TaskUtilities
    {
#if NET5_0_OR_GREATER
        /// <summary>
        /// Gets a <see cref="ValueTask{TResult}"/> from the specified value.
        /// </summary>
        /// <typeparam name="T">The type contained within the ValueTask.</typeparam>
        /// <param name="value">The value to encapsulate into the ValueTask.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> containing the specified result value.</returns>
        public static ValueTask<T> ValueTaskFromResult<T>(T value)
        {
            return ValueTask.FromResult(value);
        }
#else
#pragma warning disable CS1998 
        /// <summary>
        /// Gets a <see cref="ValueTask{TResult}"/> from the specified value.
        /// </summary>
        /// <typeparam name="T">The type contained within the ValueTask.</typeparam>
        /// <param name="value">The value to encapsulate into the ValueTask.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> containing the specified result value.</returns>
        public static async ValueTask<T> ValueTaskFromResult<T>(T value)
        {
            return value;
        }
#pragma warning restore CS1998
#endif
    }
}
