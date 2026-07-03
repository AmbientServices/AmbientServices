using System;
using System.Linq;

namespace AmbientServices.Utilities;

/// <summary>
/// A static class that holds extensions to the system <see cref="Enum"/> class.
/// </summary>
/// <remarks>
/// <pitch>The largest defined value of an enum type without paying reflection cost on every call.</pitch>
/// <plan>Delegates to <see cref="EnumMax{T}"/>, whose static initializer enumerates the enum's defined values once per closed generic type and caches the maximum for the process lifetime; an enum with no defined values yields the type's default value.</plan>
/// </remarks>
internal static class EnumUtilities
{
    /// <summary>
    /// Returns the highest possible value for an enum.
    /// </summary>
    /// <typeparam name="T">The enum to get the maximum value for.</typeparam>
    /// <returns>The highest enum value.</returns>
    public static T MaxEnumValue<T>() where T : Enum
    {
        return EnumMax<T>.Max;
    }
}

/// <summary>
/// A static class that holds onto the computed max enum value.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
/// <pitch>The per-enum-type cache backing <see cref="EnumUtilities.MaxEnumValue{T}"/>; the CLR's generic static initialization provides the once-per-type, thread-safe computation.</pitch>
/// </remarks>
internal static class EnumMax<T> where T : Enum
{
    private static T Init()
    {
        Array a = Enum.GetValues(typeof(T))!;   // I don't think it's possible to have a System.Enum for which Enum.GetValues returns null
        return a.Length == 0
            ? default!                          // apparently the compiler isn't smart enough to know that even though System.Enum is a class, any derived types are value types
            : a.Cast<T>().Max()!;
    }
    public static T Max { get; } = Init();
}
