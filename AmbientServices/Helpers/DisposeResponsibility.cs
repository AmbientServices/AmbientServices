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
    /// Gets an enumeration of all pending disposals tracked by instances of <see cref="DisposeResponsibility{T}"/>, 
    /// with the path that created them and the number of instances created through that path that have not yet been disposed.
    /// Entries are returned in descending order of the number of pending disposals.
    /// </summary>
    public static IEnumerable<(string Stack, int Count)> AllPendingDisposals => PendingDispose.AllPendingDisposals;
}

/// <summary>
/// An interface that abstracts an object that contains an <see cref="IDisposable"/> and allows transfer of the disposal responsibility between isntances (and the stack).
/// Instances should ALWAYS be disposed.
/// </summary>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
public interface IDisposeResponsibility<out T> : IDisposable
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
    where T : class, IDisposable
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
/// Ensures that 
/// Also starts tracking the need for object disposal as returned by <see cref="DisposeResponsibility.AllPendingDisposals"/>.
/// Instances of this class contained in another instance should only be contained in objects that are disposable and should ALWAYS be disposed.
/// Instances of this class on the stack should ALWAYS be in a using statement.  The responsibility to dispose may be transferred out to another instance using <see cref="TransferResponsibilityToCaller"/> passed into a constructor, by calling <see cref="TransferResponsibilityFrom(IDisposeResponsibility{T})"/> on another instance and passing in this instance, or by returning an instance to a caller, but each instance of this class should always be disposed to prevent leaks.
/// </summary>
/// <typeparam name="T">The disposable type being wrapped.</typeparam>
public sealed class DisposeResponsibility<T> : IDisposeResponsibility<T>, IShirkResponsibility
    where T : class, IDisposable
{
    private string _stackOnCreation;
    private T? _contained;

    /// <summary>
    /// The contained disposable object.  Throws an <see cref="ObjectDisposedException"/> if the object is no longer contained.
    /// </summary>
    public T Contained => (_contained == null) ? throw new ObjectDisposedException("The contained disposable object is no longer owned by this responsibility object!") : _contained;
    /// <summary>
    /// The contained disposable object, or null if no disposable is contained.
    /// </summary>
    public T? NullableContained => _contained;
    /// <summary>
    /// Returns whether or not this instance contains a disposable and therefore still has responsibility for disposing it.
    /// </summary>
    public bool ContainsDisposable => _contained != null;
    /// <summary>
    /// Gets a string containing the stack at the time the responsibility was created.
    /// </summary>
    public string StackOnCreation => _stackOnCreation;

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
    /// <param name="contained">An optional disposable object that will be owned and disposed by the instance.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="contained"/>.</param>
    public DisposeResponsibility(T? contained, string? stackOnCreation = null)
    {
        _contained = contained;
        _stackOnCreation = (contained != null) ? PendingDispose.OnConstruct(stackOnCreation, 1024) : "";
    }
    /// <summary>
    /// Constructs a dispose responsibility object that takes responsibility from the specified responsibility object.
    /// </summary>
    /// <param name="other">Another dispose responsibility object to taks responsibility from.</param>
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
    /// Disposes of any existing disposable and assumes responsibility for the newly specified disposable.
    /// </summary>
    /// <param name="newDisposable">The new disposable to take responsibility for.</param>
    /// <param name="stackOnCreation">The creation stack to associated with <paramref name="newDisposable"/>.</param>
    public void AssumeResponsibility(T? newDisposable, string? stackOnCreation = null)
    {
        Dispose();
        _contained = newDisposable;
        _stackOnCreation = PendingDispose.OnConstruct(stackOnCreation, 1024);
        GC.ReRegisterForFinalize(this);
    }
    /// <summary>
    /// Transfers the responsibility from a specified instance into this instance.
    /// </summary>
    /// <param name="sourceOwnership">The <see cref="IDisposeResponsibility{T}"/> instance whose contained disposable will will hereafter be owned by this instance.</param>
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
        GC.ReRegisterForFinalize(this);
        isr.ShirkResponsibility();
    }
    /// <summary>
    /// Intended for internal use.  Takes responsibility from the instance (presumably to transfer it to another responsibility object).
    /// </summary>
    void IShirkResponsibility.ShirkResponsibility()
    {
        _contained = null;
        _stackOnCreation = "";
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
