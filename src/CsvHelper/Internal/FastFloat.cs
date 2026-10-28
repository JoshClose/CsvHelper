using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;

namespace CsvHelper;

/// <summary>
/// Fast, exactly rounded parsing of decimal floating point numbers from UTF-8: a port of Daniel Lemire's fast_float
/// (Clinger's fast path followed by the Eisel-Lemire algorithm, see "Number Parsing at a Gigabyte per Second" and
/// "Fast Number Parsing Without Fallback"). Whenever it returns true the result is bit-identical to double.Parse /
/// float.Parse with the invariant culture (round half to even, -0, subnormals, overflow to infinity, underflow to zero).
/// It returns false, leaving the input to the BCL, for anything outside <c>[+-]digits[.digits][(e|E)[+-]digits]</c>
/// (whitespace, thousands separators, hex, "inf", ...) other than Infinity/NaN, and for the rare inputs with more than
/// 19 significant digits whose truncation to 19 digits sits right on a rounding boundary.
/// </summary>
internal static partial class FastFloat
{
	// binary64 parameters (see fast_float's binary_format<double>).
	private const int DoubleMantissaBits = 52;
	private const int DoubleMinimumExponent = -1023;
	private const int DoubleMinExponentRoundToEven = -4;
	private const int DoubleMaxExponentRoundToEven = 23;
	private const int DoubleSmallestPowerOfTen = -342;
	private const int DoubleLargestPowerOfTen = 308;
	private const ulong DoubleInfinityBits = 0x7FF0000000000000UL;

	// binary32 parameters (see fast_float's binary_format<float>).
	private const int SingleMantissaBits = 23;
	private const int SingleMinimumExponent = -127;
	private const int SingleMinExponentRoundToEven = -17;
	private const int SingleMaxExponentRoundToEven = 10;
	private const int SingleSmallestPowerOfTen = -64;
	private const int SingleLargestPowerOfTen = 38;
	private const ulong SingleInfinityBits = 0x7F800000UL;

	/// <summary>Returned by the Eisel-Lemire routines when the result cannot be decided (never a valid result).</summary>
	private const ulong Undecided = ulong.MaxValue;

	/// <summary>Exactly representable powers of ten for Clinger's fast path: 10^0..10^22.</summary>
#if NET
	private static ReadOnlySpan<double> DoublePowersOfTen =>
#else
	// Without RuntimeHelpers.CreateSpan the property would allocate the array on every access.
	private static ReadOnlySpan<double> DoublePowersOfTen => doublePowersOfTen;
	private static readonly double[] doublePowersOfTen =
#endif
	[
		1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
		1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
	];

#if NET
	private static ReadOnlySpan<ulong> UInt64PowersOfTen => [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000];
#else
	private static ReadOnlySpan<ulong> UInt64PowersOfTen => uint64PowersOfTen;
	private static readonly ulong[] uint64PowersOfTen = [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000];
#endif

	/// <summary>Parses all of <paramref name="s"/> as a double, or returns false to defer to double.Parse.</summary>
	/// <param name="s">The UTF-8 text of the number, such as <c>-1.5e3</c>.</param>
	/// <param name="value">
	/// The exactly rounded value, bit-identical to <c>double.Parse</c> with the invariant culture; meaningless when the
	/// method returns false.
	/// </param>
	/// <returns>Whether <paramref name="s"/> was parsed; false leaves it to <c>double.Parse</c>.</returns>
	public static bool TryParseDouble(ReadOnlySpan<byte> s, out double value) => TryParse(s, out value);

	/// <summary>Parses all of <paramref name="s"/> as a float (rounded once, directly), or returns false to defer to float.Parse.</summary>
	/// <param name="s">The UTF-8 text of the number, such as <c>-1.5e3</c>.</param>
	/// <param name="value">
	/// The exactly rounded value, bit-identical to <c>float.Parse</c> with the invariant culture; meaningless when the
	/// method returns false.
	/// </param>
	/// <returns>Whether <paramref name="s"/> was parsed; false leaves it to <c>float.Parse</c>.</returns>
	public static bool TryParseSingle(ReadOnlySpan<byte> s, out float value) => TryParse(s, out value);

	/// <summary>
	/// The hot path, specialized for T = double and T = float. Parses
	/// <c>[+-]digits[.digits][(e|E)[+-]digits]</c> with at most 19 mantissa digits; everything else (invalid input,
	/// Infinity/NaN, longer mantissas) goes to <see cref="TryParseSlow"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryParse<T>(ReadOnlySpan<byte> s, out T value) where T : struct
	{
		ref var p = ref MemoryMarshal.GetReference(s);
		nint len = s.Length;
		if (len == 0) goto Slow;

		nint i = 0;
		var negative = false;
		uint c = p;
		if (c == '-')
		{
			negative = true;
			i = 1;
		}
		else if (c == '+')
		{
			i = 1;
		}

		// Integer part (usually short: a plain loop is cheapest).
		var start = i;
		var w = 0UL;
		while (i < len)
		{
			var d = Unsafe.Add(ref p, i) - (ulong)'0';
			if (d > 9) break;
			w = w * 10 + d; // may wrap with more than 19 digits, which go to the slow path
			i++;
		}

		var digits = i - start;

		// Fraction: eight digits at a time and the tail without a branch per digit.
		var q = 0L;
		if (i < len && Unsafe.Add(ref p, i) == '.')
		{
			i++;
			var fractionStart = i;
			if (len >= 8)
			{
				i = ParseDigitRun(ref p, i, len, ref w);
			}
			else
			{
				while (i < len)
				{
					var d = Unsafe.Add(ref p, i) - (ulong)'0';
					if (d > 9) break;
					w = w * 10 + d;
					i++;
				}
			}

			digits += i - fractionStart;
			q = fractionStart - i;
		}

		if (digits == 0) goto Slow; // no digits: maybe Infinity or NaN

		if (i < len)
		{
			if ((Unsafe.Add(ref p, i) | 0x20) != 'e') goto Slow;
			i++;
			var negativeExponent = false;
			if (i < len)
			{
				c = Unsafe.Add(ref p, i);
				if (c == '-')
				{
					negativeExponent = true;
					i++;
				}
				else if (c == '+')
				{
					i++;
				}
			}

			if (i >= len) goto Slow;
			var e = 0L;
			do
			{
				var d = Unsafe.Add(ref p, i) - (ulong)'0';
				if (d > 9) goto Slow;
				if (e < 0x10000000) e = e * 10 + (long)d; // saturate: anything this large is zero or infinity anyway
				i++;
			}
			while (i < len);
			q += negativeExponent ? -e : e;
		}

		if (digits > 19) goto Slow;

		// Clinger's fast path: w and 10^|q| are exact doubles, so a single correctly rounded operation gives the
		// correctly rounded double.
		if (w <= (1UL << 53) && (ulong)(q + 22) <= 44)
		{
			double d = (long)w;
			ref var pow10 = ref MemoryMarshal.GetReference(DoublePowersOfTen);
			if (q < 0) d /= Unsafe.Add(ref pow10, (nint)(-q));
			else d *= Unsafe.Add(ref pow10, (nint)q);
			if (typeof(T) == typeof(double))
			{
				value = Unsafe.BitCast<double, T>(negative ? -d : d);

				return true;
			}

			// For float, rounding the double again gives the correctly rounded float unless the double landed
			// exactly on a midpoint between two floats (the exact value may lie on either side): leave those to
			// Eisel-Lemire. The result is always within the normal float range here.
#if NET
			if ((BitConverter.DoubleToUInt64Bits(d) & 0x1FFFFFFF) != 0x10000000)
#else
			if (((ulong)BitConverter.DoubleToInt64Bits(d) & 0x1FFFFFFF) != 0x10000000)
#endif
			{
				var f = (float)d;
				value = Unsafe.BitCast<float, T>(negative ? -f : f);

				return true;
			}
		}

		return Finish(typeof(T) == typeof(double) ? ComputeDouble(q, w) : ComputeSingle(q, w), negative, out value);

	Slow:
		return TryParseSlow(s, out value);
	}

	/// <summary>
	/// Accumulates the run of ASCII digits starting at <paramref name="i"/> into <paramref name="m"/> eight bytes at a
	/// time, without a branch per digit, and returns the index after the run. Requires len &gt;= 8; never reads
	/// outside [0, len).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static nint ParseDigitRun(ref byte p, nint i, nint len, ref ulong m)
	{
		while (true)
		{
			var remaining = len - i;
			ulong v;
			if (remaining >= 8) v = ReadEightBytes(ref Unsafe.Add(ref p, i));
			// Near the end: the last eight bytes, shifted so that byte i comes first and the bytes past the end are 0.
			else if (remaining > 0) v = ReadEightBytes(ref Unsafe.Add(ref p, len - 8)) >> (int)(64 - 8 * remaining);
			else return i;
			// Digit values; a byte is a digit if its value is at most 9. Borrows and carries only travel upwards, so
			// the lowest non-digit byte is always detected correctly, which is all that is needed.
			var d = v - 0x3030303030303030UL;
			var nonDigits = ((d + 0x7676767676767676UL) | d) & 0x8080808080808080UL;
			if (nonDigits == 0)
			{
				m = m * 100000000 + ConvertEightDigits(d);
				i += 8;
				continue;
			}

			// n leading digits (0..7): shift them to the top bytes (the zeros below act as leading zeros).
			var n = BitOperations.TrailingZeroCount(nonDigits) >> 3;
			if (n != 0)
			{
				m = m * Unsafe.Add(ref MemoryMarshal.GetReference(UInt64PowersOfTen), n) + ConvertEightDigits(d << (64 - 8 * n));
				i += n;
			}

			return i;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong ReadEightBytes(ref byte p)
	{
		var v = Unsafe.ReadUnaligned<ulong>(ref p);

		return BitConverter.IsLittleEndian ? v : BinaryPrimitives.ReverseEndianness(v);
	}

	/// <summary>Converts eight digit values (0..9, first digit in the lowest byte) to their value (credit: aqrit).</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint ConvertEightDigits(ulong d)
	{
		d = d * 10 + (d >> 8);
		d = ((d & 0x000000FF000000FFUL) * 0x000F424000000064UL + ((d >> 16) & 0x000000FF000000FFUL) * 0x0000271000000001UL) >> 32;

		return (uint)d;
	}

	/// <summary>Applies the sign to the unsigned IEEE bits of the result, or fails for <see cref="Undecided"/>.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool Finish<T>(ulong bits, bool negative, out T value) where T : struct
	{
		if (bits == Undecided)
		{
			value = default;

			return false;
		}

		if (typeof(T) == typeof(double))
		{
			if (negative) bits |= 1UL << 63;
			value = Unsafe.BitCast<ulong, T>(bits);
		}
		else
		{
			var b = (uint)bits;
			if (negative) b |= 1U << 31;
			value = Unsafe.BitCast<uint, T>(b);
		}

		return true;
	}

	/// <summary>
	/// Everything the hot path does not handle: validates the input, accepts Infinity/NaN as double.Parse does, and
	/// handles mantissas with more than 19 digits by truncating them to 19 significant digits w: the value then lies
	/// in [w, w + 1) * 10^q and is only accepted when both ends round to the same result.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TryParseSlow<T>(ReadOnlySpan<byte> s, out T value) where T : struct
	{
		if (!TryParseDecimalSlow(s, out var w, out var q, out var negative, out var truncated))
			return TryParseSpecial(s, out value);
		var bits = typeof(T) == typeof(double) ? ComputeDouble(q, w) : ComputeSingle(q, w);
		if (truncated && bits != (typeof(T) == typeof(double) ? ComputeDouble(q, w + 1) : ComputeSingle(q, w + 1)))
			bits = Undecided;

		return Finish(bits, negative, out value);
	}

	/// <summary>
	/// Validates <c>[+-]digits[.digits][(e|E)[+-]digits]</c> and returns the first 19 significant digits w and the
	/// exponent q such that the value is w * 10^q, flagging whether non-zero digits were dropped.
	/// </summary>
	private static bool TryParseDecimalSlow(ReadOnlySpan<byte> s, out ulong w, out long q, out bool negative, out bool truncated)
	{
		w = 0;
		q = 0;
		negative = false;
		truncated = false;
		var i = 0;
		if (s.Length != 0 && (s[0] == '-' || s[0] == '+'))
		{
			negative = s[0] == '-';
			i = 1;
		}

		int digits = 0, taken = 0;
		var scale = 0L;
		var fraction = false;
		for (; i < s.Length; i++)
		{
			uint c = s[i];
			if (c == '.' && !fraction)
			{
				fraction = true;
				continue;
			}

			var d = c - '0';
			if (d > 9) break;
			digits++;
			if (taken < 19)
			{
				if (fraction) scale--;
				if (w == 0 && d == 0) continue; // leading zeros are not significant
				w = w * 10 + d;
				taken++;
			}
			else
			{
				if (!fraction) scale++;
				if (d != 0) truncated = true;
			}
		}

		if (digits == 0) return false;

		var e = 0L;
		if (i < s.Length)
		{
			if ((s[i] | 0x20) != 'e') return false;
			i++;
			var negativeExponent = false;
			if (i < s.Length && (s[i] == '-' || s[i] == '+'))
			{
				negativeExponent = s[i] == '-';
				i++;
			}

			if (i == s.Length) return false;
			for (; i < s.Length; i++)
			{
				var d = (uint)(s[i] - '0');
				if (d > 9) return false;
				if (e < 0x10000000) e = e * 10 + d;
			}

			if (negativeExponent) e = -e;
		}

		q = scale + e;

		return true;
	}

	/// <summary>
	/// The non-numeric values double.Parse and float.Parse accept with the invariant culture, case-insensitively and
	/// with an optional sign: Infinity and NaN (but not "inf").
	/// </summary>
	private static bool TryParseSpecial<T>(ReadOnlySpan<byte> s, out T value) where T : struct
	{
		var negative = false;
		if (s.Length != 0 && (s[0] == '-' || s[0] == '+'))
		{
			negative = s[0] == '-';
			s = s.Slice(1);
		}

		if (Ascii.EqualsIgnoreCase(s, "Infinity"u8))
		{
			value = typeof(T) == typeof(double)
				? Unsafe.BitCast<double, T>(negative ? double.NegativeInfinity : double.PositiveInfinity)
				: Unsafe.BitCast<float, T>(negative ? float.NegativeInfinity : float.PositiveInfinity);

			return true;
		}

		if (Ascii.EqualsIgnoreCase(s, "NaN"u8))
		{
			value = typeof(T) == typeof(double) ? Unsafe.BitCast<double, T>(double.NaN) : Unsafe.BitCast<float, T>(float.NaN);

			return true;
		}

		value = default;

		return false;
	}

	/// <summary>
	/// Eisel-Lemire for binary64: the IEEE bits (without sign) of the correctly rounded w * 10^q, or
	/// <see cref="Undecided"/>. Port of fast_float's compute_float.
	/// </summary>
	private static ulong ComputeDouble(long q, ulong w)
	{
		if (w == 0 || q < DoubleSmallestPowerOfTen) return 0;
		if (q > DoubleLargestPowerOfTen) return DoubleInfinityBits;

		var lz = BitOperations.LeadingZeroCount(w);
		w <<= lz;
		ref var entry = ref Unsafe.Add(ref MemoryMarshal.GetReference(PowersOfFive), (nint)(q - SmallestPowerOfFive) * 2);
		// We need the top mantissa + 3 bits: implicit bit, rounding bit, and one that may be lost to normalization.
		var hi = MultiplyHigh(w, entry, out var lo);
		const ulong PrecisionMask = ulong.MaxValue >> (DoubleMantissaBits + 3);
		if ((hi & PrecisionMask) == PrecisionMask)
		{
			var hi2 = MultiplyHigh(w, Unsafe.Add(ref entry, 1));
			lo += hi2;
			if (hi2 > lo) hi++;
			// Proven unnecessary (Mushtak and Lemire, "Fast number parsing without fallback"); kept as a cheap guard.
			if (lo == ulong.MaxValue && (q < -27 || q > 55)) return Undecided;
		}

		var upperBit = (int)(hi >> 63);
		var shift = upperBit + 64 - DoubleMantissaBits - 3;
		var mantissa = hi >> shift;
		var power2 = Power((int)q) + upperBit - lz - DoubleMinimumExponent;
		if (power2 <= 0)
		{
			// Subnormal (or zero). Halfway ties cannot occur this far from 10^0, so round half up.
			if (-power2 + 1 >= 64) return 0;
			mantissa >>= -power2 + 1;
			mantissa += mantissa & 1;
			mantissa >>= 1;

			// Rounding up may carry into the exponent field and produce the smallest normal, which is fine.
			return mantissa;
		}

		// Exactly halfway between two doubles (only possible for small |q|): round to even.
		if (lo <= 1 && (ulong)(q - DoubleMinExponentRoundToEven) <= DoubleMaxExponentRoundToEven - DoubleMinExponentRoundToEven &&
			(mantissa & 3) == 1 && (mantissa << shift) == hi)
		{
			mantissa &= ~1UL;
		}

		mantissa += mantissa & 1;
		mantissa >>= 1;
		// mantissa is in [2^52, 2^53]: adding it carries the implicit bit (or a rounding overflow) into the exponent.
		var bits = mantissa + ((ulong)(power2 - 1) << DoubleMantissaBits);

		return bits >= DoubleInfinityBits ? DoubleInfinityBits : bits;
	}

	/// <summary>Eisel-Lemire for binary32: same as <see cref="ComputeDouble"/> with the float parameters.</summary>
	private static ulong ComputeSingle(long q, ulong w)
	{
		if (w == 0 || q < SingleSmallestPowerOfTen) return 0;
		if (q > SingleLargestPowerOfTen) return SingleInfinityBits;

		var lz = BitOperations.LeadingZeroCount(w);
		w <<= lz;
		ref var entry = ref Unsafe.Add(ref MemoryMarshal.GetReference(PowersOfFive), (nint)(q - SmallestPowerOfFive) * 2);
		var hi = MultiplyHigh(w, entry, out var lo);
		const ulong PrecisionMask = ulong.MaxValue >> (SingleMantissaBits + 3);
		if ((hi & PrecisionMask) == PrecisionMask)
		{
			var hi2 = MultiplyHigh(w, Unsafe.Add(ref entry, 1));
			lo += hi2;
			if (hi2 > lo) hi++;
			if (lo == ulong.MaxValue && (q < -27 || q > 55)) return Undecided;
		}

		var upperBit = (int)(hi >> 63);
		var shift = upperBit + 64 - SingleMantissaBits - 3;
		var mantissa = hi >> shift;
		var power2 = Power((int)q) + upperBit - lz - SingleMinimumExponent;
		if (power2 <= 0)
		{
			if (-power2 + 1 >= 64) return 0;
			mantissa >>= -power2 + 1;
			mantissa += mantissa & 1;
			mantissa >>= 1;

			return mantissa;
		}

		if (lo <= 1 && (ulong)(q - SingleMinExponentRoundToEven) <= SingleMaxExponentRoundToEven - SingleMinExponentRoundToEven &&
			(mantissa & 3) == 1 && (mantissa << shift) == hi)
		{
			mantissa &= ~1UL;
		}

		mantissa += mantissa & 1;
		mantissa >>= 1;
		var bits = mantissa + ((ulong)(power2 - 1) << SingleMantissaBits);

		return bits >= SingleInfinityBits ? SingleInfinityBits : bits;
	}

	/// <summary>
	/// The 128-bit product of two 64-bit values. Math.BigMul passes the low half through memory (it takes its
	/// address for mulx), so with BMI2 compute the halves separately, keeping both in registers.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong MultiplyHigh(ulong a, ulong b, out ulong low)
	{
#if NET
		if (Bmi2.X64.IsSupported)
		{
			low = a * b;

			return Bmi2.X64.MultiplyNoFlags(a, b);
		}

		return Math.BigMul(a, b, out low);
#else
		return Portable.BigMul(a, b, out low);
#endif
	}

	/// <summary>The high 64 bits of the 128-bit product of two 64-bit values.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET
	private static ulong MultiplyHigh(ulong a, ulong b) => Bmi2.X64.IsSupported ? Bmi2.X64.MultiplyNoFlags(a, b) : Math.BigMul(a, b, out _);
#else
	private static ulong MultiplyHigh(ulong a, ulong b) => Portable.BigMul(a, b, out _);
#endif

	/// <summary>floor(log2(5^q)) + q + 63 for q in (-400, 350), i.e. the binary exponent of 10^q plus 63.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Power(int q) => (((152170 + 65536) * q) >> 16) + 63;

#if !NET
	/// <summary>
	/// Stands in for <c>System.Runtime.CompilerServices.Unsafe</c> in this class, adding <c>BitCast</c>, which the
	/// .NET Standard package lacks, so the code is the same on every target.
	/// </summary>
	private static class Unsafe
	{
		/// <summary>The reference <paramref name="elementOffset"/> elements after <paramref name="source"/>.</summary>
		/// <typeparam name="T">The type of the elements.</typeparam>
		/// <param name="source">The reference to start from.</param>
		/// <param name="elementOffset">The number of elements to move by.</param>
		/// <returns>The reference to the element.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Add<T>(ref T source, int elementOffset) => ref System.Runtime.CompilerServices.Unsafe.Add(ref source, elementOffset);

		/// <summary>The reference <paramref name="elementOffset"/> elements after <paramref name="source"/>.</summary>
		/// <typeparam name="T">The type of the elements.</typeparam>
		/// <param name="source">The reference to start from.</param>
		/// <param name="elementOffset">The number of elements to move by, as a native integer.</param>
		/// <returns>The reference to the element.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Add<T>(ref T source, nint elementOffset) => ref System.Runtime.CompilerServices.Unsafe.Add(ref source, elementOffset);

		/// <summary>Reads a <typeparamref name="T"/> from <paramref name="source"/>, whatever its alignment.</summary>
		/// <typeparam name="T">The type to read.</typeparam>
		/// <param name="source">The first byte of the value.</param>
		/// <returns>The value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadUnaligned<T>(ref byte source) => System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref source);

		/// <summary>
		/// Reinterprets the bits of <paramref name="value"/> as a <typeparamref name="TTo"/>, like .NET's
		/// <c>Unsafe.BitCast</c>. Unlike it, nothing checks that the two types have the same size: the callers only cast
		/// a <see cref="double"/> or <see cref="ulong"/> to <c>T = double</c>, and a <see cref="float"/> or
		/// <see cref="uint"/> to <c>T = float</c>.
		/// </summary>
		/// <typeparam name="TFrom">The type of <paramref name="value"/>.</typeparam>
		/// <typeparam name="TTo">The type to reinterpret it as, of the same size.</typeparam>
		/// <param name="value">The value to reinterpret.</param>
		/// <returns>The bits of <paramref name="value"/> as a <typeparamref name="TTo"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTo BitCast<TFrom, TTo>(TFrom value) => System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>(ref value);
	}
#endif
}
