using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// A "static" class to track context-specific identifiers that should be appended to every log entry, for example a request identifier, a group identifier, a hostname, a user identifier, a session identifier, etc.
/// The class isn't really static, as its members are <see cref="AsyncLocal{T}"/> instances, whose contents vary based on the current async context.
/// </summary>
public static class AmbientLogContext
{
    private static readonly AsyncLocal<ImmutableStack<LogContextEntry>> aStack = new();

    /// <summary>
    /// Reset the async-local stack just in case this context has been recycled and something was left in it.
    /// </summary>
    /// <param name="baselineKey">The baseline key.</param>
    /// <param name="baselineValue">The baseline value.</param>
    public static void Reset(string baselineKey, object baselineValue)
    {
        aStack.Value = ImmutableStack<LogContextEntry>.Empty.Push(new(baselineKey, baselineValue));
    }
    /// <summary>
    /// Puts the key-value pair into the context so that that key-value pair will be added to every log entry called from this async context until the returned object is disposed.
    /// </summary>
    /// <param name="entry">The <see cref="LogContextEntry"/> whose key-value pair will be added to all log entries during the scope.</param>
    /// <returns>An object that will remove the key-value pair from the stack when it is disposed.</returns>
    public static IDisposable AddKeyValuePair(LogContextEntry entry)
    {
        aStack.Value ??= ImmutableStack<LogContextEntry>.Empty;
        return new LogContextLifetime(aStack, entry);
    }
    /// <summary>
    /// Puts the key-value pair into the context so that that key-value pair will be added to every log entry called from this async context until the returned object is disposed.
    /// </summary>
    /// <param name="entries">An enumeration of <see cref="LogContextEntry"/> records containing key-value pairs to add to the log context.</param>
    /// <returns>An object that will remove all of the key-value pairs from the stack when it is disposed.</returns>
    public static IDisposable AddKeyValuePairs(IEnumerable<LogContextEntry> entries)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(entries);
#else
        if (entries is null) throw new ArgumentNullException(nameof(entries));
#endif
        aStack.Value ??= ImmutableStack<LogContextEntry>.Empty;
        return new LogContextLifetime(aStack, entries);
    }
    /// <summary>
    /// Gets all the key-value pairs in the current context.
    /// </summary>
    public static IEnumerable<LogContextEntry> ContextLogPairs => aStack.Value ?? ImmutableStack<LogContextEntry>.Empty;
}

/// <summary>
/// A disposable that keeps a key-value pair in the log context until it is disposed.
/// </summary>
class LogContextLifetime : IDisposable
{
    private readonly ImmutableStack<LogContextEntry> _previousValue;
    private readonly AsyncLocal<ImmutableStack<LogContextEntry>> _asyncLocal;

    public LogContextLifetime(AsyncLocal<ImmutableStack<LogContextEntry>> stack, LogContextEntry entry)
    {
        _asyncLocal = stack;
        _previousValue = stack.Value ?? ImmutableStack<LogContextEntry>.Empty;
        ImmutableStack<LogContextEntry> newValue = _previousValue.Push(entry);
        stack.Value = newValue;
    }

    public LogContextLifetime(AsyncLocal<ImmutableStack<LogContextEntry>> stack, IEnumerable<LogContextEntry> entries)
    {
        _asyncLocal = stack;
        _previousValue = stack.Value ?? ImmutableStack<LogContextEntry>.Empty;
        ImmutableStack<LogContextEntry> newValue = _previousValue;
        foreach (LogContextEntry entry in entries) newValue = newValue.Push(entry);
        stack.Value = newValue;
    }

    public void Dispose()
    {
        _asyncLocal.Value = _previousValue;
    }
}

/// <summary>
/// A record containing a key-value pair for the context to be added to every log entry in the context.
/// </summary>
/// <param name="Key">The name for the context entry.</param>
/// <param name="Value">The value for the context entry.</param>
public record struct LogContextEntry(string Key, object? Value);
