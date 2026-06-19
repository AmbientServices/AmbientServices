# Module Descriptions — AmbientServices

The methodology — The **3P Protocol** (3P; **Pitch** = Value Proposition, **Pledge** = Contract, **Plan** = Implementation), why they exist, and how to use and maintain them — is defined generically in `docs/MODULE_DESCRIPTIONS.md`. Read that first. This companion adds only what is specific to PicMeServer: where the three layers physically live in C# source, and worked examples.

## Where they live in the code (C#)

All three live in the type's XML-doc `<remarks>`, using **custom elements** named for the 3 P's in The 3P Protocol. They are chosen to be orthogonal to the standard XML-doc vocabulary — so the compiler's reference checking, which validates only recognized constructs such as `cref`, `param`, and `typeparam`, never tries to validate them:

```csharp
/// <remarks>
/// <pitch>…short triage description…</pitch>
/// <pledge><see cref="IFoo"/></pledge>
/// <pledge>…extensions this realization adds to IFoo's pledge (omit if none)…</pledge>
/// <plan>…algorithms, dependencies, trade-offs…</plan>
/// </remarks>
```

- An **abstraction** (interface) carries `<pitch>` and a `<pledge>` whose body is the behavioral-protocol prose.
- A **concrete realization** carries `<pitch>`, one or more `<pledge>` elements, and `<plan>`. A `<pledge>` containing a `<see cref>` to an abstraction means "this unit fulfills that abstraction's Pledge; its terms apply here." Additional `<pledge>` elements state realization-specific extensions; omit them when the realization adds nothing.

## Examples

- **Different Pitches, similar shape.** `IAmbientLocalCache` and `IAmbientSharedCache` share the same "store an item under a string key with optional expiration" shape (`Retrieve`/`Store`/`Remove`/`Clear`), but sell different things. `IAmbientLocalCache` pitches an in-process cache that can safely hold objects with references and `IDisposable` items (with hand-off-on-retrieve semantics); `IAmbientSharedCache` pitches a cache for serializable objects that may live in another process or machine. Same shape, different Pitches — and different Pledges, since only the local one promises to handle non-serializable and disposable values.
- **Shared Pledge, different Pitches.** `AmbientFileLogger` and `AmbientTraceLogger` (the `[DefaultAmbientService]`) both realize `IAmbientLogger`, obeying the identical Pledge, yet sell opposite trade-off profiles: `AmbientFileLogger` pitches durable, rotating on-disk logs that survive a restart, while `AmbientTraceLogger` pitches higher-performance debug/trace output that is effectively discarded unless a debugger is attached. Each Pitch is the caller-facing distillation of that realization's Plan trade-offs — durability versus speed.
- **Multiple Pledges.** `IAmbientLogger` defines the line-logging Pledge and `IAmbientStructuredLogger` defines the structured-data Pledge. A realization links each interface it implements as its own `<pledge>` element. `AmbientTraceLogger` links both (`<see cref="IAmbientLogger"/>` and `<see cref="IAmbientStructuredLogger"/>`) and adds nothing further. `AmbientFileLogger` links both, plus `IDisposable`, and additionally pledges realization-specific behavior — a file path/prefix, a configurable rotation period with daily suffix rollover at midnight UTC, and timed auto-flush — stated in an extension `<pledge>` alongside the `<see cref>` links.

## AI-oriented notes

Candidate units where a sidecar `*.ai.md` is likely worth the maintenance: `BasicAmbientAtomicCache` (optimistic-concurrency retries, parallel versioned/unversioned storage, and the linked timeout/cancellation budget), `AmbientFileLogger` (file rotation and retention via `RotatingFileBuffer`), and `BasicAmbientServiceProfiler` (scope-change accounting across the notification sinks).
