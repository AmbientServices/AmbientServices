using System.Collections.Generic;
using System.Collections.Immutable;

namespace AmbientServices.Utilities;

/// <summary>
/// A static class that contains utilities for <see cref="ImmutableArray{T}"/>.
/// </summary>
/// <remarks>
/// <pitch>Builds an <see cref="ImmutableArray{T}"/> from any <see cref="IEnumerable{T}"/> uniformly across target frameworks whose immutable-collections surface lacks a convenient conversion.</pitch>
/// </remarks>
internal static class ImmutableArrayUtilities
{
    /// <summary>
    /// Creates an <see cref="ImmutableArray{T}"/> from an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type in the enumerable and array.</typeparam>
    /// <param name="source">The source enumeration.</param>
    /// <returns></returns>
    public static ImmutableArray<T> FromEnumerable<T>(IEnumerable<T> source)
    {
        ImmutableArray<T> ret = ImmutableArray<T>.Empty;
        return ret.AddRange(source);
    }
}
