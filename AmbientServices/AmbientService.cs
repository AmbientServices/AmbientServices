using System;
using System.Reflection;
using System.Threading;
#if NETCOREAPP3_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace AmbientServices;

/// <summary>
/// A class that contains information about an initialization error.
/// </summary>
/// <param name="exception">The <see cref="Exception"/> that caused the initialization error.</param>
public class InitializationErrorEventArgs(Exception exception) : EventArgs 
{
    /// <summary>
    /// The <see cref="Exception"/> that caused the initialization error.
    /// </summary>
    public Exception Exception { get; } = exception;
}
/// <summary>
/// A static class that provides access to <see cref="AmbientService{T}"/>s.
/// </summary>
/// <remarks>
/// <pitch>The front door to the library: ask it for the <see cref="AmbientService{T}"/> accessor for any service interface and cache that accessor in a static field.  Also the place to hear about errors constructing default service implementations, which are reported rather than thrown.</pitch>
/// <pledge>
/// For a given interface there is exactly one <see cref="AmbientService{T}"/> per loaded copy of this assembly, and <see cref="GetService{T}()"/> always returns that same never-null instance, so accessors may be freely cached and compared.  Requesting an interface no assembly implements is not an error — the accessor simply resolves to null until an implementation is registered.
/// <see cref="InitializationError"/> is raised (on an arbitrary thread) when constructing a discovered default implementation throws; the failure suppresses that default rather than propagating the exception to the code that touched the service.
/// </pledge>
/// </remarks>
public static class Ambient
{
    /// <summary>
    /// Gets the <see cref="AmbientService{T}"/> for the indicated type.
    /// </summary>
    /// <typeparam name="T">The type of service that is needed.</typeparam>
    /// <returns>The <see cref="AmbientService{T}"/> instance.  This should never be null.</returns>
    public static AmbientService<T> GetService<
#if NETCOREAPP3_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        T>() where T : class
    {
        return AmbientService<T>.Instance;
    }
    /// <summary>
    /// Gets the <see cref="AmbientService{T}"/> for the indicated type.
    /// </summary>
    /// <typeparam name="T">The type of service that is needed.</typeparam>
    /// <param name="service">[OUT] Receives the ambient service.</param>
    /// <returns>The <see cref="AmbientService{T}"/> instance.  This should never be null.</returns>
    public static AmbientService<T> GetService<
#if NETCOREAPP3_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        T>(out AmbientService<T> service) where T : class
    {
        service = AmbientService<T>.Instance;
        return service;
    }
    /// <summary>
    /// An event that will notify subscribers when a service initialization error occurs.
    /// The notification may happen on any arbitrary thread.
    /// Thread-safe.
    /// </summary>
    public static event EventHandler<InitializationErrorEventArgs>? InitializationError;

    internal static void NotifyInitializationError(Exception ex)
    {
        InitializationError?.Invoke(null, new InitializationErrorEventArgs(ex));
    }
}
/// <summary>
/// A generic class that provides access to an ambient service implementation.
/// Must be accessed through <see cref="Ambient.GetService{T}()"/> or <see cref="Ambient.GetService{T}(out AmbientService{T})"/>.
/// </summary>
/// <remarks>
/// <pitch>
/// The access point for one ambient service: read whichever implementation currently applies, replace it process-wide, or override or suppress it for just the current call context.
/// It exists so libraries can consume ubiquitous-but-optional services (logging, caching, clock, settings, and the like) without dependency-injection ceremony — consumers register once and every library using AmbientServices picks it up.  By project convention, services accessed this way must never unexpectedly alter the caller-visible relationship between a function's inputs and outputs.
/// </pitch>
/// <pledge>
/// <see cref="Local"/> resolves to the call-context override when one exists (including the suppressed state, which makes <see cref="Local"/> null even when a global implementation exists) and falls back to <see cref="Global"/> otherwise.  <see cref="Override"/> reads and writes only the call-context slot; setting it to null reverts the context to following the global.  Setting <see cref="Local"/> to null suppresses rather than reverts.  Setting <see cref="Global"/> to null suppresses the process-wide implementation, including any discovered default — there is no way to revert to the discovered default other than re-registering it.
/// Call-context overrides flow with <see cref="ExecutionContext"/> into awaited continuations and forked work but never leak into sibling or ancestor contexts.  All members are thread-safe without caller synchronization.
/// <see cref="GlobalChanged"/> notifications may arrive on arbitrary threads and out of order; subscribers must re-query the latest value on each notification, and should subscribe statically or weakly because this instance lives forever.
/// The default implementation (a <see cref="DefaultAmbientServiceAttribute"/>-discovered class) is constructed lazily on first read of <see cref="Global"/>/<see cref="Local"/>; a default registered by a later-loaded assembly becomes visible to subsequent reads.
/// </pledge>
/// <plan>
/// One singleton per closed generic type, reached through <see cref="Ambient.GetService{T}()"/>.  The global side lives in a <see cref="GlobalServiceReference{T}"/> — an <see cref="Interlocked"/>-exchanged field holding the implementation, null (not yet initialized), or a suppression sentinel — with default discovery delegated to <see cref="DefaultAmbientServices"/> (which scans every loaded and later-loaded assembly referencing this one for <see cref="DefaultAmbientServiceAttribute"/> classes) and at-most-once construction per concrete type via a static initializer in <c>DefaultServiceImplementation</c>.  Construction failures are traced and reported through <see cref="Ambient.InitializationError"/> instead of being thrown, and retried on later reads.
/// The local side is an <see cref="AsyncLocal{T}"/> slot, detailed in the paragraphs below.
/// </plan>
/// <priority>
/// 1. Call-context isolation over cross-context visibility: an override is confined to the logical call flow that installed it, even though a single process-wide store would be simpler, faster, and easier to reason about.  Concurrent per-test substitution and per-request isolation both depend on this, which is why the assembly-load-context subtlety described below is tolerated rather than engineered away. (public)
/// 2. Reporting a failed default construction over throwing it: a <see cref="DefaultAmbientServiceAttribute"/> class whose constructor throws is traced and raised on <see cref="Ambient.InitializationError"/> and retried on a later read, rather than thrown at whichever caller happened to touch the service first.  An optional service must never take down code that did not ask for it; the cost is that a broken default stays silent unless somebody subscribed. (public)
/// 3. Cheap steady-state resolution over eager initialization: every read is an <see cref="AsyncLocal{T}"/> read plus a field read, with discovery and construction deferred to first use — in exchange for a first read that costs far more than the rest, and an initialization order that depends on which assemblies happen to have loaded. (private)
/// </priority>
/// <para>Ambient state lives in static fields on <see cref="AmbientService{T}"/> in each loaded copy of this assembly. For a single shared ambient domain per <see cref="AppDomain"/>
/// (or a single default assembly load context on .NET Core+), the library must be loaded only once; additional custom assembly load contexts
/// that need to share overrides with the host should resolve this assembly from the default context (or another agreed single context). Otherwise scoped and global state are isolated per load, which is easy to mistake for <see cref="ExecutionContext"/> corruption.</para>
/// <para>Resolving an ambient implementation usually checks the call-context local slot first, then the global implementation.</para>
/// <para>The local slot is an <see cref="System.Threading.AsyncLocal{T}"/> holding <c>object?</c>: <see langword="null"/> (follow global), a service instance, or an internal sentinel value when <see cref="Local"/> needs to suppress the global instance.
/// Assignments to the local slot participate in <see cref="System.Threading.ExecutionContext"/> copy-on-write, so nested asynchronous work that sets its own local value does not share one mutable holder with ancestor contexts.</para>
/// </remarks>
/// <typeparam name="T">The interface for the service.</typeparam>
public class AmbientService<
#if NETCOREAPP3_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
    T> where T : class
{
    /// <summary>
    /// Gets the <see cref="AmbientService{T}"/> for the service indicated by the type.
    /// </summary>
    internal static AmbientService<T> Instance { get; } = new();
    /// <summary>
    /// The singleton call-context-local service reference (non-singleton AmbientService&lt;T&gt; can be used for unit testing).
    /// </summary>
    private readonly AsyncLocal<object?> _localReference = new();

    /// <summary>
    /// An object whose instance is used to indicate that the global implementation has been not overridden with a local instance, but rather suppressed so that it appears to be null.
    /// </summary>
    internal static readonly object SuppressedImplementation = new();

    /// <summary>
    /// Gets the raw local reference, which may be <see cref="SuppressedImplementation"/>.
    /// </summary>
    internal object? RawLocalOverride => _localReference.Value;
    /// <summary>
    /// Sets the raw local override implementation.
    /// Thread-safe without caller synchronization: values are stored in <see cref="AsyncLocal{T}"/> and follow <see cref="ExecutionContext"/> flow (including thread hops when the context is flowed).
    /// </summary>
    /// <param name="override">The new local service implementation to use, <see cref="SuppressedImplementation"/> to suppress the global implementation, or null to revert to the global implementation.</param>
    internal void SetRawLocalOverride(object? @override)
    {
        _localReference.Value = @override;
    }
#if NEEDED
    /// <summary>
    /// Sets the context-local service  implementation.
    /// Thread-safe without caller synchronization: values are stored in <see cref="AsyncLocal{T}"/> and follow <see cref="ExecutionContext"/> flow (including thread hops when the context is flowed).
    /// </summary>
    /// <param name="newLocalService">The new local service implementation to use.</param>
    internal void SetLocalOverride(T newLocalService)
    {
        _localReference.Value = newLocalService;
    }
#endif
    /// <summary>
    /// Clears the local override of the global implementation.
    /// Thread-safe without caller synchronization: values are stored in <see cref="AsyncLocal{T}"/> and follow <see cref="ExecutionContext"/> flow (including thread hops when the context is flowed).
    /// </summary>
    internal void ClearLocalOverride()
    {
        _localReference.Value = null;
    }
    /// <summary>
    /// Sets the local override of the global implementation in such a way that the global implementation is suppressed (so the "Local" value will be null, even if there is a global implementation).
    /// Thread-safe without caller synchronization: values are stored in <see cref="AsyncLocal{T}"/> and follow <see cref="ExecutionContext"/> flow (including thread hops when the context is flowed).
    /// </summary>
    internal void SuppressGlobalUsingLocalOverride()
    {
        _localReference.Value = SuppressedImplementation;
    }

    // this is only internal instead of private so that we can diagnose issues in test cases
    internal GlobalServiceReference<T> GlobalReference { get; } = new();

    /// <summary>
    /// Overrides the service implementation locally and temporarily.
    /// </summary>
    /// <remarks>
    /// <para>This override applies to the <see cref="AmbientService{T}"/> singleton for the loaded copy of this assembly. Dynamically loaded code in another assembly load context that loaded a different copy of this assembly uses separate singletons and <see cref="AsyncLocal{T}"/> storage, so the same logical call context will not see this override unless that load context shares the same physical assembly load (see class remarks).</para>
    /// </remarks>
    /// <param name="newLocalServiceImplementation">The new local service implementation to use until the returned object is disposed.  If null, temporarily removes the ambient service in this call context.</param>
    /// <returns>An <see cref="IDisposable"/> instance that, when disposed, will return the local service implementation to what it was before this call.</returns>
    public IDisposable ScopedLocalOverride(T? newLocalServiceImplementation)
    {
        return new ScopedLocalServiceOverride<T>(newLocalServiceImplementation);
    }

    /// <summary>
    /// Suppresses both the global and local implementations temporarily, optionally replacing everything with a specified implementation.
    /// This can be useful in cases where you're calling across assembly load contexts into a partially-trusted assembly that shares ambient services by default,
    /// but that you want to prevent from accessing specific ambient services.
    /// </summary>
    /// <param name="temporaryGlobalServiceImplementation">An optional implementation to use as the global implementation until the returned instance is disposed.</param>
    /// <returns>An <see cref="IDisposable"/> instance that, when disposed, will return the global and local service implementations to what they were before this call.</returns>
    public IDisposable ScopedGlobalOverride(T? temporaryGlobalServiceImplementation = null)
    {
        return new ScopedGlobalServiceOverride<T>(temporaryGlobalServiceImplementation);
    }

    internal AmbientService()
    {
    }
    /// <summary>
    /// Gets or sets the global service implementation, or null if there is no implementation or it has been suppressed.
    /// If set to null, suppresses the global service.
    /// When setting the service, overwrites any previous implementation and raises the <see cref="GlobalChanged"/> event either on this thread or another thread asynchronously.
    /// Thread-safe.
    /// </summary>
    public T? Global
    {
        get
        {
            return GlobalReference.Service;
        }
        set
        {
            GlobalReference.Service = value;
        }
    }
    /// <summary>
    /// Gets or sets the call-context-local override implementation for the service, or null if there is no override implementation.
    /// If set to null, reverts to the global service implementation and begins watching changes to that.
    /// Otherwise sets to the specified implementation.
    /// Thread-safe without caller synchronization: local values use <see cref="AsyncLocal{T}"/> and <see cref="ExecutionContext"/> flow (see also <see cref="SetRawLocalOverride"/>).
    /// </summary>
    public T? Override
    {
        get
        {
            return _localReference.Value as T;
        }
        set
        {
            if (value == null) ClearLocalOverride();
            else _localReference.Value = value;
        }
    }
    /// <summary>
    /// Gets or sets the call-context-local service implementation.
    /// If set to null, suppresses any local or global service (and begins ignoring changes to the global service).
    /// Otherwise sets the local override to the specified implementation.
    /// Thread-safe without caller synchronization: local values use <see cref="AsyncLocal{T}"/> and <see cref="ExecutionContext"/> flow (see also <see cref="SetRawLocalOverride"/>).
    /// </summary>
    public T? Local
    {
        get
        {
            return (_localReference.Value ?? GlobalReference.Service) as T;
        }
        set
        {
            if (value == null) SuppressGlobalUsingLocalOverride();
            else _localReference.Value = value;
        }
    }
    /// <summary>
    /// An event that will notify subscribers when a global service implementation is changed.  
    /// The notification may happen on any arbitrary thread.
    /// Thread-safe.
    /// </summary>
    /// <remarks>
    /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
    /// because this instance lives forever.
    /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), and
    /// the fact that each notification may proceed at a different pace, notifications may appear to come in a different order than the changes actually occurred.
    /// As a result, subscribers should query the latest value if needed when they receive the event notification.
    /// This way if multiple changes happen, they will always end up with the latest value.
    /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
    /// </remarks>
    public event EventHandler<EventArgs> GlobalChanged
    {
        add
        {
            GlobalReference.ServiceChanged += value;
        }
        remove
        {
            GlobalReference.ServiceChanged -= value;
        }
    }
}
/// <summary>
/// A scoping class that overrides the global service implementation with a specified local one during its scope.
/// Note that call context variables can sometimes survive returning from a function and calling into another function, 
/// so it is important to reset a local override before returning from the function where the override is used.
/// As a result, depending on how contexts are reused, restoring the original may be needed.
/// For example, in unit tests, the same call context is used for multiple unit tests, so any overrides need
/// to be undone when the test is complete just in case another test subsequently runs using the same call context.
/// </summary>
/// <remarks>
/// <pitch>The <c>using</c>-block way to substitute (or remove) a service implementation for just the current call context — the primary tool for isolating tests and for handing specialized implementations to a subtree of calls without touching the process-wide registration.</pitch>
/// <pledge>Construction captures the raw call-context slot — including a pre-existing suppression — and sets the local service to the given implementation (null meaning suppressed); disposal restores exactly the captured slot state, so scopes nest correctly and a scope opened inside another returns control to the enclosing override.  The global implementation is never touched.  Instances are single-use, must be disposed in the same call context that created them, and are not thread-safe.</pledge>
/// <plan>A thin wrapper over <see cref="AmbientService{T}.Local"/>: it snapshots the raw <see cref="AsyncLocal{T}"/> value (raw, so the suppression sentinel round-trips rather than degrading to null) plus the then-current global and override for debugging, and writes the snapshot back on dispose.  Redundant disposal is a no-op.</plan>
/// </remarks>
/// <typeparam name="T">The service interface type.</typeparam>
public sealed class ScopedLocalServiceOverride<
#if NETCOREAPP3_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
    T> : IDisposable where T : class
{
    private static readonly AmbientService<T> _Reference = Ambient.GetService<T>();

    private readonly object? _oldRawOverride;
    /// <summary>
    /// Gets the old local override in case it is needed by the overriding implementation.  (Mostly for debugging).
    /// </summary>
    public T? OldOverride { get; }
    /// <summary>
    /// Gets the old global implementation in case it is needed by the overriding implementation.  (Mostly for debugging).
    /// </summary>
    public T? OldGlobal { get; }

    /// <summary>
    /// Constructs a scoped override that changes the service implementation for this call context until this instance is disposed.
    /// </summary>
    /// <param name="temporaryLocalService">The service to temporarily use in this call context.</param>
    public ScopedLocalServiceOverride(T? temporaryLocalService)
    {
        _oldRawOverride = _Reference.RawLocalOverride;
        OldGlobal = _Reference.Global;
        OldOverride = _Reference.Override;
        _Reference.Local = temporaryLocalService;
    }

    #region IDisposable Support
    private bool _disposed; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _Reference.SetRawLocalOverride(_oldRawOverride);
            }
            _disposed = true;
        }
    }
    /// <summary>
    /// Disposes of the instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
    }
    #endregion
}
/// <summary>
/// A scoping class that overrides both the global and local service implementation with a specified global one during its scope.
/// </summary>
/// <remarks>
/// <pitch>Temporarily replaces the process-wide implementation for the duration of a <c>using</c> block — for the rare cases where the substitute (or suppression) must be seen by <em>every</em> call context, such as when restricting what a partially-trusted or cross-load-context callee can reach.  Prefer <see cref="ScopedLocalServiceOverride{T}"/> when only the current call context needs the substitute.</pitch>
/// <pledge>Construction captures both the global implementation and the raw call-context slot, then assigns the given implementation (null meaning suppressed) as the global; disposal restores both captures.  A call-context override in effect still shadows the temporary global within that context.  Because the global is process-wide mutable state, concurrent scopes on different threads interleave last-writer-wins — callers coordinate such use themselves.  Instances are single-use and must be disposed in the creating call context.</pledge>
/// <plan>A thin wrapper over <see cref="AmbientService{T}.Global"/> and the raw <see cref="AsyncLocal{T}"/> local slot; it snapshots both on construction and writes them back on dispose.  Redundant disposal is a no-op.</plan>
/// </remarks>
/// <typeparam name="T">The service interface type.</typeparam>
public sealed class ScopedGlobalServiceOverride<
#if NETCOREAPP3_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
    T> : IDisposable where T : class
{
    private static readonly AmbientService<T> _Reference = Ambient.GetService<T>();

    private readonly object? _oldRawOverride;
    /// <summary>
    /// Gets the old local override in case it is needed by the overriding implementation.  (Mostly for debugging).
    /// </summary>
    public T? OldOverride { get; }
    /// <summary>
    /// Gets the old global implementation in case it is needed by the overriding implementation.  (Mostly for debugging).
    /// </summary>
    public T? OldGlobal { get; }
    /// <summary>
    /// Constructs a scoped override that changes the service implementation for this call context until this instance is disposed.
    /// </summary>
    /// <param name="temporaryGlobalService">The optional service to temporarily use in this call context.</param>
    public ScopedGlobalServiceOverride(T? temporaryGlobalService = null)
    {
        _oldRawOverride = _Reference.RawLocalOverride;
        OldGlobal = _Reference.Global;
        OldOverride = _Reference.Override;
        _Reference.Global = temporaryGlobalService;
    }

    #region IDisposable Support
    private bool _disposed; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _Reference.Global = OldGlobal;
                _Reference.SetRawLocalOverride(_oldRawOverride);
            }
            _disposed = true;
        }
    }
    /// <summary>
    /// Disposes of the instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
    }
    #endregion
}

/// <summary>
/// A generic class used to ensure that only one instance of the default service implementation gets created.
/// </summary>
/// <remarks>
/// <pitch>The once-per-concrete-type construction guard for discovered default implementations.</pitch>
/// <plan>Exploits CLR static-initializer semantics: the singleton lives in a static field of the closed generic type, so the runtime guarantees at most one construction per concrete type no matter how many interfaces resolve to it or how many threads race.  Construction requires a parameterless (public or non-public) constructor and throws <see cref="InvalidOperationException"/> otherwise.</plan>
/// </remarks>
/// <typeparam name="T">The concrete type of the service.</typeparam>
internal class DefaultServiceImplementation<
#if NETCOREAPP3_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
    T> where T : class
{
    private static readonly T _ImplementationSingleton = CreateInstance();
    private static T CreateInstance()
    {
        ConstructorInfo ci = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException($"Classes with [DefaultAmbientService] attributes applied type must have a default constructor.  {typeof(T).Name} does not have a default constructor!");
        return (T)ci.Invoke([]);
    }
    public static T GetImplementation() { return _ImplementationSingleton; }
}
/// <summary>
/// A class that manages a global service reference.
/// </summary>
/// <remarks>
/// <pitch>The process-wide half of an <see cref="AmbientService{T}"/>: holds the one global implementation, discovers the default lazily, and raises change notifications.</pitch>
/// <pledge>The getter returns the registered implementation, the discovered default, or null when none exists or the service is suppressed; a set always wins over the default, with null meaning suppress.  Setting raises <see cref="ServiceChanged"/> synchronously on the setting thread; notifications from concurrent sets may be observed out of order, so subscribers must re-query.</pledge>
/// <plan>A single <c>object?</c> field interpreted as: null (default not yet resolved — retry discovery on each read until one appears, supporting assemblies that register defaults after first use), a suppression sentinel, or the implementation itself.  Writes use <see cref="Interlocked.Exchange(ref object, object)"/>; the late-discovery path uses <see cref="Interlocked.CompareExchange(ref object, object, object)"/> so a racing registration is never overwritten.  Default discovery goes through <see cref="DefaultAmbientServices.TryFind"/> and <c>DefaultServiceImplementation</c>; discovery exceptions are traced, written to the console, and reported via <see cref="Ambient.NotifyInitializationError"/> instead of thrown.</plan>
/// </remarks>
/// <typeparam name="T">The interface type for the service being managed.</typeparam>
internal class GlobalServiceReference<
#if NETCOREAPP3_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
T> where T : class
{
    /// <summary>
    /// A generic object whose instance is used to indicate that the default service implementation has been suppressed.
    /// </summary>
    private static readonly object SuppressedService = new();

    /// <summary>
    /// A reference to the current service implementation.  Null if not yet initialized.  <see cref="SuppressedService"/> if the service has been explicitly suppressed.
    /// </summary>
    private object? _service;

    internal GlobalServiceReference()
    {
        _service = DefaultImplementation();
    }
    private static T? DefaultImplementation()
    {
        try
        {
            Type? impType = DefaultAmbientServices.TryFind(typeof(T));
            if (impType == null) return null;       // there is no default implementation (yet)
            Type type = typeof(DefaultServiceImplementation<>).MakeGenericType(impType);
            MethodInfo mi = type.GetMethod(nameof(DefaultServiceImplementation<T>.GetImplementation))!; // DefaultServiceImplementation<T> has a public GetImplementation method, so this should always succeed
            T implementation = (T)mi.Invoke(null, [])!;  // DefaultServiceImplementation<T> returns a non-null T
            return implementation;
        }
        catch (Exception ex)
        {
            string traceMessage = $"Error constructing default {typeof(T).FullName}: {ex}!";
            System.Diagnostics.Trace.WriteLine(traceMessage);
            Console.WriteLine(traceMessage);
            Ambient.NotifyInitializationError(ex);
        }
        return null;
    }
    private T? LateAssignedDefaultServiceImplementation()
    {
        T? newDefaultImplementation = DefaultImplementation();
        // still no default implementation registered?  try again later
        if (newDefaultImplementation == null) return null;
        // we should almost always get a null back here below, but it's theoretically possible if two attempts to retrieve the implementation happen at the same time, but even in this case, the only way we would get back instances of different types would be if the default ambient service changed, which shouldn't be possible given the current implementation
        // as a result, the non-null case below is unlikely to get covered by tests
        return (Interlocked.CompareExchange(ref _service, newDefaultImplementation, null) is not T oldDefaultImplementation)
            ? newDefaultImplementation
            : oldDefaultImplementation;
    }

    /// <summary>
    /// Gets or sets the service.
    /// If set to null, suppresses the default service (so that the getter returns null).
    /// When setting the service, overwrites any previous service and raises the <see cref="ServiceChanged"/> event.
    /// Thread-safe.
    /// </summary>
    public T? Service
    {
        get
        {
            return (_service ?? LateAssignedDefaultServiceImplementation()) as T;
        }
        set
        {
            T? oldImplementation = Interlocked.Exchange(ref _service, value ?? SuppressedService) as T;
            ServiceChanged?.Invoke(typeof(AmbientService<T>), EventArgs.Empty);
        }
    }
    /// <summary>
    /// An event that will notify subscribers when the global service implementation is changed.  
    /// The notification will happen on the thread and within the call context from which the change is initiated.
    /// Thread-safe.
    /// </summary>
    /// <remarks>
    /// In order to avoid memory leaks, most subscribers will want to subscribe a static method or use the weak event listener pattern when subscribing to this event, 
    /// because the service reference lives forever.
    /// Because the event might be raised simultaneously on other threads or call contexts (due to multiple changes happening at the same time), 
    /// and each notification may proceed at a different pace, notifications may appear to come in a different order than the changes actually occurred.
    /// Subscribers should query the latest value if needed when they receive the event notification.
    /// This way if multiple changes happen, they will always end up with the latest value.
    /// Subscribers must take care to avoid race conditions that may be caused by such out-of-order notifications.
    /// </remarks>
    public event EventHandler<EventArgs>? ServiceChanged;
}
