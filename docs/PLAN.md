# AmbientServices — Plan

*This is the whole-library **Plan** layer of the 5P Protocol — how the ambient-service mechanism is built at the system altitude, the building blocks it rests on, and the cross-cutting trade-offs every realization inherits. See [MODULE_DESCRIPTIONS.md](MODULE_DESCRIPTIONS.md) for what the 5P are, [PITCH.md](PITCH.md) for the adoption decision, [PLEDGE.md](PLEDGE.md) for the contract this implements, and [PRIORITY.md](PRIORITY.md) for the rankings that produced the trade-offs below. This file is deliberately not a digest of the per-service Plans — each concrete implementation carries its own Plan in its XML-doc `<remarks>`; this one covers only what is common to the whole library and how the ambient machinery itself works.*

## The resolution machine

The heart of the library is `AmbientService<T>`, a small accessor that holds a service's **Global** value and exposes the **Local** (effective) value and the call-context **Override**. The override is stored in an `AsyncLocal<T>`, so it flows with the async execution context and is naturally scoped to a logical call flow rather than a thread. `Local` resolves to the override when one is present and the global otherwise. Scoped-override helpers (e.g. `ScopedLocalServiceOverride<T>` / `ScopedLocalOverride`) capture the prior value, install a new one, and restore it on `Dispose`, which is what makes overrides nest and unwind in stack order.

This `AsyncLocal`-based design is also the direct cause of the assembly-load-context behavior in the [Pledge](PLEDGE.md): the accessor's statics live in whichever copy of the AmbientServices assembly a load context resolved, so state is shared across contexts exactly when the assembly is shared, and not otherwise.

## Automatic discovery

Global defaults are supplied by types marked `[DefaultAmbientService]`, found by scanning loaded assemblies; a default is constructed lazily and used until something replaces the global. The Status subsystem performs its own reflection pass, discovering `StatusChecker`/`StatusAuditor` subclasses with public parameterless constructors and registering them, then activating them only when the status system is started. Both mechanisms trade a small, one-time reflection cost for the "works with zero registration" property the Pitch promises.

## Foundational building blocks

- **`AmbientClock`** underlies all timing in the library — timeouts, rotation, stopwatches, timers. Routing time through it (rather than `DateTime.UtcNow` / `Stopwatch` / `System.Timers.Timer` directly) is what lets tests pause and skip time deterministically, and it is the reason the coding standard mandates `AmbientClock.UtcNow`.
- **`AsyncLocal<T>`** carries call-context overrides and other context-following state (progress, call stack) so it aligns with `async`/`await` flow instead of threads.
- **Lock-free / interlocked concurrency** is the default throughout — statistics, profiler, bottleneck detector, and caches use atomic operations and optimistic update loops rather than mutual exclusion.

## Cross-cutting engineering constraints

These constraints are not incidental style; they shape every realization in the library and any functionally-equivalent reimplementation should honor them:

- **Async all the way.** `ValueTask` is preferred over `Task`, and async-avoidance patterns (`.Result`, `GetAwaiter().GetResult()`, sync-over-async) are avoided as a rule — async is propagated up the call stack instead — save for a few deliberate exceptions where the surrounding contract is synchronous (for example releasing a semaphore from a synchronous logging path, or an MSTest assembly-cleanup that cannot be async). `ConfigureAwait` is not used anywhere.
- **No `lock` keyword** (and no async-unfriendly lock types). Concurrency is handled with lock-free algorithms where possible, and async-friendly waits where a wait is genuinely required, so the code never has to be un-blocked later for asyncification.
- **Optionality and nullability** are pervasive by design — every service can be absent, so the code and its helpers treat a missing service as a no-op rather than an error.
- **Backwards-compatible evolution.** Public input schemas may only gain optional/nullable additions and output schemas may not drop non-deprecated members, so consumers can upgrade the single package without breakage.
- **Warning-free and no third-party dependencies.** The library's only *runtime* dependencies are Microsoft-published .NET packages (`System.Collections.Immutable` and `System.Text.Json`) — no assemblies outside those the .NET platform itself provides, and no third-party packages. Its one other package reference, `Microsoft.SourceLink.GitHub`, is a build-time-only source-linking tool (`PrivateAssets="All"`) that never ships to consumers. It is kept warning-free, keeping it safe to drop into any host.

## Targeting and packaging

The library targets **.NET Standard 2.0 and 2.1** plus **.NET 8.0, 9.0, and 10.0**, shipped as a single NuGet package (`AmbientServices`); the test suite runs on .NET 10.0. Broad target coverage keeps it usable from legacy hosts up through current runtimes without the consumer choosing a variant.

## System-wide trade-offs

- **Always-on, coarse-grained monitoring over precision.** The profiler and bottleneck detector are built to run continuously at negligible overhead (lock-free accumulation, per-context surveyors, rotating time windows) rather than to give profiler-grade precision. The cost is that saturation and top-N figures are approximations the consumer must interpret conservatively.
- **Optionality over guaranteed presence.** Making every service suppressible and null-tolerant removes plumbing and lets libraries adopt services non-invasively, at the cost of null-checks (mostly hidden in helpers) at each use.
- **Context-following state over thread-locality.** `AsyncLocal` gives correct behavior under `async`/`await` and scoped overrides, at the cost of the load-context sharing subtlety documented in the Pledge.
- **Regeneration caveat.** The Pitch and Pledge, with these building blocks, are enough to rebuild a *functionally equivalent* library, but not a *wire-* or *format-compatible* one: serialization formats, on-disk log/rotation layouts, and key encodings live below this altitude and are intentionally left to the per-service Plans.
