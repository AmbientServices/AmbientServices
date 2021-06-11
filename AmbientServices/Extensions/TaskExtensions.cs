using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A static class that contains extensions for <see cref="Task"/>.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Gets the specified <see cref="CancellationToken"/> as a <see cref="Task"/> that gets cancelled when the token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that controls the task.</param>
        /// <returns>A <see cref="Task"/> that gets cancelled when the <see cref="CancellationToken"/> get cancelled.</returns>
        public static Task AsTask(this CancellationToken cancellationToken)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            return tcs.Task;
        }
    }
}
