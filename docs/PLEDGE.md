# AmbientServices — Pledge

*This is the whole-library **Pledge** layer of the 4P Protocol — the behavioral contract shared by the library as a single unit: the rules that hold across every ambient service and that the interface signatures cannot express by themselves. See [MODULE_DESCRIPTIONS.md](MODULE_DESCRIPTIONS.md) for what the 4P are, [PITCH.md](PITCH.md) for whether the library fits your need, and [PLAN.md](PLAN.md) for how these promises are implemented. Each interface additionally carries its own Pledge in its XML-doc `<remarks>`; the promises below apply on top of all of them.*

## The ambient contract

The single rule that governs everything registered as an ambient service: **it must behave environmentally.** It must not change the relationship between a consuming function's inputs and its outputs in any way the caller would be surprised by. A service may have side effects, but they must fall into one of two categories — effects the caller does not observe in the result at all, or effects the caller already understands to be variable.

Concretely, per service kind:

- **Logging and profiling** never affect outputs or control flow. The only permissible effect is the record itself. (Consumers must therefore keep message-generating lambdas free of side effects, since the logger may not invoke them.)
- **Caching** may return bounded-stale results, but only for functions that already have hidden inputs (a database, a remote store). A cache key must incorporate every input that identifies the cached item; a key that omits an input makes results order-dependent, which is a usage error the caller owns.
- **Clock** may make time appear to pass faster, slower, or not at all (for testing timeouts and heavy-load scheduling). It never runs backward. Callers must not treat wall-clock progression as guaranteed.
- **Progress and cancellation** never affect the result except by aborting the operation entirely, in which case there is no result.
- **Settings** are, by definition, inputs — configuration-shaped parameters the caller has chosen not to pass on the stack. They may change a function's output, but never in a way the caller is expected to be concerned about.
- **Security context** falls back to the environment (e.g., ambient cloud credentials) rather than being threaded explicitly, because passing such credentials around is itself undesirable.

A consumer relies on this contract when it uses a service without accounting for the service's presence; an implementer promises to honor it. Anything that would violate it does not belong behind an ambient interface — it belongs in an explicit dependency.

## Optionality

Every ambient service is optional. Any service may be absent, or may be **suppressed** (set to null) for a call context. Consumers must tolerate absence — the canonical pattern is a null-conditional call (`service?.Method(...)`) that becomes a no-op when the service is not present. Helper wrappers in this library already fold this in. Code must never assume a service exists.

## Resolution and override protocol

Each ambient service is reached through three related views:

- **Global** — the process-wide default for the service. When nothing else is set, this is what callers get. It may be replaced (including with null to disable the service).
- **Override** — a call-context-local replacement that flows with the async execution context and is undone when its scope is disposed. Setting an override affects only the current logical call flow, not other threads or requests.
- **Local** — the *effective* service a caller actually gets: the call-context override if one is in force, otherwise the global. Consumers read `Local`; overriders install a replacement for the current call flow via `ScopedLocalOverride(replacement)` — a disposable that restores the prior value on dispose.

Overrides nest and restore in stack order, which is the mechanism behind per-test service substitution and per-request isolation.

**Suppression is a distinct operation from replacement.** To deny a service to the current call flow — the security-relevant case of preventing less-trusted code from reaching it — use `ScopedLocalOverride(null)` (equivalently, set `Local` to null). This installs a call-context sentinel that hides the global service as well, and it is **not** the same as clearing the `Override` property, which merely reverts to the global. Because it is call-context-scoped, the suppression flows into synchronous calls made within that scope without affecting the service other concurrent contexts see.

## Startup, shutdown, and change

The global implementation of a service is expected to be **swapped as initialization progresses** — for example, settings may begin as built-in defaults, then become a local configuration reader, then a centralized store, without any centralized startup orchestration. Because of this:

- Consumers must not depend on a service's implementation being final, or on any particular ordering, at the moment they first read it.
- Where a service exposes change notifications (settings value changes being the primary case), consumers that need to react should subscribe rather than sample. **Notifications may arrive asynchronously, on any thread, at any time**, and may arrive after the value has already changed; consumers must not assume a notification precedes an observable change.

## Automatic discovery

Two subsystems populate themselves by reflection rather than explicit registration, and callers rely on that:

- **Default implementations** are discovered from loaded assemblies and used as the global service until overridden. A consumer gets a working default for a service without registering anything.
- **Status checkers and auditors** with public parameterless constructors are discovered, constructed, and registered automatically, but only begin running after the status system is explicitly started. Checkers/auditors that need non-trivial construction can be registered manually. (Lazy registration is discouraged: detecting backend problems at startup is preferable to detecting them only when a backend is first touched.)

## Assembly-load-context boundary

Ambient state flows across an `AssemblyLoadContext` boundary **only where the AmbientServices assembly itself is shared** across those contexts. By default a load context loads its own copy of already-loaded assemblies, giving it independent global/override state — so a service set on one side is invisible on the other. Consumers that load plugins must decide, deliberately, whether to share the assembly (and thus flow ambient services) or isolate it, and must suppress individual services across the boundary when a plugin is only partially trusted. Treating context-crossing ambient state as automatically shared is a contract violation that surfaces as services mysteriously reverting to defaults.
