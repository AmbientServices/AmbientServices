using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    /// <summary>
    /// An interface that tracks progress.  This interface is disposable, so it should be in a using statement.
    /// Implementations that require I/O should do it asynchronously to avoid blocking.
    /// </summary>
    public interface IProgress : IDisposable
    {
        /// <summary>
        /// Gets the portion of the task that is complete.  Must be between zero and one (inclusive).
        /// </summary>
        float PortionComplete { get; }
        /// <summary>
        /// Gets the item currently being processed.
        /// </summary>
        string ItemCurrentlyBeingProcessed { get; }
        /// <summary>
        /// Updates the portion complete and optionally the current item being processed.
        /// </summary>
        /// <param name="portionComplete">A number between 0.0 and 1.0 indicating how much of the operation has been completed.</param>
        /// <param name="itemCurrentlyBeingProcessed">The item currently being processed, null to not update the item being processed, <see cref="string.Empty"/> to clear the item.</param>
        void Update(float portionComplete, string itemCurrentlyBeingProcessed = null);
        /// <summary>
        /// Creates another <see cref="IProgress"/> to track the progress of a part of the process represented by this progress.
        /// </summary>
        /// <param name="startPortion">The portion complete within the process represented by this progress at which the part starts.</param>
        /// <param name="portionPart">The portion delegated to the part.</param>
        /// <param name="prefix">An optional prefix to add at the beginning of the current item for tracking from the returned <see cref="IProgress"/> part.</param>
        /// <returns>A new <see cref="IProgress"/> for the part, which will update the progress here within the specified range.</returns>
        IProgress TrackPart(float startPortion, float portionPart, string prefix = null);
    }
    /// <summary>
    /// An interface that abstracts a progress tracking service.
    /// </summary>
    public interface IProgressTracker
    {
        /// <summary>
        /// Gets the most recent <see cref="IProgress"/> on the current execution thread, or the main progress tracker if called from the main logical execution context and no other trackers have yet been registered.
        /// </summary>
        /// <returns>An <see cref="IProgress"/> tracker to track the progress of the calling process.</returns>
        IProgress Progress { get; }
    }
    class Progress : IProgress
    {
        private readonly DefaultProgressTracker _tracker;
        private float _portionComplete;
        private string _itemCurrentlyBeingProcessed = "";

        public Progress(DefaultProgressTracker tracker)
        {
            _tracker = tracker;
        }
        /// <summary>
        /// Gets (or sets) the portion of the task that is complete.  Must be between zero and one (inclusive).
        /// </summary>
        public float PortionComplete
        {
            get { return _portionComplete; }
        }
        /// <summary>
        /// Gets (or sets) the item currently being processed.
        /// </summary>
        public string ItemCurrentlyBeingProcessed
        {
            get { return _itemCurrentlyBeingProcessed; }
        }
        /// <summary>
        /// Updates the portion complete and optionally the current item being processed.
        /// </summary>
        /// <param name="portionComplete">A number between 0.0 and 1.0 indicating how much of the operation has been completed.</param>
        /// <param name="itemCurrentlyBeingProcessed">The item currently being processed, null to not update the item being processed, <see cref="string.Empty"/> to clear the item.</param>
        public void Update(float portionComplete, string itemCurrentlyBeingProcessed = null)
        {
            if (portionComplete < 0.0 || portionComplete > 1.0) throw new ArgumentOutOfRangeException("portionComplete", "The portion complete must be between 0.0 and 1.0, inclusive!");
            System.Threading.Interlocked.Exchange(ref _portionComplete, portionComplete);
            if (itemCurrentlyBeingProcessed != null) System.Threading.Interlocked.Exchange(ref _itemCurrentlyBeingProcessed, itemCurrentlyBeingProcessed);
        }
        public IProgress TrackPart(float startPortion, float portionPart, string prefix = null)
        {
            return new SubProgress(_tracker, this, startPortion, portionPart, prefix);
        }
        public void Dispose()
        {
            // nothing to do in this case
        }
    }
    class SubProgress : IProgress
    {
        private readonly DefaultProgressTracker _tracker;
        private readonly IProgress _parentProgress;
        private readonly string _prefix;
        private float _startPortion;
        private float _portionPart;

        public SubProgress(DefaultProgressTracker tracker, IProgress parentProgress, float startPortion, float portionPart, string prefix = null)
        {
            _tracker = tracker;
            _prefix = prefix ?? "";
            if (startPortion < 0.0 || startPortion > 1.0) throw new ArgumentOutOfRangeException("startPortion", "The start portion must be between 0.0 and 1.0, inclusive!");
            if (portionPart < 0.0 || portionPart > 1.0) throw new ArgumentOutOfRangeException("portionPart", "The portion part must be between 0.0 and 1.0, inclusive!");
            if (startPortion + portionPart > 1.0) throw new ArgumentOutOfRangeException("portionPart", "The sum of the start portion and portion part must be 1.0 or less!");
            _parentProgress = parentProgress;
            _startPortion = startPortion;
            _portionPart = portionPart;
            tracker.PushSubProgress(this);
        }

        public float PortionComplete
        {
            get => (_parentProgress.PortionComplete - _startPortion) / _portionPart;
        }
        public string ItemCurrentlyBeingProcessed
        {
            get => _parentProgress.ItemCurrentlyBeingProcessed;
        }
        public void Update(float portionComplete, string itemCurrentlyBeingProcessed = null)
        {
            if (portionComplete < 0.0 || portionComplete > 1.0) throw new ArgumentOutOfRangeException("portionComplete", "The portion complete must be between 0.0 and 1.0, inclusive!");
            _parentProgress.Update(_startPortion + _portionPart * portionComplete, _prefix + itemCurrentlyBeingProcessed);
        }
        public IProgress TrackPart(float startPortion, float portionPart, string prefix = null)
        {
            return new SubProgress(_tracker, this, startPortion, portionPart, _prefix + prefix);
        }
        public void Dispose()
        {
            if (_tracker.Progress != this) throw new InvalidOperationException("The SubProgress object stack is corrupt!");
            _tracker.PopSubProgress();
            // tell the parent that we're done
            _parentProgress.Update(_startPortion + _portionPart);
        }

        internal IProgress Parent {  get { return _parentProgress; } }
    }
    [DefaultImplementation]
    class DefaultProgressTracker : IProgressTracker
    {
        private readonly AsyncLocal<Progress> _topProgress;
        private readonly AsyncLocal<SubProgress> _subProgress;

        public DefaultProgressTracker()
        {
            _topProgress = new AsyncLocal<Progress>();
            _subProgress = new AsyncLocal<SubProgress>();
        }

        public IProgress Progress
        {
            get
            {
                IProgress progress = null;
                if (_topProgress.Value == null)
                {
                    System.Diagnostics.Debug.Assert(_subProgress.Value == null);
                    Progress topProgress = new Progress(this);
                    _topProgress.Value = topProgress;
                    progress = topProgress;
                    return progress;
                }
                progress = _topProgress.Value;
                if (_subProgress.Value != null)
                {
                    progress = _subProgress.Value;
                }
                return progress;
            }
        }

        public void PushSubProgress(SubProgress subProgress)
        {
            _subProgress.Value = subProgress;
        }
        public void PopSubProgress()
        {
            SubProgress subProgress = _subProgress.Value;
            IProgress parent = subProgress.Parent;
            _subProgress.Value = parent as SubProgress;
        }
    }
}
