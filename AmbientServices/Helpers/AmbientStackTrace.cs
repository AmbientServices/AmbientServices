using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// An interface that is used to notify a subscriber about stack trace information updates.
/// </summary>
/// <remarks>
/// <pitch>The push side of ambient stack tracking: implement this to observe the logical stack maintained by <see cref="AmbientStackTrace"/> as frames are pushed and popped.</pitch>
/// <pledge><see cref="OnStackTraceUpdated"/> is called after each change to the current call context's trace stack, receiving the complete new stack as an immutable snapshot that may be retained or read at any time without synchronization.  Calls arrive on whatever thread performs the trace, and only for changes within contexts where the sink was registered via <see cref="AmbientStackTrace.Reset"/>.</pledge>
/// </remarks>
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
/// <remarks>
/// <pitch>A <em>logical</em> stack trace for async code: physical stack traces dissolve into state-machine noise across <c>await</c>s, so this lets code annotate its own meaningful frames (via <c>using</c>-scoped <see cref="Trace"/> calls) and gives diagnostics a readable where-are-we answer per call context.  Coverage is opt-in — only explicitly traced frames appear.</pitch>
/// <pledge>
/// Each call context carries its own stack; <see cref="Trace"/> pushes a caller-identifying frame and the returned object pops back to the prior stack on dispose, so scopes must be disposed in the context that created them and naturally nest.
/// <see cref="Reset"/> replaces the context's stack with a single baseline frame and registers the sink that will be notified of subsequent pushes and pops (the reset itself is not notified) — call it at the start of reused contexts (thread pool, test frameworks) to clear leftovers.
/// Snapshots handed to the sink are immutable and safe to retain.
/// </pledge>
/// <plan>Two <see cref="AsyncLocal{T}"/> slots — an <see cref="ImmutableStack{T}"/> of frame strings and the registered <see cref="IStackTraceUpdateSink"/> — with frames built from <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>-family data at compile time, so pushing costs a small string format and an immutable-stack push rather than a stack walk.  The pop restores the previous immutable snapshot captured at push time, which also self-heals if an intervening frame leaks undisposed.</plan>
/// </remarks>
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



