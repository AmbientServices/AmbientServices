using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A static class that contains utility functions applicable across all <see cref="DisposeResponsibility{T}"/> types.
/// For example, it allows you to query <see cref="DisposeResponsibility{T}"/> instances to see how many outstanding disposals remain for each unique construction call stack.
/// </summary>
public static class DisposeResponsibility
{
    /// <summary>
    /// Gets an enumeration of all pending disposals tracked by instances of <see cref="DisposeTracker{T}"/>, 
    /// with the path that created them and the number of instances created through that path that have not yet been disposed.
    /// Entries are returned in descending order of the number of pending disposals.
    /// </summary>
    public static IEnumerable<(string Stack, int Count)> AllPendingDisposals => PendingDispose.AllPendingDisposals;
}

/// <summary>
/// A class that wraps a contained <see cref="IDisposable"/> and allows transfer of the disposal responsibility between objects (and the stack).
/// Also starts tracking the need for object disposal as returned by <see cref="DisposeResponsibility.AllPendingDisposals"/>.
/// This class should ALWAYS be disposed.
/// </summary>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
/// <param name="Contained">A <see cref="IDisposable"/> type contained within the tracker.</param>
public sealed class DisposeResponsibility<T> : IDisposable
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
    where T : class, IDisposable
{
    private string _stackOnCreation;
    private T? _contained;

    /// <summary>
    /// The contained disposable object.  Throws an <see cref="ObjectDisposedException"/> if the object is no longer contained.
    /// </summary>
    public T Contained => (_contained == null) ? throw new ObjectDisposedException("The contained disposable object is no longer owned by this responsibility object!") : _contained;
    /// <summary>
    /// Returns whether or not this instance contains a disposable and therefore still has responsibility for disposing it.
    /// </summary>
    public bool ContainsDisposable => _contained != null;

#if DEBUG
    ~DisposeResponsibility()
    {
        System.Diagnostics.Debug.Assert(_contained == null, $"Disposable object was not disposed.  Object was constructed at {_stackOnCreation}.");
    }
#endif

    /// <summary>
    /// Constructs an empty dispose responsibility object which can later take responsibility for disposing a specified disposable object.
    /// </summary>
    public DisposeResponsibility()
    {
        _stackOnCreation = "";
    }
    /// <summary>
    /// Constructs a dispose responsibility object which takes responsibility for disposing the specified disposable object.
    /// </summary>
    /// <param name="contained">The disposable object that is owned and will be disposed by the instance.</param>
    public DisposeResponsibility(T contained, string? stackOnCreation = null)
    {
        _contained = contained;
        _stackOnCreation = PendingDispose.OnConstruct(stackOnCreation, 1024);
    }

    /// <summary>
    /// Constructs a dispose responsibility object that takes responsibility from the specified responsibility object.
    /// </summary>
    /// <param name="other">Another dispose responsibility object to taks responsibility from.</param>
    public DisposeResponsibility(DisposeResponsibility<T> other)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(other);
#else
        if (other == null) throw new ArgumentNullException(nameof(other));
#endif
        _stackOnCreation = other._stackOnCreation;
        _contained = other._contained;
        other._contained = null;
        other._stackOnCreation = "";
    }

    /// <summary>
    /// Disposes of this instance by disposing of the contained instance.
    /// </summary>
    public void Dispose()
    {
        if (_contained is not null)
        {
            PendingDispose.OnDispose(_stackOnCreation);
            Contained.Dispose();
            GC.SuppressFinalize(this);
            _contained = null;
        }
    }
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// Asynchornously disposes of this instance by disposing of the contained instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Contained is IAsyncDisposable ad)
        {
            if (_contained is not null)
            {
                PendingDispose.OnDispose(_stackOnCreation);
                await ad.DisposeAsync().ConfigureAwait(false);
                GC.SuppressFinalize(this);
                _contained = null;
            } // else no need to dispse synchronously or asynchronously
        }
        else
        {
            Contained.Dispose();
        }
    }
#endif
    /// <summary>
    /// Transfers the responsibility in this instance into the target instance.
    /// </summary>
    /// <param name="targetOwnership">The <see cref="DisposeResponsibility{T}"/> instance that will hereafter take responsibility (and responsibility to dispose).</param>
    public void TransferTo(DisposeResponsibility<T> targetOwnership)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(targetOwnership);
#else
        if (targetOwnership == null) throw new ArgumentNullException(nameof(targetOwnership));
#endif
        targetOwnership.Dispose();
        targetOwnership._contained = _contained;
        targetOwnership._stackOnCreation = _stackOnCreation;
        _contained = null;
        _stackOnCreation = "";
    }
    /// <summary>
    /// Transfers the responsibility from a specified instance into this instance.
    /// </summary>
    /// <param name="sourceOwnership">The <see cref="DisposeResponsibility{T}"/> instance whose contained disposable will will hereafter be owned by this instance.</param>
    public void TransferFrom(DisposeResponsibility<T> sourceOwnership)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(sourceOwnership);
#else
        if (sourceOwnership == null) throw new ArgumentNullException(nameof(sourceOwnership));
#endif
        Dispose();
        _contained = sourceOwnership._contained;
        _stackOnCreation = sourceOwnership._stackOnCreation;
        sourceOwnership._contained = null;
        sourceOwnership._stackOnCreation = "";
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
