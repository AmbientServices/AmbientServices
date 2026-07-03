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
/// <remarks>
/// <pitch>The test seam for CPU-dependent logic: register an implementation as the ambient service and <see cref="CpuMonitor.RecentUsage"/> reports your scripted value instead of real CPU usage, making load-dependent branches deterministically testable.</pitch>
/// <pledge><see cref="RecentUsage"/> returns the value to report as the most recent CPU usage, between 0.0 and 1.0; it may be read at any time and from any thread.</pledge>
/// </remarks>
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
/// <remarks>
/// <pitch>The environment-specific strategy behind <see cref="CpuMonitor"/> — one realization per way of measuring CPU (standard process time, Linux cgroup quotas).</pitch>
/// <pledge><c>Sample</c> is called periodically to close a measurement window; <c>GetUsage</c> returns the utilization (0.0–1.0) of the last closed window and <c>GetPendingUsage</c> the utilization since the window was closed, both readable at any time.</pledge>
/// </remarks>
internal interface ICpuSampler
{
    void Sample();
    float GetUsage();
    float GetPendingUsage();
}

/// <summary>
/// A class that monitors process CPU utilization.
/// </summary>
/// <remarks>
/// <pitch>Continuous, low-overhead CPU utilization for this process — a windowed average suitable for throttling decisions and pressure reporting, with container awareness (measured against the cgroup CPU quota on Linux) and an ambient mock hook for testing.</pitch>
/// <pledge>
/// <see cref="RecentUsage"/> reports the average utilization (0.0–1.0, across all processors or the container quota) over the last completed sampling window, so it is stable within a window and at least one window stale; <see cref="PendingUsage"/> reports the average since the current window began.  Both are readable from any thread at any time.
/// When an ambient <see cref="IMockCpuUsage"/> is registered, <see cref="RecentUsage"/> returns its value instead of a measurement.  Disposal stops sampling.
/// </pledge>
/// <plan>
/// An <see cref="AmbientEventTimer"/> fires at the construction-time window size (default 250ms) and tells the sampler to close its window.  The sampler is chosen once at construction: <see cref="LinuxContainerCpuSampler"/> on Linux (reads cgroup v1/v2 usage and quota files so containerized readings reflect the container's actual CPU allowance) and <see cref="StandardCpuSampler"/> elsewhere (compares <see cref="Process.TotalProcessorTime"/> deltas against wall-clock time from <see cref="Stopwatch"/> timestamps, normalized by processor count).  Cost is one sample per window regardless of reader count.
/// </plan>
/// </remarks>
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
/// <remarks>
/// <pitch>An immutable point-in-time pairing of wall-clock and process-CPU timestamps; utilization is only meaningful as the ratio of the deltas between two samples.</pitch>
/// <pledge><see cref="CpuUtilization"/> of two samples yields the process's average utilization across all processors over the intervening span, clamped to 0.0–1.0; in environments with no process information (browser), samples degrade to zero CPU time and utilization reports 0.</pledge>
/// </remarks>
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

/// <summary>
/// The default <see cref="ICpuSampler"/>, measuring process CPU time against wall-clock time.
/// </summary>
/// <remarks>
/// <pitch>The sampler for ordinary (non-cgroup-limited) environments: accurate process utilization relative to the whole machine's processors.</pitch>
/// <pledge><see cref="ICpuSampler"/></pledge>
/// <plan>Keeps the last <see cref="CpuSample"/> and computes utilization as the CPU-time delta over the wall-clock delta (normalized by processor count) — two timestamp reads per sample, no OS counters or files.</plan>
/// </remarks>
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

/// <summary>
/// An <see cref="ICpuSampler"/> that measures CPU usage against the Linux cgroup CPU quota, so containerized processes see utilization relative to their actual allowance.
/// </summary>
/// <remarks>
/// <pitch>The sampler for Linux containers: a pod limited to half a CPU reads 100% when it uses its whole allowance, instead of the near-zero number machine-relative measurement would report.</pitch>
/// <pledge><see cref="ICpuSampler"/></pledge>
/// <pledge>When cgroup usage or quota information is unavailable (no quota set, files missing, or parse failures), usage reports 0 rather than failing — this sampler is only meaningful where a CPU quota is enforced.</pledge>
/// <plan>
/// At construction it discovers the cgroup layout once: detects v2 versus v1 (presence of <c>cgroup.controllers</c>), extracts the docker container id from <c>/proc/self/cgroup</c> when present, and probes the standard candidate directories for the usage file (<c>cpu.stat</c> v2 / <c>cpuacct.usage</c> v1) and the quota files (<c>cpu.max</c> v2 / <c>cpu.cfs_quota_us</c>+<c>cpu.cfs_period_us</c> v1).  An optional root-prefix parameter redirects all paths into a mirrored directory tree for tests.
/// Each sample reads cumulative CPU nanoseconds and computes utilization as the usage delta over (quota-fraction × elapsed <see cref="DateTime.UtcNow"/> time), clamped to 0.0–1.0.  All file reads are per-sample (cheap at the default 250ms window) and failure-swallowing.
/// </plan>
/// </remarks>
internal sealed class LinuxContainerCpuSampler : ICpuSampler
{
    private static readonly char[] SlashCharacterArray = ['/'];

    private long? _lastUsage;
    private DateTime? _lastSampleTime;
    private float _lastUsagePercent;

    private readonly string? _cpuUsagePath;
    private readonly string? _cpuQuotaPath;
    private readonly string? _cpuPeriodPath;
    private readonly bool _isCgroupV2;

    /// <summary>
    /// Constructs a cgroup-based CPU sampler. For production on Linux, use the parameterless form so paths resolve to real <c>/proc</c> and <c>/sys/fs/cgroup</c>.
    /// </summary>
    /// <param name="cgroupFilesystemRoot">Optional directory that mirrors the root filesystem layout (e.g. contains <c>proc/self/cgroup</c> and <c>sys/fs/cgroup</c>) for tests; null or empty uses real absolute paths.</param>
    internal LinuxContainerCpuSampler(string? cgroupFilesystemRoot = null)
    {
        (_cpuUsagePath, _cpuQuotaPath, _cpuPeriodPath, _isCgroupV2, _) = DiscoverCgroupPaths(cgroupFilesystemRoot);
    }

    public void Sample()
    {
        long? usage = GetCgroupCpuUsage();
        double? limit = GetCgroupCpuLimit();
        DateTime now = AmbientClock.UtcNow;

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
        DateTime now = AmbientClock.UtcNow;

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

    private long? GetCgroupCpuUsage()
    {
        if (_cpuUsagePath == null) return null;

        long? ret = null;
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
                        if (long.TryParse(usageStr, out long usageUsec))
                        {
                            return usageUsec * 1000; // Convert to nanoseconds
                        }
                    }
                }
            }
            else
            {
                // cgroup v1 format: direct file read
                ret = long.Parse(File.ReadAllText(_cpuUsagePath), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch { }

        return ret;
    }

    private double? GetCgroupCpuLimit()
    {
        if (_cpuQuotaPath == null || _cpuPeriodPath == null) return null;

        double? ret = null;
        try
        {
            if (_isCgroupV2)
            {
                // cgroup v2 format: read from cpu.max
                string content = File.ReadAllText(_cpuQuotaPath).Trim();
                string[] parts = content.Split(' ');
                if (parts.Length == 2 && long.TryParse(parts[0], out long quota) && long.TryParse(parts[1], out long period))
                {
                    if (quota > 0 && period > 0) ret = (double)quota / period;
                }
            }
            else
            {
                // cgroup v1 format: read from separate quota and period files
                long quota = long.Parse(File.ReadAllText(_cpuQuotaPath), System.Globalization.CultureInfo.InvariantCulture);
                long period = long.Parse(File.ReadAllText(_cpuPeriodPath), System.Globalization.CultureInfo.InvariantCulture);
                if (quota > 0 && period > 0) ret = (double)quota / period;
            }
        }
        catch { }

        return ret;
    }

    private static string ResolvePath(string? rootPrefix, string absoluteUnixPath)
    {
        if (rootPrefix == null || string.IsNullOrEmpty(rootPrefix)) return absoluteUnixPath;
        string trimmed = absoluteUnixPath.TrimStart('/');
        if (trimmed.Length == 0) return Path.GetFullPath(rootPrefix);
        string combined = rootPrefix;
        foreach (string segment in trimmed.Split(SlashCharacterArray, StringSplitOptions.RemoveEmptyEntries))
        {
            combined = Path.Combine(combined, segment);
        }
        return Path.GetFullPath(combined);
    }

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupPaths(string? cgroupFilesystemRoot)
    {
        string? containerId = GetContainerId(cgroupFilesystemRoot);
        bool isCgroupV2 = IsCgroupV2(cgroupFilesystemRoot);

        if (isCgroupV2)
        {
            return DiscoverCgroupV2Paths(containerId, cgroupFilesystemRoot);
        }
        else
        {
            return DiscoverCgroupV1Paths(containerId, cgroupFilesystemRoot);
        }
    }

    private static string? GetContainerId(string? cgroupFilesystemRoot)
    {
        try
        {
            // Read container ID from /proc/self/cgroup
            string[] lines = File.ReadAllLines(ResolvePath(cgroupFilesystemRoot, "/proc/self/cgroup"));
            foreach (string line in lines)
            {
                // Look for docker container ID in the path
#if NETSTANDARD2_1 || NETCOREAPP || NET5_0_OR_GREATER
                if (line.Contains("docker", StringComparison.Ordinal))
#else
                if (line.Contains("docker"))
#endif
                {
                    string[] parts = line.Split(':');
                    if (parts.Length >= 3)
                    {
                        string path = parts[2];
                        // Extract container ID from path like "docker/1234567890abcdef"
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
                        int dockerIndex = path.IndexOf("/docker/", StringComparison.Ordinal);
#else
                        int dockerIndex = path.IndexOf("/docker/");
#endif
                        if (dockerIndex >= 0)
                        {
                            string containerPart = path.Substring(dockerIndex + 8); // Skip "/docker/"
                            // Container ID is typically 64 characters, but can be shorter
#if NETSTANDARD2_1 || NETCOREAPP || NET5_0_OR_GREATER
                            int slashIndex = containerPart.IndexOf('/', StringComparison.Ordinal);
#else
                            int slashIndex = containerPart.IndexOf('/');
#endif
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

    private static bool IsCgroupV2(string? cgroupFilesystemRoot)
    {
        // Check if cgroup v2 is mounted
        return File.Exists(ResolvePath(cgroupFilesystemRoot, "/sys/fs/cgroup/cgroup.controllers"));
    }

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupV2Paths(string? containerId, string? cgroupFilesystemRoot)
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
            string resolvedBase = ResolvePath(cgroupFilesystemRoot, basePath);
            if (Directory.Exists(resolvedBase))
            {
                string cpuStatPath = Path.Combine(resolvedBase, "cpu.stat");
                string cpuMaxPath = Path.Combine(resolvedBase, "cpu.max");

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

    private static (string? cpuUsagePath, string? cpuQuotaPath, string? cpuPeriodPath, bool isCgroupV2, string? containerId) DiscoverCgroupV1Paths(string? containerId, string? cgroupFilesystemRoot)
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
            string resolvedBase = ResolvePath(cgroupFilesystemRoot, basePath);
            string usagePath = Path.Combine(resolvedBase, "cpuacct.usage");
            if (File.Exists(usagePath))
            {
                cpuUsagePath = usagePath;
                break;
            }
        }

        // Find CPU quota and period paths
        foreach (string basePath in possibleCpuBasePaths)
        {
            string resolvedBase = ResolvePath(cgroupFilesystemRoot, basePath);
            string quotaPath = Path.Combine(resolvedBase, "cpu.cfs_quota_us");
            string periodPath = Path.Combine(resolvedBase, "cpu.cfs_period_us");

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