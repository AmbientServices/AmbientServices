using System;

namespace AmbientServices;

/// <summary>
/// A class that edits the caller context *after* an asynchronous operation completes so that subsequent calls inherit the applied context,
/// which is not the case if the context is edited in the asynchronous operation itself.
/// Have the asynchronous function return this type (or a <see cref="ContextMutator{T}"/>) and call <see cref="ApplyContextChanges"/> immediately after the asynchronous operation completes.
/// It is *not* possible to wrap this logic and thereby simplify calling the asynchronous function and then calling <see cref="ApplyContextChanges"/> on the result, because the context editing will not be applied to the caller context.
/// </summary>
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
    /// Reverts the context changes applied in the constructor.
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
