using System;

namespace AmbientServices;

/// <summary>
/// A class that edits the caller context *after* an asynchronous operation completes so that subsequent calls inherit the applied context,
/// which is not the case if the context is edited in the asynchronous operation itself.
/// Have the asynchronous function return this type (or a <see cref="ContextMutator{T}"/>) and call <see cref="ApplyContextChanges"/> immediately after the asynchronous operation completes.
/// It is *not* possible to wrap this logic and thereby simplify calling the asynchronous function and then calling <see cref="ApplyContextChanges"/> on the result, because the context editing will not be applied to the caller context.
/// </summary>
/// <remarks>
/// <pitch>Solves the async context-mutation problem: an <see cref="System.Threading.AsyncLocal{T}"/> write made <em>inside</em> an awaited function is lost when the function returns, because <see cref="System.Threading.ExecutionContext"/> flows down into callees but never back up.  This carries the intended mutation back to the caller as a value so the caller can apply it on its own context.</pitch>
/// <pledge>The mutation happens only when the <em>caller</em> invokes <see cref="ApplyContextChanges"/>, and it applies to whichever call context invokes it — so it must be called directly in the frame that should inherit the change, immediately after awaiting, not from inside any further wrapper (each wrapping layer reintroduces the very problem this solves).  The mutator itself is a passive value: transporting it performs no context changes.</pledge>
/// <plan>Wraps a single caller-supplied <see cref="Action"/>; the class adds no state or logic beyond signaling the calling convention through its type.</plan>
/// </remarks>
public sealed class ContextMutator
{
    private readonly Action _applyContextChanges;

    /// <summary>
    /// Constructs the context editor with actions to be executed on return.
    /// </summary>
    /// <param name="applyContextChanges">The action to call after returning from the asynchronous function.</param>
    public ContextMutator(Action applyContextChanges)
    {
        _applyContextChanges = applyContextChanges;
    }
    /// <summary>
    /// Calls the context editing action to be executed after returning from the asynchronous function.
    /// </summary>
    public void ApplyContextChanges()
    {
        _applyContextChanges.Invoke();
    }
}

/// <summary>
/// A class that temporarily changes the caller context *after* an asynchronous operation completes so that subsequent calls inherit the applied context.
/// It is *not* possible to wrap this logic and thereby simplify calling the asynchronous function and then calling <see cref="ApplyContextChanges"/> on the result, because the context editing will not be applied to the caller context.
/// </summary>
/// <remarks>
/// <pitch>The scoped variant of <see cref="ContextMutator"/>: applies a context change in the caller's frame and undoes it when the scope ends, for mutations that should only cover a region of the caller's subsequent work.</pitch>
/// <pledge>Follows the <see cref="ContextMutator"/> calling convention — apply must be invoked by the frame that should see the change, immediately after awaiting — and adds a paired revert on <see cref="Dispose"/>.  <see cref="ApplyContextChanges"/> returns the instance so it can be chained directly into a <c>using</c> statement.  Disposal reverts unconditionally; it does not track whether apply was ever called.</pledge>
/// <plan>Wraps a caller-supplied apply <see cref="Action"/> and revert <see cref="Action"/>; no other state.</plan>
/// </remarks>
public sealed class TemporaryContextMutator: IDisposable
{
    private readonly Action _applyContextChanges;
    private readonly Action _revertContextChanges;

    /// <summary>
    /// Constructs the context editor with actions to be executed on return.
    /// </summary>
    /// <param name="applyContextChanges">The action to call after returning from the asynchronous function.</param>
    /// <param name="revertContextChanges">The action to call when the temporary changes are no longer desired.</param>
    public TemporaryContextMutator(Action applyContextChanges, Action revertContextChanges)
    {
        _applyContextChanges = applyContextChanges;
        _revertContextChanges = revertContextChanges;
    }
    /// <summary>
    /// Calls the context editing action to be executed after returning from the asynchronous function.
    /// </summary>
    /// <returns>The <see cref="TemporaryContextMutator"/> instance, in case the caller wants to chain the call to this function as part of a using statement.</returns>
    public TemporaryContextMutator ApplyContextChanges()
    {
        _applyContextChanges.Invoke();
        return this;
    }
    /// <summary>
    /// Reverts the context changes applied by <see cref="ApplyContextChanges"/>.
    /// </summary>
    public void Dispose()
    {
        _revertContextChanges.Invoke();
    }
}

/// <summary>
/// A class that edits the caller context *after* an asynchronous operation completes so that subsequent calls inherit the applied context,
/// which is not the case if the context is edited in the asynchronous operation itself.
/// Have the asynchronous function return this type (or a <see cref="ContextMutator"/>) and call <see cref="ApplyContextChanges"/> immediately after the asynchronous operation completes.
/// It is *not* possible to wrap this logic and thereby simplify calling the asynchronous function and then calling <see cref="ApplyContextChanges"/> on the result, because the context editing will not be applied to the caller context.
/// </summary>
/// <remarks>
/// <pitch>The value-returning variant of <see cref="ContextMutator"/>, for asynchronous functions that need to both mutate the caller's context and hand back a result.</pitch>
/// <pledge>Identical to the <see cref="ContextMutator"/> contract — the caller's frame must invoke <see cref="ApplyContextChanges"/> immediately after awaiting — with the function's result returned from that call, so obtaining the result and applying the mutation cannot be accidentally separated.</pledge>
/// <plan>Wraps a single caller-supplied <see cref="Func{T}"/>; no other state.</plan>
/// </remarks>
public sealed class ContextMutator<T>
{
    private readonly Func<T> _applyContextChanges;

    /// <summary>
    /// Constructs the context editor with actions to be executed on return (and possibly dispose).
    /// </summary>
    /// <param name="applyContextChanges">The action to call after returning from the asynchronous function.</param>
    public ContextMutator(Func<T> applyContextChanges)
    {
        _applyContextChanges = applyContextChanges;
    }
    /// <summary>
    /// Calls the context editing action to be executed after returning from the asynchronous function.
    /// </summary>
    /// <returns>The result of the function.</returns>
    public T ApplyContextChanges()
    {
        return _applyContextChanges.Invoke();
    }
}
