using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A class that weakly proxies for an event subscriber and routes event notifications to static functions for it, thus preventing the event hook from keeping the event subscriber alive.
    /// Automatically unsubscribes the first time the event is triggered after the event subscriber is collected.
    /// </summary>
    /// <typeparam name="TTYPETOWEAKEN">The type that is being weakly referenced.</typeparam>
    /// <typeparam name="TEVENTARG1">The first argument for the event handler (usually the sender object).</typeparam>
    /// <typeparam name="TEVENTARG2">The second argument for the event handler (usually the event args).</typeparam>
    class LazyUnsubscribeWeakEventListenerProxy<TTYPETOWEAKEN, TEVENTARG1, TEVENTARG2> where TTYPETOWEAKEN : class
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
            TTYPETOWEAKEN weak;
            if (_weakSubscriber.TryGetTarget(out weak))
            {
                // the event subscriber is still alive, so call it's static event notification function
                _staticNotify(weak, arg1, arg2);
            }
            else
            {
                // the subscriber is dead, so unsubscribe us (that way we'll go away too)
                _staticUnsubscribe(this);
            }
        }
    }
}
