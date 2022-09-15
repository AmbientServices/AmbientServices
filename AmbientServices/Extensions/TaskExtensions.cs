using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace AmbientServices.Extensions
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
        /// <returns>A <see cref="Task"/> that completes when the <see cref="CancellationToken"/> get cancelled.</returns>
        public static Task AsTask(this CancellationToken cancellationToken)
        {
            TaskCompletionSource<object> tcs = new();
            cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            return tcs.Task;
        }
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
        /// <summary>
        /// Gets the specified <see cref="CancellationToken"/> as a <see cref="Task"/> that gets cancelled when the token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that controls the task.</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the <see cref="CancellationToken"/> get cancelled.</returns>
        public static async ValueTask AsValueTask(this CancellationToken cancellationToken)
        {
            await cancellationToken.AsTask().ConfigureAwait(false);
//            await Task.Delay(-1, cancellationToken).ContinueWith(_ => { }, default, TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Current).ConfigureAwait(false);
        }
#else
        /// <summary>
        /// Gets the specified <see cref="CancellationToken"/> as a <see cref="ValueTask"/> that gets cancelled when the token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that controls the task.</param>
        /// <returns>A <see cref="ValueTask"/> that gets cancelled when the <see cref="CancellationToken"/> get cancelled.</returns>
        public static async ValueTask AsValueTask(this CancellationToken cancellationToken)
        {
            await cancellationToken.AsTask().ConfigureAwait(false);
        }
#endif
    }
}
