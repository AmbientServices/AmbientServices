# Drift Ledger

Known disagreements between the code and its prose layers (Pitch/Pledge/Plan, summaries, comments), plus deferred TODO items. Per the 3P Protocol (`docs/MODULE_DESCRIPTIONS.md`), reconciling drift is a **fix**-category change agreed in prose first; until then it is recorded here so it isn't silently re-discovered or, worse, "corrected" in the wrong direction. Remove an entry in the same change that reconciles it.

Most items below were found while writing the initial library-wide 3P documentation (2026-07). The library functions as-is, so these are latent or minor — but each is a real disagreement between two expressions of intent.

## Likely bugs (code probably wrong)

- [ ] `BasicAmbientLocalCache.Retrieve` does not remove dispose-on-discard items on retrieval, though the `IAmbientLocalCache` Pledge and the companion doc promise single-consumer hand-off. (`DefaultImplementation/BasicAmbientLocalCache.cs` ~97–125)
- [ ] `BasicAmbientLocalCache.Remove<T>` removes the entry even when it isn't a `T`, returning default without disposing a dispose-on-discard entry — possible dispose leak on type mismatch.
- [ ] `AmbientTwoStageCache.Retrieve` never falls back to the shared tier when a local cache service exists; the class summary says it does. (`Helpers/AmbientTwoStageCache.cs` ~54–63)
- [ ] `StatusRating.RangeBackgroundColors` has 6 entries for 5 rating ranges, shifting the Okay/Superlative colors and leaving `#dfdfef` unreachable; disagrees with the class's own `StyleDefinition` CSS. (`Status/StatusRating.cs:83`)
- [ ] `ConcurrentHashSet<T>` set-algebra members (`IntersectWith`, `IsSubsetOf`, `IsProperSubsetOf`) build temporary `HashSet<T>`s with the default comparer, ignoring the construction-time `Comparer`. (`Types/ConcurrentHashSet.cs`)
- [ ] `Progress` cancellation-source ownership appears inverted: every constructor path sets `_ownCancelSource = true` but disposal only disposes when `!_ownCancelSource`, so sources are never disposed. (`DefaultImplementation/BasicAmbientProgress.cs:140,149,192`)
- [ ] `Progress.Update`: `_prefix + item ?? ""` binds as `(_prefix + item) ?? ""`, so a null item (meaning "don't update") still overwrites the parent's item with just the prefix.
- [ ] `BasicAmbientProgress.PopSubProgress` throws `InvalidOperationException` even in the "late pop" case its own comment says needs no action, and `Progress.Dispose` calls it — out-of-order disposal throws from `Dispose`.
- [ ] `ThreadPoolPressurePoint.Pressure`: `threadsAdded` reads `_previousSampleThreadCount` after `Interlocked.Exchange` already replaced it, so it is always 0 (the captured local is unused; `_threadsAddedThisSample` is set to the count and never read); and `Math.Min(0.0f, …)` clamps thread pressure to ≤0 — almost certainly meant `Math.Min(1.0f, …)`. (`Helpers/InternalPressurePoints.cs` ~156, ~167)
- [ ] `CallContextCostTracker` replaces per-service/per-customer accumulators (`_accumulatorsByService[serviceId] = new(charge)`) instead of accruing via `ChargeAccumulator.Accrue` as the process/time-window tracker does; breakdowns retain only the last report. Latent: never read. (`DefaultImplementation/BasicAmbientCostTracker.cs` ~251/268)
- [ ] `ArrayExtensions`: `ValueEquals` compares jagged arrays by value recursively, but `ValueHashCode` hashes nested arrays by reference, so value-equal jagged arrays can hash differently.
- [ ] `TaskExtensions.AsTask` never disposes its cancellation-token registration; repeated calls against a long-lived token accumulate registrations.
- [ ] `FilteredStackTrace.ShouldFilterMethodAfterFirst`/`FilterFrames` docs describe per-cluster collapsing ("first frame in a cluster"), but the `first` flag is only true for frame 0, so matching frames after frame 0 are all dropped.
- [ ] `MissingSampleHandlingExtensions` leading-gap extrapolation emits nearest-first (input `[null, null, 5, 7]` yields `[4, 3, 5, 7]` rather than `[3, 4, 5, 7]`) — needs a test and a decision. (`Utilities/StatisticsSampleExtrapolation.cs`)
- [ ] `InterlockedUtilities.TryAgainAfterOptimisticMissDelay` comments say sleep happens "between five and ten misses" and spinning "between one and three", but the code spins for attempts 3–4 and does nothing for attempts 1–2.

## Convention violations (project rules)

- [ ] Async-avoidance patterns: `BasicAmbientLocalCache.Store` uses `c.Dispose().AsTask().Wait()` inside `AddOrUpdate` (~138); `BasicAmbientAtomicCache.VersionedPut` uses `.AsTask().GetAwaiter().GetResult()` (~497).
- [ ] `DateTime.UtcNow` without a documented exception: `Helpers/LoggerHelpers.cs` (~270, ~352, ~526 — log-entry timestamps are not steerable by the ambient clock in tests) and `LinuxContainerCpuSampler` (`Helpers/CpuMonitor.cs`).
- [ ] `ConsoleBuffer.Flush` returns `Task` while its structural sibling `TraceBuffer.Flush` returns `ValueTask`.
- [ ] `StackTraceExtensions` sits in namespace `AmbientServices` rather than `AmbientServices.Extensions` like its siblings (breaking to move).

## Doc-only corrections (summaries/comments wrong; code fine)

- [ ] `AmbientLogSplitter` class summary says "writes log messages to a rotating set of files" (copy-paste from `AmbientFileLogger`); it fans entries out to registered loggers.
- [ ] `AmbientConsoleLogger` class summary says output is "effectively tossed unless running under a debugger" (copy-paste from the trace logger); console output goes to stdout regardless.
- [ ] `AmbientSettings` class summary is a copy-paste from the clock helpers ("utilizes the IAmbientClock…").
- [ ] `IAmbientSetting<T>` summary claims it "provides an event to notify the user of changes"; no such event exists.
- [ ] `StringExtensions.ReplaceOrdinal` summary says "Checks if a string contains a character"; it replaces a substring.
- [ ] `StringExtensions.CompareNaturalInvariant` summaries claim more leading zeros sort after fewer; the implementation zero-pads digit runs to a common width so "a007" and "a7" compare equal.
- [ ] `AssemblyExtensions.DoesAssemblyReferToAssembly` reads as transitive vs. its "Directly" sibling but performs only the direct check (in-code comment acknowledges the abandoned recursion).
- [ ] `StatusChecker` summary references "StatusTestNode", a stale former class name.
- [ ] `Status.RefreshAsync` summary says it returns checkers that "did not complete before cancellation or catastrophically failed"; faulted checkers get exception results recorded and are not returned.
- [ ] `DefaultPropertyThresholdsAttribute` ctor: `okayVsSuperlativeThreshold`/`alertVsOkayThreshold` param docs are swapped; the class ctor's "Default is LowIsGood" note sits on the wrong param; attribute default nature is `HighIsGood` while the class ctor default is `LowIsGood` — the inconsistency needs a deliberate decision. (`Status/StatusPropertyThresholds.cs` ~100, ~285)
- [ ] `PressureMonitor` summary says "A static class"; it is an instantiable `IDisposable`.
- [ ] `WindowScope` summary says it "extends System.DateTime"; it contains no extension methods and also operates on `TimeSpan`.
- [ ] `Pseudorandom.Next` doc says "Because Pseudorandom is a value type"; it is a class.
- [ ] `AmbientCostTrackerCoordinator.OnChargesAccrued` param doc reads "The charge (in s)" (truncated); its class summary and several cost/bottleneck registration summaries say "service profiler" (copy-paste).
- [ ] `IAmbientBottleneckDetector.RegisterAccessNotificationSink` param doc says notifications occur "when a bottleneck is entered" for an exit sink.
- [ ] `DefaultAmbientServiceAttribute` remarks claim "the constructor may be called more than once" under races — stale; `DefaultServiceImplementation<T>` constructs via a closed-generic static initializer, which the CLR runs once. Also says "public empty constructor" while non-public parameterless constructors are accepted.
- [ ] `TemporaryContextMutator.Dispose` summary says it reverts changes "applied in the constructor"; changes are applied by `ApplyContextChanges`.
- [ ] `IAmbientStatisticReader.StatisicType` is misspelled (public API — fixing requires an obsolete-alias migration).
- [ ] `StatusResultsBuilder.cs:15–21` contains a leftover "Unmerged change from project" merge-artifact comment block.
- [ ] `AmbientEventTimer.SchedulingClock` prefers `Override ?? Local`, but `AmbientService<T>.Local` already folds in the override — the code comment implies a distinction that doesn't exist.
- [ ] `CpuPressurePoint` comment references a nonexistent `_cpuMonitor`; `MemoryPressurePoint` computes an unused `linearPressureOffset` local.

## Semantic decisions needed (which side is right is unclear)

- [ ] Ongoing-cost units: `IAmbientCostTracker` says picodollars per **month**; `BasicAmbientCostTracker` param docs say per **minute** and `CostAccumulator` field names say per-minute. The 3P documents per-month (interface authority) pending a decision.
- [ ] `AmbientImmutableSettingsSet` does not subscribe to `SettingsRegistry.SettingRegistered` (the mutable sets do), so settings registered after construction keep raw strings as typed values — possibly deliberate for immutability; confirm and document.
- [ ] `AmbientSetting<T>` suppressed-service path is commented "use the default value" but `SettingInfo<T>.GlobalOrDefaultValue` can return the cached global-set value — decide whether suppression should yield the default.
- [ ] `AmbientSettings.GetSettingsSetSetting` param doc says a null set means "always the default value", but `SettingsSetSetting.GetValueSet()` falls back to the ambient local set.
