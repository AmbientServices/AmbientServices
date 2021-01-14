using System.Collections.Generic;
using System.Collections.Immutable;

namespace AmbientServices
{
    internal static class ImmutableArrayExtensions
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
}
