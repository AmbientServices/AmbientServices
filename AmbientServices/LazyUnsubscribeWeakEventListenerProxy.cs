using System;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
    /// <summary>
    /// A class that weakly proxies for an event listener and routes event notifications to static functions for it, thus preventing the event hook from keeping the event listener alive.
    /// Automatically unsubscribes itself when the event listener is collected.
    /// </summary>
    /// <typeparam name="WEAKTYPE">The type that is being weakly referenced.</typeparam>
    /// <typeparam name="ARG1">The first argument for the event handler (usually the sender object).</typeparam>
    /// <typeparam name="ARG2">The second argument for the event handler (usually the event args).</typeparam>
    class LazyUnsubscribeWeakEventListenerProxy<WEAKTYPE, ARG1, ARG2> where WEAKTYPE : class
    {
        private readonly WeakReference<WEAKTYPE> _weakReference;
        private readonly Action<WEAKTYPE, ARG1, ARG2> _staticNotify;
        private readonly Action<LazyUnsubscribeWeakEventListenerProxy<WEAKTYPE, ARG1, ARG2>> _staticUnsubscribe;

        public LazyUnsubscribeWeakEventListenerProxy(WEAKTYPE instance, Action<WEAKTYPE, ARG1, ARG2> staticNotify, Action<LazyUnsubscribeWeakEventListenerProxy<WEAKTYPE, ARG1, ARG2>> staticUnsubscribe)
        {
            _weakReference = new WeakReference<WEAKTYPE>(instance);
            _staticNotify = staticNotify;
            _staticUnsubscribe = staticUnsubscribe;
        }
        /// <summary>
        /// A function that can be subscribed to an event without keeping the associated event listener alive.
        /// </summary>
        /// <param name="arg1">The first argument for the event handler (usually the sender object).</param>
        /// <param name="arg2">The second argument for the event handler (usually the event args).</param>
        public void WeakEventHandler(ARG1 arg1, ARG2 arg2)
        {
            // is the instance still alive?
            WEAKTYPE weak;
            if (_weakReference.TryGetTarget(out weak))
            {
                // the event listener is still alive, so call it's static event notification function
                _staticNotify(weak, arg1, arg2);
            }
            else
            {
                // the listener is dead, so unsubscribe us (that way we'll go away too)
                _staticUnsubscribe(this);
            }
        }
    }
}
