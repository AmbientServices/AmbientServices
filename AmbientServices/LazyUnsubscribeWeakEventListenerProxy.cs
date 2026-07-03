using System;

namespace AmbientServices;

/// <summary>
/// A class that weakly proxies for an event subscriber and routes event notifications to static functions for it, thus preventing the event hook from keeping the event subscriber alive.
/// Automatically unsubscribes the first time the event is raised after the event subscriber is collected.
/// </summary>
/// <typeparam name="TTYPETOWEAKEN">The type that is being weakly referenced.</typeparam>
/// <typeparam name="TEVENTARG1">The first argument for the event handler (usually the sender object).</typeparam>
/// <typeparam name="TEVENTARG2">The second argument for the event handler (usually, but not necessarily a <see cref="EventArgs"/> or the TEventArgs from <see cref="EventHandler{TEventArgs}"/>).</typeparam>
/// <remarks>
/// <pitch>Lets a short-lived object subscribe to a long-lived event (such as <see cref="AmbientService{T}.GlobalChanged"/>, whose source lives forever) without the subscription keeping the subscriber alive.</pitch>
/// <pledge>
/// While the subscriber is alive, every raise of the event is forwarded to the supplied static notify delegate along with the (strongly-held-for-the-call) subscriber instance.  Once the subscriber has been collected, the first subsequent raise triggers the static unsubscribe delegate, detaching the proxy so it can be collected too — meaning cleanup is lazy: a dead subscriber's proxy lingers until the event next fires, and an event that never fires again never unsubscribes.
/// Both delegates must be static (or otherwise capture no reference to the subscriber); a delegate that captures the subscriber defeats the weak reference and recreates the leak this class exists to prevent.  <see cref="Unsubscribe"/> may be called at any time for deterministic detachment.
/// </pledge>
/// <plan>Holds a <see cref="WeakReference{T}"/> to the subscriber plus the two static delegates; <see cref="WeakEventHandler"/> is the method actually subscribed to the event, and on each raise it either resolves the weak reference and forwards, or self-unsubscribes.  No timers, no finalizers — liveness checking costs one weak-reference resolution per event raise.</plan>
/// </remarks>
internal class LazyUnsubscribeWeakEventListenerProxy<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2> where TTYPETOWEAKEN : class
{
    private readonly WeakReference<TTYPETOWEAKEN> _weakSubscriber;
    private readonly Action<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2> _staticNotify;
    private readonly Action<LazyUnsubscribeWeakEventListenerProxy<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2>> _staticUnsubscribe;

    /// <summary>
    /// Create a lazy unsubscribe event listener proxy.
    /// </summary>
    /// <param name="instance">The instance that needs to be collected and should be proxied weakly.</param>
    /// <param name="staticNotify">A static function that will receive the instance pointer, but must not be an instance function so that the instance can be collected.</param>
    /// <param name="staticUnsubscribe">A delegate that will receive this lazy unsubscribe instance and unsubscribe the weak event handler from the event.  Note that this must *not* reference a member variable, or the instance will never be collected.</param>
    public LazyUnsubscribeWeakEventListenerProxy(TTYPETOWEAKEN instance, Action<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2> staticNotify, Action<LazyUnsubscribeWeakEventListenerProxy<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2>> staticUnsubscribe)
    {
        _weakSubscriber = new WeakReference<TTYPETOWEAKEN>(instance);
        _staticNotify = staticNotify;
        _staticUnsubscribe = staticUnsubscribe;
    }
    /// <summary>
    /// Unsubscribes immediately.
    /// </summary>
    public void Unsubscribe()
    {
        _staticUnsubscribe(this);
    }
    /// <summary>
    /// A function that can be subscribed to an event without keeping the associated event subscriber alive.
    /// </summary>
    /// <param name="arg1">The first argument for the event handler (usually the sender object).</param>
    /// <param name="arg2">The second argument for the event handler (usually the event args).</param>
    public void WeakEventHandler(TEVENTARG1 arg1, TEVENTARG2 arg2)
    {
        // is the instance still alive?
        TTYPETOWEAKEN? weak;
        if (_weakSubscriber.TryGetTarget(out weak))
        {
            // the event subscriber is still alive, so call it's static event notification function
            _staticNotify(weak, arg1, arg2);
        }
        else
        {
            // the subscriber is dead, so unsubscribe us (that way we'll go away too)
            Unsubscribe();
        }
    }
}
