using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices;

/// <summary>
/// Extension-based monotonic split-cache API for <see cref="IAmbientAtomicCache"/> on all target frameworks
/// (versioned head + unversioned payload per revision). Implemented as extensions so netstandard2.0 and similar
/// targets get the same API as modern runtimes without relying on default interface members.
/// </summary>
public static class AmbientAtomicSplitCacheExtensions
{
    /// <summary>
    /// Separator (ASCII unit separator) reserved for split-cache key composition. It must not appear in <c>baseLogicalKey</c>.
    /// </summary>
    public const char MonotonicSplitCacheKeySeparator = '\u001f';

    /// <summary>
    /// Logical key passed to <see cref="IAmbientAtomicCache.VersionedGet{T}"/> / <see cref="IAmbientAtomicCache.VersionedPut{T}"/> for the split-cache head.
    /// </summary>
    public static string GetMonotonicSplitCacheHeadKey(string baseLogicalKey)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(baseLogicalKey);
#else
        if (baseLogicalKey is null) throw new ArgumentNullException(nameof(baseLogicalKey));
#endif
        return baseLogicalKey + MonotonicSplitCacheKeySeparator + "ambient.split.head" + MonotonicSplitCacheKeySeparator;
    }

    /// <summary>
    /// Logical key passed to <see cref="IAmbientAtomicCache.GetOrAdd{T}"/> / <see cref="IAmbientAtomicCache.Remove{T}"/> for the split-cache payload at <paramref name="headVersion"/>.
    /// </summary>
    public static string GetMonotonicSplitCachePayloadKey(string baseLogicalKey, long headVersion)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(baseLogicalKey);
#else
        if (baseLogicalKey is null) throw new ArgumentNullException(nameof(baseLogicalKey));
#endif
        return baseLogicalKey + MonotonicSplitCacheKeySeparator + "ambient.split.payload" + MonotonicSplitCacheKeySeparator + headVersion.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the split-cache versioned head only (step one of a two-step read).
    /// </summary>
    public static ValueTask<(THead? Head, long HeadVersion)> MonotonicSplitCacheGetHeadAsync<THead>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long minHeadVersion = -1,
        TimeSpan? headRefresh = null,
        TimeSpan? headTimeout = null,
        CancellationToken cancel = default)
        where THead : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
#endif
        return cache.VersionedGet<THead>(GetMonotonicSplitCacheHeadKey(baseLogicalKey), minHeadVersion, headRefresh, headTimeout, cancel);
    }

    /// <summary>
    /// Writes a new split-cache head revision (step one of a publish). Returns the new monotonic version to use for <see cref="GetMonotonicSplitCachePayloadKey"/>.
    /// </summary>
    public static ValueTask<long> MonotonicSplitCachePutHeadAsync<THead>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        THead head,
        TimeSpan? maxCacheDuration = null,
        DateTime? expiration = null,
        TimeSpan? timeout = null,
        CancellationToken cancel = default)
        where THead : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(head);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (head is null) throw new ArgumentNullException(nameof(head));
#endif
        return cache.VersionedPut(GetMonotonicSplitCacheHeadKey(baseLogicalKey), head, maxCacheDuration, expiration, timeout, cancel);
    }

    /// <summary>
    /// Resolves or creates the split-cache payload for a known <paramref name="headVersion"/> (step two of a read/write).
    /// </summary>
    public static ValueTask<TPayload> MonotonicSplitCacheGetOrAddPayloadAsync<TPayload>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long headVersion,
        Func<ValueTask<(TPayload Item, DateTime? Expires)>> create,
        TimeSpan? payloadRefresh = null,
        TimeSpan? payloadTimeout = null,
        CancellationToken cancel = default)
        where TPayload : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(create);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (create is null) throw new ArgumentNullException(nameof(create));
#endif
        return cache.GetOrAdd(GetMonotonicSplitCachePayloadKey(baseLogicalKey, headVersion), create, payloadRefresh, payloadTimeout, cancel);
    }

    /// <summary>
    /// Two-step read: versioned head (staleness via <paramref name="minHeadVersion"/>), then unversioned payload for that head revision.
    /// When the head is missing, expired, or rejected as too old for <paramref name="minHeadVersion"/>, the payload is not loaded and <c>Head</c> is null.
    /// </summary>
    public static async ValueTask<(THead? Head, TPayload? Payload, long HeadVersion)> MonotonicSplitCacheGetHeadAndPayloadAsync<THead, TPayload>(
        this IAmbientAtomicCache cache,
        string baseLogicalKey,
        long minHeadVersion,
        Func<ValueTask<(TPayload Item, DateTime? Expires)>> getOrCreatePayload,
        TimeSpan? headRefresh = null,
        TimeSpan? headTimeout = null,
        TimeSpan? payloadRefresh = null,
        TimeSpan? payloadTimeout = null,
        CancellationToken cancel = default)
        where THead : class
        where TPayload : class
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(getOrCreatePayload);
#else
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (getOrCreatePayload is null) throw new ArgumentNullException(nameof(getOrCreatePayload));
#endif
        (THead? head, long v) = await cache.MonotonicSplitCacheGetHeadAsync<THead>(baseLogicalKey, minHeadVersion, headRefresh, headTimeout, cancel);
        if (head is null)
        {
            return (null, null, v);
        }

        TPayload payload = await cache.MonotonicSplitCacheGetOrAddPayloadAsync(baseLogicalKey, v, getOrCreatePayload, payloadRefresh, payloadTimeout, cancel);
        return (head, payload, v);
    }
}
