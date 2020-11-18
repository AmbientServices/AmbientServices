using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Performance
{
    [DefaultAmbientServiceProvider]
    class BasicAmbientBottleneckDetector : IAmbientBottleneckDetector
    {
        private readonly long BaselineStopwatchTimestamp = AmbientClock.Ticks;
        private readonly long BaselineDateTimeTicks = AmbientClock.UtcNow.Ticks;

        public BasicAmbientBottleneckDetector()
        {
        }

        public AmbientBottleneckAccessor EnterBottleneck(AmbientBottleneck bottleneck)
        {
            AmbientBottleneckAccessor access = new AmbientBottleneckAccessor(this, bottleneck, AmbientClock.Ticks);
            BottleneckEntered?.Invoke(this, access);
            return access;
        }
        internal void LeaveBottleneck(AmbientBottleneckAccessor ambientBottleneckAccess)
        {
            BottleneckExited?.Invoke(this, ambientBottleneckAccess);
        }

        public event EventHandler<AmbientBottleneckAccessor> BottleneckEntered;
        public event EventHandler<AmbientBottleneckAccessor> BottleneckExited;

        internal long StopwatchTimestampToDateTime(long stopwatchTimestamp)
        {
            long stopwatchTicksAgo = BaselineStopwatchTimestamp - stopwatchTimestamp;
            long dateTimeTicksAgo = stopwatchTicksAgo * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
            return BaselineDateTimeTicks - dateTimeTicksAgo;
        }

        internal long DateTimeToStopwatchTimestamp(long dateTimeTicks)
        {
            long dateTimeTicksAgo = BaselineDateTimeTicks - dateTimeTicks;
            long stopwatchTicksAgo = dateTimeTicksAgo * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
            return BaselineStopwatchTimestamp - stopwatchTicksAgo;
        }
    }
    class CallContextSurveyManager : IDisposable
    {
        private readonly IAmbientBottleneckDetector _bottleneckDetector;
        private readonly AsyncLocal<CallContextAccessNotificationDistributor> _callContextSurveyors;
        private bool disposedValue;

        public CallContextSurveyManager(IAmbientBottleneckDetector bottleneckDetector)
        {
            _callContextSurveyors = new AsyncLocal<CallContextAccessNotificationDistributor>();
            if (bottleneckDetector != null)
            {
                bottleneckDetector.BottleneckExited += BottleneckDetector_BottleneckExited;
                _bottleneckDetector = bottleneckDetector;
            }
        }

        private CallContextAccessNotificationDistributor CallContextDistributor
        {
            get
            {
                CallContextAccessNotificationDistributor callContextDistributor = _callContextSurveyors.Value;
                if (callContextDistributor == null)
                {
                    _callContextSurveyors.Value = callContextDistributor = new CallContextAccessNotificationDistributor();
                }
                return callContextDistributor;
            }
        }

        private void BottleneckDetector_BottleneckExited(object sender, AmbientBottleneckAccessor e)
        {
            CallContextDistributor.OnBottleneckAccessEnded(sender, e);
        }

        internal IAmbientBottleneckSurveyor CreateCallContextSurveyor(string scopeName, Regex allow, Regex block)
        {
            ScopeBottleneckSurveyor surveyor = new ScopeBottleneckSurveyor(scopeName, CallContextDistributor, allow, block);
            return surveyor;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_bottleneckDetector != null)
                    {
                        _bottleneckDetector.BottleneckExited -= BottleneckDetector_BottleneckExited;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
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
    class CallContextAccessNotificationDistributor
    {
        public event EventHandler<AmbientBottleneckAccessor> BottleneckExited;

        public CallContextAccessNotificationDistributor()
        {
        }
        public void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor access)
        {
            BottleneckExited?.Invoke(sender, access);
        }
    }
    class ScopeBottleneckSurveyor : IAmbientBottleneckSurveyor
    {
        private readonly CallContextAccessNotificationDistributor _callContextDistributor;
        private readonly IAmbientBottleneckDetector _bottleneckDetector;
        private readonly string _scopeName;
        private readonly Regex _allow;
        private readonly Regex _block;
        private readonly Dictionary<string, AmbientBottleneckAccessor> _bottleneckAccesses;
        private bool _disposedValue;

        public ScopeBottleneckSurveyor(string scopeName, CallContextAccessNotificationDistributor callContextDistributor, Regex allow, Regex block)
        {
            _scopeName = scopeName;
            _allow = allow;
            _block = block;
            _bottleneckAccesses = new Dictionary<string, AmbientBottleneckAccessor>();
            if (callContextDistributor != null)
            {
                callContextDistributor.BottleneckExited += OnBottleneckAccessEnded;
                _callContextDistributor = callContextDistributor;
            }
        }

        public ScopeBottleneckSurveyor(string scopeName, IAmbientBottleneckDetector bottleneckDetector, Regex allow, Regex block)
        {
            _scopeName = scopeName;
            _allow = allow;
            _block = block;
            _bottleneckAccesses = new Dictionary<string, AmbientBottleneckAccessor>();
            if (bottleneckDetector != null)
            {
                bottleneckDetector.BottleneckExited += OnBottleneckAccessEnded;
                _bottleneckDetector = bottleneckDetector;
            }
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor MostUtilizedBottleneck
        {
            get
            {
                return _bottleneckAccesses.Values.Max();
            }
        }

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _bottleneckAccesses.Values.OrderBy(m => m.Utilization).Take(count);
        }

        internal void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor evnt)
        {
            if (evnt == null) throw new ArgumentNullException(nameof(evnt));
            string bottleneckId = evnt.Bottleneck.Id;
            // is this bottleneck being surveyed?
            bool blocked = _block?.IsMatch(bottleneckId) ?? false;
            bool allowed = blocked ? false : _allow?.IsMatch(bottleneckId) ?? true;
            if (allowed)
            {
                AmbientBottleneckAccessor metric;
                if (_bottleneckAccesses.TryGetValue(bottleneckId, out metric))
                {
                    _bottleneckAccesses[bottleneckId] = metric.Combine(evnt);
                }
                else
                {
                    _bottleneckAccesses.Add(bottleneckId, evnt);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_bottleneckDetector != null)
                    {
                        _bottleneckDetector.BottleneckExited -= OnBottleneckAccessEnded;
                    }
                    if (_callContextDistributor != null)
                    {
                        _callContextDistributor.BottleneckExited -= OnBottleneckAccessEnded;
                    }
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
    internal class TimeWindowSurveyManager : IDisposable
    {
        private readonly IAmbientBottleneckDetector _bottleneckDetector;
        private readonly AmbientEventTimer _timer;
        private TimeWindowBottleneckSurvey _currentWindowSurvey;   // interlocked
        private bool _disposedValue;

        public TimeWindowSurveyManager(TimeSpan windowSize, Func<IAmbientBottleneckSurvey, Task> onWindowComplete, IAmbientBottleneckDetector bottleneckDetector, Regex allow, Regex block)
        {
            System.Timers.ElapsedEventHandler rotateTimeWindow = (s, e) =>
            {
                TimeWindowBottleneckSurvey survey = new TimeWindowBottleneckSurvey(allow, block, AmbientClock.Ticks, windowSize);
                TimeWindowBottleneckSurvey oldAnalyzer = System.Threading.Interlocked.Exchange(ref _currentWindowSurvey, survey);
                if (oldAnalyzer != null)
                {
                    // copy all the accesses still in progress
                    survey.SwitchAnalyzer(oldAnalyzer);
                    onWindowComplete(oldAnalyzer);
                }
            };
            rotateTimeWindow.Invoke(null, null);
            AmbientEventTimer timer = new AmbientEventTimer();
            timer.AutoReset = true;
            timer.Elapsed += rotateTimeWindow;
            timer.Interval = windowSize.TotalMilliseconds;
            timer.Enabled = true;
            _timer = timer;
            if (bottleneckDetector != null)
            {
                bottleneckDetector.BottleneckEntered += OnBottleneckAccessBegun;
                bottleneckDetector.BottleneckExited += OnBottleneckAccessEnded;
                _bottleneckDetector = bottleneckDetector;
            }
        }
        internal void OnBottleneckAccessBegun(object sender, AmbientBottleneckAccessor access)
        {
            if (access == null) throw new ArgumentNullException(nameof(access));
            string bottleneckId = access.Bottleneck.Id;
            _currentWindowSurvey.OnBottleneckAccessBegun(bottleneckId, access);
        }
        internal void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor access)
        {
            if (access == null) throw new ArgumentNullException(nameof(access));
            string bottleneckId = access.Bottleneck.Id;
            _currentWindowSurvey.OnBottleneckAccessEnded(bottleneckId, access);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _timer.Dispose();
                    if (_bottleneckDetector != null)
                    {
                        _bottleneckDetector.BottleneckEntered -= OnBottleneckAccessBegun;
                        _bottleneckDetector.BottleneckExited -= OnBottleneckAccessEnded;
                    }
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
    internal class TimeWindowBottleneckSurvey : IAmbientBottleneckSurvey
    {
        private readonly string _scopeName;
        private readonly Regex _allow;
        private readonly Regex _block;
        private readonly long _windowStartStopwatchTicks;
        private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
        private readonly ConcurrentDictionary<string, (long, double)> _startAccessCountAndLimitUsage;

        public TimeWindowBottleneckSurvey(Regex allow, Regex block, long stopwatchTicks, TimeSpan windowSize)
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

        public AmbientBottleneckAccessor MostUtilizedBottleneck
        {
            get
            {
                return _metrics.Values.Max();
            }
        }

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
                    (AmbientBottleneckAccessor, AmbientBottleneckAccessor) records = access.Split(_windowStartStopwatchTicks, nowStopwatchTicks, accessCountAndLimitUsage.Item1, accessCountAndLimitUsage.Item2);
                    // replace the entry in the old dictionary
                    oldAnalyzer._metrics[bottleneckId] = records.Item1;
                    // add the new half into the new window.  note that when the original instance (which we don't own) finishes, it  will behave like a new access when combined with the second half we're putting in now
                    if (records.Item2 != null) _metrics.AddOrUpdate(bottleneckId, records.Item2, (s, m) => m.Combine(records.Item2));   // note that covering the 'update' case here would require a matching entry in the new window's metrics to have been put in on another thread during the execution of this function
                    // update the starting access count and the limit usage for the new window
                    _startAccessCountAndLimitUsage[bottleneckId] = (access.AccessCount, access.LimitUsed);
                }
            }
        }

        public void OnBottleneckAccessBegun(string bottleneckId, AmbientBottleneckAccessor access)
        {
            if (access == null) throw new ArgumentNullException(nameof(access));
            System.Diagnostics.Debug.Assert(bottleneckId == access.Bottleneck.Id);
            // is this bottleneck being surveyed?
            bool blocked = _block?.IsMatch(bottleneckId) ?? false;
            bool allowed = blocked ? false : _allow?.IsMatch(bottleneckId) ?? true;
            if (allowed)
            {
                _metrics.AddOrUpdate(bottleneckId, access, (s, m) => m.Combine(access));
            }
        }
        public void OnBottleneckAccessEnded(string bottleneckId, AmbientBottleneckAccessor access)
        {
            if (access == null) throw new ArgumentNullException(nameof(access));
            System.Diagnostics.Debug.Assert(bottleneckId == access.Bottleneck.Id);
            // is this bottleneck being surveyed?
            bool blocked = _block?.IsMatch(bottleneckId) ?? false;
            bool allowed = blocked ? false : _allow?.IsMatch(bottleneckId) ?? true;
            if (allowed)
            {
                _metrics.AddOrUpdate(bottleneckId, access, (s, m) => m.Combine(access));
            }
        }
    }
    internal class ProcessBottleneckSurveyor : IAmbientBottleneckSurveyor
    {
        private readonly IAmbientBottleneckDetector _bottleneckDetector;
        private readonly string _scopeName;
        private readonly Regex _allow;
        private readonly Regex _block;
        private readonly ConcurrentDictionary<string, AmbientBottleneckAccessor> _metrics;
        private bool _disposedValue;

        public ProcessBottleneckSurveyor(string processScopeName, IAmbientBottleneckDetector bottleneckDetector, Regex allow, Regex block)
        {
            System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
            _scopeName = string.IsNullOrEmpty(processScopeName) ? FormattableString.Invariant($"Process {process.ProcessName} ({process.Id})") : processScopeName;
            _allow = allow;
            _block = block;
            _metrics = new ConcurrentDictionary<string, AmbientBottleneckAccessor>();
            if (bottleneckDetector != null)
            {
                bottleneckDetector.BottleneckExited += OnBottleneckAccessEnded;
                _bottleneckDetector = bottleneckDetector;
            }
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor MostUtilizedBottleneck
        {
            get
            {
                return _metrics.Values.Max();
            }
        }

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
        }

        public void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor evnt)
        {
            if (evnt == null) throw new ArgumentNullException(nameof(evnt));
            string bottleneckId = evnt.Bottleneck.Id;
            // is this bottleneck being surveyed?
            bool blocked = _block?.IsMatch(bottleneckId) ?? false;
            bool allowed = blocked ? false : _allow?.IsMatch(bottleneckId) ?? true;
            if (allowed)
            {
                _metrics.AddOrUpdate(bottleneckId, evnt, (s, m) => m.Combine(evnt));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_bottleneckDetector != null)
                    {
                        _bottleneckDetector.BottleneckExited -= OnBottleneckAccessEnded;
                    }
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
    class ThreadSurveyManager : IDisposable
    {
        private readonly IAmbientBottleneckDetector _bottleneckDetector;
        private readonly ThreadLocal<ThreadAccessDistributor> _threadDistributors;
        private bool disposedValue;

        public ThreadSurveyManager(IAmbientBottleneckDetector bottleneckDetector)
        {
            _threadDistributors = new ThreadLocal<ThreadAccessDistributor>();
            if (bottleneckDetector != null)
            {
                bottleneckDetector.BottleneckExited += BottleneckDetector_BottleneckExited;
                _bottleneckDetector = bottleneckDetector;
            }
        }

        private ThreadAccessDistributor ThreadDistributor
        {
            get
            {
                ThreadAccessDistributor threadDistributor = _threadDistributors.Value;
                if (threadDistributor == null)
                {
                    _threadDistributors.Value = threadDistributor = new ThreadAccessDistributor();
                }
                return threadDistributor;
            }
        }

        private void BottleneckDetector_BottleneckExited(object sender, AmbientBottleneckAccessor e)
        {
            ThreadDistributor.OnBottleneckAccessEnded(sender, e);
        }

        internal ThreadBottleneckSurveyor CreateThreadSurveyor(string scopeName, Regex allow, Regex block)
        {
            ThreadBottleneckSurveyor surveyor = new ThreadBottleneckSurveyor(scopeName, ThreadDistributor, allow, block);
            return surveyor;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _threadDistributors.Dispose();
                    if (_bottleneckDetector != null)
                    {
                        _bottleneckDetector.BottleneckExited -= BottleneckDetector_BottleneckExited;
                    }
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
    internal class ThreadAccessDistributor
    {
        public event EventHandler<AmbientBottleneckAccessor> BottleneckExited;

        public ThreadAccessDistributor()
        {
        }
        public void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor access)
        {
            BottleneckExited?.Invoke(sender, access);
        }
    }
    internal class ThreadBottleneckSurveyor : IAmbientBottleneckSurveyor
    {
        private readonly ThreadAccessDistributor _threadDistributor;
        private readonly string _scopeName;
        private readonly Regex _allow;
        private readonly Regex _block;
        private readonly Dictionary<string, AmbientBottleneckAccessor> _metrics;
        private bool _disposedValue;

        public ThreadBottleneckSurveyor(string scopeName, ThreadAccessDistributor threadDistributor, Regex allow, Regex block)
        {
            _scopeName = string.IsNullOrEmpty(scopeName) ? (string.IsNullOrEmpty(System.Threading.Thread.CurrentThread.Name) ? $"Thread {System.Threading.Thread.CurrentThread.ManagedThreadId}" : System.Threading.Thread.CurrentThread.Name) : scopeName;
            _allow = allow;
            _block = block;
            _metrics = new Dictionary<string, AmbientBottleneckAccessor>();
            if (threadDistributor != null)
            {
                threadDistributor.BottleneckExited += OnBottleneckAccessEnded;
                _threadDistributor = threadDistributor;
            }
        }

        public string ScopeName => _scopeName;

        public AmbientBottleneckAccessor MostUtilizedBottleneck
        {
            get
            {
                return _metrics.Values.Max();
            }
        }

        public IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count)
        {
            return _metrics.Values.OrderBy(m => m.Utilization).Take(count);
        }

        public void OnBottleneckAccessEnded(object sender, AmbientBottleneckAccessor evnt)
        {
            if (evnt == null) throw new ArgumentNullException(nameof(evnt));
            string bottleneckId = evnt.Bottleneck.Id;
            // is this bottleneck being surveyed?
            bool blocked = _block?.IsMatch(bottleneckId) ?? false;
            bool allowed = blocked ? false : _allow?.IsMatch(bottleneckId) ?? true;
            if (allowed)
            {
                AmbientBottleneckAccessor metric;
                if (_metrics.TryGetValue(bottleneckId, out metric))
                {
                    _metrics[bottleneckId] = metric.Combine(evnt);
                }
                else
                {
                    _metrics.Add(bottleneckId, evnt);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_threadDistributor != null)
                    {
                        _threadDistributor.BottleneckExited -= OnBottleneckAccessEnded;
                    }
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
