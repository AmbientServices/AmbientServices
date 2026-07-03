using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Extensions;

/// <summary>
/// A static class that contains extensions for <see cref="Task"/>.
/// </summary>
/// <remarks>
/// <pitch>Bridges <see cref="CancellationToken"/> into task composition: get a task that completes when the token is cancelled, so cancellation can race arbitrary work in <see cref="Task.WhenAny(Task[])"/>-style combinators.</pitch>
/// <pledge>The returned task never completes successfully and never faults — it transitions to canceled when (and only when) the token is cancelled, so a token that is never cancelled yields a task that never completes.  The cancellation callback runs without capturing a synchronization context.</pledge>
/// <plan>A <see cref="TaskCompletionSource{TResult}"/> canceled from a token registration; the registration lives as long as the token, so repeatedly converting a long-lived token accumulates registrations.</plan>
/// </remarks>
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
        await cancellationToken.AsTask();
//            await Task.Delay(-1, cancellationToken).ContinueWith(_ => { }, default, TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Current);
    }
#else
    /// <summary>
    /// Gets the specified <see cref="CancellationToken"/> as a <see cref="ValueTask"/> that gets cancelled when the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that controls the task.</param>
    /// <returns>A <see cref="ValueTask"/> that gets cancelled when the <see cref="CancellationToken"/> get cancelled.</returns>
    public static async ValueTask AsValueTask(this CancellationToken cancellationToken)
    {
        await cancellationToken.AsTask();
    }
#endif
}
