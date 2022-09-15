using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace AmbientServices
{
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
    /// A class that monitors process CPU utilization.
    /// </summary>
    public class CpuMonitor
    {
        private static readonly AmbientService<IMockCpuUsage> _MockCpu = Ambient.GetService<IMockCpuUsage>();

        private readonly long _updateWindowTicks;
        private long _wallTicksAtWindowStart;
        private long _cpuTicksAtWindowStart;
        private volatile float _averageCpuUsageLastWindow;

        /// <summary>
        /// Constructs a system CPU usage monitor.
        /// </summary>
        /// <param name="windowMilliseconds">The minimum number of milliseconds to wait before computing a new usage metric (more frequent queries will return the same value).</param>
        /// <param name="mock">An optional <see cref="IMockCpuUsage"/> to use to override actual system CPU usage measurements.</param>
        public CpuMonitor(long windowMilliseconds = 250, IMockCpuUsage? mock = null)
        {
            _updateWindowTicks = TimeSpan.FromMilliseconds(windowMilliseconds).Ticks;
            _wallTicksAtWindowStart = DateTime.UtcNow.Ticks - _updateWindowTicks;
            _cpuTicksAtWindowStart =
#if NET5_0_OR_GREATER
                OperatingSystem.IsBrowser() ? 0 :
#endif
                Process.GetCurrentProcess().TotalProcessorTime.Ticks;
        }
        /// <summary>
        /// Constructs a system CPU usage monitor.
        /// </summary>
        /// <param name="windowSize">A <see cref="TimeSpan"/> indicating the minimum sampling window size.</param>
        /// <param name="mock">An optional <see cref="IMockCpuUsage"/> to use to override actual system CPU usage measurements.</param>
        public CpuMonitor(TimeSpan windowSize, IMockCpuUsage? mock = null) : this((long)windowSize.TotalMilliseconds, mock)
        {
        }

        /// <summary>
        /// Gets the proportion of time the CPU was in use (average across all CPUs) in the previous measurement window, which will be at least the minimum window specified in the constructor.
        /// </summary>
        public float RecentUsage => _MockCpu.Local?.RecentUsage ?? RecentCpuUsage();

        private float RecentCpuUsage()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long lastWallTicksAtWindowStart = Interlocked.Read(ref _wallTicksAtWindowStart);
            // time to update and we won the race?
            if (nowTicks - lastWallTicksAtWindowStart > _updateWindowTicks && Interlocked.CompareExchange(ref _wallTicksAtWindowStart, nowTicks, lastWallTicksAtWindowStart) == lastWallTicksAtWindowStart)
            {
                long oldWallTicks = lastWallTicksAtWindowStart;
                long nowCpuTicks =
#if NET5_0_OR_GREATER
                    OperatingSystem.IsBrowser() ? 0 :
#endif
                    Process.GetCurrentProcess().TotalProcessorTime.Ticks;
                long oldCpuTicks = Interlocked.Exchange(ref _cpuTicksAtWindowStart, nowCpuTicks);
                long wallTicks = nowTicks - oldWallTicks;
                long cpuTicks =
#if NET5_0_OR_GREATER
                    OperatingSystem.IsBrowser() ? wallTicks * Environment.ProcessorCount * 3 / 4 :       // for now, hardcode CPU usage at 75% for browsers
#endif
                    nowCpuTicks - oldCpuTicks;
                Interlocked.Exchange(ref _averageCpuUsageLastWindow, cpuTicks * 1.0f / wallTicks / Environment.ProcessorCount);
            }
            // else either no need to update or we lost the race, so just use the most recently computed value
            return _averageCpuUsageLastWindow;
        }
    }
}
