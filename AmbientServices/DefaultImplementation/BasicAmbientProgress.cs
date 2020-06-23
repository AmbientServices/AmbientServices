using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AmbientServices
{
    [DefaultAmbientService]
    class BasicAmbientProgress : IAmbientProgress
    {
        private readonly AsyncLocal<Progress> _topProgress;
        private readonly AsyncLocal<SubProgress> _subProgress;

        public BasicAmbientProgress()
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
    class Progress : IProgress
    {
        private readonly CancellationToken _cancel;
        private readonly BasicAmbientProgress _tracker;
        private float _portionComplete;
        private string _itemCurrentlyBeingProcessed = "";

        public Progress(BasicAmbientProgress tracker, CancellationToken cancel = default(CancellationToken))
        {
            _tracker = tracker;
            _cancel = cancel;
        }
        public CancellationToken CancellationToken
        {
            get { return _cancel; }
        }
        public float PortionComplete
        {
            get { return _portionComplete; }
        }
        public string ItemCurrentlyBeingProcessed
        {
            get { return _itemCurrentlyBeingProcessed; }
        }
        public void Update(float portionComplete, string itemCurrentlyBeingProcessed = null)
        {
            if (portionComplete < 0.0 || portionComplete > 1.0) throw new ArgumentOutOfRangeException("portionComplete", "The portion complete must be between 0.0 and 1.0, inclusive!");
            System.Threading.Interlocked.Exchange(ref _portionComplete, portionComplete);
            if (itemCurrentlyBeingProcessed != null) System.Threading.Interlocked.Exchange(ref _itemCurrentlyBeingProcessed, itemCurrentlyBeingProcessed);
        }
        public IProgress TrackPart(float startPortion, float portionPart, string prefix = null, CancellationToken cancel = default(CancellationToken))
        {
            _portionComplete = startPortion;
            return new SubProgress(_tracker, this, startPortion, portionPart, prefix, cancel);
        }
        public void Dispose()
        {
            // nothing to do in this case
        }
    }
    class SubProgress : IProgress
    {
        private CancellationToken _cancel;
        private readonly BasicAmbientProgress _tracker;
        private readonly IProgress _parentProgress;
        private readonly string _prefix;
        private float _startPortion;
        private float _portionPart;
        private float _portionComplete;
        private string _currentItem;

        public SubProgress(BasicAmbientProgress tracker, IProgress parentProgress, float startPortion, float portionPart, string prefix = null, CancellationToken cancel = default(CancellationToken))
        {
            _tracker = tracker;
            _prefix = prefix ?? "";
            if (startPortion < 0.0 || startPortion > 1.0) throw new ArgumentOutOfRangeException("startPortion", "The start portion must be between 0.0 and 1.0, inclusive!");
            if (portionPart < 0.0 || portionPart > 1.0) throw new ArgumentOutOfRangeException("portionPart", "The portion part must be between 0.0 and 1.0, inclusive!");
            if (startPortion + portionPart > 1.0) throw new ArgumentOutOfRangeException("portionPart", "The sum of the start portion and portion part must be 1.0 or less!");
            _parentProgress = parentProgress;
            _startPortion = startPortion;
            _portionPart = portionPart;
            _cancel = cancel;
            tracker.PushSubProgress(this);
        }

        public CancellationToken CancellationToken
        {
            get { return _cancel; }
        }
        public float PortionComplete
        {
            get => _portionComplete;
        }
        public string ItemCurrentlyBeingProcessed
        {
            get => _currentItem;
        }
        public void Update(float portionComplete, string itemCurrentlyBeingProcessed = null)
        {
            if (portionComplete < 0.0 || portionComplete > 1.0) throw new ArgumentOutOfRangeException("portionComplete", "The portion complete must be between 0.0 and 1.0, inclusive!");
            System.Threading.Interlocked.Exchange(ref _portionComplete, portionComplete);
            System.Threading.Interlocked.Exchange(ref _currentItem, itemCurrentlyBeingProcessed);
            _parentProgress.Update(_startPortion + _portionPart * portionComplete, _prefix + itemCurrentlyBeingProcessed);
        }
        public IProgress TrackPart(float startPortion, float portionPart, string prefix = null, CancellationToken cancel = default(CancellationToken))
        {
            _parentProgress.Update(startPortion);
            return new SubProgress(_tracker, this, startPortion, portionPart, _prefix + prefix, cancel);
        }
        public void Dispose()
        {
            if (_tracker.Progress != this) throw new InvalidOperationException("The SubProgress object stack is corrupt!");
            _tracker.PopSubProgress();
            // tell the parent that we're done
            _parentProgress.Update(_startPortion + _portionPart);
        }

        internal IProgress Parent { get { return _parentProgress; } }
    }
    /// <summary>
    /// A static class that holds extension methods for <see cref="IProgress"/>.
    /// </summary>
    public static class IProgressExtensions
    {
        /// <summary>
        /// Gets the <see cref="CancellationToken"/> from the specified <see cref="IProgress"/> if it is not null, or the default <see cref="CancellationToken"/> if the specified <see cref="IProgress"/> is null.
        /// </summary>
        /// <param name="progress">The <see cref="IProgress"/> to check, or null if there is no <see cref="IProgress"/>.</param>
        /// <returns>A <see cref="CancellationToken"/> from the <see cref="IProgress"/>, or the default <see cref="CancellationToken"/> if the <see cref="IProgress"/> is null.</returns>
        public static CancellationToken GetCancellationTokenOrDefault(this IProgress progress)
        {
            return (progress == null) ? default(CancellationToken) : progress.CancellationToken;
        }
    }
}
