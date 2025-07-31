using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
using System.Threading.Tasks;
#endif

namespace AmbientServices;

/// <summary>
/// An interface that can be used to mock recent CPU usage so that code branches depending on CPU utilization can be tested.
/// </summary>
public interface IMockCpuUsage
{
    /// <summary>
    /// Gets the value to use as the most recent CPU usage, which should be a number between 0.0 and 1.0.
    /// </summary>
    float RecentUsage { get; }
}

/// <summary>
/// An interface for CPU usage samplers.
/// </summary>
internal interface ICpuSampler
{
    void Sample();
    float GetUsage();
    float GetPendingUsage();
}

/// <summary>
/// A class that monitors process CPU utilization.
/// </summary>
public sealed class CpuMonitor : IDisposable
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
    , IAsyncDisposable
#endif
{
    private static readonly AmbientService<IMockCpuUsage> _MockCpu = Ambient.GetService<IMockCpuUsage>();
    private readonly AmbientEventTimer _timer = new();
    private readonly ICpuSampler _sampler;

    /// <summary>
    /// Constructs a system CPU usage monitor.
    /// </summary>
    /// <param name="windowMilliseconds">The number of milliseconds between samples.</param>
    public CpuMonitor(long windowMilliseconds = 250)
    {
        _sampler = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? new LinuxContainerCpuSampler()
            : new StandardCpuSampler();
        _timer.AutoReset = true;
        _timer.Interval = windowMilliseconds;
        _timer.Enabled = true;
        _timer.Elapsed += (s, e) => _sampler.Sample();
        _sampler.Sample();
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
    public float RecentUsage => _MockCpu.Local?.RecentUsage ?? _sampler.GetUsage();

    /// <summary>
    /// Gets the proportion of time the CPU was in use (average across all CPUs) since the last sample was taken.
    /// </summary>
    public float PendingUsage => _sampler.GetPendingUsage();

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
}

/// <summary>
/// A class that represents a single sample of CPU usage.
/// Two samples can be compared to see how much CPU the process used between the time the first sample was taken and the time the second sample was taken.
/// </summary>
internal readonly struct CpuSample : IEquatable<CpuSample>
{
    /// <summary>
    /// Gets the current <see cref="Process"/>.
    /// </summary>
    /// <remarks>Note that when you want CPU usage time, this *cannot* be cached--it must be called each time.</remarks>
    /// <returns>The current <see cref="Process"/>, if available, or null if not available.</returns>
    private static Process? GetCurrentProcess() =>
#if NET5_0_OR_GREATER
        OperatingSystem.IsBrowser() ? null : 
#endif
        Process.GetCurrentProcess();

    private readonly long _wallClockTicks;
    private readonly long _processTicks;

    private CpuSample(long wallClockTicks = 0, long processTicks = 0)
    {
        _wallClockTicks = wallClockTicks;
        _processTicks = processTicks;
    }
    /// <summary>
    /// Checks if this sample is equal to another object.
    /// </summary>
    /// <param name="other">The other CPU usage sample.</param>
    /// <returns>true if the objects are logically equal, otherwise false.</returns>
    public bool Equals(CpuSample other)
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
        return (obj is CpuSample other) && Equals(other);
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
    public static bool operator ==(CpuSample left, CpuSample right)
    {
        return left.Equals(right);
    }
    /// <summary>
    /// Checks if two samples are unequal.
    /// </summary>
    /// <param name="left">The left sample.</param>
    /// <param name="right">The right sample.</param>
    /// <returns>true if the samples are logically unequal, false if they are logically equal.</returns>
    public static bool operator !=(CpuSample left, CpuSample right)
    {
        return !(left == right);
    }
    /// <summary>
    /// Computes the CPU utilization between the two specified samples.
    /// </summary>
    /// <param name="first">The first sample.</param>
    /// <param name="second">The second sample.</param>
    /// <returns>The average CPU utilization (between 0.0 and 1.0) for the calling process between the time <paramref name="first"/> was taken and the time <paramref name="second"/> was taken.</returns>
    public static float CpuUtilization(CpuSample first, CpuSample second)
    {
        long wallTicks = second._wallClockTicks - first._wallClockTicks;
        long cpuTicks = second._processTicks - first._processTicks;
        return Math.Min(1.0f, Math.Max(0.0f, (cpuTicks * 1.0f) / wallTicks / Environment.ProcessorCount));
    }
    /// <summary>
    /// Samples the current CPU state for the process.
    /// </summary>
    /// <returns>A <see cref="CpuSample"/> containing the state.</returns>
    public static CpuSample GetSample()
    {
        return new(Stopwatch.GetTimestamp(),
#if NET5_0_OR_GREATER
            OperatingSystem.IsBrowser() ? 0 : 
#endif
            GetCurrentProcess()?.TotalProcessorTime.Ticks ?? 0);
    }
}

internal sealed class StandardCpuSampler : ICpuSampler
{
    private CpuSample _lastSample;
    private float _lastUsagePercent;

    public StandardCpuSampler()
    {
        _lastSample = CpuSample.GetSample();
    }

    public void Sample()
    {
        CpuSample newSample = CpuSample.GetSample();
        _lastUsagePercent = CpuSample.CpuUtilization(_lastSample, newSample);
        _lastSample = newSample;
    }

    public float GetUsage() => _lastUsagePercent;

    public float GetPendingUsage()
    {
        CpuSample currentSample = CpuSample.GetSample();
        return CpuSample.CpuUtilization(_lastSample, currentSample);
    }
}

internal sealed class LinuxContainerCpuSampler : ICpuSampler
{
    private long? _lastUsage;
    private DateTime? _lastSampleTime;
    private float _lastUsagePercent;

    // Cached paths discovered at startup
    private static readonly string? _cpuUsagePath;
    private static readonly string? _cpuQuotaPath;
    private static readonly string? _cpuPeriodPath;
    private static readonly bool _isCgroupV2;
    private static readonly string? _containerId;

    static LinuxContainerCpuSampler()
    {
        (_cpuUsagePath, _cpuQuotaPath, _cpuPeriodPath, _isCgroupV2, _containerId) = DiscoverCgroupPaths();

        //// Log the discovered paths for debugging
        //Console.WriteLine($"Cgroup paths discovered:");
        //Console.WriteLine($"  Container ID: {_containerId ?? "unknown"}");
        //Console.WriteLine($"  Cgroup version: {(_isCgroupV2 ? "v2" : "v1")}");
        //Console.WriteLine($"  CPU usage path: {_cpuUsagePath ?? "not found"}");
        //Console.WriteLine($"  CPU quota path: {_cpuQuotaPath ?? "not found"}");
        //Console.WriteLine($"  CPU period path: {_cpuPeriodPath ?? "not found"}");
    }

    public void Sample()
    {
        long? usage = GetCgroupCpuUsage();
        double? limit = GetCgroupCpuLimit();
        DateTime now = DateTime.UtcNow;

        if (usage == null || limit == null)
        {
            _lastUsagePercent = 0f;
            return;
        }

        if (_lastUsage == null || _lastSampleTime == null)
        {
            _lastUsage = usage;
            _lastSampleTime = now;
            _lastUsagePercent = 0f;
            return;
        }

        long usageDelta = usage.Value - _lastUsage.Value;
        double timeDelta = (now - _lastSampleTime.Value).TotalSeconds;
        _lastUsage = usage;
        _lastSampleTime = now;

        if (timeDelta <= 0)
        {
            _lastUsagePercent = 0f;
            return;
        }

        double cpuSeconds = usageDelta / 1_000_000_000.0;
        double percent = cpuSeconds / (limit.Value * timeDelta);
        _lastUsagePercent = (float)Math.Min(Math.Max(percent, 0.0), 1.0);
    }

    public float GetUsage() => _lastUsagePercent;

    public float GetPendingUsage()
    {
        long? usage = GetCgroupCpuUsage();
        double? limit = GetCgroupCpuLimit();
        DateTime now = DateTime.UtcNow;

        if (usage == null || limit == null || _lastUsage == null || _lastSampleTime == null)
            return 0f;

        long usageDelta = usage.Value - _lastUsage.Value;
        double timeDelta = (now - _lastSampleTime.Value).TotalSeconds;

        if (timeDelta <= 0)
            return 0f;

        double cpuSeconds = usageDelta / 1_000_000_000.0;
        double percent = cpuSeconds / (limit.Value * timeDelta);
        return (float)Math.Min(Math.Max(percent, 0.0), 1.0);
    }

    private static long? GetCgroupCpuUsage()
    {
        if (_cpuUsagePath == null) return null;

        try
        {
            if (_isCgroupV2)
            {
                // cgroup v2 format: read from cpu.stat
                string[] lines = File.ReadAllLines(_cpuUsagePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("usage_usec ", StringComparison.Ordinal))
                    {
                        string usageStr = line.Substring("usage_usec ".Length);
                        if (long.TryParse(usageStr, out long usageUsec)) return usageUsec * 1000; // Convert to nanoseconds
                    }
                }
            }
            else
            {
                // cgroup v1 format: direct file read
                return long.Parse(File.ReadAllText(_cpuUsagePath), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch { }

        return null;
    }

    private static double? GetCgroupCpuLimit()
    {
        if (_cpuQuotaPath == null || _cpuPeriodPath == null) return null;

        try
        {
            if (_isCgroupV2)
            {
                // cgroup v2 format: read from cpu.max
                string content = File.ReadAllText(_cpuQuotaPath).Trim();
                string[] parts = content.Split(' ');
                if (parts.Length == 2 && long.TryParse(parts[0], out long quota) && long.TryParse(parts[1], out long period))
                {
                    if (quota > 0 && period > 0) return (double)quota / period;
                }
            }
            else
            {
                // cgroup v1 format: read from separate quota and period files
                long quota = long.Parse(File.ReadAllText(_cpuQuotaPath), System.Globalization.CultureInfo.InvariantCulture);
                long period = long.Parse(File.ReadAllText(_cpuPeriodPath), System.Globalization.CultureInfo.InvariantCulture);
                if (quota > 0 && period > 0) return (double)quota / period;
            }
        }
        catch { }

        return null;
    }

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupPaths()
    {
        string? containerId = GetContainerId();
        bool isCgroupV2 = IsCgroupV2();

        if (isCgroupV2)
        {
            return DiscoverCgroupV2Paths(containerId);
        }
        else
        {
            return DiscoverCgroupV1Paths(containerId);
        }
    }

    private static string? GetContainerId()
    {
        try
        {
            // Read container ID from /proc/self/cgroup
            string[] lines = File.ReadAllLines("/proc/self/cgroup");
            foreach (string line in lines)
            {
                // Look for docker container ID in the path
                if (line.Contains("docker"))
                {
                    string[] parts = line.Split(':');
                    if (parts.Length >= 3)
                    {
                        string path = parts[2];
                        // Extract container ID from path like "docker/1234567890abcdef"
                        int dockerIndex = path.IndexOf("/docker/");
                        if (dockerIndex >= 0)
                        {
                            string containerPart = path.Substring(dockerIndex + 8); // Skip "/docker/"
                            // Container ID is typically 64 characters, but can be shorter
                            int slashIndex = containerPart.IndexOf('/');
                            if (slashIndex > 0) return containerPart.Substring(0, slashIndex);
                            return containerPart;
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static bool IsCgroupV2()
    {
        // Check if cgroup v2 is mounted
        return File.Exists("/sys/fs/cgroup/cgroup.controllers");
    }

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupV2Paths(string? containerId)
    {
        string? cpuUsagePath = null;
        string? cpuQuotaPath = null;
        string? cpuPeriodPath = null;

        // Try different possible paths for cgroup v2
        string[] possibleBasePaths = {
            "/sys/fs/cgroup",
            $"/sys/fs/cgroup/docker/{containerId}",
            $"/sys/fs/cgroup/system.slice/docker-{containerId}.scope"
        };

        foreach (string basePath in possibleBasePaths)
        {
            if (Directory.Exists(basePath))
            {
                string cpuStatPath = Path.Combine(basePath, "cpu.stat");
                string cpuMaxPath = Path.Combine(basePath, "cpu.max");

                if (File.Exists(cpuStatPath) && File.Exists(cpuMaxPath))
                {
                    cpuUsagePath = cpuStatPath;
                    cpuQuotaPath = cpuMaxPath;
                    cpuPeriodPath = cpuMaxPath; // Same file for v2
                    break;
                }
            }
        }

        return (cpuUsagePath, cpuQuotaPath, cpuPeriodPath, true, containerId);
    }

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupV1Paths(string? containerId)
    {
        string? cpuUsagePath = null;
        string? cpuQuotaPath = null;
        string? cpuPeriodPath = null;

        // Try different possible paths for cgroup v1
        string[] possibleBasePaths = {
            "/sys/fs/cgroup/cpuacct",
            $"/sys/fs/cgroup/cpuacct/docker/{containerId}",
            $"/sys/fs/cgroup/cpuacct/system.slice/docker-{containerId}.scope"
        };

        string[] possibleCpuBasePaths = {
            "/sys/fs/cgroup/cpu",
            $"/sys/fs/cgroup/cpu/docker/{containerId}",
            $"/sys/fs/cgroup/cpu/system.slice/docker-{containerId}.scope"
        };

        // Find CPU usage path
        foreach (string basePath in possibleBasePaths)
        {
            string usagePath = Path.Combine(basePath, "cpuacct.usage");
            if (File.Exists(usagePath))
            {
                cpuUsagePath = usagePath;
                break;
            }
        }

        // Find CPU quota and period paths
        foreach (string basePath in possibleCpuBasePaths)
        {
            string quotaPath = Path.Combine(basePath, "cpu.cfs_quota_us");
            string periodPath = Path.Combine(basePath, "cpu.cfs_period_us");

            if (File.Exists(quotaPath) && File.Exists(periodPath))
            {
                cpuQuotaPath = quotaPath;
                cpuPeriodPath = periodPath;
                break;
            }
        }

        return (cpuUsagePath, cpuQuotaPath, cpuPeriodPath, false, containerId);
    }
}