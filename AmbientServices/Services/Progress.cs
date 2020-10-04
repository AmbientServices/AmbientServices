using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// An interface that tracks ambient progress and cancellation.
    /// Implementors that perform I/O operations should do it asynchronously to avoid blocking.
    /// </summary>
    public interface IAmbientProgress
    {
        /// <summary>
        /// Resets the associated cancellation token source to one that is *not* cancelled.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating how long to wait before timing out.</param>
        void ResetCancellation(TimeSpan timeout);
        /// <summary>
        /// Resets the associated cancellation token source to one from a framework source.
        /// </summary>
        /// <param name="cancellationTokenSource">A <see cref="CancellationTokenSource"/> from which to construct an ambient cancellation token source.  If not specified, creates a new cancellation token source that much be cancelled manually.</param>
        void ResetCancellation(CancellationTokenSource cancellationTokenSource = null);
        /// <summary>
        /// Checks <see cref="CancellationToken"/> and throws the <see cref="OperationCanceledException"/> if the operation should be cancelled.
        /// </summary>
        void ThrowIfCancelled();
        /// <summary>
        /// Gets the <see cref="CancellationToken"/> associated with this progress.
        /// </summary>
        CancellationToken CancellationToken { get; }
        /// <summary>
        /// Gets the <see cref="AmbientCancellationTokenSource"/> associated with this progress.
        /// Note that the <see cref="AmbientCancellationTokenSource"/> is owned by the progress tracker and need not be disposed by the caller.
        /// </summary>
        AmbientCancellationTokenSource CancellationTokenSource { get; }
        /// <summary>
        /// Gets the portion of the task that is complete.  Must be between zero and one (inclusive).
        /// </summary>
        float PortionComplete { get; }
        /// <summary>
        /// Gets the item currently being processed.
        /// </summary>
        string ItemCurrentlyBeingProcessed { get; }
        /// <summary>
        /// Updates the portion complete and optionally the current item being processed.
        /// Also checks <see cref="CancellationToken"/> and throws the <see cref="OperationCanceledException"/> if the operation should be cancelled.
        /// </summary>
        /// <param name="portionComplete">A number between 0.0 and 1.0 indicating how much of the operation has been completed.</param>
        /// <param name="itemCurrentlyBeingProcessed">The item currently being processed, null to not update the item being processed, <see cref="string.Empty"/> to clear the item.</param>
        void Update(float portionComplete, string itemCurrentlyBeingProcessed = null);
        /// <summary>
        /// Starts a sub-part of the processing and begins tracking that specified range of the process.
        /// The sub-progress becomes the new ambient progress returned by <see cref="IAmbientProgressProvider"/>.
        /// Checks to see if cancellation has been requested and updates the parent process to indicate that the sub-part has started.
        /// </summary>
        /// <param name="startPortion">The portion complete within the process represented by this progress at which the part starts.</param>
        /// <param name="portionPart">The portion delegated to the part.</param>
        /// <param name="prefix">An optional prefix to add at the beginning of the current item for tracking from the returned <see cref="IAmbientProgress"/> part.</param>
        /// <param name="inheritCancellationTokenSource">Whether or not to inherit the cancellation token source from this "parent" progress.  Defaults to false.</param>
        /// <returns>An <see cref="IDisposable"/> used to scope the sub-part.  When disposed, checks to see if cancellation has been requested and updates the parent process indicating that the sub-part has been completed.</returns>
        /// <remarks>
        /// The <see cref="IDisposable"/>s returned by this function must be disposed in order, ie. those created as sub-parts must all be disposed before the parent part is.
        /// </remarks>
        IDisposable TrackPart(float startPortion, float portionPart, string prefix = null, bool inheritCancellationTokenSource = false);
    }
    /// <summary>
    /// An interface that abstracts an ambient progress tracking service.
    /// </summary>
    public interface IAmbientProgressProvider
    {
        /// <summary>
        /// Gets the most recent <see cref="IAmbientProgress"/> for the current execution context.  
        /// </summary>
        /// <returns>An <see cref="IAmbientProgress"/> tracker to track the progress of the calling process.</returns>
        /// <remarks>
        /// The first time this property is retrieved, a new top-level <see cref="IAmbientProgress"/> is created.
        /// The <see cref="IAmbientProgress"/> that is returned is thread-safe, but since each execution context gets its own progress tracker,
        /// cross-thread calls should only happen when the executing thread is working on the same operation as the thread whose progress 
        /// tracker is being called and when the threads are using some external method for coordinating the processing.
        /// </remarks>
        IAmbientProgress Progress { get; }
    }
}
