using System;
using System.Threading;

namespace AmbientServices;

[DefaultAmbientService]
internal class BasicAmbientProgress : IAmbientProgressService
{
    private readonly AsyncLocal<Progress?> _progress;

    public BasicAmbientProgress()
    {
        _progress = new AsyncLocal<Progress?>();
    }

    public IAmbientProgress? Progress
    {
        get
        {
            IAmbientProgress? ambientProgress = _progress.Value;
            Progress? topProgress = ambientProgress as Progress;
            // no progress in this context yet, or the context has been disposed (the docs say not to do that, but we have no control over it).
            if (ambientProgress == null || (topProgress?.Disposed ?? false))
            {
                topProgress = new Progress(this, null, 0.0f, 1.0f, null, false);
                _progress.Value = topProgress;
                ambientProgress = topProgress;
            }
            else
            {
                ambientProgress = _progress.Value;
            }
            return ambientProgress;
        }
    }

    public void PushSubProgress(Progress subProgress)
    {
        _progress.Value = subProgress;
    }
    public void PopSubProgress(Progress specified)
    {
        Progress? expected = _progress.Value;
        Progress? pop = expected;
        // are the specified progress and the one at the top of the stack *not* the same?
        if (expected != specified)
        {
            // walk up from both the expected progress and the specified progress to try to find the others
            Progress? tryFindExpected = Progress as Progress;
            Progress? specifiedPopperAncestor = specified;
            Progress? expectedPopperAncestor = tryFindExpected;
            while (specifiedPopperAncestor != null || expectedPopperAncestor != null)
            {
                specifiedPopperAncestor = specifiedPopperAncestor?.Parent as Progress;
                expectedPopperAncestor = expectedPopperAncestor?.Parent as Progress;
                // did we find the expected progress up the chain from the specified one (popping the specified progress late)
                if (specifiedPopperAncestor == tryFindExpected)
                {
                    // in this case, we've *already* popped an ancestor of the specified progress, so there is no need to do anything else (we should have already been disposed)
                    pop = null;
                    // if we add dispose code below, we could also check to see if things are disposed here
                    break;
                }
                // did we find the specified progress up the stack from the expected progress (popping the specified progress early)
                else if (expectedPopperAncestor == specified)
                {
                    // pop everything up to the specified progress
                    pop = specified;
                    // if there were anything to do other than the stack and the progress update (which isn't needed because we're doing the higher one), we might want to loop through all the intermediate items and dispose them
                    break;
                }
                // else just keep walking up the stack
            }
            // pop all the way up the the specified item (if needed)
            if (pop != null) Pop(pop);
            // the best we can do at this point is to just pop like normal and hope that the correct number of pops occur?
            throw new InvalidOperationException("The SubProgress object stack is corrupt!");
        }
        Pop(pop);
    }
    private void Pop(Progress? subProgress)
    {
        IAmbientProgress? parent = subProgress?.Parent;
        _progress.Value = parent as Progress;
    }
}

internal class Progress : IAmbientProgress, IDisposable
{
    private readonly BasicAmbientProgress _tracker;
    private readonly string _prefix;
    private bool _inheritedCancelSource;                        // if we inherited the cancel source, there is no need to dispose of it, as the parent progress is the owner
    private bool _ownCancelSource;
    private readonly float _startPortion;
    private readonly float _portionPart;
    private float _portionComplete;
    private string _currentItem;

    public Progress(BasicAmbientProgress progress)
         : this (progress, null, 0.0f, 1.0f, null, false)
    {
    }
    public Progress(BasicAmbientProgress progressService, IAmbientProgress? parentProgress, float startPortion, float portionPart, string? prefix = null, bool inheritCancellationSource = true)
    {
        _tracker = progressService;
        _prefix = prefix ?? "";
        _currentItem = "";
        if (startPortion < 0.0 || startPortion > 1.0) throw new ArgumentOutOfRangeException(nameof(startPortion), "The start portion must be between 0.0 and 1.0, inclusive!");
        if (portionPart < 0.0 || portionPart > 1.0) throw new ArgumentOutOfRangeException(nameof(portionPart), "The portion part must be between 0.0 and 1.0, inclusive!");
        if (startPortion + portionPart > 1.0) throw new ArgumentOutOfRangeException(nameof(portionPart), "The sum of the start portion and portion part must be 1.0 or less!");
        Parent = parentProgress;
        _startPortion = startPortion;
        _portionPart = portionPart;
        if (_inheritedCancelSource = inheritCancellationSource) // note that this is an ASSIGNMENT in addition to a test
        {
            AmbientCancellationTokenSource? parentCancellationSource = parentProgress?.CancellationTokenSource;
            if (parentCancellationSource != null)
            {
                CancellationTokenSource = parentCancellationSource;
                _ownCancelSource = true;
            }
            else
            {
                CancellationTokenSource = new AmbientCancellationTokenSource();
                _ownCancelSource = true;
            }
        }
        else
        {
            CancellationTokenSource = new AmbientCancellationTokenSource();
            _ownCancelSource = true;
        }
        progressService.PushSubProgress(this);
    }

    public void ResetCancellation(TimeSpan timeout)
    {
        AmbientCancellationTokenSource cancelSource = new(timeout);
        // dispose of any previously-held cancellation token source and swap in the new one
        if (!_ownCancelSource) CancellationTokenSource.Dispose();
        _inheritedCancelSource = false;
        _ownCancelSource = true;
        CancellationTokenSource = cancelSource;
    }
    public void ResetCancellation(CancellationTokenSource? cancellationTokenSource = null)
    {
        AmbientCancellationTokenSource? cancelSource = (cancellationTokenSource == null) ? new AmbientCancellationTokenSource(null) :  new AmbientCancellationTokenSource(cancellationTokenSource);
        // dispose of any previously-held cancellation token source and swap in the new one
        if (!_ownCancelSource) CancellationTokenSource.Dispose();
        _inheritedCancelSource = false;
        _ownCancelSource = true;
        CancellationTokenSource = cancelSource;
    }
    public void ThrowIfCancelled()
    {
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
    }
    public CancellationToken CancellationToken => CancellationTokenSource.Token;
    public AmbientCancellationTokenSource CancellationTokenSource { get; private set; }
    public float PortionComplete => _portionComplete;
    public string ItemCurrentlyBeingProcessed => _currentItem;
    public void Update(float portionComplete, string? itemCurrentlyBeingProcessed = null)
    {
        if (portionComplete < 0.0 || portionComplete > 1.0) throw new ArgumentOutOfRangeException(nameof(portionComplete), "The portion complete must be between 0.0 and 1.0, inclusive!");
        Interlocked.Exchange(ref _portionComplete, portionComplete);
        if (itemCurrentlyBeingProcessed != null) Interlocked.Exchange(ref _currentItem, itemCurrentlyBeingProcessed);
        // have we been canceled?
        if ((!_inheritedCancelSource || Parent == null) && CancellationTokenSource.IsCancellationRequested) CancellationTokenSource.Token.ThrowIfCancellationRequested();
        Parent?.Update(_startPortion + _portionPart * portionComplete, _prefix + itemCurrentlyBeingProcessed ?? "");
    }
    public IDisposable TrackPart(float startPortion, float portionPart, string? prefix = null, bool inheritCancellationTokenSource = false)
    {
        string partPrefix = _prefix + prefix;
        Progress ret = new(_tracker, this, startPortion, portionPart, partPrefix, inheritCancellationTokenSource);
        ret.Update(0.0f);
        return ret;
    }
    public void Dispose()
    {
        if (!Disposed)
        {
            try
            {
                // make sure the parent knows we're done, but prevent throwing a cancellation exception during disposal
                Update(1.0f);
            }
            catch (OperationCanceledException) { }  // ignore these in Dispose!
            _tracker.PopSubProgress(this);
            Disposed = true;   // mark that we're disposed to help us make some kind of attempt to recover from progress stack corruptions above

            // only dispose of the cancel source if we own it
            if (!_ownCancelSource) CancellationTokenSource.Dispose();   // note that this will cancel any associated tokens!
        }
    }
    internal bool Disposed { get; private set; }
    internal IAmbientProgress? Parent { get; }
}
