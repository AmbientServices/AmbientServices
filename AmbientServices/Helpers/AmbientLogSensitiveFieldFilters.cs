using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AmbientServices;

/// <summary>
/// Registry of per-caller regex filters used to mask sensitive field names in structured log output.
/// Multiple unrelated assemblies may each register filters; all active filters are applied when rendering log data.
/// </summary>
public static class AmbientLogSensitiveFieldFilters
{
    /// <summary>
    /// The string substituted for values whose field names match a registered sensitive-field pattern.
    /// </summary>
    public const string MaskedValue = "***";

    private static int _nextRegistrationId;
    private static readonly ConcurrentDictionary<int, Regex> _filters = new();

    /// <summary>
    /// Registers a regex that matches sensitive log field names (property or dictionary keys).
    /// </summary>
    /// <remarks>
    /// Unregister by disposing the returned <see cref="IDisposable"/> (for example with <c>using</c> or by calling <see cref="IDisposable.Dispose"/>).
    /// Each caller should keep and dispose its own registration; disposing one registration does not affect filters registered by other callers.
    /// </remarks>
    /// <param name="fieldNamePattern">A regex matched against field names (not values).</param>
    /// <returns>An <see cref="IDisposable"/> that removes this filter when disposed.</returns>
    public static IDisposable RegisterFieldNameFilter(Regex fieldNamePattern)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fieldNamePattern);
#else
        if (fieldNamePattern is null) throw new ArgumentNullException(nameof(fieldNamePattern));
#endif
        int id = Interlocked.Increment(ref _nextRegistrationId);
        _filters[id] = fieldNamePattern;
        return new Registration(id);
    }

    /// <summary>
    /// Registers a regex that matches sensitive log field names (property or dictionary keys). Dispose the returned instance to unregister.
    /// </summary>
    /// <param name="pattern">The regex pattern string.</param>
    /// <param name="options">Regex options. Defaults to <see cref="RegexOptions.IgnoreCase"/> | <see cref="RegexOptions.CultureInvariant"/>.</param>
    /// <returns>An <see cref="IDisposable"/> that removes this filter when disposed.</returns>
    public static IDisposable RegisterFieldNameFilter(string pattern, RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(pattern);
#else
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
#endif
        return RegisterFieldNameFilter(new Regex(pattern, options));
    }

    /// <summary>
    /// Returns whether <paramref name="fieldName"/> matches any currently registered sensitive-field filter.
    /// </summary>
    public static bool ShouldMaskFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;
        foreach (Regex filter in _filters.Values)
        {
            if (filter.IsMatch(fieldName)) return true;
        }
        return false;
    }

    /// <summary>
    /// Masks <paramref name="value"/> when <paramref name="fieldName"/> matches a registered filter; otherwise returns the original value.
    /// </summary>
    public static object? MaskValueIfSensitive(string fieldName, object? value)
    {
        return ShouldMaskFieldName(fieldName) ? MaskedValue : value;
    }

    /// <summary>
    /// Returns a snapshot of the currently registered filters (for diagnostics and tests).
    /// </summary>
    public static IReadOnlyCollection<Regex> GetRegisteredFilters()
    {
        return _filters.Values.ToArray();
    }

    private sealed class Registration : IDisposable
    {
        private readonly int _id;
        private int _disposed;

        public Registration(int id) => _id = id;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _ = _filters.TryRemove(_id, out _);
        }
    }
}
