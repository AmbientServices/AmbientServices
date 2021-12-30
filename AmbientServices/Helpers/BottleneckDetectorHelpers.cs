using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace AmbientServices
{
    /// <summary>
    /// A class that contains immutable information about a bottleneck, something that could potentially limit performance as system use intensifies.
    /// </summary>
    public class AmbientBottleneck
    {
        private static readonly AmbientService<IAmbientBottleneckDetector> _BottleneckDetector = Ambient.GetService<IAmbientBottleneckDetector>(out _BottleneckDetector);
        /// <summary>
        /// The identifier for the bottleneck.  The identifier is used in combination with regular expressions to filter which bottlenecks are tracked and analyzed.
        /// </summary>
        /// <remarks>
        /// The identifier string is dash-delimited and starts with the most generic classification and progresses down to ID of the specific bottleneck.
        /// </remarks>
        public string Id { get; private set; }
        /// <summary>
        /// A <see cref="AmbientBottleneckUtilizationAlgorithm"/> indicating the type of bottleneck.
        /// </summary>
        public AmbientBottleneckUtilizationAlgorithm UtilizationAlgorithm { get; private set; }
        /// <summary>
        /// Whether or not this bottleneck is measured automatically using elapsed time (in units of stopwatch ticks).
        /// If not automatic, no usage will be recorded unless either <see cref="AmbientBottleneckAccessor.SetUsage"/> or <see cref="AmbientBottleneckAccessor.AddUsage"/> is called.
        /// </summary>
        public bool Automatic { get; private set; }
        /// <summary>
        /// A human-readable description of the bottleneck.
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// The limit that is enforced, if any.  See <see cref="AmbientBottleneckUtilizationAlgorithm"/> for what this value means.
        /// For automatic bottlenecks, the limit is in terms of stopwatch ticks.
        /// </summary>
        public double? Limit { get; private set; }
        /// <summary>
        /// A <see cref="TimeSpan"/> indicating the period in which the limit is enforced, if any.
        /// </summary>
        public TimeSpan? LimitPeriod { get; private set; }
        /// <summary>
        /// Constructs an AmbientBottleneck with the specified properties.
        /// </summary>
        /// <param name="id">The string identifier for the bottleneck.</param>
        /// <param name="utilizationAlgorithm">An <see cref="AmbientBottleneckUtilizationAlgorithm"/> indicating the algorithm to use to calculate the utilization.</param>
        /// <param name="automatic">Whether or not the bottleneck's usage is measured automaticall or using <see cref="AmbientBottleneckAccessor.SetUsage"/> or <see cref="AmbientBottleneckAccessor.AddUsage"/>.</param>
        /// <param name="description">A description of the bottleneck.</param>
        /// <param name="limit">An optional limit (for automatic bottlenecks, in units of stopwatch ticks).</param>
        /// <param name="limitPeriod">A <see cref="TimeSpan"/> indicating the period during which the limit is applied.</param>
        public AmbientBottleneck(string id, AmbientBottleneckUtilizationAlgorithm utilizationAlgorithm, bool automatic, string description, double? limit = null, TimeSpan? limitPeriod = null)
        {
            if (limit <= 0.0) throw new ArgumentOutOfRangeException(nameof(limit), "The bottleneck limit must be null or positive!");
            if (limitPeriod?.Ticks <= 0.0) throw new ArgumentOutOfRangeException(nameof(limitPeriod), "The bottleneck limitPeriod must be null or positive!");
            Id = id;
            UtilizationAlgorithm = utilizationAlgorithm;
            Automatic = automatic;
            Description = description;
            Limit = limit;
            LimitPeriod = limitPeriod;
        }
        /// <summary>
        /// Enters the bottleneck, if there is a configured detector.
        /// </summary>
        /// <returns>An <see cref="AmbientBottleneckAccessor"/> that should be disposed when exiting the bottleneck, or null, if there is no ambient bottleneck detector.</returns>
        public AmbientBottleneckAccessor? EnterBottleneck()
        {
            return _BottleneckDetector.Local?.EnterBottleneck(this);
        }
        /// <summary>
        /// Gets a string that represents this object.
        /// </summary>
        /// <returns>A string that represents this object.</returns>
        public override string ToString()
        {
            return Id;
        }
    }
    /// <summary>
    /// An interface that abstracts a survey of bottleneck statistics.
    /// </summary>
    public interface IAmbientBottleneckSurvey
    {
        /// <summary>
        /// Gets the name of the scope that was surveyed.
        /// </summary>
        string ScopeName { get; }
        /// <summary>
        /// Gets the <see cref="AmbientBottleneckAccessor"/> that was utilized the most, if any were used.
        /// </summary>
        AmbientBottleneckAccessor? MostUtilizedBottleneck { get; }
        /// <summary>
        /// Gets the most used <see cref="AmbientBottleneckAccessor"/> within the scope.
        /// </summary>
        /// <param name="count">The number of top limits to get.  Due to the potentially-large number of bottlenecks and the fact that this function sorts them before returning results, large values for this parameter are not recommended.</param>
        /// <returns>An enumeration of <see cref="AmbientBottleneckAccessor"/>s for the most utilized bottlenecks.</returns>
        IEnumerable<AmbientBottleneckAccessor> GetMostUtilizedBottlenecks(int count);
    }
    /// <summary>
    /// An interface that combines <see cref="IAmbientBottleneckSurvey"/> and <see cref="IDisposable"/> in order to scope the duration of the survey.
    /// </summary>
    public interface IAmbientBottleneckSurveyor : IAmbientBottleneckSurvey, IDisposable
    {
    }
    /// <summary>
    /// A class that manages bottleneck surveyors.
    /// </summary>
    public class AmbientBottleneckSurveyorCoordinator : IDisposable
    {
        private static readonly AmbientService<IAmbientSettingsSet> _SettingsSet = Ambient.GetService<IAmbientSettingsSet>();
        private static readonly AmbientService<IAmbientBottleneckDetector> _AmbientBottleneckDetector = Ambient.GetService<IAmbientBottleneckDetector>();

        private readonly IAmbientSetting<Regex?> _defaultAllowSetting;
        private readonly IAmbientSetting<Regex?> _defaultBlockSetting;
        private readonly IAmbientBottleneckDetector? _bottleneckDetector;
        private readonly CallContextSurveyManager _callContextSurveyor;
        private readonly ThreadSurveyManager _threadSurveyor;
        private bool _disposedValue;

        /// <summary>
        /// Constructs a AmbientBottleneckTracker using the ambient settings set.
        /// </summary>
        public AmbientBottleneckSurveyorCoordinator()
            : this(_SettingsSet.Local)
        {
        }
        /// <summary>
        /// Constructs a AmbientBottleneckTracker using the specified settings set.
        /// </summary>
        /// <param name="settingsSet">An <see cref="IAmbientSettingsSet"/> to get settings from.</param>
        public AmbientBottleneckSurveyorCoordinator(IAmbientSettingsSet? settingsSet)
        {
            _defaultAllowSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, nameof(AmbientBottleneckSurveyorCoordinator) + "-DefaultAllow",
                @"A `Regex` string used to match bottleneck identifiers that should be tracked.  By default, all bottlenecks are allowed.",
                s => string.IsNullOrEmpty(s) ? (Regex?)null : new Regex(s, RegexOptions.Compiled));
            _defaultBlockSetting = AmbientSettings.GetSetting<Regex?>(settingsSet, nameof(AmbientBottleneckSurveyorCoordinator) + "-DefaultBlock",
                @"A `Regex` string used to match bottleneck identifiers that should NOT be tracked.  By default, no bottlenecks are blocked.",
                s => string.IsNullOrEmpty(s) ? (Regex?)null : new Regex(s, RegexOptions.Compiled));
            _bottleneckDetector = _AmbientBottleneckDetector.Local;
            _callContextSurveyor = new CallContextSurveyManager(_bottleneckDetector);
            _threadSurveyor = new ThreadSurveyManager(_bottleneckDetector);
        }
        /// <summary>
        /// Creates a call context bottleneck survey.
        /// </summary>
        /// <param name="scopeName">A name of the call context to attach to the analyzer.  Defaults to the name of the calling function.</param>
        /// <param name="overrideAllowRegex">A <see cref="Regex"/> string to override the default allow filter.</param>
        /// <param name="overrideBlockRegex">A <see cref="Regex"/> string to override the default block filter.</param>
        /// <returns>A <see cref="IAmbientBottleneckSurveyor"/> that surveys bottleneck statistics for this call context.  Note that the returned object is NOT thread-safe.</returns>
        public IAmbientBottleneckSurveyor CreateCallContextSurveyor([CallerMemberName] string? scopeName = null, string? overrideAllowRegex = null, string? overrideBlockRegex = null)
        {
            return _callContextSurveyor.CreateCallContextSurveyor(scopeName,
                    (overrideAllowRegex == null) ? _defaultAllowSetting.Value : new Regex(overrideAllowRegex, RegexOptions.Compiled),
                   (overrideBlockRegex == null) ? _defaultBlockSetting.Value : new Regex(overrideBlockRegex, RegexOptions.Compiled)
                );
        }
        /// <summary>
        /// Creates a bottleneck survey generator that generates bottleneck statistics surveys for periodic time windows until the returned <see cref="IDisposable"/> is disposed.
        /// Note that the name of each window's scope is generated automatically.
        /// </summary>
        /// <param name="windowSize">The size of the temporal windows that will be used for contention tracking.</param>
        /// <param name="onWindowComplete">An async delegate that is invoked whenever a time window has ended, making a new survey available.  Note that the surveys returned are not thread-safe.</param>
        /// <param name="overrideAllowRegex">A <see cref="Regex"/> string to override the default allow filter.</param>
        /// <param name="overrideBlockRegex">A <see cref="Regex"/> string to override the default block filter.</param>
        /// <returns>A <see cref="IDisposable"/> that scopes the collection of the surveys.</returns>
        public IDisposable CreateTimeWindowSurveyor(TimeSpan windowSize, Func<IAmbientBottleneckSurvey, Task> onWindowComplete, string? overrideAllowRegex = null, string? overrideBlockRegex = null)
        {
            if (onWindowComplete == null) throw new ArgumentNullException(nameof(onWindowComplete), "Time Window Surveys are pointless without a window completion delegate!");
            Regex? allow = (overrideAllowRegex == null) ? _defaultAllowSetting.Value : new Regex(overrideAllowRegex, RegexOptions.Compiled);
            Regex? block = (overrideBlockRegex == null) ? _defaultBlockSetting.Value : new Regex(overrideBlockRegex, RegexOptions.Compiled);
            return new TimeWindowSurveyManager(windowSize, onWindowComplete, _bottleneckDetector, allow, block);
        }
        /// <summary>
        /// Creates a bottleneck survey which analyzes limit proximities for everything in the process until the process terminates. 
        /// Note that this is only useful to determine the limits for an entire process from beginning to end, which is not useful if the process is very long-lived.
        /// <see cref="CreateTimeWindowSurveyor"/> is a better match in most situations.
        /// </summary>
        /// <param name="processScopeName">The name of the thread scope, or <b>null</b> to automatically build one with the name or ID of the current thread.</param>
        /// <param name="overrideAllowRegex">A <see cref="Regex"/> string to override the default allow filter.</param>
        /// <param name="overrideBlockRegex">A <see cref="Regex"/> string to override the default block filter.</param>
        /// <returns>A <see cref="IAmbientBottleneckSurveyor"/> that surveys bottleneck statistics survey for the entire process.  Note that the returned object is NOT thread-safe.</returns>
        /// <remarks>
        /// This is different from using <see cref="CreateCallContextSurveyor"/> because that will only survey the call context it's called from, 
        /// whereas this will survey all threads and call contexts in the process.  
        /// They will produce the same results only for programs where there is only a single call context (no parallelization).
        /// </remarks>
#if NET5_0_OR_GREATER
        [UnsupportedOSPlatform("browser")]
#endif
        public IAmbientBottleneckSurveyor CreateProcessSurveyor(string? processScopeName = null, string? overrideAllowRegex = null, string? overrideBlockRegex = null)
        {
            ProcessBottleneckSurveyor analyzer = new ProcessBottleneckSurveyor(processScopeName, _bottleneckDetector,
                    (overrideAllowRegex == null) ? _defaultAllowSetting.Value : new Regex(overrideAllowRegex, RegexOptions.Compiled),
                    (overrideBlockRegex == null) ? _defaultBlockSetting.Value : new Regex(overrideBlockRegex, RegexOptions.Compiled)
                );
            return analyzer;
        }
        /// <summary>
        /// Creates a bottleneck survey which tracks limit proximities for everything in the thread until the thread terminates. 
        /// Note that <see cref="CreateTimeWindowSurveyor"/> is a better match in most situations.
        /// </summary>
        /// <param name="threadScopeName">The name of the thread scope, or <b>null</b> to automatically build one with the name or ID of the current thread.</param>
        /// <param name="overrideAllowRegex">A <see cref="Regex"/> string to override the default allow filter.</param>
        /// <param name="overrideBlockRegex">A <see cref="Regex"/> string to override the default block filter.</param>
        /// <returns>A <see cref="IAmbientBottleneckSurveyor"/> that surveys bottleneck statistics survey for the current thread.  Note that the returned object is NOT thread-safe.</returns>
        public IAmbientBottleneckSurveyor CreateThreadSurveyor(string? threadScopeName = null, string? overrideAllowRegex = null, string? overrideBlockRegex = null)
        {
            return _threadSurveyor.CreateThreadSurveyor(threadScopeName,
                    (overrideAllowRegex == null) ? _defaultAllowSetting.Value : new Regex(overrideAllowRegex, RegexOptions.Compiled),
                   (overrideBlockRegex == null) ? _defaultBlockSetting.Value : new Regex(overrideBlockRegex, RegexOptions.Compiled)
                );
        }
        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <param name="disposing">Whether the instance is being disposed (as opposed to finalized).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _callContextSurveyor.Dispose();
                    _threadSurveyor.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~AmbientBottleneckSurveyorCoordinator()
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
}
