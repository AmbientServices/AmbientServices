using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    /// <param name="key">The key, which will be used as a property name in the log entries.</param>
    /// <param name="value">The value, which will be used as the value for the property with the key name in the log entries.  The value should be structured log data (a dictionary or an anonymous object), or a string.</param>
    /// <returns>An object that will remove the key-value pair from the stack when it is disposed.</returns>
    public static IDisposable AddKeyValuePair(string key, object value)
    {
        aStack.Value ??= ImmutableStack<LogContextEntry>.Empty;
        return new LogContextLifetime(aStack, key, value);
    }
    /// <summary>
    /// Gets all the key-value pairs in the current context.
    /// </summary>
    public static IEnumerable<(string Key, object Value)> ContextLogPairs => (aStack.Value ?? ImmutableStack<LogContextEntry>.Empty).Select(e => (e.Key, e.Value));
}

/// <summary>
/// A disposable that keeps a key-value pair in the log context until it is disposed.
/// </summary>
class LogContextLifetime : IDisposable
{
    private readonly ImmutableStack<LogContextEntry> _previousValue;
    private readonly AsyncLocal<ImmutableStack<LogContextEntry>> _asyncLocal;

    public LogContextLifetime(AsyncLocal<ImmutableStack<LogContextEntry>> stack, string key, object value)
    {
        _asyncLocal = stack;
        _previousValue = stack.Value ?? ImmutableStack<LogContextEntry>.Empty;
        ImmutableStack<LogContextEntry> newValue = _previousValue.Push(new(key, value));
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
record struct LogContextEntry(string Key, object Value);
