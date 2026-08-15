# AmbientServices — Pitch

*This is the whole-library **Pitch** layer of the 5P Protocol — the "is this what I need?" triage layer for the project as a single unit. See [MODULE_DESCRIPTIONS.md](MODULE_DESCRIPTIONS.md) for what the 5P are, [PLEDGE.md](PLEDGE.md) for what the library promises, [PLAN.md](PLAN.md) for how it is built, and [PRIORITY.md](PRIORITY.md) for which way it bends when it cannot satisfy everything.*

## The problem

Almost every non-trivial program leans on a handful of services that are simultaneously **ubiquitous** and **optional**: caching, logging, settings, progress/cancellation, performance monitoring, backend status checks, and maybe an artificially-controllable clock. They are needed almost everywhere, yet any single program may want a rich implementation, a trivial one, or none at all, sometimes using different ones in different contexts in the same program, especially when running tests.

Dependency injection is the usual answer, but it charges a high price for services that are optional anyway. The consumer has to thread each dependency through every constructor — or worse, every function — and when a library it uses *adds or removes* one of these dependencies, every one of those construction sites has to change, and if the location of that call is deep within the system, this can result in a lot of changes, and middle-layer code that inexplicably takes parameters for these services needed by lower layers, whose implementation should be completely hidden from those layers. For services that are required, that cost may be justified. For services that are environmental and optional, it is mostly friction.

## What this library gives you

AmbientServices lets library authors program against small **ambient** interfaces and lets consumers supply implementations — or not — with a single registration that every participating library automatically picks up. A library gains the benefit of a new ambient service without changing its public surface, and a consumer adopts (or ignores) that service without editing a single call site.  It's like adding features to the language instead of tracking and building it all yourself

You would reach for AmbientServices when you want:

- To use cross-cutting services (cache, clock, logging, settings, progress) without plumbing them through every layer of your system.
- To ship a library whose optional dependencies can be upgraded by the consumer, later, from the outside.
- Always-on, low-overhead performance and saturation monitoring that does not require a profiler run.
- Automated backend status checking with concise, farm-wide rollups.

## What it deliberately is not

- **Not a replacement for DI of *required* services.** Ambient resolution is for things whose absence is acceptable and whose presence is environmental; genuinely required collaborators still belong in constructors.
- **Not a channel for values that change outputs in ways the caller cares about.** By design, an ambient service must behave environmentally — see [PLEDGE.md](PLEDGE.md). Caching may return bounded-stale results, a clock may simulate time, settings act as inputs, but nothing may alter a function's input→output relationship *unexpectedly*.
- **Not a heavyweight framework.** It takes no third-party dependencies — only Microsoft-published .NET packages — and stays out of your object graph until you ask for a service.

## What's inside (so you can decide)

- **Basic services** — local cache, atomic (single-flight) cache, clock, logger, progress/cancellation, and settings.
- **Performance services** — statistics, an always-on coarse-grained service profiler, and a bottleneck/saturation detector for estimating scalability before load testing.
- **Status** — background backend dependency testing with heterogeneous/homogeneous aggregation and SMS-and-detail rollups across server farms.
- **Utilities** — notably `DisposeResponsibility<T>` for making dispose ownership visible and leak-detectable, plus assorted threading, string, and formatting helpers.

If the per-service trade-offs are what you're weighing, each interface and each implementation carries its own Pitch in its XML-doc `<remarks>`; this file is only the library-wide decision.
