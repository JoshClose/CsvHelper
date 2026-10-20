#if !NET
using System.Runtime.CompilerServices;
using CsvHelper;

namespace System.Numerics
{
	/// <summary>The members of .NET's <c>System.Numerics.BitOperations</c> that the library uses.</summary>
	internal static class BitOperations
	{
		/// <summary>The number of set bits; stands in for <c>BitOperations.PopCount(ulong)</c>.</summary>
		/// <param name="value">The value whose bits are counted.</param>
		/// <returns>The number of bits of <paramref name="value"/> that are 1, from 0 to 64.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PopCount(ulong value) => Portable.PopCount(value);

		/// <summary>The number of set bits; stands in for <c>BitOperations.PopCount(uint)</c>.</summary>
		/// <param name="value">The value whose bits are counted.</param>
		/// <returns>The number of bits of <paramref name="value"/> that are 1, from 0 to 32.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PopCount(uint value) => Portable.PopCount(value);

		/// <summary>The number of zero bits below the lowest set bit; stands in for <c>BitOperations.TrailingZeroCount(ulong)</c>.</summary>
		/// <param name="value">The value whose low zero bits are counted.</param>
		/// <returns>The index of the lowest set bit of <paramref name="value"/>, or 64 when <paramref name="value"/> is 0.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int TrailingZeroCount(ulong value) => Portable.TrailingZeroCount(value);

		/// <summary>The number of zero bits below the lowest set bit; stands in for <c>BitOperations.TrailingZeroCount(uint)</c>.</summary>
		/// <param name="value">The value whose low zero bits are counted.</param>
		/// <returns>The index of the lowest set bit of <paramref name="value"/>, or 32 when <paramref name="value"/> is 0.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int TrailingZeroCount(uint value) => Portable.TrailingZeroCount(value);

		/// <summary>The number of zero bits above the highest set bit; stands in for <c>BitOperations.LeadingZeroCount(ulong)</c>.</summary>
		/// <param name="value">The value whose high zero bits are counted.</param>
		/// <returns>The number of leading zero bits of <paramref name="value"/>, or 64 when <paramref name="value"/> is 0.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeadingZeroCount(ulong value) => Portable.LeadingZeroCount(value);

		/// <summary>The number of zero bits above the highest set bit; stands in for <c>BitOperations.LeadingZeroCount(uint)</c>.</summary>
		/// <param name="value">The value whose high zero bits are counted.</param>
		/// <returns>The number of leading zero bits of <paramref name="value"/>, or 32 when <paramref name="value"/> is 0.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeadingZeroCount(uint value) => Portable.LeadingZeroCount(value);

		/// <summary>Rotates the bits right; stands in for <c>BitOperations.RotateRight(ulong, int)</c>.</summary>
		/// <param name="value">The value to rotate.</param>
		/// <param name="offset">The number of bits to rotate by; only its low 6 bits are used.</param>
		/// <returns><paramref name="value"/> rotated right by <paramref name="offset"/> bits.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RotateRight(ulong value, int offset) => Portable.RotateRight(value, offset);

		/// <summary>Rotates the bits right; stands in for <c>BitOperations.RotateRight(uint, int)</c>.</summary>
		/// <param name="value">The value to rotate.</param>
		/// <param name="offset">The number of bits to rotate by; only its low 5 bits are used.</param>
		/// <returns><paramref name="value"/> rotated right by <paramref name="offset"/> bits.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotateRight(uint value, int offset) => Portable.RotateRight(value, offset);
	}
}
#endif
