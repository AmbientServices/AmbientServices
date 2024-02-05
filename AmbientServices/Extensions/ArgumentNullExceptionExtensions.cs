using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace AmbientServices.Extensions;

#if !NET5_0_OR_GREATER
public static class ArgumentNullExceptionExtensions
{
    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull(
#if !NETSTANDARD2_0
        [NotNull]
#endif
        object? argument,
#if !NETSTANDARD2_0 && !NETSTANDARD2_1
        [CallerArgumentExpression("argument")] 
#endif
        string? paramName = null)
    {
        if (argument is null) Throw(paramName);
    }
#if !NETSTANDARD2_0
    [DoesNotReturn]
#endif
    private static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
}
#endif
