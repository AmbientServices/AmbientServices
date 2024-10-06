using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// A class that monitors process CPU utilization.
/// </summary>
public sealed class CpuMonitor : IDisposable
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
    , IAsyncDisposable
#endif
{
    private readonly AmbientEventTimer _timer = new();
    private float _averageCpuUsageLastWindow;
    private volatile object _mostRecentSample;

    /// <summary>
    /// Constructs a system CPU usage monitor.
    /// </summary>
    /// <param name="windowMilliseconds">The number of milliseconds between samples.</param>
    public CpuMonitor(long windowMilliseconds = 250)
    {
        _mostRecentSample = CpuUsageSample.GetSample();
        _timer.AutoReset = true;
        _timer.Interval = windowMilliseconds;
        _timer.Enabled = true;
        _timer.Elapsed += UpdateSample;
    }
    /// <summary>
    /// Constructs a system CPU usage monitor.
    /// </summary>
    /// <param name="minimumWindow">A <see cref="TimeSpan"/> indicating the minimum sampling window size.</param>
    public CpuMonitor(TimeSpan minimumWindow) : this((long)minimumWindow.TotalMilliseconds)
    {
    }

    /// <summary>
    /// Gets the proportion of time the CPU was in use (average across all CPUs) in the previous measurement window, which will be at least the minimum window specified in the constructor.
    /// </summary>
    public float RecentUsage => _averageCpuUsageLastWindow;

    /// <summary>
    /// Gets the proportion of time the CPU that has been used in the current partial window.
    /// </summary>
    public float PendingUsage
    {
        get
        {
            return CpuUsageSample.CpuUtilization((CpuUsageSample)_mostRecentSample, CpuUsageSample.GetSample());
        }
    }

    /// <summary>
    /// Disposes of the CPU monitor.
    /// </summary>
    public void Dispose()
    {
        _timer.Enabled = false;
        _timer.Dispose();
    }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
    /// <summary>
    /// Disposes of the CPU monitor.
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        _timer.Enabled = false;
        _timer.Dispose();
#if NETCOREAPP1_0_OR_GREATER
        return ValueTask.CompletedTask;
#else
        return default;
#endif
    }
#endif
    private void UpdateSample(object? s, System.Timers.ElapsedEventArgs e)
    {
        CpuUsageSample newSample = CpuUsageSample.GetSample();
        CpuUsageSample oldSample = (CpuUsageSample)Interlocked.Exchange(ref _mostRecentSample, newSample);
        Interlocked.Exchange(ref _averageCpuUsageLastWindow, CpuUsageSample.CpuUtilization(oldSample, newSample));
    }
}


/// <summary>
/// A class that represents a single sample of CPU usage.
/// Two samples can be compared to see how much CPU the process used between the time the first sample was taken and the time the second sample was taken.
/// </summary>
public readonly struct CpuUsageSample : IEquatable<CpuUsageSample>
{
    private static readonly Process? _ThisProcess =
#if NET5_0_OR_GREATER
        OperatingSystem.IsBrowser() ? null : 
#endif
        Process.GetCurrentProcess();

    private readonly long _wallClockTicks;
    private readonly long _processTicks;

    private CpuUsageSample(long wallClockTicks = 0, long processTicks = 0)
    {
        _wallClockTicks = wallClockTicks;
        _processTicks = processTicks;
    }
    /// <summary>
    /// Checks if this sample is equal to another object.
    /// </summary>
    /// <param name="other">The other CPU usage sample.</param>
    /// <returns>true if the objects are logically equal, otherwise false.</returns>
    public bool Equals(CpuUsageSample other)
    {
        return _wallClockTicks == other._wallClockTicks && _processTicks == other._processTicks;
    }
    /// <summary>
    /// Checks if this sample is equal to another object.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>true if the objects are logically equal, otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        return (obj is CpuUsageSample other) ? Equals(other) : false;
    }
    /// <summary>
    /// Gets a hash code for this sample.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return _wallClockTicks.GetHashCode() ^ _processTicks.GetHashCode();
    }
    /// <summary>
    /// Checks if two samples are equal.
    /// </summary>
    /// <param name="left">The left sample.</param>
    /// <param name="right">The right sample.</param>
    /// <returns>true if the samples are logically equal, false if they are not.</returns>
    public static bool operator ==(CpuUsageSample left, CpuUsageSample right)
    {
        return left.Equals(right);
    }
    /// <summary>
    /// Checks if two samples are unequal.
    /// </summary>
    /// <param name="left">The left sample.</param>
    /// <param name="right">The right sample.</param>
    /// <returns>true if the samples are logically unequal, false if they are logically equal.</returns>
    public static bool operator !=(CpuUsageSample left, CpuUsageSample right)
    {
        return !(left == right);
    }
    /// <summary>
    /// Computes the CPU utilization between the two specified samples.
    /// </summary>
    /// <param name="first">The first sample.</param>
    /// <param name="second">The second sample.</param>
    /// <returns>The average CPU utilization (between 0.0 and 1.0) for the calling process between the time <paramref name="first"/> was taken and the time <paramref name="second"/> was taken.</returns>
    public static float CpuUtilization(CpuUsageSample first, CpuUsageSample second)
    {
        long wallTicks = second._wallClockTicks - first._wallClockTicks;
        long cpuTicks = second._processTicks - first._processTicks;
        return (cpuTicks * 1.0f) / wallTicks / Environment.ProcessorCount;
    }
    /// <summary>
    /// Samples the current CPU state for the process.
    /// </summary>
    /// <returns>A <see cref="CpuUsageSample"/> containing the state.</returns>
    public static CpuUsageSample GetSample()
    {
        return new(Stopwatch.GetTimestamp(),
#if NET5_0_OR_GREATER
            OperatingSystem.IsBrowser() ? 0 : 
#endif
            _ThisProcess?.TotalProcessorTime.Ticks ?? 0);
    }
}
