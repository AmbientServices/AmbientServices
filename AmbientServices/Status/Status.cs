using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices
{
    /// <summary>
    /// A single-instance class that holds status for the entire system.
    /// </summary>
    public class Status
    {
        internal const string DefaultSource = "LOCALHOST";
        internal const string DefaultTarget = "Unknown Target";

        private readonly static Status _DefaultInstance = new Status();
        /// <summary>
        /// Gets the base instance that contains the overall status.  Note that even the default instance must be started before checks and audits will occur.
        /// </summary>
        public static Status DefaultInstance { get { return _DefaultInstance; } }

        private ConcurrentBag<StatusChecker> _checkers = new ConcurrentBag<StatusChecker>();
        private int _shuttingDown;          // interlocked
        private int _started;               // interlocked

        /// <summary>
        /// Checks to see whether or not we're started shutting down the status system.
        /// </summary>
        internal bool ShuttingDown { get { return _shuttingDown != 0; } }

        /// <summary>
        /// Starts the status system for all <see cref="StatusChecker"/> classes currently loaded and subsequently loaded so that status information may be gathered.
        /// A call to Start must be matched by a call to <see cref="Stop"/> or else disposable items will not be disposed and DEBUG warnings may occur.
        /// Start may only be called once.
        /// </summary>
        /// <param name="cancel">A <see cref="CancellationToken"/> the caller can use to stop the operation before it completes.</param>
        public Task Start(CancellationToken cancel = default(CancellationToken))
        {
            if (System.Threading.Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("The Status system has already been started!");
            // add checkers and auditors from all assemblies subsequently loaded
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
            // add checkers and auditors from all assemblies currently loaded
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                cancel.ThrowIfCancellationRequested();
                // add checkers and auditors from this assembly
                AddCheckersAndAuditors(assembly);
            }
            return Task.CompletedTask;
        }

        private void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            AddCheckersAndAuditors(args.LoadedAssembly);
        }

        /// <summary>
        /// Adds checkers and auditors defined in the specified assembly.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> to look in.</param>
        private void AddCheckersAndAuditors(Assembly assembly)
        {
            // does the loaded assembly refer to this one?  if it doesn't, there can't possibly be any of the classes we're looking for
            if (assembly.DoesAssemblyReferToAssembly(System.Reflection.Assembly.GetExecutingAssembly()))
            {
                // loop through all the types looking for types that are not abstract, inherit from StatusNode (directly or indirectly) and have a public empty constructor
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    if (IsTestableStatusCheckerClass(type))
                    {
                        // construct an instance and add it to our list which will be disposed when we stop
                        StatusChecker checker = (StatusChecker)Activator.CreateInstance(type);
                        AddCheckerOrAuditor(checker);
                    }
                }
            }
        }
        /// <summary>
        /// Adds the specified checker.
        /// </summary>
        /// <param name="checker">The <see cref="StatusChecker"/> to add.</param>
        private void AddCheckerOrAuditor(StatusChecker checker)
        {
            _checkers.Add(checker);
        }

        private static float? Rating(StatusResults results)
        {
            if (results == null || results.Report == null || results.Report.Alert == null) return null;
            return results.Report.Alert.Rating;
        }
        internal static int RatingCompare(StatusResults a, StatusResults b)
        {
            float? fa = Rating(a);
            float? fb = Rating(b);
            if (fa == null) return (fb == null) ? 0 : -1;
            return (fb == null) ? 1 : fa.Value.CompareTo(fb.Value);
        }
        /// <summary>
        /// Refreshes the status audits immediately.
        /// </summary>
        public async Task<StatusResults> RefreshAsync(CancellationToken cancel = default(CancellationToken))
        {
            // asynchronously get the status of each system
            IEnumerable<Task<StatusResults>> checkerTasks = _checkers.Select(checker => checker.GetStatus(cancel));
            await Task.WhenAll(checkerTasks).ConfigureAwait(false);
            // sort the results
            List<StatusResults> results = new List<StatusResults>(checkerTasks.Select(t => t.Result));
            results.Sort((a, b) => RatingCompare(a, b));
            StatusResults overallResults = new StatusResults(null, "/", results);
            return overallResults;
        }
        /// <summary>
        /// Gets the <see cref="StatusResults"/> for the entire system.
        /// </summary>
        public StatusResults Results
        {
            get
            {
                DateTime now = AmbientClock.UtcNow;
                List<StatusResults> results = new List<StatusResults>(_checkers.Select(checker => checker.LatestResults));
                results.Sort((a, b) => RatingCompare(a, b));
                StatusResults overallResults = new StatusResults(null, "/", now, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, results);
                return overallResults;
            }
        }
        /// <summary>
        /// Gets the <see cref="StatusAuditAlert"/> containing the full summarized results for the entire system.
        /// </summary>
        public StatusAuditAlert Summary
        {
            get
            {
                DateTime now = AmbientClock.UtcNow;
                List<StatusResults> results = new List<StatusResults>(_checkers.Select(checker => checker.LatestResults));
                results.Sort((a, b) => RatingCompare(a, b));
                StatusResults overallResults = new StatusResults(null, "/", now, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, results);
                StatusAuditAlert alerts = overallResults.GetSummaryAlerts(true, float.MaxValue, false);
                return alerts;
            }
        }
        /// <summary>
        /// Gets the <see cref="StatusAuditAlert"/> containing the summarized alerts and failures for the entire system.
        /// </summary>
        public StatusAuditAlert SummaryAlertsAndFailures
        {
            get
            {
                DateTime now = AmbientClock.UtcNow;
                List<StatusResults> results = new List<StatusResults>(_checkers.Select(checker => checker.LatestResults));
                results.Sort((a, b) => RatingCompare(a, b));
                StatusResults overallResults = new StatusResults(null, "/", now, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, results);
                StatusAuditAlert alerts = overallResults.GetSummaryAlerts(true, StatusRating.Alert, false);
                return alerts;
            }
        }
        /// <summary>
        /// Gets the <see cref="StatusAuditAlert"/> containing the summarized failures for the entire system.
        /// </summary>
        public StatusAuditAlert SummaryFailures
        {
            get
            {
                DateTime now = AmbientClock.UtcNow;
                List<StatusResults> results = new List<StatusResults>(_checkers.Select(checker => checker.LatestResults));
                results.Sort((a, b) => RatingCompare(a, b));
                StatusResults overallResults = new StatusResults(null, "/", now, 0, Array.Empty<StatusProperty>(), StatusNatureOfSystem.ChildrenHeterogenous, results);
                StatusAuditAlert alerts = overallResults.GetSummaryAlerts(true, StatusRating.Fail, false);
                return alerts;
            }
        }
        /// <summary>
        /// Stops the status system by disposing of all the status nodes.
        /// </summary>
        public async Task Stop()
        {
            // make sure everyone can tell we're shutting down
            System.Threading.Interlocked.Exchange(ref _shuttingDown, 1);
            // stop the timers on each node
            foreach (StatusChecker checker in _checkers)
            {
                await checker.BeginStop().ConfigureAwait(false);
            }
            // wait for each one to stop
            foreach (StatusChecker checker in _checkers)
            {
                await checker.FinishStop().ConfigureAwait(false);
            }
            // dispose each one
            foreach (StatusChecker checker in _checkers)
            {
                checker.Dispose();
            }
        }
        /// <summary>
        /// Checks to see if the secified type represents a testable status checker class (ie. one with a public constructor that takes no parameters).
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>true if the specified type is a testable status checker class.</returns>
        public static bool IsTestableStatusCheckerClass(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return !type.IsAbstract && typeof(StatusChecker).IsAssignableFrom(type) && type.GetConstructor(Array.Empty<Type>()) != null;
        }
    }
}
