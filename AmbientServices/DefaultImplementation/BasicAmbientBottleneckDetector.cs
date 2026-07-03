using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A basic default implementation of <see cref="IAmbientBottleneckDetector"/> that stamps accesses and broadcasts them to registered sinks.
/// </summary>
/// <remarks>
/// <pitch>The zero-configuration, in-process bottleneck detector used unless overridden.  Each access costs one accessor allocation, two timestamp reads, and a sink fan-out, so it is cheap enough to leave on in production.</pitch>
/// <pledge><see cref="IAmbientBottleneckDetector"/></pledge>
/// <plan>
/// Entirely stateless except for the sink set (a <see cref="ConcurrentHashSet{T}"/>, so registration is idempotent and fan-out is lock-free).  <see cref="EnterBottleneck"/> stamps a new <see cref="AmbientBottleneckAccessor"/> with <see cref="AmbientClock.Ticks"/> and synchronously notifies registered sinks that also implement <see cref="IAmbientBottleneckEnterNotificationSink"/>; the accessor's disposal calls back into <c>LeaveBottleneck</c>, which synchronously fans the completed access out to every exit sink.  No accumulation, filtering, or ranking happens here — surveyors do that from the broadcast accesses.
/// </plan>
/// </remarks>
[DefaultAmbientService]
internal class BasicAmbientBottleneckDetector : IAmbientBottleneckDetector
{
    private readonly ConcurrentHashSet<IAmbientBottleneckExitNotificationSink> _notificationSinks = new();

    public BasicAmbientBottleneckDetector()
    {
    }

    public AmbientBottleneckAccessor EnterBottleneck(AmbientBottleneck bottleneck)
    {
        AmbientBottleneckAccessor access = new(this, bottleneck, AmbientClock.Ticks);
        foreach (IAmbientBottleneckExitNotificationSink notificationSink in _notificationSinks)
        {
            IAmbientBottleneckEnterNotificationSink? enterSink = notificationSink as IAmbientBottleneckEnterNotificationSink;
            enterSink?.BottleneckEntered(access);
        }
        return access;
    }
    internal void LeaveBottleneck(AmbientBottleneckAccessor ambientBottleneckAccess)
    {
        foreach (IAmbientBottleneckExitNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.BottleneckExited(ambientBottleneckAccess);
        }
    }

    public bool RegisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}
/// <summary>
/// A class that routes bottleneck exit notifications to surveyors scoped to the call context that created them.
/// </summary>
/// <remarks>
/// <pitch>The per-call-context tap on the bottleneck exit stream: one process-wide registration with the detector, from which each call context gets its own private distributor so call-context surveys never see other contexts' accesses.</pitch>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <plan>Registers itself with the detector once at construction and holds an <see cref="AsyncLocal{T}"/> of per-context <see cref="CallContextAccessNotificationDistributor"/>s, created lazily on first use in each context; exit notifications are simply forwarded to the current context's distributor, with which <see cref="ScopedBottleneckSurveyor"/>s register.  Disposal deregisters from the detector.</plan>
/// </remarks>
internal class CallContextSurveyManager : IAmbientBottleneckExitNotificationSink, IDisposable
{
    private readonly IAmbientBottleneckDetector? _bottleneckDetector;
    private readonly AsyncLocal<CallContextAccessNotificationDistributor> _callContextSurveyors;
    private bool _disposed;

    public CallContextSurveyManager(IAmbientBottleneckDetector? bottleneckDetector)
    {
        _callContextSurveyors = new AsyncLocal<CallContextAccessNotificationDistributor>();
        if (bottleneckDetector != null)
        {
            _bottleneckDetector = bottleneckDetector;
            bottleneckDetector.RegisterAccessNotificationSink(this);
        }
    }

    private CallContextAccessNotificationDistributor CallContextDistributor
    {
        get
        {
            CallContextAccessNotificationDistributor? callContextDistributor = _callContextSurveyors.Value;
            if (callContextDistributor == null)
            {
                _callContextSurveyors.Value = callContextDistributor = new CallContextAccessNotificationDistributor();
            }
            return callContextDistributor;
        }
    }

    void IAmbientBottleneckExitNotificationSink.BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
        CallContextDistributor.BottleneckExited(bottleneckAccessor);
    }

    internal IAmbientBottleneckSurveyor CreateCallContextSurveyor(string? scopeName, Regex? allow, Regex? block)
    {
        ScopedBottleneckSurveyor surveyor = new(scopeName, CallContextDistributor, allow, block);
        return surveyor;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _bottleneckDetector?.DeregisterAccessNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposed = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~CallContextSurveyManager()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A class that fans bottleneck exit notifications out to the sinks registered within one call context.
/// </summary>
/// <remarks>
/// <pitch>The fan-out node for one call context: surveyors register here instead of with the process-wide detector, so their view is limited to their own context's accesses.</pitch>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <plan>A <see cref="ConcurrentHashSet{T}"/> of sinks with synchronous fan-out; idempotent registration.</plan>
/// </remarks>
internal class CallContextAccessNotificationDistributor : IAmbientBottleneckExitNotificationSink
{
    private readonly ConcurrentHashSet<IAmbientBottleneckExitNotificationSink> _notificationSinks = new();

    public CallContextAccessNotificationDistributor()
    {
    }
    public bool RegisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
    public void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
        foreach (IAmbientBottleneckExitNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.BottleneckExited(bottleneckAccessor);
        }
    }
}

/// <summary>
/// A class that accumulates a bottleneck survey for a single scope (a call context, or anything the caller brackets with construction and disposal).
/// </summary>
/// <remarks>
/// <pitch>The survey collector for an explicitly-bracketed scope: construct it to start listening, dispose it to stop, and read the ranked results any time in between or after.</pitch>
/// <pledge><see cref="IAmbientBottleneckSurveyor"/></pledge>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <pledge>Only accesses whose bottleneck identifier passes the allow/block regex filters (block wins over allow; a missing filter allows everything) are accumulated.  The instance is not thread-safe to read and is intended for single-context use.</pledge>
/// <plan>Registers with either a per-call-context distributor or the process-wide detector at construction, and deregisters on disposal.  Accesses accumulate in a plain (unsynchronized) <see cref="Dictionary{TKey,TValue}"/> keyed by bottleneck identifier, combining repeat accesses to the same bottleneck via <see cref="AmbientBottleneckAccessor"/>.<c>Combine</c>; ranking sorts the accumulated accessors by utilization on demand.</plan>
/// </remarks>
internal class ScopedBottleneckSurveyor : IAmbientBottleneckSurveyor, IAmbientBottleneckExitNotificationSink
{
    private readonly CallContextAccessNotificationDistributor? _callContextDistributor;
    private readonly IAmbientBottleneckDetector? _bottleneckDetector;
    private readonly Regex? _allow;
    private readonly Regex? _block;
    private readonly Dictionary<string, AmbientBottleneckAccessor> _bottleneckAccesses;
    private bool _disposedValue;

    public ScopedBottleneckSurveyor(string? scopeName, CallContextAccessNotificationDistributor? callContextDistributor, Regex? allow, Regex? block)
    {
        ScopeName = scopeName ?? "";
        _allow = allow;
        _block = block;
        _bottleneckAccesses = new Dictionary<string, AmbientBottleneckAccessor>();
        if (callContextDistributor != null)
        {
            _callContextDistributor = callContextDistributor;
            callContextDistributor.RegisterAccessNotificationSink(this);
        }
    }

    public ScopedBottleneckSurveyor(string? scopeName, IAmbientBottleneckDetector? bottleneckDetector, Regex? allow, Regex? block)
    {
        ScopeName = scopeName ?? "";
        _allow = allow;
        _block = block;
        _bottleneckAccesses = new Dictionary<string, AmbientBottleneckAccessor>();
        if (bottleneckDetector != null)
        {
            _bottleneckDetector = bottleneckDetector;
            bottleneckDetector.RegisterAccessNotificationSink(this);
        }
    }

    public string ScopeName { get; }

    public AmbientBottleneckAccessor? MostUtilizedBottleneck => _bottleneckAccesses.Values.Max();

    public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
    {
        return _bottleneckAccesses.Values.OrderBy(m => m.Utilization).Take(count);
    }

    public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        string bottleneckId = bottleneckAccessor.Bottleneck.Id;
        // is this bottleneck being surveyed?
        bool blocked = _block?.IsMatch(bottleneckId) ?? false;
        bool allowed = !blocked && (_allow?.IsMatch(bottleneckId) ?? true);
        if (allowed)
        {
            AmbientBottleneckAccessor? metric;
            if (_bottleneckAccesses.TryGetValue(bottleneckId, out metric))
            {
                _bottleneckAccesses[bottleneckId] = metric.Combine(bottleneckAccessor);
            }
            else
            {
                _bottleneckAccesses.Add(bottleneckId, bottleneckAccessor);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _bottleneckDetector?.DeregisterAccessNotificationSink(this);
                _callContextDistributor?.DeregisterAccessNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ScopeBottleneckAnalyzer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// A class that produces a bottleneck survey for each successive time window, delivering each completed window to a callback.
/// </summary>
/// <remarks>
/// <pitch>Periodic bottleneck reporting: every window of the configured size yields a finished <see cref="IAmbientBottleneckSurvey"/> covering exactly that window, including the in-window portion of accesses that span window boundaries.</pitch>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <pledge><see cref="IAmbientBottleneckEnterNotificationSink"/></pledge>
/// <plan>An <see cref="AmbientEventTimer"/> rotates the current <see cref="TimeWindowBottleneckSurvey"/> atomically (via <see cref="Interlocked.Exchange{T}(ref T, T)"/>) at each window boundary, carries still-open accesses forward into the new window, and hands the closed window to the completion delegate.  It listens for both enter and exit notifications (registered with the detector as a single sink) so that long-running accesses are visible to the window in which they started rather than only the one in which they end.  Disposal stops the timer and deregisters.</plan>
/// </remarks>
internal class TimeWindowSurveyManager : IAmbientBottleneckExitNotificationSink, IAmbientBottleneckEnterNotificationSink, IDisposable
{
    private readonly IAmbientBottleneckDetector? _bottleneckDetector;
    private readonly AmbientEventTimer _timer;
    private TimeWindowBottleneckSurvey _currentWindowSurvey;   // interlocked
    private bool _disposedValue;

    public TimeWindowSurveyManager(TimeSpan windowSize, Func<IAmbientBottleneckSurvey, Task> onWindowComplete, IAmbientBottleneckDetector? bottleneckDetector, Regex? allow, Regex? block)
    {
        TimeWindowBottleneckSurvey initialSurvey = new(allow, block, AmbientClock.Ticks, windowSize);
        _currentWindowSurvey = initialSurvey;
        void rotateTimeWindow(object? s, System.Timers.ElapsedEventArgs e)
        {
            TimeWindowBottleneckSurvey survey = new(allow, block, AmbientClock.Ticks, windowSize);
            TimeWindowBottleneckSurvey oldAnalyzer = Interlocked.Exchange(ref _currentWindowSurvey, survey);
            // copy all the accesses still in progress
            survey.SwitchAnalyzer(oldAnalyzer);
            onWindowComplete(oldAnalyzer);
        }
        AmbientEventTimer timer = new();
        timer.AutoReset = true;
        timer.Elapsed += rotateTimeWindow;
        timer.Interval = windowSize.TotalMilliseconds;
        timer.Enabled = true;
        _timer = timer;
        if (bottleneckDetector != null)
        {
            _bottleneckDetector = bottleneckDetector;
            bottleneckDetector.RegisterAccessNotificationSink(this);
        }
    }
    public void BottleneckEntered(AmbientBottleneckAccessor? bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        _currentWindowSurvey.BottleneckEntered(bottleneckAccessor);
    }

    public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        _currentWindowSurvey.BottleneckExited(bottleneckAccessor);
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _timer.Dispose();
                _bottleneckDetector?.DeregisterAccessNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~TimeWindowSurveyor()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// A class that accumulates the bottleneck survey for one time window.
/// </summary>
/// <remarks>
/// <pitch>One window's worth of bottleneck accounting, charging each window only the usage that occurred within it even when accesses span multiple windows.</pitch>
/// <pledge><see cref="IAmbientBottleneckSurvey"/></pledge>
/// <pledge>Only accesses whose bottleneck identifier passes the allow/block regex filters are accumulated, and reported usage for a boundary-spanning access is clipped to this window's time range.</pledge>
/// <plan>
/// Accesses accumulate in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by bottleneck identifier, combining repeats via <see cref="AmbientBottleneckAccessor"/>.<c>Combine</c>.  At rotation, <c>SwitchAnalyzer</c> walks the old window's entries and uses <c>Split</c> to divide each still-open access at the boundary: the closed half (clipped to the old window) replaces the old window's entry, the open half seeds the new window, and a per-bottleneck snapshot of access count and limit usage taken at the boundary (<c>_startAccessCountAndLimitUsage</c>) ensures manually-reported usage already attributed to earlier windows is not charged again.  The window's scope name embeds the UTC window identifier and size from <see cref="WindowScope"/>.
/// </plan>
/// </remarks>
internal class TimeWindowBottleneckSurvey : IAmbientBottleneckExitNotificationSink, IAmbientBottleneckSurvey
{
    private readonly Regex? _allow;
    private readonly Regex? _block;
    private readonly long _windowStartStopwatchTicks;
    private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
    private readonly ConcurrentDictionary<string, (long, double)> _startAccessCountAndLimitUsage;

    public TimeWindowBottleneckSurvey(Regex? allow, Regex? block, long stopwatchTicks, TimeSpan windowSize)
    {
        string windowName = WindowScope.WindowId(AmbientClock.UtcNow, windowSize);
        ScopeName = "TimeWindow " + windowName + "(" + WindowScope.WindowSize(windowSize) + ")";
        _allow = allow;
        _block = block;
        _windowStartStopwatchTicks = stopwatchTicks;
        _metrics = new ConcurrentDictionary<string, AmbientBottleneckAccessor>();
        _startAccessCountAndLimitUsage = new ConcurrentDictionary<string, (long, double)>();
    }

    public string ScopeName { get; }

    public AmbientBottleneckAccessor? MostUtilizedBottleneck => _metrics.Values.Max();

    public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
    {
        return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
    }

    internal void SwitchAnalyzer(TimeWindowBottleneckSurvey oldAnalyzer)
    {
        if (oldAnalyzer != null)
        {
            long nowStopwatchTicks = AmbientClock.Ticks;
            // enumerate the accessors that are still open
            foreach (AmbientBottleneckAccessor access in oldAnalyzer._metrics.Values)
            {
                string bottleneckId = access.Bottleneck.Id;
                (long, double) accessCountAndLimitUsage;
                if (!_startAccessCountAndLimitUsage.TryGetValue(bottleneckId, out accessCountAndLimitUsage)) accessCountAndLimitUsage = (0, 0);
                (AmbientBottleneckAccessor, AmbientBottleneckAccessor?) records = access.Split(_windowStartStopwatchTicks, nowStopwatchTicks, accessCountAndLimitUsage.Item1, accessCountAndLimitUsage.Item2);
                // replace the entry in the old dictionary
                oldAnalyzer._metrics[bottleneckId] = records.Item1;
                // add the new half into the new window.  note that when the original instance (which we don't own) finishes, it  will behave like a new access when combined with the second half we're putting in now
                if (records.Item2 is not null) _metrics.AddOrUpdate(bottleneckId, records.Item2, (s, m) => m.Combine(records.Item2));   // note that covering the 'update' case here would require a matching entry in the new window's metrics to have been put in on another thread during the execution of this function
                // update the starting access count and the limit usage for the new window
                _startAccessCountAndLimitUsage[bottleneckId] = (access.AccessCount, access.LimitUsed);
            }
        }
    }

    public void BottleneckEntered(AmbientBottleneckAccessor bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        // is this bottleneck being surveyed?
        string bottleneckId = bottleneckAccessor.Bottleneck.Id;
        bool blocked = _block?.IsMatch(bottleneckId) ?? false;
        bool allowed = !blocked && (_allow?.IsMatch(bottleneckId) ?? true);
        if (allowed)
        {
            _metrics.AddOrUpdate(bottleneckId, bottleneckAccessor, (s, m) => m.Combine(bottleneckAccessor));
        }
    }

    public void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        // is this bottleneck being surveyed?
        string bottleneckId = bottleneckAccessor.Bottleneck.Id;
        bool blocked = _block?.IsMatch(bottleneckId) ?? false;
        bool allowed = !blocked && (_allow?.IsMatch(bottleneckId) ?? true);
        if (allowed)
        {
            _metrics.AddOrUpdate(bottleneckId, bottleneckAccessor, (s, m) => m.Combine(bottleneckAccessor));
        }
    }
}
/// <summary>
/// A class that accumulates a bottleneck survey for the whole process, across all threads and call contexts.
/// </summary>
/// <remarks>
/// <pitch>The everything-since-construction survey: useful for short-lived processes measured start to finish; for long-lived processes a time-window surveyor is usually the better fit.</pitch>
/// <pledge><see cref="IAmbientBottleneckSurveyor"/></pledge>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <pledge>Only accesses whose bottleneck identifier passes the allow/block regex filters are accumulated.  Accumulation is thread-safe (accesses arrive from all contexts), but reading the results is not.</pledge>
/// <plan>Registers directly with the detector at construction (deregistering on disposal) and accumulates into a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by bottleneck identifier, combining repeats via <see cref="AmbientBottleneckAccessor"/>.<c>Combine</c>; the default scope name embeds the process name and id.</plan>
/// </remarks>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
internal class ProcessBottleneckSurveyor : IAmbientBottleneckExitNotificationSink, IAmbientBottleneckSurveyor
{
    private readonly IAmbientBottleneckDetector? _bottleneckDetector;
    private readonly Regex? _allow;
    private readonly Regex? _block;
    private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
    private bool _disposedValue;

    public ProcessBottleneckSurveyor(string? processScopeName, IAmbientBottleneckDetector? bottleneckDetector, Regex? allow, Regex? block)
    {
        System.Diagnostics.Process process = Process.GetCurrentProcess();
        ScopeName = (string.IsNullOrEmpty(processScopeName) ? FormattableString.Invariant($"Process {process.ProcessName} ({process.Id})") : processScopeName)!;
        _allow = allow;
        _block = block;
        _metrics = new ConcurrentDictionary<string, AmbientBottleneckAccessor>();
        if (bottleneckDetector != null)
        {
            _bottleneckDetector = bottleneckDetector;
            bottleneckDetector.RegisterAccessNotificationSink(this);
        }
    }

    public string ScopeName { get; }

    public AmbientBottleneckAccessor? MostUtilizedBottleneck => _metrics.Values.Max();

    public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
    {
        return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
    }

    public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        string bottleneckId = bottleneckAccessor.Bottleneck.Id;
        // is this bottleneck being surveyed?
        bool blocked = _block?.IsMatch(bottleneckId) ?? false;
        bool allowed = !blocked && (_allow?.IsMatch(bottleneckId) ?? true);
        if (allowed)
        {
            _metrics.AddOrUpdate(bottleneckId, bottleneckAccessor, (s, m) => m.Combine(bottleneckAccessor));
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _bottleneckDetector?.DeregisterAccessNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ProcessBottleneckSurveyor()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A class that routes bottleneck exit notifications to surveyors scoped to the thread that created them.
/// </summary>
/// <remarks>
/// <pitch>The per-thread analog of <see cref="CallContextSurveyManager"/>: one process-wide registration with the detector, from which each thread gets its own private distributor.</pitch>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <plan>Registers itself with the detector once at construction and holds a <see cref="ThreadLocal{T}"/> of per-thread <see cref="ThreadAccessDistributor"/>s created lazily; exit notifications are forwarded to the notifying thread's distributor, so an access is attributed to whichever thread disposes the accessor.  Disposal deregisters and drops the thread-local storage.</plan>
/// </remarks>
internal class ThreadSurveyManager : IAmbientBottleneckExitNotificationSink, IDisposable
{
    private readonly IAmbientBottleneckDetector? _bottleneckDetector;
    private ThreadLocal<ThreadAccessDistributor>? _threadDistributors;
    private bool disposedValue;

    public ThreadSurveyManager(IAmbientBottleneckDetector? bottleneckDetector)
    {
        _threadDistributors = new ThreadLocal<ThreadAccessDistributor>();
        if (bottleneckDetector != null)
        {
            _bottleneckDetector = bottleneckDetector;
            bottleneckDetector.RegisterAccessNotificationSink(this);
        }
    }

    private ThreadAccessDistributor ThreadDistributor
    {
        get
        {
            ThreadAccessDistributor? threadDistributor = _threadDistributors?.Value;
            if (threadDistributor == null)
            {
                threadDistributor = new ThreadAccessDistributor();
                if (_threadDistributors != null) _threadDistributors.Value = threadDistributor;
            }
            return threadDistributor;
        }
    }
    void IAmbientBottleneckExitNotificationSink.BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
        ThreadDistributor.BottleneckExited(bottleneckAccessor);
    }

    internal ThreadBottleneckSurveyor CreateThreadSurveyor(string? scopeName, Regex? allow, Regex? block)
    {
        ThreadBottleneckSurveyor surveyor = new(scopeName, ThreadDistributor, allow, block);
        return surveyor;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _bottleneckDetector?.DeregisterAccessNotificationSink(this);
                _threadDistributors?.Dispose();
                _threadDistributors = null;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ThreadSurveyManager()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// A class that fans bottleneck exit notifications out to the sinks registered by one thread.
/// </summary>
/// <remarks>
/// <pitch>The fan-out node for one thread: surveyors register here instead of with the process-wide detector, so their view is limited to their own thread's accesses.</pitch>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <plan>A <see cref="ConcurrentHashSet{T}"/> of sinks with synchronous fan-out; idempotent registration.</plan>
/// </remarks>
internal class ThreadAccessDistributor : IAmbientBottleneckExitNotificationSink
{
    private readonly ConcurrentHashSet<IAmbientBottleneckExitNotificationSink> _notificationSinks = new();

    public ThreadAccessDistributor()
    {
    }
    public void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
        foreach (IAmbientBottleneckExitNotificationSink notificationSink in _notificationSinks)
        {
            notificationSink.BottleneckExited(bottleneckAccessor);
        }
    }
    public bool RegisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Add(sink);
    }
    public bool DeregisterAccessNotificationSink(IAmbientBottleneckExitNotificationSink sink)
    {
        return _notificationSinks.Remove(sink);
    }
}
/// <summary>
/// A class that accumulates a bottleneck survey for a single thread.
/// </summary>
/// <remarks>
/// <pitch>The survey collector for one thread's accesses, bracketed by construction and disposal; the default scope name identifies the thread.</pitch>
/// <pledge><see cref="IAmbientBottleneckSurveyor"/></pledge>
/// <pledge><see cref="IAmbientBottleneckExitNotificationSink"/></pledge>
/// <pledge>Only accesses whose bottleneck identifier passes the allow/block regex filters are accumulated.  Because delivery is confined to one thread, accumulation uses no synchronization and the instance must not be shared across threads.</pledge>
/// <plan>Registers with its thread's <see cref="ThreadAccessDistributor"/> at construction (deregistering on disposal) and accumulates into a plain <see cref="Dictionary{TKey,TValue}"/> keyed by bottleneck identifier, combining repeats via <see cref="AmbientBottleneckAccessor"/>.<c>Combine</c>.</plan>
/// </remarks>
internal class ThreadBottleneckSurveyor : IAmbientBottleneckSurveyor, IAmbientBottleneckExitNotificationSink
{
    private readonly ThreadAccessDistributor _threadDistributor;
    private readonly Regex? _allow;
    private readonly Regex? _block;
    private readonly Dictionary<string, AmbientBottleneckAccessor> _metrics;
    private bool _disposedValue;

    public ThreadBottleneckSurveyor(string? scopeName, ThreadAccessDistributor threadDistributor, Regex? allow, Regex? block)
    {
        ScopeName = (string.IsNullOrEmpty(scopeName) ? (string.IsNullOrEmpty(Thread.CurrentThread.Name) ? $"Thread {Environment.CurrentManagedThreadId}" : Thread.CurrentThread.Name) : scopeName)!;
        _allow = allow;
        _block = block;
        _metrics = new Dictionary<string, AmbientBottleneckAccessor>();
        _threadDistributor = threadDistributor;
        threadDistributor.RegisterAccessNotificationSink(this);
    }

    public string ScopeName { get; }

    public AmbientBottleneckAccessor? MostUtilizedBottleneck => _metrics.Values.Max();

    public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
    {
        return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
    }

    public void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(bottleneckAccessor);
#else
        if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
#endif
        string bottleneckId = bottleneckAccessor.Bottleneck.Id;
        // is this bottleneck being surveyed?
        bool blocked = _block?.IsMatch(bottleneckId) ?? false;
        bool allowed = !blocked && (_allow?.IsMatch(bottleneckId) ?? true);
        if (allowed)
        {
            AmbientBottleneckAccessor? metric;
            if (_metrics.TryGetValue(bottleneckId, out metric))
            {
                _metrics[bottleneckId] = metric.Combine(bottleneckAccessor);
            }
            else
            {
                _metrics.Add(bottleneckId, bottleneckAccessor);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _threadDistributor.DeregisterAccessNotificationSink(this);
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ThreadBottleneckAnalyzer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
