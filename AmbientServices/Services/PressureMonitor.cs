using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// An interface that abstracts an external pressure point.
/// </summary>
public interface IPressurePoint
{
    /// <summary>
    /// Gets the name of the pressure point, used for the performance counter instance and status reports.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Gets the pressure value (between 0.0 and 1.0).
    /// </summary>
    float Pressure { get; }
}
/// <summary>
/// A static class that manages registrations of <see cref="IPressurePoint"/> instances for internal pressures.
/// </summary>
public static class InternalPressurePoints
{
    private static readonly ConcurrentDictionary<string, IPressurePoint> _PressurePoints = new();

    /// <summary>
    /// Gets an enumeration of the pressure points
    /// </summary>
    public static IEnumerable<IPressurePoint> List => _PressurePoints.Select(p => p.Value);

    /// <summary>
    /// Regsiters the specified <see cref="IPressurePoint"/> for inclusion in <see cref="PressureMonitor.ExternalPressure"/> and <see cref="PressureMonitor.OverallPressure"/>.
    /// </summary>
    /// <param name="pp">The <see cref="IPressurePoint"/> to register.</param>
    /// <returns>true if the pressure point was successfully registered, false if there was already a pressure point with that name registered.</returns>
    public static bool Register(IPressurePoint pp)
    {
        if (pp == null) throw new ArgumentNullException(nameof(pp));
        return _PressurePoints.TryAdd(pp.Name, pp);
    }
}
/// <summary>
/// A static class that manages registrations of <see cref="IPressurePoint"/> instances for external pressures.
/// </summary>
public static class ExternalPressurePoints
{
    private static readonly ConcurrentDictionary<string, IPressurePoint> _PressurePoints = new();

    /// <summary>
    /// Gets an enumeration of the pressure points
    /// </summary>
    public static IEnumerable<IPressurePoint> List => _PressurePoints.Select(p => p.Value);

    /// <summary>
    /// Regsiters the specified <see cref="IPressurePoint"/> for inclusion in <see cref="PressureMonitor.ExternalPressure"/> and <see cref="PressureMonitor.OverallPressure"/>.
    /// </summary>
    /// <param name="pp">The <see cref="IPressurePoint"/> to register.</param>
    /// <returns>true if the pressure point was successfully registered, false if there was already a pressure point with that name registered.</returns>
    public static bool Register(IPressurePoint pp)
    {
        if (pp == null) throw new ArgumentNullException(nameof(pp));
        return _PressurePoints.TryAdd(pp.Name, pp);
    }
}
/// <summary>
/// A static class that monitors and reports on system pressure so that background processing can be adjusted accordingly to prevent the system from getting overwhelmed and to prevent background processing from interfering with interactive processing.
/// </summary>
public class PressureMonitor : IDisposable
{
    private const int PressureRecalculateFrequencyMilliseconds = 1000;
    private static readonly PressureMonitor _Default = new();

    /// <summary>
    /// Gets the default system pressure monitor.
    /// </summary>
    public static PressureMonitor Default => _Default;

    private readonly AmbientCallbackTimer _timer;

    private float _internalPressure;
    private float _externalPressure;
    private float _overallPressure;
    private bool disposedValue;

    /// <summary>
    /// Gets the (overall) internal system pressure (the highest of the individual internal pressures).
    /// </summary>
    public float InternalPressure => _internalPressure;
    /// <summary>
    /// Gets the (overall) external system pressure (the highest of the individual external pressures).
    /// </summary>
    public float ExternalPressure => _externalPressure;
    /// <summary>
    /// Gets the overall system pressure (the highest of the individual pressures, including external pressures like database and throttling).
    /// </summary>
    public float OverallPressure => _overallPressure;

    /// <summary>
    /// Constructs a new pressure monitor with the specified frequency.
    /// </summary>
    /// <param name="frequencyMilliseconds">The frequency to recompute pressure, in milliseconds.</param>
    public PressureMonitor(int? frequencyMilliseconds = null)
    {
        TimeSpan frequency = TimeSpan.FromMilliseconds(frequencyMilliseconds ?? PressureRecalculateFrequencyMilliseconds);
        _timer = new(OnTimerCallback, null, frequency, frequency);
    }

    private void OnTimerCallback(object? state)
    {
        float internalPressure = 0;
        foreach (IPressurePoint pp in InternalPressurePoints.List)
        {
            float pressure = pp.Pressure;
            internalPressure = Max(internalPressure, pressure);
        }
        Interlocked.Exchange(ref _internalPressure, internalPressure);

        float externalPressure = 0;
        foreach (IPressurePoint pp in ExternalPressurePoints.List)
        {
            float pressure = pp.Pressure;
            externalPressure = Max(externalPressure, pressure);
        }
        Interlocked.Exchange(ref _externalPressure, externalPressure);

        float overallPressure = Math.Min(1.0f, Max(0.0f, internalPressure, externalPressure));
        Interlocked.Exchange(ref _overallPressure, overallPressure);
    }
    /// <summary>
    /// Gets the maximum of all the specified values.
    /// </summary>
    /// <param name="items">A variable-length array of floating point numbers.</param>
    /// <returns>The highest of all the specified floating point numbers.</returns>
    public static float Max(params float[] items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        float max = float.MinValue;
        foreach (float f in items)
        {
            if (f > max) max = f;
        }
        return max;
    }
    /// <summary>
    /// Gets the maximum of all the specified values.
    /// </summary>
    /// <param name="items">An enumeration of floating point numbers.</param>
    /// <returns>The highest of all the specified floating point numbers.</returns>
    public static float Max(IEnumerable<float> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        float max = float.MinValue;
        foreach (float f in items)
        {
            if (f > max) max = f;
        }
        return max;
    }
    /// <summary>
    /// Disposes or finalizes the instance.
    /// </summary>
    /// <param name="disposing">Whether or not we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _timer.Dispose();
            }
            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~PressureMonitor()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    /// <summary>
    /// Disposes of the instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
/// <summary>
/// An <see cref="IPressurePoint"/> implementation that measures local CPU pressure.
/// </summary>
public sealed class CpuPressure : IPressurePoint
{
#if NET5_0_OR_GREATER
    private readonly float _neutralValue;
#endif
    private object _previousSample;         // interlocked

#if NET5_0_OR_GREATER
    /// <summary>
    /// Constructs a CPU pressure point.
    /// </summary>
    /// <param name="neutralValue">A neutral value to use in case we're running in a browser and this information is not available.</param>
    public CpuPressure(float neutralValue = 0.89f)
    {
        _previousSample = CpuUsageSample.GetSample();
        _neutralValue = neutralValue;
    }
#else
    /// <summary>
    /// Constructs a CPU pressure point.
    /// </summary>
    public CpuPressure()
    {
        _previousSample = CpuUsageSample.GetSample();
    }
#endif

    /// <summary>
    /// Gets the name of the pressure point, used for the performance counter instance and status reports.
    /// </summary>
    public string Name => "Cpu";

    /// <summary>
    /// Gets the pressure value (between 0.0 and 1.0).
    /// </summary>
    public float Pressure
    {
        get
        {
#if NET5_0_OR_GREATER
            if (OperatingSystem.IsBrowser()) return _neutralValue;
#endif
            CpuUsageSample newSample = CpuUsageSample.GetSample();
            CpuUsageSample oldSample = (CpuUsageSample)Interlocked.Exchange(ref _previousSample, newSample);
            return 0.02f + CpuUsageSample.CpuUtilization(oldSample, newSample);   // _cpuMonitor is *process* usage, so we'll add a little extra to account for the rest of the system
        }
    }
}

/// <summary>
/// A <see cref="IPressurePoint"/> implementation that measures local thread pool pressure.
/// </summary>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public sealed class ThreadPoolPressure : IPressurePoint
{
    private readonly int _maxPoolThreads;
    private readonly int _maxProcessThreads;
#if NETCOREAPP1_0_OR_GREATER
    private readonly int _maxBufferedThreadPoolActions;
    private readonly int _maxThreadsPerSecond;
    private int _previousSampleThreadCount;
    private int _threadsAddedThisSample;
#endif

#if NETCOREAPP1_0_OR_GREATER
    /// <summary>
    /// Constructs a pressure point that measures thread pool pressure.
    /// </summary>
    /// <param name="maxProcessThreads">The maxiumum number of threads to allow for this process.</param>
    /// <param name="maxPoolThreads">The maximum number of threads to allow for the thread pool.</param>
    /// <param name="maxThreadPerSecond">The maximum number of threads being created per second to allow.</param>
    /// <param name="maxBufferedThreadPoolActions">The maximum number of buffered thread pool actions to allow.</param>
    public ThreadPoolPressure(int maxProcessThreads = 64 * 1024, int maxPoolThreads = 64 * 1024, int maxThreadPerSecond = 1024, int maxBufferedThreadPoolActions = 64 * 1024)
    {
        _maxProcessThreads = maxProcessThreads;
        _maxPoolThreads = maxPoolThreads;
        _maxThreadsPerSecond = maxThreadPerSecond;
        _maxBufferedThreadPoolActions = maxBufferedThreadPoolActions;
    }
#else
    /// <summary>
    /// Constructs a pressure point that measures thread pool pressure.
    /// </summary>
    /// <param name="maxProcessThreads">The maxiumum number of threads to allow for this process.</param>
    /// <param name="maxPoolThreads">The maximum number of threads to allow for the thread pool.</param>
    public ThreadPoolPressure(int maxProcessThreads = 64 * 1024, int maxPoolThreads = 64 * 1024)
    {
        _maxProcessThreads = maxProcessThreads;
        _maxPoolThreads = maxPoolThreads;
    }
#endif

    /// <summary>
    /// Gets the name of the pressure point, used for the performance counter instance and status reports.
    /// </summary>
    public string Name => "ThreadPool";

    /// <summary>
    /// Gets the pressure value (between 0.0 and 1.0).
    /// </summary>
    public float Pressure
    {
        get
        {
            Process currentProcess = Process.GetCurrentProcess();
            float processThreadPressure = 0.0f;
#if NET5_0_OR_GREATER
        if (!OperatingSystem.IsBrowser()) 
        {
#endif
            int processThreads = currentProcess.Threads.Count;
            processThreadPressure = (1.0f * processThreads) / _maxProcessThreads;
#if NET5_0_OR_GREATER
        }
#endif
#if NETCOREAPP1_0_OR_GREATER
            float pendingWorkPressure = Math.Min(1.0f, (1.0f * ThreadPool.PendingWorkItemCount) / _maxBufferedThreadPoolActions);

            int newThreadCount = ThreadPool.ThreadCount;
            int previousThreadCount = Interlocked.Exchange(ref _previousSampleThreadCount, newThreadCount);
            int threadsAdded = Math.Max(0, newThreadCount - _previousSampleThreadCount);
            float threadCountChangePressure = Math.Max(0.0f, (threadsAdded * 1.0f) / _maxThreadsPerSecond);
            Interlocked.Exchange(ref _threadsAddedThisSample, newThreadCount);
#endif
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            ThreadPool.GetAvailableThreads(out int potentialAdditionalWorkerThreads, out int potentialAdditionalCompletionPortThreads);
            int workerThreads = maxWorkerThreads - potentialAdditionalWorkerThreads;
            int completionPortThreads = maxCompletionPortThreads - potentialAdditionalCompletionPortThreads;
            float workerPressure = (1.0f * workerThreads / maxWorkerThreads);
            float completionPortPressure = (1.0f * completionPortThreads / maxCompletionPortThreads);
            float totalThreadPressure = Math.Min(0.0f, (workerThreads + completionPortThreads) * 1.0f / _maxPoolThreads);

            return PressureMonitor.Max(
#if NETCOREAPP1_0_OR_GREATER
                    threadCountChangePressure, pendingWorkPressure,
#endif
                    processThreadPressure, workerPressure, completionPortPressure, totalThreadPressure
                    );
        }
    }
}

/// <summary>
/// A <see cref="IPressurePoint"/> implementation that measures local system memory pressure.
/// </summary>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public sealed class MemoryPressure : IPressurePoint
{
#if NETCOREAPP1_0_OR_GREATER
    /// <summary>
    /// Constructs a pressure point that measures memory pressure.
    /// </summary>
    public MemoryPressure()
    {
    }
#else
    private readonly long _maxBytesAllowed;

    /// <summary>
    /// Constructs a pressure point that measures memory pressure.
    /// </summary>
    /// <param name="maxBytesAllowed">The maximum number of bytes allowed to be used by this process.</param>
    public MemoryPressure(long maxBytesAllowed = long.MaxValue)
    {
        _maxBytesAllowed = maxBytesAllowed;
    }
#endif

    /// <summary>
    /// Gets the name of the pressure point, used for the performance counter instance and status reports.
    /// </summary>
    public string Name => "ThreadPool";

    /// <summary>
    /// Gets the pressure value (between 0.0 and 1.0).
    /// </summary>
    public float Pressure
    {
        get
        {
#if NETCOREAPP1_0_OR_GREATER
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            long totalPhysicalMemory = info.TotalAvailableMemoryBytes;
            long reservedMemory = Math.Max(totalPhysicalMemory / 10, 25000000);
            long usableMemory = totalPhysicalMemory - reservedMemory;
            float loadMemoryPressure = (1.0f * info.MemoryLoadBytes) / usableMemory;
            float workingSetMemoryPressure = 0;;
#if NET5_0_OR_GREATER
            if (!OperatingSystem.IsBrowser())
            {
#endif
                long workingSetMemory = Process.GetCurrentProcess().WorkingSet64;
                workingSetMemoryPressure = (1.0f * workingSetMemory) / usableMemory;
#if NET5_0_OR_GREATER
            }
#endif
            return Math.Max(loadMemoryPressure, workingSetMemoryPressure);
#else
            long totalBytes = GC.GetTotalMemory(false);
            return (totalBytes * 1.0f) / _maxBytesAllowed;
#endif
        }
    }
}
