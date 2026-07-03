using System;

namespace AmbientServices.Utilities;

/// <summary>
/// A static class that adds utilities for <see cref="System.Array"/>.
/// </summary>
/// <remarks>
/// <pitch>The comparison and hashing engine behind <see cref="AmbientServices.Extensions.ArrayExtensions"/>: deep value equality and matching value hashing for arbitrary <see cref="Array"/> instances, including multidimensional and jagged arrays.</pitch>
/// <pledge>Arrays are equal only when they agree in rank and in length in every dimension and every corresponding pair of elements is equal; elements that are themselves arrays are compared by value recursively rather than by reference.  Two null arrays are equal; a null and a non-null array are not.  <see cref="ValueHashCode(Type, Array?)"/> recurses into nested arrays exactly where equality does, so value-equal arrays produce equal hash codes as long as leaf (non-array) element types hash by value.</pledge>
/// <plan>Multidimensional arrays are walked without recursion by mapping a single linear offset to a per-dimension cursor using precomputed dimension sizes; only nested (jagged) element arrays recurse.  The hash shares that traversal and recurses into nested arrays in lockstep with equality; a recursion-depth cap bounds the nesting defensively (statically-typed jagged arrays cannot actually cycle, since each nested level's element type is one array rank lower, so the cap never fires for valid inputs).</plan>
/// </remarks>
internal static class ArrayUtilities
{
    /// <summary>The value returned for a nested array deeper than <see cref="MaxHashRecursionDepth"/>; a defensive backstop that valid statically-typed arrays never reach.</summary>
    private const int CycleHashMarker = 0x0C0FFEE;
    /// <summary>The maximum nested-array recursion depth for <see cref="ValueHashCode(Type, Array?)"/>; far deeper than any real jagged array type nests.</summary>
    private const int MaxHashRecursionDepth = 64;

    /// <summary>
    /// Checks to see if the contents of two arrays are equal.
    /// </summary>
    /// <param name="elementType">The type of items in the array.</param>
    /// <param name="array1">The first array.</param>
    /// <param name="array2">The second array.</param>
    /// <returns>Whether or not the content of the arrays are equal.</returns>
    public static bool ValueEquals(Type elementType, Array? array1, Array? array2)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(elementType);
#else
        if (elementType is null) throw new ArgumentNullException(nameof(elementType));
#endif
        if (array1 == null)
        {
            return array2 == null;
        }
        else if (array2 == null)
        {
            return false;
        }
        // compare ranks
        int rank = array1.Rank;
        if (rank != array2.Rank) return false;
        // create a cursor and figure out how many items are contained within each dimension and every dimension below
        long[] cursor = new long[rank];
        long[] size = new long[rank + 1];
        size[rank] = 1;
        for (int dimension = rank - 1; dimension >= 0; --dimension)                // for example: [10,8,5]--> [10*8*5,8*5,5,1]
        {
            cursor[dimension] = 0;
            int array1DimensionLength = array1.GetLength(dimension);
            int array2DimensionLength = array2.GetLength(dimension);
            // lengths differ in this dimension?
            if (array1DimensionLength != array2DimensionLength) return false;
            size[dimension] = array1DimensionLength * size[dimension + 1];
        }
        // now loop through the arrays comparing each item
        for (long offset = 0; offset < size[0]; ++offset)
        {
            long remainder = offset;
            for (int dimension = 0; dimension < rank; ++dimension)
            {
                cursor[dimension] = remainder / size[dimension + 1];
                remainder %= size[dimension + 1];
            }
            bool eq = (elementType.IsArray)
                    // I could be wrong, but I'm pretty sure if elementType.IsArray is true, GetElementType() cannot return null
                ? ValueEquals(elementType.GetElementType()!, (Array?)array1.GetValue(cursor), (Array?)array2.GetValue(cursor))
                : Equals(array1.GetValue(cursor), array2.GetValue(cursor));
            if (!eq) return false;
        }
        // they are equal!
        return true;
    }
    /// <summary>
    /// Gets a value-based hash code for an array, recursing into nested (jagged/multidimensional) element arrays so the hash agrees with <see cref="ValueEquals(Type, Array?, Array?)"/>.
    /// </summary>
    /// <param name="elementType">The declared element type of the array.</param>
    /// <param name="array">The array to hash, or null.</param>
    /// <returns>A value-based hash code, or zero for a null array.</returns>
    public static int ValueHashCode(Type elementType, Array? array)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(elementType);
#else
        if (elementType is null) throw new ArgumentNullException(nameof(elementType));
#endif
        return ValueHashCode(elementType, array, 0);
    }
    private static int ValueHashCode(Type elementType, Array? array, int depth)
    {
        if (array == null) return 0;
        // recursion guard: bound the nested-array depth so a pathological input cannot recurse without limit (valid jagged arrays never approach this)
        if (depth > MaxHashRecursionDepth) return CycleHashMarker;
        int rank = array.Rank;
        int code = rank;
        long[] cursor = new long[rank];
        long[] size = new long[rank + 1];
        size[rank] = 1;
        for (int dimension = rank - 1; dimension >= 0; --dimension)
        {
            int dimensionLength = array.GetLength(dimension);
            code = code * 31 + dimensionLength;    // fold each dimension's length into the hash so different shapes hash differently
            size[dimension] = dimensionLength * size[dimension + 1];
        }
        bool elementIsArray = elementType.IsArray;
        Type? nestedElementType = elementIsArray ? elementType.GetElementType() : null;
        // walk every element via the same linear-offset/cursor scheme as ValueEquals, recursing into nested arrays exactly where equality does
        for (long offset = 0; offset < size[0]; ++offset)
        {
            long remainder = offset;
            for (int dimension = 0; dimension < rank; ++dimension)
            {
                cursor[dimension] = remainder / size[dimension + 1];
                remainder %= size[dimension + 1];
            }
            object? element = array.GetValue(cursor);
            int elemhashcode = elementIsArray
                ? ValueHashCode(nestedElementType!, (Array?)element, depth + 1)
                : (element?.GetHashCode() ?? 0);
            int shift = (int)(offset % 32);
            code ^= (elemhashcode >> (32 - shift)) ^ (elemhashcode << shift) ^ 0x1A7FCA3B;
        }
        return code;
    }
}
