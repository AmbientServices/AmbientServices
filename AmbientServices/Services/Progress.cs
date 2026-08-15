using System;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// An interface that tracks ambient progress and cancellation.
/// Implementors that perform I/O operations should do it asynchronously to avoid blocking.
/// </summary>
/// <remarks>
/// <pitch>
/// Report how far a long-running operation has progressed (a zero-to-one portion plus the item currently being worked on) and observe cooperative cancellation from one place, with sub-operations mapping their own zero-to-one range onto a slice of the parent's — so deeply-nested code reports progress without knowing where it sits in the overall operation.  Reporting progress never alters an operation's results; the only outcome it can change is aborting the operation via cancellation.
/// </pitch>
/// <pledge>
/// <see cref="PortionComplete"/> is always between zero and one inclusive, and updates outside that range are rejected.  <see cref="Update"/> both records progress and polls for cancellation, throwing <see cref="OperationCanceledException"/> when cancellation has been requested; progress recorded on a sub-part propagates to its parent scaled into the range the part was delegated, with the part's prefix applied to the item name, so a parent observer sees smoothly-advancing overall progress.
/// <see cref="TrackPart"/> begins a sub-part which becomes the ambient progress for the call context until the returned <see cref="IDisposable"/> is disposed; parts must be disposed in the reverse order of creation (innermost first), and disposing a part reports it complete to its parent.  A part's cancellation is independent of its parent's unless inheritance is requested at creation.  <see cref="ResetCancellation(TimeSpan)"/> replaces the tracker's cancellation source, and the tracker owns whichever source is current — callers never dispose it.
/// </pledge>
/// <priority>
/// 1. Nested code's ignorance of the whole over precise overall progress: every part reports zero-to-one against its own work and the tracker scales that into the slice its parent delegated, so code deep in a call stack never needs to know what fraction of the overall operation it represents.  A sibling requiring absolute progress would produce exact numbers and is rejected because the code best placed to report progress is the code least able to know its own share. (public)
/// 2. Cancellation travelling with progress over separating the two concerns: <see cref="Update"/> both records progress and throws when cancellation has been requested, so a loop that reports progress cannot forget to poll for cancellation.  The cost is an interface that does two things and a progress call that can throw. (public)
/// </priority>
/// </remarks>
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
    /// <param name="cancellationTokenSource">A <see cref="CancellationTokenSource"/> from which to construct an ambient cancellation token source.  If not specified, creates a new cancellation token source that must be cancelled manually.</param>
    void ResetCancellation(CancellationTokenSource? cancellationTokenSource = null);
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
    void Update(float portionComplete, string? itemCurrentlyBeingProcessed = null);
    /// <summary>
    /// Starts a sub-part of the processing and begins tracking that specified range of the process.
    /// The sub-progress becomes the new ambient progress returned by <see cref="IAmbientProgressService"/>.
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
    IDisposable TrackPart(float startPortion, float portionPart, string? prefix = null, bool inheritCancellationTokenSource = false);
}
/// <summary>
/// An interface that abstracts an ambient progress tracking service.
/// </summary>
/// <remarks>
/// <pitch>The entry point for progress tracking: hands each execution context its own current <see cref="IAmbientProgress"/>, so libraries can report progress and honor cancellation without being passed a tracker — and callers that don't care simply never look.</pitch>
/// <pledge>
/// <see cref="Progress"/> returns the innermost live progress for the calling execution context, lazily creating a top-level tracker (covering the whole operation) on first access; sub-parts started through <see cref="IAmbientProgress.TrackPart"/> become the value returned here until they are disposed.  Each execution context gets its own tracker, so concurrent operations never see each other's progress; the returned tracker is thread-safe, but cross-thread use is only meaningful among threads cooperating on the same operation.
/// </pledge>
/// </remarks>
public interface IAmbientProgressService
{
    /// <summary>
    /// Gets the most recent <see cref="IAmbientProgress"/> for the current execution context.  
    /// </summary>
    /// <returns>An <see cref="IAmbientProgress"/> tracker to track the progress of the calling process, or null if progress tracking is not active.</returns>
    /// <remarks>
    /// The first time this property is retrieved, a new top-level <see cref="IAmbientProgress"/> is created.
    /// The <see cref="IAmbientProgress"/> that is returned is thread-safe, but since each execution context gets its own progress tracker,
    /// cross-thread calls should only happen when the executing thread is working on the same operation as the thread whose progress 
    /// tracker is being called and when the threads are using some external method for coordinating the processing.
    /// </remarks>
    IAmbientProgress? Progress { get; }
}
