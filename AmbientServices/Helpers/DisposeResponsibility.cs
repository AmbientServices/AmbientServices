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
/// <see cref="StackOnCreation"/> identifies where the responsibility originated so leaks can be attributed to their creation site.  It is the string the creator supplied (when one was supplied), otherwise a rendering of the stack captured at construction, otherwise empty — creation-site detail is only gathered when it was explicitly enabled, so callers must treat an empty string as "no detail was collected" rather than as an error.
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
    /// Gets a string containing the stack at the time the responsibility was created, or the creation-site string the creator supplied, or an empty string if no creation-site detail was collected.
    /// </summary>
    /// <remarks>
    /// When the creation site was captured as a stack (see <see cref="DisposeResponsibility.CollectLeakDetails"/>), the string is rendered on each read rather than at construction, so reading this repeatedly is not free.
    /// </remarks>
    public string StackOnCreation { get; }
}

internal interface IShirkResponsibility
{
    /// <summary>
    /// Intended for internal use.  Takes responsibility from the instance (presumably to transfer it to another responsibility object).
    /// </summary>
    internal void ShirkResponsibility();
    /// <summary>
    /// Intended for internal use.  Gets the creation-site string the creator supplied, or null if none was supplied.
    /// Transferring this (instead of <see cref="IDisposeResponsibility{T}.StackOnCreation"/>) is what keeps a transfer from rendering a deferred stack capture.
    /// </summary>
    internal string? ExplicitCreationSite { get; }
    /// <summary>
    /// Intended for internal use.  Gets the (unrendered) stack captured at construction, or null when no stack was captured (detail collection was off, or an explicit creation site was supplied).
    /// </summary>
    internal System.Diagnostics.StackTrace? CapturedCreationStack { get; }
    /// <summary>
    /// Intended for internal use.  Gets the identifier of this instance's entry in the DEBUG-build pending-disposal census (zero when it has no entry), so a transfer can hand the entry to the instance that takes responsibility.
    /// </summary>
    internal long PendingDisposalCensusId { get; }
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
/// <pitch>The standard realization of <see cref="IDisposeResponsibility{T}"/>, with leak detection built in: an instance that dies undisposed announces itself (log, debugger break, event, and deferred test-time assertion), along with the stack that created it when creation-site collection is enabled.  Not for statics or non-disposable contents.</pitch>
/// <pledge><see cref="IDisposeResponsibility{T}"/></pledge>
/// <pledge>
/// Beyond the transfer contract: <see cref="AssumeResponsibility"/> disposes any current contents before taking the new ones; disposal handles contents that are <see cref="IDisposable"/>, <see cref="IAsyncDisposable"/> (synchronously waited from <see cref="Dispose"/>, natively awaited from <c>DisposeAsync</c>), or tuples of disposables (each element disposed).
/// An instance that is finalized while still owning responsibility reports a leak: through the <see cref="DisposeResponsibility.ResponsibilityNotDisposed"/> event when subscribed, otherwise via a warning log, a debugger break when attached, and a record that <see cref="DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> later surfaces.  An emptied (transferred-from) instance never reports.
/// Leak reports always identify the leak (the contained type, and the creation-site string when the creator supplied one), but only carry a creation stack when <see cref="DisposeResponsibility.CollectLeakDetails"/> was on where the instance was constructed; with it off, the report degrades to the leak occurrence rather than disappearing.
/// Individual instances are not thread-safe; concurrent transfer and dispose require caller coordination.
/// </pledge>
/// <plan>
/// Three fields — the contained object, the creation site, and a still-responsible flag — plus a finalizer that fires only on leaks: proper disposal and shirking both call <see cref="GC.SuppressFinalize"/> (and <see cref="AssumeResponsibility"/>/<see cref="TransferResponsibilityFrom"/> re-register), so the finalizer costs nothing on the happy path.  Transfer works through the internal <c>IShirkResponsibility</c> back-channel, which empties the source and clears its responsibility so it cannot double-dispose or false-report.
/// The creation site is either the string the creator supplied (no capture cost at all — what hot paths should pass) or, when <see cref="DisposeResponsibility.CollectLeakDetails"/> is on, an unrendered <see cref="System.Diagnostics.StackTrace"/> object whose <see cref="object.ToString"/> is deferred to report time; the setting is only consulted when no explicit site was supplied, so explicit-site callers never pay for reading it.  Rendering is wrapped in a catch-all that degrades to a short placeholder, because the one modern way a capture can fail to render is a collectible <c>AssemblyLoadContext</c> unloading between capture and report, and losing the leak report to that would be worse than losing the frames.
/// The stack is captured without file information: that keeps capture (which resolves file/line eagerly when asked for it) close to its historical cost, at the price of frame-only reports; ask for file info here only if a build's PDB-reading cost is acceptable at every construction.
/// Deferral has a history worth keeping: the string used to be rendered eagerly at construction because .NET Framework ran finalizers at AppDomain unload and process shutdown, where deferred resolution could touch already-finalized state.  Modern .NET has no AppDomain unloads and never runs finalizers at shutdown, which is what makes deferring the rendering to report time safe.
/// In DEBUG builds each still-undisposed instance also has an entry in <c>PendingDispose</c> (a creation-site object per instance, grouped and rendered only when <c>DisposeResponsibility.AllPendingDisposals</c> is enumerated, so the census does not force eager rendering either); transfers hand the entry to the instance taking responsibility.
/// </plan>
/// </remarks>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
public sealed class DisposeResponsibility<T> : IDisposeResponsibility<T>, IShirkResponsibility
{
    private static readonly AmbientLogger<DisposeResponsibility<T>> Logger = new();

    private string? _explicitStackOnCreation;                           // the creation-site string the creator supplied, or null if none was supplied
    private System.Diagnostics.StackTrace? _capturedStackOnCreation;    // the creation stack, captured but NOT rendered (see the Plan), or null when nothing was captured
    private T? _contained;
    private bool _stillResponsible;                                     // whether this instance still owes a disposal, and therefore should report a leak if it is finalized
#if DEBUG
    private long _pendingDisposalCensusId;                              // this instance's entry in the DEBUG-only pending-disposal census, or zero if it has no entry
#endif

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
    /// Gets a string containing the stack at the time the responsibility was created, or the creation-site string that was passed in when the responsibility was created, or an empty string if no creation-site detail was collected (see <see cref="DisposeResponsibility.CollectLeakDetails"/>).
    /// </summary>
    /// <remarks>
    /// A captured stack is rendered here, on demand, rather than at construction, so each read of this property on a captured-stack instance costs a stack-trace rendering.
    /// </remarks>
    public string StackOnCreation
    {
        get
        {
            string? explicitSite = _explicitStackOnCreation;
            if (explicitSite != null) return explicitSite;
            System.Diagnostics.StackTrace? capturedStack = _capturedStackOnCreation;
            if (capturedStack == null) return "";
            try
            {
                return capturedStack.ToString();
            }
#pragma warning disable CA1031 // Do not catch general exception types--losing the leak report because the frames could no longer be rendered (a collectible AssemblyLoadContext unloading between capture and report) would be far worse than losing the frames
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return $"<the creation stack could not be rendered: {ex.GetType().Name}: {ex.Message}>";
            }
        }
    }

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
        if (!_stillResponsible) return;  // responsibility has been shirked or fulfilled; no leak to report
        string stackOnCreation = StackOnCreation;  // this is where a captured stack gets rendered--at report time, not at construction time
        if (DisposeResponsibility.NotifyEvent(this, new ResponsibilityNotDisposedEventArgs(_contained, stackOnCreation))) return;

        // note that the leak is always reported, even when no creation-site detail was collected--the occurrence and the type are still worth knowing
        string notice = string.IsNullOrEmpty(stackOnCreation)
            ? $"Disposable object ({typeof(T).FullName}) was not disposed.  No creation site was collected: enable the {DisposeResponsibility.CollectLeakDetailsSettingKey} setting (see {nameof(DisposeResponsibility)}.{nameof(DisposeResponsibility.ScopedLeakDetailCollection)}) to get creation stacks."
            : $"Disposable object ({typeof(T).FullName}) was not disposed.  Object was constructed at {stackOnCreation}.";
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
        // note that the stack is captured but NOT rendered here--rendering is deferred to report time (see the Plan)
        _capturedStackOnCreation = DisposeResponsibility.CollectLeakDetails ? new System.Diagnostics.StackTrace(1) : null;
        _stillResponsible = true;
#if DEBUG
        _pendingDisposalCensusId = PendingDispose.OnConstruct(_capturedStackOnCreation);
#endif
        // Note that the rules are that this MUST be disposed, so we want to enforce that even it the contents are null!
    }
    /// <summary>
    /// Constructs a dispose responsibility object which takes responsibility for disposing the specified disposable object.
    /// </summary>
    /// <param name="contained">An optional disposable object that will be owned and disposed by the instance.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="contained"/>.  When this is specified, no stack is captured (which is how hot paths avoid the capture cost entirely) and <see cref="DisposeResponsibility.CollectLeakDetails"/> is not consulted.</param>
    public DisposeResponsibility(T? contained, string? stackOnCreation = null)
    {
        _contained = contained;
        _explicitStackOnCreation = stackOnCreation;
        // only consult the setting when we would otherwise walk the stack, so callers that pass their own creation site pay nothing for it
        _capturedStackOnCreation = (stackOnCreation == null && DisposeResponsibility.CollectLeakDetails) ? new System.Diagnostics.StackTrace(1) : null;
        _stillResponsible = true;
#if DEBUG
        _pendingDisposalCensusId = PendingDispose.OnConstruct(_explicitStackOnCreation ?? (object?)_capturedStackOnCreation);
#endif
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
        // transfer the creation site as-is (an unrendered capture stays unrendered)
        _explicitStackOnCreation = isr.ExplicitCreationSite;
        _capturedStackOnCreation = isr.CapturedCreationStack;
        _contained = other.Contained;
        _stillResponsible = true;
#if DEBUG
        _pendingDisposalCensusId = isr.PendingDisposalCensusId;
#endif
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
        // dispose the contained object (if any) first, so that if disposal throws, the instance stays reportable and (in DEBUG) censused
        if (_contained is not null)
        {
            DisposeContained(_contained);
            _contained = default;
        }
        // clear the responsibility bookkeeping even when there was no contained object: a responsibility must be disposed regardless of its contents, so a disposed instance must never later report a leak or linger in the pending-disposal census
#if DEBUG
        PendingDispose.OnDispose(_pendingDisposalCensusId);
        _pendingDisposalCensusId = 0;
#endif
        _explicitStackOnCreation = null;
        _capturedStackOnCreation = null;
        _stillResponsible = false;
        GC.SuppressFinalize(this);
    }
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// Asynchronously disposes of this instance by disposing of the contained instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // dispose the contained object (if any) first, so that if disposal throws, the instance stays reportable and (in DEBUG) censused
        if (_contained is not null)
        {
            await DisposeContainedAsync(_contained);
            _contained = default;
        }
        // clear the responsibility bookkeeping even when there was no contained object, mirroring Dispose so an async-disposed instance never later reports a leak or lingers in the pending-disposal census
#if DEBUG
        PendingDispose.OnDispose(_pendingDisposalCensusId);
        _pendingDisposalCensusId = 0;
#endif
        _explicitStackOnCreation = null;
        _capturedStackOnCreation = null;
        _stillResponsible = false;
        GC.SuppressFinalize(this);
    }
#endif
    /// <summary>
    /// Disposes of any existing disposable and assumes responsibility for the newly specified disposable.
    /// </summary>
    /// <param name="newDisposable">The new disposable to take responsibility for.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="newDisposable"/>.  When this is specified, no stack is captured and <see cref="DisposeResponsibility.CollectLeakDetails"/> is not consulted.</param>
    public void AssumeResponsibility(T? newDisposable, string? stackOnCreation = null)
    {
        Dispose();
        _contained = newDisposable;
        _explicitStackOnCreation = stackOnCreation;
        _capturedStackOnCreation = (stackOnCreation == null && DisposeResponsibility.CollectLeakDetails) ? new System.Diagnostics.StackTrace(1) : null;
        _stillResponsible = true;
#if DEBUG
        _pendingDisposalCensusId = PendingDispose.OnConstruct(_explicitStackOnCreation ?? (object?)_capturedStackOnCreation);
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
        // transfer the creation site as-is (an unrendered capture stays unrendered)
        _explicitStackOnCreation = isr.ExplicitCreationSite;
        _capturedStackOnCreation = isr.CapturedCreationStack;
        _stillResponsible = true;
#if DEBUG
        _pendingDisposalCensusId = isr.PendingDisposalCensusId;
#endif
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
        _explicitStackOnCreation = null;
        _capturedStackOnCreation = null;
        _stillResponsible = false;  // clear so FinalizeLogic won't report a false leak
#if DEBUG
        _pendingDisposalCensusId = 0;   // the census entry (if there was one) now belongs to whoever took responsibility
#endif
#pragma warning disable CA1816     // intentional: shirking transfers ownership away, so no finalizer needed
        GC.SuppressFinalize(this);
#pragma warning restore CA1816
    }
    /// <summary>
    /// Intended for internal use.  Gets the creation-site string the creator supplied, or null if none was supplied.
    /// </summary>
    string? IShirkResponsibility.ExplicitCreationSite => _explicitStackOnCreation;
    /// <summary>
    /// Intended for internal use.  Gets the unrendered stack captured at construction, or null if none was captured.
    /// </summary>
    System.Diagnostics.StackTrace? IShirkResponsibility.CapturedCreationStack => _capturedStackOnCreation;
    /// <summary>
    /// Intended for internal use.  Gets this instance's entry in the DEBUG-only pending-disposal census, or zero if it has none.
    /// </summary>
    long IShirkResponsibility.PendingDisposalCensusId =>
#if DEBUG
        _pendingDisposalCensusId;
#else
        0;  // there is no census in non-DEBUG builds
#endif
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
/// <pitch>The cross-type leak-reporting surface for <see cref="DisposeResponsibility{T}"/>: subscribe to hear about undisposed instances as they finalize, or assert at test teardown that none leaked — and turn on the creation-site detail gathering that makes those reports actionable.</pitch>
/// <pledge>
/// <see cref="ResponsibilityNotDisposed"/> is raised from finalizer threads when a leaked instance is detected; while at least one handler is subscribed, the leak is considered handled and is not recorded for the deferred assertion.
/// <see cref="AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> forces full collections and finalizer drains, then throws <see cref="InvalidOperationException"/> listing every unhandled leak recorded since the last drain — intended for assembly-level test cleanup, where finalizer-time asserts would destabilize the test host.
/// Detail gathering is off by default, and asking for an explicit leak report while it is off is a setup error, not a silent degradation: <see cref="AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> throws <see cref="InvalidOperationException"/> naming what to enable, without collecting or draining anything.  The finalizer path makes no such demand — it keeps reporting leaks (without creation stacks) whether detail gathering is on or off.
/// <see cref="CollectLeakDetails"/> follows the ambient settings, so enabling it is a per-process configuration choice or, via <see cref="ScopedLeakDetailCollection"/>, a per-call-context one; instances capture their creation site according to the value in effect where they were constructed, not where they are reported.
/// </pledge>
/// <plan>Leak notices queue in a static <see cref="ConcurrentQueue{T}"/> as finalizers run; the assertion performs repeated <see cref="GC.Collect(int, GCCollectionMode)"/>/<see cref="GC.WaitForPendingFinalizers"/> passes before draining, since one pass cannot finalize objects that only became unreachable during finalization of others.
/// The toggle is a declared <see cref="IAmbientSetting{T}"/> (off by default) rather than a static flag, so that it is discoverable with every other setting in the process and so that overriding it per call context — the scoped-override idiom used throughout this library — isolates concurrently-running tests from each other.  <see cref="ScopedLeakDetailCollection"/> composes that override by layering a one-setting set over whatever settings set the context already had, so nothing else in the context's configuration is lost.</plan>
/// </remarks>
public static class DisposeResponsibility
{
    private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
    private static readonly IAmbientSetting<bool> _CollectLeakDetailsSetting = AmbientSettings.GetAmbientSetting<bool>(nameof(DisposeResponsibility) + "-CollectLeakDetails",
        "Whether or not `DisposeResponsibility<T>` should gather creation-site detail (a deferred stack capture) for instances constructed without an explicit creation-site string.  This defaults to `false` because capturing a stack on every construction is expensive, so it should be turned on only in the projects and scenarios that report leaks.  Note that `DisposeResponsibility.AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc` throws when this is off.",
        false, s => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "1", StringComparison.Ordinal) || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the ambient settings key that controls <see cref="CollectLeakDetails"/>, so callers can override the setting without repeating a magic string.
    /// </summary>
    public static string CollectLeakDetailsSettingKey => _CollectLeakDetailsSetting.Key;
    /// <summary>
    /// Gets whether or not <see cref="DisposeResponsibility{T}"/> instances constructed in this call context should gather creation-site detail (a stack capture that is only rendered if a leak is reported).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>off by default</b>, because capturing a stack on every construction is expensive.
    /// Turn it on (with the <see cref="CollectLeakDetailsSettingKey"/> ambient setting, or for one call context with <see cref="ScopedLeakDetailCollection"/>) in the projects and scenarios that actually report leaks —
    /// typically test assemblies and diagnostic runs, which are the same places that call <see cref="AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc"/> (which throws if this is off).
    /// </para>
    /// <para>
    /// Callers that pass an explicit creation-site string when constructing a <see cref="DisposeResponsibility{T}"/> get that string in leak reports whether this is on or off, and never pay for a capture; that remains the right choice for hot paths.
    /// </para>
    /// </remarks>
    public static bool CollectLeakDetails => _CollectLeakDetailsSetting.Value;
    /// <summary>
    /// Turns creation-site detail gathering on (or off) for the current call context until the returned object is disposed, leaving the rest of the context's settings alone.
    /// </summary>
    /// <param name="collect">Whether detail gathering should be on (the default) or off within the scope.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous settings for the call context when it is disposed.</returns>
    /// <remarks>
    /// This is the convenient way for a test or a diagnostic scenario to opt in: instances constructed within the scope capture their creation stacks, and concurrently-running call contexts are unaffected.
    /// Note that instances capture according to the value in effect where they are <em>constructed</em>, so the scope has to cover the construction, not just the reporting.
    /// </remarks>
    public static IDisposable ScopedLeakDetailCollection(bool collect = true)
    {
        BasicAmbientSettingsSet scopedSettings = new(nameof(ScopedLeakDetailCollection));
        scopedSettings.ChangeSetting(_CollectLeakDetailsSetting.Key, collect ? "true" : "false");
        // layer the one setting over whatever settings set the context already had so that overriding this setting doesn't hide the rest of the context's settings
        return new ScopedLocalServiceOverride<IAmbientSettingsSet>(new AmbientSettingsLayers(_SettingsSet.Local, scopedSettings));
    }
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
    /// <para>
    /// Calling this while <see cref="CollectLeakDetails"/> is off (its default) is treated as a setup mistake and throws immediately without collecting or draining anything,
    /// because leaks found that way could not name their creation sites and would be nearly impossible to track down.
    /// Enable detail collection in the project or scenario that verifies leaks (see <see cref="CollectLeakDetailsSettingKey"/> and <see cref="ScopedLeakDetailCollection"/>), covering the code that constructs the instances.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when deferred leaks were recorded from finalization without a <see cref="ResponsibilityNotDisposed"/> handler, or when <see cref="CollectLeakDetails"/> is off.</exception>
    public static void AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc()
    {
        // fail loudly instead of "verifying" leaks that could never name where they came from
        if (!CollectLeakDetails) throw new InvalidOperationException($"{nameof(AssertNoUndisposedDisposeResponsibilityLeaksAfterFullGc)} was called while {nameof(DisposeResponsibility)} leak-detail collection is off, so any leak it found could not report where the instance was created!  Turn detail collection on for the code being verified by setting the ambient setting \"{CollectLeakDetailsSettingKey}\" to \"true\" (process-wide, for example in a test assembly's initialization) or by wrapping the scope that constructs and verifies the instances in {nameof(DisposeResponsibility)}.{nameof(ScopedLeakDetailCollection)}().  It is off by default because capturing creation stacks is expensive.");
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
/// <summary>
/// A DEBUG-build census of the creation sites of <see cref="DisposeResponsibility{T}"/> instances that have not been disposed yet.
/// </summary>
/// <remarks>
/// <pitch>Answers "what is piling up undisposed right now, and where was it created?" while chasing a leak, without needing any instance to be finalized first.</pitch>
/// <pledge>An instance is counted from the moment it takes responsibility until it is disposed (a transfer moves the entry to whoever took responsibility, and a leaked instance stays counted forever, which is the point).  Instances with no creation-site detail at all are not counted, since there would be nothing to attribute them to.  Enumerating the census never renders more than the entries that exist at that moment, and never throws because of a frame it could not render.</pledge>
/// <plan>A <see cref="ConcurrentDictionary{TKey,TValue}"/> from a monotonically increasing id (handed back to the instance, which returns it on dispose) to that instance's creation site as an <em>unrendered</em> object — either the caller's string or a <see cref="System.Diagnostics.StackTrace"/>.  Grouping by creation site therefore happens at enumeration time, by rendering (and capping) each entry then, which is what keeps the census from forcing the eager stack rendering this class used to require.</plan>
/// </remarks>
class PendingDispose
{
    private const int StackTraceCharLimit = 1024;

    private static readonly ConcurrentDictionary<long, object> _PendingDisposals = new();   // id -> the creation site (a string or an unrendered StackTrace) of an instance that has not been disposed yet
    private static long _NextId;                                                            // interlocked

    /// <summary>
    /// Adds a census entry for an instance that has just taken responsibility.
    /// </summary>
    /// <param name="creationSite">The instance's creation site: the caller-supplied string, or the unrendered <see cref="System.Diagnostics.StackTrace"/> captured at construction, or null if no detail was collected.</param>
    /// <returns>The id of the new entry, or zero if no entry was added.</returns>
    public static long OnConstruct(object? creationSite)
    {
        if (creationSite == null) return 0;     // nothing to attribute this instance to, so don't census it
        long id = System.Threading.Interlocked.Increment(ref _NextId);
        _PendingDisposals[id] = creationSite;
        return id;
    }
    /// <summary>
    /// Removes the census entry with the specified id (if any), because that instance's disposal responsibility has been fulfilled.
    /// </summary>
    /// <param name="id">The id returned by <see cref="OnConstruct"/>, or zero if the instance has no entry.</param>
    public static void OnDispose(long id)
    {
        if (id != 0) _PendingDisposals.TryRemove(id, out _);
    }
    public static IEnumerable<(string Stack, int Count)> AllPendingDisposals => _PendingDisposals.Values.GroupBy(RenderCreationSite, StringComparer.Ordinal).Select(g => (Stack: g.Key, Count: g.Count())).OrderByDescending(e => e.Count);
    private static string RenderCreationSite(object creationSite)
    {
        string site;
        if (creationSite is string explicitSite)
        {
            site = explicitSite;
        }
        else
        {
            try
            {
                site = creationSite.ToString() ?? "";
            }
#pragma warning disable CA1031 // Do not catch general exception types--one unrenderable entry must not break the whole census
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                site = $"<the creation stack could not be rendered: {ex.GetType().Name}>";
            }
        }
        return (site.Length > StackTraceCharLimit) ? site.Substring(0, StackTraceCharLimit) : site;
    }
}
#endif
