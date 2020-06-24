using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// An interface that tracks progress and cancellation.  This interface is disposable, so it should be in a using statement.
    /// Implementations that require I/O should do it asynchronously to avoid blocking.
    /// </summary>
    public interface IProgress : IDisposable
    {
        /// <summary>
        /// Gets the <see cref="CancellationToken"/> if there is one.
        /// </summary>
        CancellationToken CancellationToken { get; }
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
        /// Creates another <see cref="IProgress"/> to track the progress of a part of the process represented by this progress.
        /// </summary>
        /// <param name="startPortion">The portion complete within the process represented by this progress at which the part starts.</param>
        /// <param name="portionPart">The portion delegated to the part.</param>
        /// <param name="prefix">An optional prefix to add at the beginning of the current item for tracking from the returned <see cref="IProgress"/> part.</param>
        /// <param name="cancel">An optional <see cref="CancellationToken"/> that will override the cancellation token for the main process.</param>
        /// <returns>A new <see cref="IProgress"/> for the part, which will update the progress here within the specified range.</returns>
        IProgress TrackPart(float startPortion, float portionPart, string prefix = null, CancellationToken cancel = default(CancellationToken));
    }
    /// <summary>
    /// An interface that abstracts an ambient progress tracking service.
    /// </summary>
    public interface IAmbientProgress
    {
        /// <summary>
        /// Gets the most recent <see cref="IProgress"/> for the current execution context.  
        /// </summary>
        /// <returns>An <see cref="IProgress"/> tracker to track the progress of the calling process.</returns>
        /// <remarks>
        /// The first time this property is retrieved, a top-level progress is created.
        /// The <see cref="IProgress"/> that is returned is thread-safe, but since each execution context gets its own progress tracker,
        /// cross-thread calls should only happen when the executing thread is working on the same operation as the thread whose progress 
        /// tracker is being called and when the threads are using some external method for coordinating the processing.
        /// </remarks>
        IProgress Progress { get; }
    }
}
