using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// An <see cref="IPressurePoint"/> implementation that measures local CPU pressure.
/// </summary>
public sealed class CpuPressurePoint : IPressurePoint
{
    private const double FixedFloatingPointAdjustment = 100_000_000;
    private const long MinRawValue = 0;
    private const long MaxRawValue = (long)(1.00f * FixedFloatingPointAdjustment);
    private const long NeutralRawValue = (long)(0.89f * FixedFloatingPointAdjustment);
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    private readonly IAmbientStatistic? _cpuPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(CpuPressurePoint), "CPU Pressure", "A measure of the CPU pressure level indicating the proportion of the CPU that was used, between 0 and 1", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);

#if NET5_0_OR_GREATER
    private readonly float _neutralValue;
#endif
    private object _previousSample;         // interlocked

#if NET5_0_OR_GREATER
    /// <summary>
    /// Constructs a CPU pressure point.
    /// </summary>
    /// <param name="neutralValue">A neutral value to use in case we're running in a browser and this information is not available.</param>
    public CpuPressurePoint(float neutralValue = 0.89f)
    {
        _previousSample = CpuSample.GetSample();
        _neutralValue = neutralValue;
    }
#else
    /// <summary>
    /// Constructs a CPU pressure point.
    /// </summary>
    public CpuPressurePoint()
    {
        _previousSample = CpuSample.GetSample();
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
            CpuSample newSample = CpuSample.GetSample();
            CpuSample oldSample = (CpuSample)Interlocked.Exchange(ref _previousSample, newSample);
            float newPressure = 0.02f + CpuSample.CpuUtilization(oldSample, newSample);   // _cpuMonitor is *process* usage, so we'll add a little extra to account for the rest of the system
            _cpuPressure?.SetValue(newPressure);
            return newPressure;
        }
    }
}

/// <summary>
/// A <see cref="IPressurePoint"/> implementation that measures local thread pool pressure.
/// </summary>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public sealed class ThreadPoolPressurePoint : IPressurePoint
{
    private const double FixedFloatingPointAdjustment = 100_000_000;
    private const long MinRawValue = 0;
    private const long MaxRawValue = (long)(1.00f * FixedFloatingPointAdjustment);
    private const long NeutralRawValue = (long)(0.89f * FixedFloatingPointAdjustment);

    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    private readonly IAmbientStatistic? _threadPoolPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-Overall", "ThreadPool Pressure", "The overall thread pool pressure", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);
    private readonly IAmbientStatistic? _processThreadPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-ProcessThreads", "Process Thread Pressure", "The process thread pressure level", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);

    private readonly int _maxPoolThreads;
    private readonly int _maxProcessThreads;
    private readonly IAmbientStatistic? _workerPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-Workers", "Worker Pressure", "The pressure due to the number of thread pool worker threads", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);
    private readonly IAmbientStatistic? _completionPortPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-CompletionPorts", "Completion Port Pressure", "The pressure due to the number of thread pool completion port threads", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);
    private readonly IAmbientStatistic? _totalThreadPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-TotalThreads", "Total Thread Pressure", "The pressure due to the number of thread pool total threads", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);
#if NETCOREAPP1_0_OR_GREATER
    private readonly IAmbientStatistic? _threadCountChangePressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(ThreadPoolPressurePoint) + "-ThreadCountChange", "Thread Creation Pressure", "The pressure due to new threads starting", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent);
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
    public ThreadPoolPressurePoint(int maxProcessThreads = 64 * 1024, int maxPoolThreads = 64 * 1024, int maxThreadPerSecond = 1024, int maxBufferedThreadPoolActions = 64 * 1024)
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
    public ThreadPoolPressurePoint(int maxProcessThreads = 64 * 1024, int maxPoolThreads = 64 * 1024)
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
                _processThreadPressure?.SetValue(processThreadPressure);
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
            _threadCountChangePressure?.SetValue(threadCountChangePressure);
#endif
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            ThreadPool.GetAvailableThreads(out int potentialAdditionalWorkerThreads, out int potentialAdditionalCompletionPortThreads);
            int workerThreads = maxWorkerThreads - potentialAdditionalWorkerThreads;
            int completionPortThreads = maxCompletionPortThreads - potentialAdditionalCompletionPortThreads;
            float workerPressure = (1.0f * workerThreads / maxWorkerThreads);
            float completionPortPressure = (1.0f * completionPortThreads / maxCompletionPortThreads);
            float totalThreadPressure = Math.Min(0.0f, (workerThreads + completionPortThreads) * 1.0f / _maxPoolThreads);
            _workerPressure?.SetValue(workerPressure);
            _completionPortPressure?.SetValue(completionPortPressure);
            _totalThreadPressure?.SetValue(totalThreadPressure);

            float overallThreadPressure = PressureMonitor.Max(
#if NETCOREAPP1_0_OR_GREATER
                    threadCountChangePressure, pendingWorkPressure,
#endif
                    processThreadPressure, workerPressure, completionPortPressure, totalThreadPressure
                    );
            _threadPoolPressure?.SetValue(overallThreadPressure);
            return overallThreadPressure;
        }
    }
}

/// <summary>
/// A <see cref="IPressurePoint"/> implementation that measures local system memory pressure.
/// Memory usage is not directly proportional to memory pressure, 
/// because significant memory is always in use even when nothing is happening,
/// so this pressure point uses a skewed scale to better represent the pressure.
/// </summary>
#if NET5_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public sealed class MemoryPressurePoint : IPressurePoint
{
    private static readonly int[] SkewedProportions = new int[] {                   // a more smooth function would look something like this (called a logistic function):
         0,  0,  0,  0,  0,  0,  0,  0,  0,  0,     // 9% = 0% pressure             // skewed = 1 / (1 + e^(-steepness * (linear - focus)))
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,     // 19% = 1% pressure            // where steepness adjusts the steepness of the slope of the curve leading from low values (near zero) to high values (near 1)
         2,  2,  2,  2,  2,  3,  3,  3,  3,  3,     // 29% = 3% pressure            // and focus is where the curve crosses 0.5 (the inflection point)
         4,  4,  4,  4,  5,  5,  5,  5,  6,  6,     // 39% = 6% pressure            // 
         6,  6,  7,  7,  7,  8,  8,  8,  9,  9,     // 49% = 9% pressure            // 
        10, 10, 11, 11, 12, 12, 13, 13, 14, 14,     // 59% = 14% pressure           // 
        15, 16, 17, 18, 19, 20, 21, 22, 23, 24,     // 69% = 24% pressure           // 
        25, 26, 27, 28, 29, 30, 31, 32, 33, 34,     // 79% = 34% pressure           // 
        36, 38, 40, 42, 44, 46, 48, 50, 52, 64,     // 89% = 64% pressure           // 
        67, 70, 73, 76, 79, 82, 86, 90, 94, 98,     // 99% = 98% pressure           // 
    };
    private const double FixedFloatingPointAdjustment = 100_000_000;
    private const long MinRawValue = 0;
    private const long MaxRawValue = (long)(1.00f * FixedFloatingPointAdjustment);
    private const long NeutralRawValue = (long)(0.89f * FixedFloatingPointAdjustment);
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    private readonly IAmbientStatistic? _memoryPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(MemoryPressurePoint) + "-Overall", "Memory Pressure", "The pressure due to memory used", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max);

#if NETCOREAPP1_0_OR_GREATER
    private readonly IAmbientStatistic? _memoryLoadPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(MemoryPressurePoint) + "-MemoryLoad", "Memory Load", "The pressure due to memory load", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max);
    private readonly IAmbientStatistic? _workingSetPressure = AmbientStatistics.Local?.GetOrAddStatistic(AmbientStatisicType.Raw, nameof(MemoryPressurePoint) + "-WorkingSet", "Working Set", "The pressure due to the working set", false, NeutralRawValue, MinRawValue, MaxRawValue, "p", FixedFloatingPointAdjustment, AggregationTypes.Average | AggregationTypes.Min | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Min | AggregationTypes.Max);
    /// <summary>
    /// Constructs a pressure point that measures memory pressure.
    /// </summary>
    public MemoryPressurePoint()
    {
    }
#else
    private readonly long _maxBytesAllowed;

    /// <summary>
    /// Constructs a pressure point that measures memory pressure.
    /// </summary>
    /// <param name="maxBytesAllowed">The maximum number of bytes allowed to be used by this process.</param>
    public MemoryPressurePoint(long maxBytesAllowed = long.MaxValue)
    {
        _maxBytesAllowed = maxBytesAllowed;
    }
#endif

    /// <summary>
    /// Gets the name of the pressure point, used for the performance counter instance and status reports.
    /// </summary>
    public string Name => "Memory";

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
            long reservedMemory = Math.Min(Math.Max(totalPhysicalMemory / 10, 25_000_000), 4_000_000_000);
            long usableMemory = totalPhysicalMemory - reservedMemory;
            float loadMemoryPressure = (1.0f * info.MemoryLoadBytes) / usableMemory;
            _memoryLoadPressure?.SetValue(loadMemoryPressure);
            float workingSetMemoryPressure = 0;
#if NET5_0_OR_GREATER
            if (!OperatingSystem.IsBrowser())
            {
#endif
                long workingSetMemory = Process.GetCurrentProcess().WorkingSet64;
                workingSetMemoryPressure = (1.0f * workingSetMemory) / usableMemory;
                _workingSetPressure?.SetValue(workingSetMemoryPressure);
#if NET5_0_OR_GREATER
            }
#endif
            float linearPressure = Math.Max(loadMemoryPressure, workingSetMemoryPressure);
            int linearPressureOffset = (int)(linearPressure * 100);
            float memoryPressure = LinearPressureToMemoryPressure(linearPressure);
            _memoryPressure?.SetValue(memoryPressure);
            return memoryPressure;
#else
            long totalBytes = GC.GetTotalMemory(false);
            float linearPressure = (totalBytes * 1.0f) / _maxBytesAllowed;
            float memoryPressure = LinearPressureToMemoryPressure(linearPressure);
            _memoryPressure?.SetValue(memoryPressure);
            return memoryPressure;
#endif
        }
    }
    internal static float LinearPressureToMemoryPressure(float linearPressure)
    {
        if (linearPressure <= 0.0f) return 0.0f;
        if (linearPressure  > 1.1f) return 1.0f;
        if (linearPressure > 0.99f) return 0.99f + 0.01f * (linearPressure - 0.99f) / 0.11f;
        int linearPressureOffset = (int)(linearPressure * 100);
        float memoryPressure = (SkewedPressure(linearPressureOffset) + (SkewedPressure(linearPressureOffset + 1) - SkewedPressure(linearPressureOffset)) * (linearPressure * 100.0f - linearPressureOffset)) / 100.0f;
        return memoryPressure;
    }
    private static int SkewedPressure(int linearPressureOffset)
    {
        if (linearPressureOffset <= 0) return 0;
        if (linearPressureOffset >= 100) return 100;
        return SkewedProportions[linearPressureOffset];
    }
}
