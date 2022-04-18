using AmbientServices.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace AmbientServices
{
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

    internal class ScopedBottleneckSurveyor : IAmbientBottleneckSurveyor, IAmbientBottleneckExitNotificationSink
    {
        private readonly CallContextAccessNotificationDistributor? _callContextDistributor;
        private readonly IAmbientBottleneckDetector? _bottleneckDetector;
        private readonly string _scopeName;
        private readonly Regex? _allow;
        private readonly Regex? _block;
        private readonly Dictionary<string, AmbientBottleneckAccessor> _bottleneckAccesses;
        private bool _disposedValue;

        public ScopedBottleneckSurveyor(string? scopeName, CallContextAccessNotificationDistributor? callContextDistributor, Regex? allow, Regex? block)
        {
            _scopeName = scopeName ?? "";
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
            _scopeName = scopeName ?? "";
            _allow = allow;
            _block = block;
            _bottleneckAccesses = new Dictionary<string, AmbientBottleneckAccessor>();
            if (bottleneckDetector != null)
            {
                _bottleneckDetector = bottleneckDetector;
                bottleneckDetector.RegisterAccessNotificationSink(this);
            }
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor? MostUtilizedBottleneck => _bottleneckAccesses.Values.Max();

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _bottleneckAccesses.Values.OrderBy(m => m.Utilization).Take(count);
        }

        public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
        {
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
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
            void rotateTimeWindow(object s, System.Timers.ElapsedEventArgs e)
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
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
            _currentWindowSurvey.BottleneckEntered(bottleneckAccessor);
        }

        public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
        {
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
            _currentWindowSurvey.BottleneckExited( bottleneckAccessor);
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
    internal class TimeWindowBottleneckSurvey : IAmbientBottleneckExitNotificationSink, IAmbientBottleneckSurvey
    {
        private readonly string _scopeName;
        private readonly Regex? _allow;
        private readonly Regex? _block;
        private readonly long _windowStartStopwatchTicks;
        private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
        private readonly ConcurrentDictionary<string, (long, double)> _startAccessCountAndLimitUsage;

        public TimeWindowBottleneckSurvey(Regex? allow, Regex? block, long stopwatchTicks, TimeSpan windowSize)
        {
            string windowName = WindowScope.WindowId(AmbientClock.UtcNow, windowSize);
            _scopeName = "TimeWindow " + windowName + "(" + WindowScope.WindowSize(windowSize) + ")";
            _allow = allow;
            _block = block;
            _windowStartStopwatchTicks = stopwatchTicks;
            _metrics = new ConcurrentDictionary<string, AmbientBottleneckAccessor>();
            _startAccessCountAndLimitUsage = new ConcurrentDictionary<string, (long, double)>();
        }

        public string ScopeName => _scopeName;

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
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
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
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
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
#if NET5_0_OR_GREATER
    [UnsupportedOSPlatform("browser")]
#endif
    internal class ProcessBottleneckSurveyor : IAmbientBottleneckExitNotificationSink, IAmbientBottleneckSurveyor
    {
        private readonly IAmbientBottleneckDetector? _bottleneckDetector;
        private readonly string _scopeName;
        private readonly Regex? _allow;
        private readonly Regex? _block;
        private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
        private bool _disposedValue;

        public ProcessBottleneckSurveyor(string? processScopeName, IAmbientBottleneckDetector? bottleneckDetector, Regex? allow, Regex? block)
        {
            System.Diagnostics.Process process = Process.GetCurrentProcess();
            _scopeName = (string.IsNullOrEmpty(processScopeName) ? FormattableString.Invariant($"Process {process.ProcessName} ({process.Id})") : processScopeName)!;
            _allow = allow;
            _block = block;
            _metrics = new ConcurrentDictionary<string, AmbientBottleneckAccessor>();
            if (bottleneckDetector != null)
            {
                _bottleneckDetector = bottleneckDetector;
                bottleneckDetector.RegisterAccessNotificationSink(this);
            }
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor? MostUtilizedBottleneck => _metrics.Values.Max();

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
        }

        public void BottleneckExited(AmbientBottleneckAccessor? bottleneckAccessor)
        {
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
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
    internal class ThreadBottleneckSurveyor : IAmbientBottleneckSurveyor, IAmbientBottleneckExitNotificationSink
    {
        private readonly ThreadAccessDistributor _threadDistributor;
        private readonly string _scopeName;
        private readonly Regex? _allow;
        private readonly Regex? _block;
        private readonly Dictionary<string, AmbientBottleneckAccessor> _metrics;
        private bool _disposedValue;

        public ThreadBottleneckSurveyor(string? scopeName, ThreadAccessDistributor threadDistributor, Regex? allow, Regex? block)
        {
            _scopeName = (string.IsNullOrEmpty(scopeName) ? (string.IsNullOrEmpty(Thread.CurrentThread.Name) ? $"Thread {Environment.CurrentManagedThreadId}" : Thread.CurrentThread.Name) : scopeName)!;
            _allow = allow;
            _block = block;
            _metrics = new Dictionary<string, AmbientBottleneckAccessor>();
            _threadDistributor = threadDistributor;
            threadDistributor.RegisterAccessNotificationSink(this);
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor? MostUtilizedBottleneck => _metrics.Values.Max();

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
        }

        public void BottleneckExited(AmbientBottleneckAccessor bottleneckAccessor)
        {
            if (bottleneckAccessor is null) throw new ArgumentNullException(nameof(bottleneckAccessor));
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
}
