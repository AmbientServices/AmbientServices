using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// An interface that is used to notify a subscriber about stack trace information updates.
/// </summary>
public interface IStackTraceUpdateSink
{
    /// <summary>
    /// Called when the stack trace information is updated.
    /// </summary>
    /// <param name="trace">The new stack trace information.</param>
    void OnStackTraceUpdated(ImmutableStack<string> trace);
}

/// <summary>
/// A "static" class to track the state of the call stack.
/// The class isn't really static, as its members are <see cref="AsyncLocal{T}"/> instances, whose contents vary based on the current async context.
/// </summary>
public static class AmbientStackTrace
{
    private static readonly AsyncLocal<IStackTraceUpdateSink> aNotify = new();
    private static readonly AsyncLocal<ImmutableStack<string>> aStack = new();

    /// <summary>
    /// Reset the async-local stack just in case this context has been recycled and something was left in it.
    /// Note that the baseline string is registered on the new stack, but notification is not sent to <paramref name="subscriber"/> until the first call to <see cref="Trace"/>.
    /// </summary>
    /// <param name="subscriber">A <see cref="IStackTraceUpdateSink"/> that will receive notifications of updates to the stack trace information.</param>
    /// <param name="baseline">The baseline string.</param>
    public static void Reset(IStackTraceUpdateSink subscriber, string baseline)
    {
        aNotify.Value = subscriber;
        aStack.Value = ImmutableStack<string>.Empty.Push(baseline);
    }
    /// <summary>
    /// Puts the caller member name, caller file path, and caller line number on the trace stack for this context, keeping it there until the returned object is disposed.
    /// </summary>
    /// <param name="memberName">The caller's member name (filled in automatically).</param>
    /// <param name="filePath">The caller's file name (filled in automatically).</param>
    /// <param name="lineNumber">The caller's line number (filled in automatically).</param>
    /// <returns>An object that will remove the string from the stack when it is disposed.</returns>
    public static IDisposable Trace([CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        aStack.Value ??= ImmutableStack<string>.Empty;
        return new TraceLifetime(aStack, aNotify, $"at {memberName} in {Path.GetFileName(filePath ?? "")}:line {lineNumber}");
    }
}

class TraceLifetime : IDisposable
{
    private readonly ImmutableStack<string> _previousValue;
    private readonly AsyncLocal<ImmutableStack<string>> _asyncLocal;
    private readonly AsyncLocal<IStackTraceUpdateSink> _notify;

    public TraceLifetime(AsyncLocal<ImmutableStack<string>> stack, AsyncLocal<IStackTraceUpdateSink> notify, string str)
    {
        _asyncLocal = stack;
        _notify = notify;
        _previousValue = stack.Value ?? ImmutableStack<string>.Empty;
        ImmutableStack<string> newValue = _previousValue.Push(str);
        stack.Value = newValue;
        notify.Value?.OnStackTraceUpdated(newValue);
    }

    public void Dispose()
    {
        _asyncLocal.Value = _previousValue;
        _notify.Value?.OnStackTraceUpdated(_previousValue);
    }
}



