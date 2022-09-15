using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace AmbientServices.Utilities
{
    /// <summary>
    /// A static class that adds utilities for <see cref="System.Array"/>.
    /// </summary>
    internal static class ArrayUtilities
    {
        /// <summary>
        /// Checks to see if the contents of two arrays are equal.
        /// </summary>
        /// <param name="elementType">The type of items in the array.</param>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns>Whether or not the content of the arrays are equal.</returns>
        public static bool ValueEquals(Type elementType, Array? array1, Array? array2)
        {
            if (elementType == null) throw new ArgumentNullException(nameof(elementType));
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
    }
}
