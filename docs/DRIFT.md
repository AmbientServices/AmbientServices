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


## Semantic decisions needed (which side is right is unclear)


