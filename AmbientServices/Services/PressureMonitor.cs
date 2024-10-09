using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AmbientServices;

#pragma warning disable CA1510
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
    private static readonly AmbientService<IAmbientStatistics> AmbientStatistics = Ambient.GetService<IAmbientStatistics>();
    private const int PressureRecalculateFrequencyMilliseconds = 1000;
    private const short FixedFloatingPointDigits = 8;
    private static readonly long MaxValue = (long)(1.00f * Math.Pow(10, FixedFloatingPointDigits));
    private static readonly long NeutralValue = (long)(0.89f * Math.Pow(10, FixedFloatingPointDigits));
    private static readonly PressureMonitor _Default = new();

    /// <summary>
    /// Gets the default system pressure monitor.
    /// </summary>
    public static PressureMonitor Default => _Default;

    private readonly AmbientCallbackTimer _timer;
    private readonly IAmbientStatistic? _internalPressureStat = AmbientStatistics.Local?.GetOrAddStatistic(false, nameof(PressureMonitor) + "-Internal", "Internal Pressure", "The pressure level for internal attributes of this system", false, "p", NeutralValue, 0, MaxValue, FixedFloatingPointDigits,  AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.MostRecent, AggregationTypes.Average, MissingSampleHandling.LinearEstimation);
    private readonly IAmbientStatistic? _externalPressureStat = AmbientStatistics.Local?.GetOrAddStatistic(false, nameof(PressureMonitor) + "-External", "External Pressure", "The pressure level for external systems as seen by this system", false, "p", NeutralValue, 0, MaxValue, FixedFloatingPointDigits, AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.MostRecent, AggregationTypes.Average, MissingSampleHandling.LinearEstimation);
    private readonly IAmbientStatistic? _overallPressureStat = AmbientStatistics.Local?.GetOrAddStatistic(false, nameof(PressureMonitor) + "-Overall", "Overall Pressure", "The overall pressure level for this system", false, "p", NeutralValue, 0, MaxValue, FixedFloatingPointDigits,AggregationTypes.Average | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.Average | AggregationTypes.Sum | AggregationTypes.Max | AggregationTypes.MostRecent, AggregationTypes.MostRecent, AggregationTypes.Average, MissingSampleHandling.LinearEstimation);

    private float _internalPressure;
    private float _externalPressure;
    private float _overallPressure;
    private bool _disposed;

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
    /// <param name="frequency">The frequency to recompute pressure.</param>
    public PressureMonitor(TimeSpan frequency)
    {
        _timer = new(OnTimerCallback, null, frequency, frequency);
    }
    /// <summary>
    /// Constructs a new pressure monitor with the specified frequency.
    /// </summary>
    /// <param name="frequencyMilliseconds">The frequency to recompute pressure, in milliseconds.</param>
    public PressureMonitor(int? frequencyMilliseconds = null) : this(TimeSpan.FromMilliseconds(frequencyMilliseconds ?? PressureRecalculateFrequencyMilliseconds))
    {
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
        _internalPressureStat?.SetValue(internalPressure);

        float externalPressure = 0;
        foreach (IPressurePoint pp in ExternalPressurePoints.List)
        {
            float pressure = pp.Pressure;
            externalPressure = Max(externalPressure, pressure);
        }
        Interlocked.Exchange(ref _externalPressure, externalPressure);
        _externalPressureStat?.SetValue(externalPressure);

        float overallPressure = Math.Min(1.0f, Max(0.0f, internalPressure, externalPressure));
        Interlocked.Exchange(ref _overallPressure, overallPressure);
        _overallPressureStat?.SetValue(overallPressure);
    }
    /// <summary>
    /// Gets the maximum of all the specified values.
    /// </summary>
    /// <param name="items">A variable-length array of floating point numbers.</param>
    /// <returns>The highest of all the specified floating point numbers.</returns>
    public static float Max(params float[] items)
    {
        return Max((IEnumerable<float>)items);
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
        if (!_disposed)
        {
            if (disposing)
            {
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _timer.Dispose();
                _internalPressureStat?.Dispose();
                _externalPressureStat?.Dispose();
                _overallPressureStat?.Dispose();
            }
            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposed = true;
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
