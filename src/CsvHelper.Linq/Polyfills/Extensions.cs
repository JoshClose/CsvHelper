#if !NET
using System;
using System.Runtime.CompilerServices;

namespace CsvHelper.Linq;

/// <summary>Static members that .NET Standard 2.0 lacks, as C# 14 static extension members.</summary>
internal static class PolyfillStaticExtensions
{
	extension(ArgumentNullException)
	{
		/// <summary>
		/// Throws when an argument is <see langword="null"/>; stands in for <c>ArgumentNullException.ThrowIfNull</c>.
		/// </summary>
		/// <param name="argument">The argument to check.</param>
		/// <param name="paramName">The name of the parameter, which the compiler fills in from the argument expression.</param>
		/// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
		public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
		{
			if (argument is null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}

	extension(ArgumentOutOfRangeException)
	{
		/// <summary>
		/// Throws when a value is negative; stands in for <c>ArgumentOutOfRangeException.ThrowIfNegative</c>.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <param name="paramName">The name of the parameter, which the compiler fills in from the argument expression.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
		public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must not be negative.");
			}
		}

		/// <summary>
		/// Throws when a value is zero or negative; stands in for <c>ArgumentOutOfRangeException.ThrowIfNegativeOrZero</c>.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <param name="paramName">The name of the parameter, which the compiler fills in from the argument expression.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
		public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value <= 0)
			{
				throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive.");
			}
		}
	}
}
#endif
