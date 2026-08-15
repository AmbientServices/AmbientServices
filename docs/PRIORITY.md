# AmbientServices — Priority

*This is the whole-library **Priority** layer of the 5P Protocol — the rankings the library applies whenever two otherwise-legal options compete, so the same forks are not re-decided differently each time they come up. See [MODULE_DESCRIPTIONS.md](MODULE_DESCRIPTIONS.md) for what the 5P are, [PITCH.md](PITCH.md) for the adoption decision, [PLEDGE.md](PLEDGE.md) for the contract, and [PLAN.md](PLAN.md) for how it is built. Individual types carry their own Priority in their XML-doc `<remarks>` where they have one; the rankings below apply on top of all of them, and a type may extend them but may not invert one without inverting it here first.*

Each entry names what wins **and** what loses. `(public)` means consumers may rely on the ordering — it says which way the library will bend the next time it is forced to choose, not merely which way it bent already. `(private)` binds maintainers only and may be reordered without notice.

Entries are ranked: when two of them collide, the earlier one wins.

## 1. Backwards compatibility over API elegance *(public)*

A consumer must be able to take a new version of the single `AmbientServices` package without editing anything. When a cleaner surface conflicts with that, the surface stays as it is: input schemas may only gain optional or nullable members, output schemas may not drop members that were never deprecated, and a naming or shape mistake that shipped is corrected by addition, not replacement.

*What this costs:* accumulated surface, names we would not choose again, and deprecated members that outlive their usefulness.

## 2. The consumer's zero-friction adoption over the library's internal simplicity *(public)*

The entire value proposition is that a library author programs against an ambient interface and a consumer registers nothing. Whenever that convenience can be bought by absorbing complexity into this library, it is bought: assembly-scanning discovery of `[DefaultAmbientService]` implementations and of status checkers/auditors rather than an explicit registry, lazy construction rather than a startup phase, `AsyncLocal` call-context resolution rather than an explicit context parameter.

*What this costs:* reflection at startup, resolution that is invisible at the call site and therefore not greppable, non-deterministic-looking initialization order, and the assembly-load-context subtlety documented in the [Pledge](PLEDGE.md) — all of which the library owns rather than the consumer.

An alternative library could meet the same Pitch by requiring a registration call and a context object, and would be substantially simpler inside. That is the sibling this entry rejects.

## 3. Graceful absence over early failure *(public)*

Every service may be missing or suppressed, and the answer to a missing service is a no-op, never an exception. Helpers fold the null check in so consuming code does not carry it. Similarly, a default implementation that throws during construction is reported through `Ambient.InitializationError` and retried on a later read rather than thrown at whoever happened to touch the service first.

*What this costs:* a misconfiguration can be silent — logs that go nowhere, a cache that never caches — and the consumer must go looking rather than being told. This is the deliberate trade for services whose absence is, by definition, acceptable.

## 4. Deterministic testability over runtime directness *(public)*

Time, cancellation, and every service reach the code through an indirection that a test can replace for its own call context only. This is why `AmbientClock.UtcNow` and `AmbientClock.Ticks` are mandatory in place of `DateTime.UtcNow` and `Stopwatch`, why timers route through `AmbientEventTimer`, and why overrides are call-context-scoped rather than global — a suite must be able to run every test concurrently, repeatedly, in one process, with no test able to observe another's substitutions.

*What this costs:* an indirection on every timing path, `AsyncLocal` reads in hot paths, and a rule that is easy to violate accidentally by reaching for the framework API directly.

## 5. Host compatibility over implementation convenience *(public)*

The package has to be safe to drop into an arbitrary host, so it takes **no third-party dependencies** — only Microsoft-published .NET packages — targets .NET Standard 2.0 and 2.1 alongside .NET 8/9/10, and stays warning-free. Where a mature third-party library would do the job, the library writes its own instead (`RotatingFileBuffer`, the lock-free collections, the statistics accumulators).

*What this costs:* re-implementation of solved problems, less feature depth than a dedicated library would have, conditional compilation and polyfills for the older targets, and the ongoing cost of not being able to reach for a package.

## 6. Continuous negligible overhead over measurement precision *(public)*

The monitoring services — profiler, bottleneck detector, statistics, cost tracker, pressure monitor — are built to be left on in production. When precision and per-event cost conflict, cost wins: lock-free accumulation, per-context surveyors, rotating time windows, and raw broadcast of events to sinks rather than in-band accumulation or ranking.

*What this costs:* saturation figures, top-N rankings, and window boundaries are approximations that a consumer must interpret conservatively, and no figure here is a substitute for a profiler run when one is warranted.

## 7. The cost of a future asyncification over the cost of present simplicity *(private)*

The `lock` keyword and async-unfriendly lock types are not used anywhere, `.Result` / `GetAwaiter().GetResult()` / sync-over-async are avoided as a rule with async propagated up the call stack instead, `ValueTask` is preferred to `Task`, and `ConfigureAwait` is never used. Concurrency is lock-free where an algorithm exists and async-friendly waiting where one does not, with the reason for the wait written down at the site.

*What this costs:* lock-free algorithms that are harder to write, harder to read, and harder to prove correct than the mutual-exclusion versions that would behave identically today; and a handful of deliberate, individually documented exceptions where the surrounding contract is unavoidably synchronous.

## 8. Test concurrency and repeatability over test simplicity *(private)*

Every test must be runnable more than once at a time and concurrently with every other test. Tests therefore isolate through scoped overrides and unique per-test keys rather than shared fixtures or process-global state, and `[DoNotParallelize]` requires a full written justification.

*What this costs:* more setup per test than a shared fixture would need, and some assertions that must be written against a range rather than an exact value because a concurrent peer is running.

---

**A note on what is deliberately absent.** "Correctness over performance", "security over convenience", and "readability over cleverness" are not listed. They hold, but they are everyone's defaults; recording them would dilute the list without changing a single decision. An entry earns its place here only when a reasonable engineer might have ranked it the other way.
