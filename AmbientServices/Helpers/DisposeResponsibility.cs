using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
using System.Threading.Tasks;
#endif
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace AmbientServices;

/// <summary>
/// An interface that abstracts an object that contains an <see cref="IDisposable"/> and allows transfer of the disposal responsibility between instances (and the stack).
/// Instances should ALWAYS be disposed.
/// </summary>
/// <remarks>
/// <pitch>Makes ownership of a disposable explicit and transferable: instead of comments and conventions about who disposes what, the responsibility travels as a first-class object that can be handed between frames and containers — and that can tell you when someone dropped it.</pitch>
/// <pledge>
/// At any moment at most one responsibility instance owns a given disposable; a transfer empties the source (its <see cref="ContainsDisposable"/> becomes false and <see cref="Contained"/> throws <see cref="ObjectDisposedException"/>) and fills the destination, which alone will dispose the contained object.
/// Disposing a responsibility disposes its contained object if it still owns one, and is safe to call regardless; every instance must be disposed exactly along its ownership chain — each holder disposes what it still holds.
/// <see cref="StackOnCreation"/> identifies where the responsibility originated so leaks can be attributed to their creation site.
/// </pledge>
/// </remarks>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
public interface IDisposeResponsibility<out T> : IDisposable
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
{
    /// <summary>
    /// The contained disposable object.  Throws an <see cref="ObjectDisposedException"/> if the object is no longer contained.
    /// </summary>
    public T Contained { get; }
    /// <summary>
    /// The contained disposable object, or null if no disposable is contained.
    /// </summary>
    public T? NullableContained { get; }
    /// <summary>
    /// Returns whether or not this instance contains a disposable and therefore still has responsibility for disposing it.
    /// </summary>
    public bool ContainsDisposable { get; }
    /// <summary>
    /// Gets a string containing the stack at the time the responsibility was created.
    /// </summary>
    public string StackOnCreation { get; }
}

internal interface IShirkResponsibility
{
    /// <summary>
    /// Intended for internal use.  Takes responsibility from the instance (presumably to transfer it to another responsibility object).
    /// </summary>
    internal void ShirkResponsibility();
}

/// <summary>
/// A class that wraps a contained <see cref="IDisposable"/> and allows transfer of the disposal responsibility between objects (and the stack).
/// Failure to dispose of this object will result in a finalizer that will notify you that the contained object was not disposed.
/// The DEBUG version of the code also has a property in <see cref="DisposeResponsibility"/> that tracks all tracked undisposed objects if you're having trouble tracking down a leak.
/// Instances of this class contained in another instance should only be contained in objects that are disposable and should ALWAYS be disposed.
/// DO NOT use this for static objects or objects that are not disposable.
/// Instances of this class on the stack should ALWAYS be in a using statement.  
/// The responsibility to dispose may be transferred out to another instance using <see cref="TransferResponsibilityToCaller"/> passed into a constructor, by calling <see cref="TransferResponsibilityFrom(IDisposeResponsibility{T})"/> on another instance and passing in this instance, or by returning an instance to a caller, but each instance of this class should always be disposed to prevent leaks.
/// </summary>
/// <remarks>
/// <pitch>The standard realization of <see cref="IDisposeResponsibility{T}"/>, with leak detection built in: an instance that dies undisposed announces itself (log, debugger break, event, and deferred test-time assertion) along with the stack that created it.  Not for statics or non-disposable contents.</pitch>
/// <pledge><see cref="IDisposeResponsibility{T}"/></pledge>
/// <pledge>
/// Beyond the transfer contract: <see cref="AssumeResponsibility"/> disposes any current contents before taking the new ones; disposal handles contents that are <see cref="IDisposable"/>, <see cref="IAsyncDisposable"/> (synchronously waited from <see cref="Dispose"/>, natively awaited from <c>DisposeAsync</c>), or tuples of disposables (each element disposed).
/// An instance that is finalized while still owning responsibility reports a leak: through the <see cref="DisposeResponsibility.ResponsibilityNotDisposed"/> event when subscribed, otherwise via a warning log, a debugger break when attached, and a record that <see cref="DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> later surfaces.  An emptied (transferred-from) instance never reports.
/// Individual instances are not thread-safe; concurrent transfer and dispose require caller coordination.
/// </pledge>
/// <plan>
/// Two fields — the contained object and the creation stack string — plus a finalizer that fires only on leaks: proper disposal and shirking both call <see cref="GC.SuppressFinalize"/> (and <see cref="AssumeResponsibility"/>/<see cref="TransferResponsibilityFrom"/> re-register), so the finalizer costs nothing on the happy path.  Transfer works through the internal <c>IShirkResponsibility</c> back-channel, which empties the source and clears its stack so it cannot double-dispose or false-report.
/// Creation stacks come from <see cref="System.Diagnostics.StackTrace"/> (capped and centrally counted per unique stack by <c>PendingDispose</c> in DEBUG builds, exposed via <c>DisposeResponsibility.AllPendingDisposals</c>), trading capture cost for attributable leak reports.
/// </plan>
/// </remarks>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
public sealed class DisposeResponsibility<T> : IDisposeResponsibility<T>, IShirkResponsibility
{
    private static readonly AmbientLogger<DisposeResponsibility<T>> Logger = new();

    private string _stackOnCreation;
    private T? _contained;

    /// <summary>
    /// The contained disposable object.  Throws an <see cref="ObjectDisposedException"/> if the object is no longer contained.
    /// </summary>
    public T Contained
    {
        get
        {
#if NET7_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_contained == null, this);
#else
            if (_contained == null) throw new ObjectDisposedException("The contained disposable object is no longer owned by this responsibility object!");
#endif
            return _contained;
        }
    }
    /// <summary>
    /// The contained disposable object, or null if no disposable is contained.
    /// </summary>
    public T? NullableContained
    {
        get
        {
            return _contained;
        }
    }
    /// <summary>
    /// Returns whether or not this instance contains a disposable and therefore still has responsibility for disposing it.
    /// </summary>
    public bool ContainsDisposable => _contained != null;
    /// <summary>
    /// Gets a string containing the stack at the time the responsibility was created.
    /// </summary>
    public string StackOnCreation => _stackOnCreation;

    /// <summary>
    /// Does the logic as if the finalizer was called (for testing).
    /// There is no reason to call this except for testing.
    /// </summary>
    /// <remarks>
    /// Unlike the real finalizer, this does not enqueue a deferred leak for <see cref="DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/>,
    /// so unit tests can simulate finalization without failing assembly-level verification.
    /// </remarks>
    public void FinalizeLogic()
    {
        NotifyUndisposedResponsibilityLeak(recordForDeferredAssemblyVerification: false);
    }

    /// <summary>
    /// Finalizes the object and ensures that the contained object was disposed as expected.
    /// Note that when used properly, this finalizer is never used, so there isn't a significant performance hit from having this.
    /// Proper disposal short circuits this.
    /// </summary>
    ~DisposeResponsibility()
    {
        NotifyUndisposedResponsibilityLeak(recordForDeferredAssemblyVerification: true);
    }

    private void NotifyUndisposedResponsibilityLeak(bool recordForDeferredAssemblyVerification)
    {
        if (string.IsNullOrEmpty(_stackOnCreation)) return;  // responsibility has been shirked; no leak to report
        if (DisposeResponsibility.NotifyEvent(this, new ResponsibilityNotDisposedEventArgs(_contained, _stackOnCreation))) return;

        string notice = $"Disposable object was not disposed.  Object was constructed at {_stackOnCreation}.";
        Logger.Filter(AmbientLogLevel.Warning)?.Log(new { Action = "UndisposedDisposeResponsibility", Message = notice });
        // stop/notify *if we can*, but if not, queue up a record of this that someone can query later, presumably during cleanup
		if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
		else System.Diagnostics.Trace.WriteLine(notice);
		if (recordForDeferredAssemblyVerification) DisposeResponsibility.RecordDeferredUndisposedLeak(notice);
	}

	/// <summary>
	/// Constructs an empty dispose responsibility object which can later take responsibility for disposing a specified disposable object.
	/// </summary>
	public DisposeResponsibility()
    {
        _stackOnCreation =
#if DEBUG
            PendingDispose.OnConstruct(null, 1024)
#else
            (new System.Diagnostics.StackTrace(1).ToString())
#endif
            ;
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
    }
    /// <summary>
    /// Constructs a dispose responsibility object which takes responsibility for disposing the specified disposable object.
    /// </summary>
    /// <param name="contained">An optional disposable object that will be owned and disposed by the instance.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="contained"/>.</param>
    public DisposeResponsibility(T? contained, string? stackOnCreation = null)
    {
        _contained = contained;
        _stackOnCreation = 
#if DEBUG
            PendingDispose.OnConstruct(stackOnCreation, 1024)
#else
            (stackOnCreation ?? new System.Diagnostics.StackTrace(1).ToString())
#endif
            ;
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
    }
    /// <summary>
    /// Constructs a dispose responsibility object that takes responsibility from the specified responsibility object.
    /// </summary>
    /// <param name="other">Another dispose responsibility object to take responsibility from.</param>
    public DisposeResponsibility(IDisposeResponsibility<T> other)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(other);
#else
        if (other == null) throw new ArgumentNullException(nameof(other));
#endif
        if (other is not IShirkResponsibility isr) throw new NotImplementedException("Unable to transfer responsibility from instances that don't support IShirkResponsibility!");
        _stackOnCreation = other.StackOnCreation;
        _contained = other.Contained;
        isr.ShirkResponsibility();
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
    }

    private static void DisposeContained(T contained)
    {
        if (contained == null) return;

        if (contained is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (contained is IAsyncDisposable asyncDisposable)
        {
            // since we've been called synchronously but the contained object only has an async disposer, we have to wait synchronously
            asyncDisposable.DisposeAsync().AsTask().Wait();
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
        else if (contained is ITuple tuple)
        {
            for (int i = 0; i < tuple.Length; i++)
            {
                if (tuple[i] is IDisposable d) d.Dispose();
            }
        }
#endif
    }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER  // this is just to exclude this function when it is not used--the contents could work fine in any version
    private static async ValueTask DisposeContainedAsync(T contained)
    {
        if (contained == null) return;

        if (contained is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (contained is IDisposable disposable)
        {
            disposable.Dispose();
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
        else if (contained is ITuple tuple)
        {
            for (int i = 0; i < tuple.Length; i++)
            {
                if (tuple[i] is IAsyncDisposable ad) await ad.DisposeAsync();
                else if (tuple[i] is IDisposable d) d.Dispose();
            }
        }
#endif
    }
#endif

    /// <summary>
    /// Disposes of this instance by disposing of the contained instance.
    /// </summary>
    public void Dispose()
    {
        if (_contained is not null)
        {
#if DEBUG
            PendingDispose.OnDispose(_stackOnCreation);
#endif
            DisposeContained(_contained);
            _contained = default;
            _stackOnCreation = "";
        }
        GC.SuppressFinalize(this);
    }
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// Asynchronously disposes of this instance by disposing of the contained instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_contained is not null)
        {
#if DEBUG
            PendingDispose.OnDispose(_stackOnCreation);
#endif
            await DisposeContainedAsync(_contained);
            GC.SuppressFinalize(this);
            _contained = default;
        }
        else // no need to dispose synchronously or asynchronously, but we were disposed, so no need to finalize
        {
            GC.SuppressFinalize(this);
        }
    }
#endif
    /// <summary>
    /// Disposes of any existing disposable and assumes responsibility for the newly specified disposable.
    /// </summary>
    /// <param name="newDisposable">The new disposable to take responsibility for.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="newDisposable"/>.</param>
    public void AssumeResponsibility(T? newDisposable, string? stackOnCreation = null)
    {
        Dispose();
        _contained = newDisposable;
        _stackOnCreation =
#if DEBUG
        PendingDispose.OnConstruct(stackOnCreation, 1024);
#else
        (stackOnCreation ?? new System.Diagnostics.StackTrace(1).ToString());
#endif
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
        GC.ReRegisterForFinalize(this);
    }
    /// <summary>
    /// Transfers the responsibility from a specified instance into this instance.
    /// </summary>
    /// <param name="sourceOwnership">The <see cref="IDisposeResponsibility{T}"/> instance whose contained disposable will hereafter be owned by this instance.</param>
    public void TransferResponsibilityFrom(IDisposeResponsibility<T> sourceOwnership)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(sourceOwnership);
#else
        if (sourceOwnership == null) throw new ArgumentNullException(nameof(sourceOwnership));
#endif
        if (sourceOwnership is not IShirkResponsibility isr) throw new NotImplementedException("Unable to transfer responsibility from instances that don't support IShirkResponsibility!");
        Dispose();
        _contained = sourceOwnership.NullableContained;
        _stackOnCreation = sourceOwnership.StackOnCreation;
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
        GC.ReRegisterForFinalize(this);
        isr.ShirkResponsibility();
    }
    /// <summary>
    /// Intended for internal use.  Takes responsibility from the instance (presumably to transfer it to another responsibility object).
    /// </summary>
    void IShirkResponsibility.ShirkResponsibility()
    {
        _contained = default;
        _stackOnCreation = "";      // clear so FinalizeLogic won't report a false leak
#pragma warning disable CA1816     // intentional: shirking transfers ownership away, so no finalizer needed
        GC.SuppressFinalize(this);
#pragma warning restore CA1816
    }
    /// <summary>
    /// Returns a new instance to be returned from the containing function, with dispose responsibility transferred from this instance to that one.
    /// </summary>
    /// <returns>A new <see cref="DisposeResponsibility{T}"/> with disposal responsibility.</returns>
    public DisposeResponsibility<T> TransferResponsibilityToCaller()
    {
        DisposeResponsibility<T> newInstance = new();
        newInstance.TransferResponsibilityFrom(this);
        return newInstance;
    }
    /// <summary>
    /// Gets a string representation of the contained disposable (if any).
    /// </summary>
    /// <returns>A string representation of the contained disposable (if any).</returns>
    public override string ToString()
    {
        return _contained?.ToString() ?? "";
    }
}
/// <summary>
/// A class containing the arguments for the <see cref="DisposeResponsibility.ResponsibilityNotDisposed"/> event.
/// </summary>
public class ResponsibilityNotDisposedEventArgs : EventArgs
{
    /// <summary>
    /// Constructs a new <see cref="ResponsibilityNotDisposedEventArgs"/> with the specified stack on creation.
    /// </summary>
    /// <param name="contained">The instance contained in the <see cref="DisposeResponsibility{T}"/>.</param>
    /// <param name="stackOnCreation">The stack trace captured when the instance was created.</param>
    public ResponsibilityNotDisposedEventArgs(object? contained, string stackOnCreation)
    {
        Contained = contained;
        StackOnCreation = stackOnCreation;
    }
    /// <summary>
    /// The object contained within the <see cref="DisposeResponsibility{T}"/>.
    /// </summary>
    public object? Contained { get; }
    /// <summary>
    /// A string containing the stack trace captured when the disposable instance was created.
    /// </summary>
    public string StackOnCreation { get; }
}

/// <summary>
/// A static class that contains utility functions applicable across all <see cref="DisposeResponsibility{T}"/> types.
/// For example, it allows you to query <see cref="DisposeResponsibility{T}"/> instances to see how many outstanding disposals remain for each unique construction call stack.
/// </summary>
/// <remarks>
/// <pitch>The cross-type leak-reporting surface for <see cref="DisposeResponsibility{T}"/>: subscribe to hear about undisposed instances as they finalize, or assert at test teardown that none leaked.</pitch>
/// <pledge>
/// <see cref="ResponsibilityNotDisposed"/> is raised from finalizer threads when a leaked instance is detected; while at least one handler is subscribed, the leak is considered handled and is not recorded for the deferred assertion.
/// <see cref="AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> forces full collections and finalizer drains, then throws <see cref="InvalidOperationException"/> listing every unhandled leak recorded since the last drain — intended for assembly-level test cleanup, where finalizer-time asserts would destabilize the test host.
/// </pledge>
/// <plan>Leak notices queue in a static <see cref="ConcurrentQueue{T}"/> as finalizers run; the assertion performs repeated <see cref="GC.Collect(int, GCCollectionMode)"/>/<see cref="GC.WaitForPendingFinalizers"/> passes before draining, since one pass cannot finalize objects that only became unreachable during finalization of others.</plan>
/// </remarks>
public static class DisposeResponsibility
{
#if DEBUG
    /// <summary>
    /// Gets an enumeration of all pending disposals tracked by instances of <see cref="DisposeResponsibility{T}"/>, 
    /// with the path that created them and the number of instances created through that path that have not yet been disposed.
    /// Entries are returned in descending order of the number of pending disposals.
    /// </summary>
    public static IEnumerable<(string Stack, int Count)> AllPendingDisposals => PendingDispose.AllPendingDisposals;
#endif
    private static readonly ConcurrentQueue<string> DeferredUndisposedLeaks = new();

    internal static void RecordDeferredUndisposedLeak(string notice)
    {
        DeferredUndisposedLeaks.Enqueue(notice);
    }

    /// <summary>
    /// Runs a full garbage collection and waits for pending finalizers (repeatedly) so that unreachable
    /// <see cref="DisposeResponsibility{T}"/> instances run their finalizers, then fails if any undisposed wrappers
    /// were detected on those finalization paths without a <see cref="ResponsibilityNotDisposed"/> handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Test hosts such as Microsoft Testing Platform can behave poorly when finalizers assert or block.
    /// Undisposed instances are instead recorded when finalized and
    /// reported here so tests can call this from assembly cleanup (after a full GC and finalizer drain).
    /// </para>
    /// <para>
    /// Subscribe to <see cref="ResponsibilityNotDisposed"/> (and optionally call <see cref="System.Diagnostics.Debugger.Break"/>)
    /// if you need immediate notification during interactive debugging.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when deferred leaks were recorded from finalization without a <see cref="ResponsibilityNotDisposed"/> handler.</exception>
    public static void AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

        List<string> leaksCopy = DrainDeferredUndisposedLeaks();

        if (leaksCopy.Count == 0)
            return;

        StringBuilder message = new();
        message.Append("Undisposed DisposeResponsibility leak(s) detected after full GC (no ResponsibilityNotDisposed handler).");
        for (int i = 0; i < leaksCopy.Count; i++)
            message.AppendLine().Append('[').Append(i).Append("] ").Append(leaksCopy[i]);
        throw new InvalidOperationException(message.ToString());
    }

    private static List<string> DrainDeferredUndisposedLeaks()
    {
        List<string> drained = [];
        while (DeferredUndisposedLeaks.TryDequeue(out string? notice))
            drained.Add(notice);
        return drained;
    }

    internal static bool NotifyEvent(object? sender, ResponsibilityNotDisposedEventArgs args)
    {
        if (ResponsibilityNotDisposed == null) return false;
        ResponsibilityNotDisposed.Invoke(sender, args);
        return true;
    }
    /// <summary>
    /// An event that notifies subscribers that a <see cref="DisposeResponsibility{T}"/> was not properly disposed.
    /// </summary>
    public static event EventHandler<ResponsibilityNotDisposedEventArgs>? ResponsibilityNotDisposed;
}

#if DEBUG
class PendingDispose
{
    private static readonly ConcurrentDictionary<string, PendingDispose> _PendingDisposals = new();

    private int _count;

    public PendingDispose() { _count = 1; }

    private PendingDispose Increment()
    {
        System.Threading.Interlocked.Increment(ref _count);
        return this;
    }
    private PendingDispose Decrement()
    {
        System.Threading.Interlocked.Decrement(ref _count);
        return this;
    }
    public static string OnConstruct(string? stackOnCreation = null, int stackTraceCharLimit = 1024)
    {
        string construct = stackOnCreation ?? new System.Diagnostics.StackTrace(2).ToString();
        if (construct.Length > stackTraceCharLimit) construct = construct.Substring(0, stackTraceCharLimit);
        _PendingDisposals.AddOrUpdate(construct, new PendingDispose(), (k, v) => v.Increment());
        return construct;
    }
    public static void OnDispose(string stackOnConstruct)
    {
        _PendingDisposals.AddOrUpdate(stackOnConstruct, new PendingDispose(), (k, v) => v.Decrement());
    }
    public static IEnumerable<(string Stack, int Count)> AllPendingDisposals => _PendingDisposals.OrderByDescending(p => p.Value._count).Select(p => (p.Key, p.Value._count));
}
#endif
