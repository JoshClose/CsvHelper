using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace CsvHelper;

/// <summary>
/// Shortest round-trip formatting of <see cref="double"/> and <see cref="float"/> to UTF-8 that produces exactly the
/// bytes of <c>value.TryFormat(utf8Destination, out written, default, CultureInfo.InvariantCulture)</c> on .NET 10.
/// </summary>
/// <remarks>
/// <para>
/// Digits: Junekey Jeon's Dragonbox algorithm ("Dragonbox: A New Floating-Point Binary-to-Decimal Conversion
/// Algorithm", 2020; reference implementation github.com/jk-jeon/dragonbox) for round-to-nearest-even inputs. It yields
/// the shortest decimal that rounds back to the input (the rounding interval includes its end points when the binary
/// significand is even) and, among those, the one closest to the exact binary value, ties to even. That is the digit
/// string the BCL's Grisu3/Dragon4 produce.
/// </para>
/// <para>
/// Layout: the BCL's "G" rules for the shortest round-trippable string. Writing the digits as 0.d1d2..dn * 10^scale,
/// scientific notation is used when scale &gt; 17 (double) / 9 (float) or scale &lt; -3, with an exponent of at least
/// two digits and an explicit sign ("1E+17", "1E-05", "5E-324"). Otherwise fixed notation: "0.0001", "123.45",
/// "10000000000000000". Zero is "0" / "-0", NaN is "NaN" whatever its sign, the infinities are "Infinity" / "-Infinity".
/// </para>
/// <para>
/// On failure (destination too small) nothing is written; on success exactly <c>written</c> bytes are written.
/// </para>
/// </remarks>
internal static partial class FloatFormatter
{
	/// <summary>Longest double output, e.g. "-2.2250738585072014E-308".</summary>
	public const int MaxDoubleLength = 24;

	/// <summary>Longest float output, e.g. "-1.00000015E-10".</summary>
	public const int MaxSingleLength = 15;

	// The BCL switches to scientific notation when the decimal exponent exceeds the round-trip digit count.
	private const int DoubleMaxRoundTripDigits = 17;
	private const int SingleMaxRoundTripDigits = 9;

	private const ulong ModInverse5 = 0xCCCC_CCCC_CCCC_CCCDUL; // 5 * ModInverse5 == 1 (mod 2^64)
	private const ulong ModInverse25 = unchecked(ModInverse5 * ModInverse5);
	private const uint ModInverse5U = 0xCCCC_CCCDu; // 5 * ModInverse5U == 1 (mod 2^32)
	private const uint ModInverse25U = unchecked(ModInverse5U * ModInverse5U);

	/// <summary>
	/// Writes the shortest round-trippable representation of <paramref name="value"/>, identical to
	/// <c>value.TryFormat(destination, out written, default, CultureInfo.InvariantCulture)</c>. Returns false and writes
	/// nothing when <paramref name="destination"/> is too small; <see cref="MaxDoubleLength"/> bytes are always enough.
	/// </summary>
	/// <param name="value">The value to format.</param>
	/// <param name="destination">The buffer for the UTF-8 (ASCII) text.</param>
	/// <param name="written">The number of bytes written, or 0 when <paramref name="destination"/> is too small.</param>
	/// <returns>Whether the text fit in <paramref name="destination"/>.</returns>
	public static bool TryFormat(double value, Span<byte> destination, out int written)
	{
		if (!BitConverter.IsLittleEndian)
		{
#if NET
			return value.TryFormat(destination, out written, default, CultureInfo.InvariantCulture);
#else
			return TryCopyAscii(value.ToString("R", CultureInfo.InvariantCulture), destination, out written);
#endif
		}

#if NET
		var bits = BitConverter.DoubleToUInt64Bits(value);
#else
		var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
#endif
		var fraction = bits & 0x000F_FFFF_FFFF_FFFFUL;
		var biasedExponent = (uint)(bits >> 52) & 0x7FF;
		var negative = (long)bits < 0;

		if (biasedExponent - 1 < 0x7FE)
		{
			// Normal: value = c * 2^-mq with c = 2^52 + fraction.
			var c = fraction | (1UL << 52);
			var mq = 1075 - (int)biasedExponent;
			if ((uint)(mq - 1) < 52u && (c & ((1UL << mq) - 1)) == 0)
			{
				// An integer below 2^53: its exact digits are the shortest representation, always in fixed notation.
				written = WriteInteger(c >> mq, negative, ref MemoryMarshal.GetReference(destination), destination.Length);

				return written != 0;
			}
		}
		else if (biasedExponent != 0)
		{
			return TryWriteNonFinite(fraction != 0, negative, destination, out written);
		}
		else if (fraction == 0)
		{
			return TryWriteZero(negative, destination, out written);
		}

		var significand = ToShortestDecimal(fraction, (int)biasedExponent, out var exponent);
		written = WriteDecimal(significand, exponent, negative, DoubleMaxRoundTripDigits, ref MemoryMarshal.GetReference(destination), destination.Length);

		return written != 0;
	}

	/// <summary>
	/// Writes the shortest round-trippable representation of <paramref name="value"/>, identical to
	/// <c>value.TryFormat(destination, out written, default, CultureInfo.InvariantCulture)</c>. Returns false and writes
	/// nothing when <paramref name="destination"/> is too small; <see cref="MaxSingleLength"/> bytes are always enough.
	/// </summary>
	/// <param name="value">The value to format.</param>
	/// <param name="destination">The buffer for the UTF-8 (ASCII) text.</param>
	/// <param name="written">The number of bytes written, or 0 when <paramref name="destination"/> is too small.</param>
	/// <returns>Whether the text fit in <paramref name="destination"/>.</returns>
	public static bool TryFormat(float value, Span<byte> destination, out int written)
	{
		if (!BitConverter.IsLittleEndian)
		{
#if NET
			return value.TryFormat(destination, out written, default, CultureInfo.InvariantCulture);
#else
			return TryCopyAscii(value.ToString("R", CultureInfo.InvariantCulture), destination, out written);
#endif
		}

#if NET
		var bits = BitConverter.SingleToUInt32Bits(value);
#else
		var bits = Unsafe.As<float, uint>(ref value);
#endif
		var fraction = bits & 0x007F_FFFF;
		var biasedExponent = (bits >> 23) & 0xFF;
		var negative = (int)bits < 0;

		if (biasedExponent - 1 < 0xFE)
		{
			// Normal: value = c * 2^-mq with c = 2^23 + fraction.
			var c = fraction | (1u << 23);
			var mq = 150 - (int)biasedExponent;
			if ((uint)(mq - 1) < 23u && (c & ((1u << mq) - 1)) == 0)
			{
				written = WriteInteger(c >> mq, negative, ref MemoryMarshal.GetReference(destination), destination.Length);

				return written != 0;
			}
		}
		else if (biasedExponent != 0)
		{
			return TryWriteNonFinite(fraction != 0, negative, destination, out written);
		}
		else if (fraction == 0)
		{
			return TryWriteZero(negative, destination, out written);
		}

		var significand = ToShortestDecimal(fraction, (int)biasedExponent, out var exponent);
		written = WriteDecimal(significand, exponent, negative, SingleMaxRoundTripDigits, ref MemoryMarshal.GetReference(destination), destination.Length);

		return written != 0;
	}

	// ------------------------------------------------------------------------------------------------------------
	// Dragonbox
	// ------------------------------------------------------------------------------------------------------------

	/// <summary>floor(e * log10(2)) for |e| &lt;= 2620.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int FloorLog10Pow2(int e) => (e * 315653) >> 20;

	/// <summary>floor(e * log2(10)) for |e| &lt;= 1233.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int FloorLog2Pow10(int e) => (e * 1741647) >> 19;

	/// <summary>floor(e * log10(2) - log10(4/3)) for |e| &lt;= 2936.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int FloorLog10Pow2MinusLog10FourThirds(int e) => (e * 631305 - 261663) >> 21;

	/// <summary>
	/// Returns the shortest decimal significand (without trailing zeros) and exponent of the positive finite double
	/// with the given fraction bits and biased exponent, not both zero.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong ToShortestDecimal(ulong fraction, int biasedExponent, out int exponent)
	{
		const int Kappa = 2;
		var twoFc = fraction << 1;
		int e;
		if (biasedExponent != 0)
		{
			e = biasedExponent - 1075;
			if (twoFc == 0)
			{
				return ShorterIntervalDouble(e, out exponent);
			}

			twoFc |= 1UL << 53;
		}
		else
		{
			e = -1074;
		}

		var closed = (fraction & 1) == 0; // the rounding interval includes its end points for even significands

		var minusK = FloorLog10Pow2(e) - Kappa;
		ref var cache = ref Unsafe.Add(ref MemoryMarshal.GetReference(DoubleCache), (nint)(uint)(-minusK - DoubleCacheMinK) * 2);
		var cacheHigh = cache;
		var cacheLow = Unsafe.Add(ref cache, 1);
		var beta = e + FloorLog2Pow10(-minusK);

		var deltai = (uint)(cacheHigh >> (63 - beta));
		var u = (twoFc | 1) << beta;

		// z = upper 128 bits of the 192-bit product u * cache.
		var zHigh = MulHigh(u, cacheHigh);
		var zLow = u * cacheHigh;
		var carry = MulHigh(u, cacheLow);
		zLow += carry;
		zHigh += zLow < carry ? 1UL : 0UL;

		// Step 2: try the larger divisor 10^(kappa + 1).
		var significand = zHigh / 1000;
		var r = (uint)(zHigh - 1000 * significand);
		if (r < deltai)
		{
			// Exclude the right end point if necessary.
			if (r == 0 && zLow == 0 && !closed)
			{
				significand--;
				r = 1000;
				goto SmallDivisor;
			}
		}
		else if (r > deltai)
		{
			goto SmallDivisor;
		}
		else
		{
			// r == deltai: compare the fractional parts of x and z.
			ComputeMulParity(twoFc - 1, cacheHigh, cacheLow, beta, out var xParity, out var xIsInteger);
			if (!(xParity | (xIsInteger & closed)))
			{
				goto SmallDivisor;
			}
		}

		exponent = minusK + Kappa + 1;

		return RemoveTrailingZeros(significand, ref exponent);

	SmallDivisor:
		// Step 3: find the significand with the smaller divisor 10^kappa.
		significand *= 10;
		exponent = minusK + Kappa;
		var dist = r - (deltai / 2) + 50;
		var approxYParity = ((dist ^ 50) & 1) != 0;
		dist *= 656; // dist <= 1000: (dist * 656) >> 16 == dist / 100, and the low bits tell whether 100 divides dist
		var divisible = (dist & 0xFFFF) < 656;
		significand += dist >> 16;
		if (divisible)
		{
			// y is either z - epsilon or one less; tell them apart by parity, and break a tie towards even.
			ComputeMulParity(twoFc, cacheHigh, cacheLow, beta, out var yParity, out var yIsInteger);
			if (yParity != approxYParity)
			{
				significand--;
			}
			else if (yIsInteger & ((significand & 1) != 0))
			{
				significand--;
			}
		}

		return significand;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ComputeMulParity(ulong twoF, ulong cacheHigh, ulong cacheLow, int beta, out bool parity, out bool isInteger)
	{
		// Lower 128 bits of the 192-bit product twoF * cache.
		var high = twoF * cacheHigh + MulHigh(twoF, cacheLow);
		var low = twoF * cacheLow;
		parity = ((high >> (64 - beta)) & 1) != 0;
		isInteger = ((high << beta) | (low >> (64 - beta))) == 0;
	}

	/// <summary>Powers of two, where the gap to the next lower double is half the gap to the next higher one.</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ulong ShorterIntervalDouble(int e, out int exponent)
	{
		if ((e == -77 || e == -1010) && PowerOfTwoQuirk.Enabled)
		{
			// 2^-25 and 2^-958 on runtimes with the Dragon4 margin bug; see PowerOfTwoQuirk.
			exponent = e == -77 ? -23 : -304;

			return e == -77 ? 2_980_232_238_769_531UL : 4_104_536_801_298_376UL;
		}

		var minusK = FloorLog10Pow2MinusLog10FourThirds(e);
		var beta = e + FloorLog2Pow10(-minusK);
		var cache = Unsafe.Add(ref MemoryMarshal.GetReference(DoubleCache), (nint)(uint)(-minusK - DoubleCacheMinK) * 2);

		// The interval is closed (the significand is even). The left end point is an integer only for e in [2, 3].
		var xi = (cache - (cache >> 54)) >> (11 - beta);
		var zi = (cache + (cache >> 53)) >> (11 - beta);
		if (e is < 2 or > 3)
		{
			xi++;
		}

		var significand = zi / 10;
		if (significand * 10 >= xi)
		{
			exponent = minusK + 1;

			return RemoveTrailingZeros(significand, ref exponent);
		}

		significand = ((cache >> (10 - beta)) + 1) / 2;
		exponent = minusK;
		if ((significand & 1) != 0 && e == -77)
		{
			significand--; // the only tie
		}
		else if (significand < xi)
		{
			significand++;
		}

		return significand;
	}

	/// <summary>Float version of <see cref="ToShortestDecimal(ulong, int, out int)"/>.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint ToShortestDecimal(uint fraction, int biasedExponent, out int exponent)
	{
		const int Kappa = 1;
		var twoFc = fraction << 1;
		int e;
		if (biasedExponent != 0)
		{
			e = biasedExponent - 150;
			if (twoFc == 0)
			{
				return ShorterIntervalSingle(e, out exponent);
			}

			twoFc |= 1u << 24;
		}
		else
		{
			e = -149;
		}

		var closed = (fraction & 1) == 0;

		var minusK = FloorLog10Pow2(e) - Kappa;
		var cache = Unsafe.Add(ref MemoryMarshal.GetReference(SingleCache), (nint)(uint)(-minusK - SingleCacheMinK));
		var beta = e + FloorLog2Pow10(-minusK);

		var deltai = (uint)(cache >> (63 - beta));
		var u = (twoFc | 1) << beta;
		var z = MulHigh((ulong)u << 32, cache); // upper 64 bits of the 96-bit product u * cache
		var zi = (uint)(z >> 32);

		var significand = zi / 100;
		var r = zi - 100 * significand;
		if (r < deltai)
		{
			if (r == 0 && (uint)z == 0 && !closed)
			{
				significand--;
				r = 100;
				goto SmallDivisor;
			}
		}
		else if (r > deltai)
		{
			goto SmallDivisor;
		}
		else
		{
			ComputeMulParity(twoFc - 1, cache, beta, out var xParity, out var xIsInteger);
			if (!(xParity | (xIsInteger & closed)))
			{
				goto SmallDivisor;
			}
		}

		exponent = minusK + Kappa + 1;

		return RemoveTrailingZeros(significand, ref exponent);

	SmallDivisor:
		significand *= 10;
		exponent = minusK + Kappa;
		var dist = r - (deltai / 2) + 5;
		var approxYParity = ((dist ^ 5) & 1) != 0;
		dist *= 6554; // dist <= 100: (dist * 6554) >> 16 == dist / 10, and the low bits tell whether 10 divides dist
		var divisible = (dist & 0xFFFF) < 6554;
		significand += dist >> 16;
		if (divisible)
		{
			ComputeMulParity(twoFc, cache, beta, out var yParity, out var yIsInteger);
			if (yParity != approxYParity)
			{
				significand--;
			}
			else if (yIsInteger & ((significand & 1) != 0))
			{
				significand--;
			}
		}

		return significand;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ComputeMulParity(uint twoF, ulong cache, int beta, out bool parity, out bool isInteger)
	{
		var r = twoF * cache; // lower 64 bits of the 96-bit product
		parity = ((r >> (64 - beta)) & 1) != 0;
		isInteger = (uint)(r >> (32 - beta)) == 0;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static uint ShorterIntervalSingle(int e, out int exponent)
	{
		var minusK = FloorLog10Pow2MinusLog10FourThirds(e);
		var beta = e + FloorLog2Pow10(-minusK);
		var cache = Unsafe.Add(ref MemoryMarshal.GetReference(SingleCache), (nint)(uint)(-minusK - SingleCacheMinK));

		var xi = (uint)((cache - (cache >> 25)) >> (40 - beta));
		var zi = (uint)((cache + (cache >> 24)) >> (40 - beta));
		if (e is < 2 or > 3)
		{
			xi++;
		}

		var significand = zi / 10;
		if (significand * 10 >= xi)
		{
			exponent = minusK + 1;

			return RemoveTrailingZeros(significand, ref exponent);
		}

		significand = (uint)(((cache >> (39 - beta)) + 1) / 2);
		exponent = minusK;
		if ((significand & 1) != 0 && e == -35)
		{
			significand--; // the only tie
		}
		else if (significand < xi)
		{
			significand++;
		}

		return significand;
	}

	/// <summary>
	/// .NET 10 runtimes up to at least 10.0.12 have a Dragon4 bug (dotnet/runtime#135582, fixed for a later servicing
	/// release by #135614): the "unequal margins" test for exact powers of two is <c>mantissa == (1U &lt;&lt; 52)</c>, and
	/// C# masks that shift to <c>1U &lt;&lt; 20</c>. Whenever Grisu3 gives up on a double power of two, Dragon4 then
	/// ignores the narrower gap below it. Of all 2046 normal powers of two this changes only 2^-25
	/// ("2.980232238769531E-08") and 2^-958 ("4.104536801298376E-289"), and neither string round-trips. To be
	/// byte-for-byte identical with whichever runtime we run on, ask the BCL once.
	/// </summary>
	private static class PowerOfTwoQuirk
	{
		/// <summary>
		/// Whether the running BCL formats 2^-25 as the buggy <c>2.980232238769531E-08</c>, so that the formatter writes
		/// the same digits for the two affected powers of two. Always false outside .NET.
		/// </summary>
		public static readonly bool Enabled = Detect();

		private static bool Detect()
		{
#if NET
			Span<byte> buffer = stackalloc byte[32];

			return Math.ScaleB(1.0, -25).TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture)
				&& buffer.Slice(0, written).SequenceEqual("2.980232238769531E-08"u8);
#else
			// There is no .NET 10 BCL to match: always write the correct shortest representation.
			return false;
#endif
		}
	}

#if !NET
	/// <summary>Copies the ASCII <paramref name="text"/> to <paramref name="destination"/> if it fits.</summary>
	private static bool TryCopyAscii(string text, Span<byte> destination, out int written)
	{
		if (text.Length > destination.Length)
		{
			written = 0;

			return false;
		}

		for (var i = 0; i < text.Length; i++) destination[i] = (byte)text[i];
		written = text.Length;

		return true;
	}
#endif

	/// <summary>The upper 64 bits of the 128-bit product.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong MulHigh(ulong a, ulong b)
	{
#if NET
		if (Bmi2.X64.IsSupported)
		{
			return Bmi2.X64.MultiplyNoFlags(a, b);
		}

		if (ArmBase.Arm64.IsSupported)
		{
			return ArmBase.Arm64.MultiplyHigh(a, b);
		}

		return Math.BigMul(a, b, out _);
#else
		return Portable.BigMul(a, b, out _);
#endif
	}

	/// <summary>Removes the trailing decimal zeros of 0 &lt; m &lt; 10^17, adding their count to <paramref name="e"/>.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong RemoveTrailingZeros(ulong m, ref int e)
	{
		// Granlund-Montgomery: for odd d, m * inverse(d) mod 2^64 <= (2^64 - 1) / d exactly when d divides m;
		// rotating right by s folds in the factor 2^s of 10^s.
		var q = BitOperations.RotateRight(m * ModInverse5, 1);
		if (q > ulong.MaxValue / 10)
		{
			return m;
		}

		m = q;
		e++;

		// m < 10^16 now. Divisible by 10^8? (Magic = ceil(2^90 / 10^8); exact for m < 1.4e21.)
		const ulong Magic = 12_379_400_392_853_802_749UL;
		var high = MulHigh(m, Magic);
		if ((high & ((1UL << 26) - 1)) == 0 && m * Magic < Magic)
		{
			e += 8;

			return RemoveTrailingZerosSmall((uint)(high >> 26), ref e);
		}

		while (true)
		{
			q = BitOperations.RotateRight(m * ModInverse25, 2);
			if (q > ulong.MaxValue / 100)
			{
				break;
			}

			m = q;
			e += 2;
		}

		q = BitOperations.RotateRight(m * ModInverse5, 1);
		if (q <= ulong.MaxValue / 10)
		{
			m = q;
			e++;
		}

		return m;
	}

	/// <summary>Removes the trailing decimal zeros of 0 &lt; m, adding their count to <paramref name="e"/>.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint RemoveTrailingZeros(uint m, ref int e)
	{
		var q = BitOperations.RotateRight(m * ModInverse5U, 1);
		if (q > uint.MaxValue / 10)
		{
			return m;
		}

		e++;

		return RemoveTrailingZerosSmall(q, ref e);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint RemoveTrailingZerosSmall(uint m, ref int e)
	{
		while (true)
		{
			var q = BitOperations.RotateRight(m * ModInverse25U, 2);
			if (q > uint.MaxValue / 100)
			{
				break;
			}

			m = q;
			e += 2;
		}

		var q1 = BitOperations.RotateRight(m * ModInverse5U, 1);
		if (q1 <= uint.MaxValue / 10)
		{
			m = q1;
			e++;
		}

		return m;
	}

	// ------------------------------------------------------------------------------------------------------------
	// Output
	// ------------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes m * 10^e (m &lt; 10^17 without trailing zeros) in the BCL "G" layout for shortest round-trip strings.
	/// Returns the number of bytes written, or 0 (writing nothing) when they do not fit.
	/// </summary>
	private static int WriteDecimal(ulong m, int e, bool negative, int maxDigits, ref byte destination, int destinationLength)
	{
		var n = CountDigits(m);
		var scale = n + e; // value = 0.d1..dn * 10^scale
		var sign = negative ? 1 : 0;
		int length;

		if ((uint)(scale + 3) <= (uint)(maxDigits + 3))
		{
			if (e >= 0)
			{
				// "ddd000": an integer, padded with zeros.
				length = sign + scale;
				if ((uint)length > (uint)destinationLength)
				{
					return 0;
				}

				destination = (byte)'-'; // overwritten by the first digit when positive
				WriteDigitsOutOfLine(ref Unsafe.Add(ref destination, sign), m * Pow10(e), scale);

				return length;
			}

			if (scale > 0)
			{
				// "ddd.ddd". The split does not depend on the digit count, only on e.
				var fractionDigits = -e;
				var integerPart = DivPow10(m, fractionDigits);
				var fractionPart = m - integerPart * Pow10(fractionDigits);
				length = sign + n + 1;
				if ((uint)length > (uint)destinationLength)
				{
					return 0;
				}

				destination = (byte)'-';
				ref var d = ref Unsafe.Add(ref destination, sign);
				WriteDigits(ref d, integerPart, scale);
				Unsafe.Add(ref d, scale) = (byte)'.';
				WriteDigits(ref Unsafe.Add(ref d, scale + 1), fractionPart, fractionDigits);

				return length;
			}

			{
				// "0.000ddd"
				length = sign + 2 - scale + n;
				if ((uint)length > (uint)destinationLength)
				{
					return 0;
				}

				destination = (byte)'-';
				ref var d = ref Unsafe.Add(ref destination, sign);
				Unsafe.WriteUnaligned(ref d, (ushort)('0' | ('.' << 8)));
				WriteDigits(ref Unsafe.Add(ref d, 2), m, n - scale); // the zero padding provides the leading zeros

				return length;
			}
		}

		{
			// "d.dddE+XX"
			var exponent = scale - 1;
			var absExponent = (uint)(exponent < 0 ? -exponent : exponent);
			var exponentDigits = absExponent >= 100 ? 3 : 2;
			var mantissaLength = n > 1 ? n + 1 : 1;
			length = sign + mantissaLength + 2 + exponentDigits;
			if ((uint)length > (uint)destinationLength)
			{
				return 0;
			}

			destination = (byte)'-';
			ref var d = ref Unsafe.Add(ref destination, sign);
			if (m >= 1_000_000_000_000_000UL)
			{
				// 16 or 17 digits (full precision doubles): convert both 8-digit halves without first dividing by
				// 10^(n - 1), and select between the two layouts without branching.
				var high = m / 100_000_000;
				var low = (uint)(m - high * 100_000_000);
				var first = (uint)(m / 10_000_000_000_000_000UL); // 0 when n == 16
				var middle = EightDigits((uint)(high - first * 100_000_000UL));
				var seventeen = n == 17;
				var lead = seventeen ? '0' + (ulong)first : middle;
				var next = seventeen ? middle : middle >> 8;
				d = (byte)lead;
				Unsafe.Add(ref d, 1) = (byte)'.';
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref d, 2), next);
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref d, n - 7), EightDigits(low));
			}
			else if (n > 1)
			{
				var first = DivPow10(m, n - 1);
				d = (byte)('0' + (uint)first);
				Unsafe.Add(ref d, 1) = (byte)'.';
				WriteDigits(ref Unsafe.Add(ref d, 2), m - first * Pow10(n - 1), n - 1);
			}
			else
			{
				d = (byte)('0' + (uint)m);
			}

			ref var x = ref Unsafe.Add(ref d, mantissaLength);
			Unsafe.WriteUnaligned(ref x, (ushort)('E' | ((exponent < 0 ? '-' : '+') << 8)));
			// With two digits hundreds is 0 and the pair overwrites it at x + 2; with three the pair goes to x + 3.
			var hundreds = absExponent / 100;
			Unsafe.Add(ref x, 2) = (byte)('0' + hundreds);
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref x, exponentDigits), Pair(absExponent - hundreds * 100));

			return length;
		}
	}

	/// <summary>Writes the integer 0 &lt; v &lt; 2^53 (fixed notation for both float and double).</summary>
	private static int WriteInteger(ulong v, bool negative, ref byte destination, int destinationLength)
	{
		var n = CountDigits(v);
		var sign = negative ? 1 : 0;
		var length = sign + n;
		if ((uint)length > (uint)destinationLength)
		{
			return 0;
		}

		destination = (byte)'-';
		WriteDigits(ref Unsafe.Add(ref destination, sign), v, n);

		return length;
	}

	private static bool TryWriteZero(bool negative, Span<byte> destination, out int written)
	{
		var length = negative ? 2 : 1;
		if ((uint)length > (uint)destination.Length)
		{
			written = 0;

			return false;
		}

		ref var p = ref MemoryMarshal.GetReference(destination);
		p = (byte)'-';
		Unsafe.Add(ref p, length - 1) = (byte)'0';
		written = length;

		return true;
	}

	private static bool TryWriteNonFinite(bool isNaN, bool negative, Span<byte> destination, out int written)
	{
		var text = isNaN ? "NaN"u8 : negative ? "-Infinity"u8 : "Infinity"u8;
		if (text.TryCopyTo(destination))
		{
			written = text.Length;

			return true;
		}

		written = 0;

		return false;
	}

	/// <summary>Number of decimal digits of 0 &lt; v.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int CountDigits(ulong v)
	{
		// Both table lookups depend only on the bit length, so they run in parallel.
		nint lz = BitOperations.LeadingZeroCount(v);
		int digits = Unsafe.Add(ref MemoryMarshal.GetReference(DigitsByLeadingZeroCount), lz);
		var threshold = Unsafe.Add(ref MemoryMarshal.GetReference(DigitCountThresholds), lz);

		return digits + (v >= threshold ? 1 : 0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Pow10(int i) => Unsafe.Add(ref MemoryMarshal.GetReference(PowersOf10), (nint)(uint)i);

	/// <summary>m / 10^j for 0 &lt; j &lt;= 16 and m &lt; 2^57, with a multiplication instead of a division.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ulong DivPow10(ulong m, int j)
	{
		var reciprocal = Unsafe.Add(ref MemoryMarshal.GetReference(Pow10Reciprocals), (nint)(uint)j);
		int shift = Unsafe.Add(ref MemoryMarshal.GetReference(Pow10ReciprocalShifts), (nint)(uint)j);

		return MulHigh(m, reciprocal) >> shift;
	}

	/// <summary>The two ASCII digits of 0 &lt;= value &lt; 100.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ushort Pair(uint value) =>
		Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetReference(DigitPairs), (nint)(value * 2)));

	/// <summary>The eight ASCII digits of 0 &lt;= value &lt; 10^8, most significant first in memory order.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ulong EightDigits(uint value)
	{
		// The three quotients are independent, so the four pairs are ready after one multiply-shift and one
		// multiply-subtract rather than a chain of three divisions.
		var d6 = value / 1_000_000;
		var d4 = value / 10_000;
		var d2 = value / 100;

		return Pair(d6) | ((ulong)Pair(d4 - d6 * 100) << 16) | ((ulong)Pair(d2 - d4 * 100) << 32) | ((ulong)Pair(value - d2 * 100) << 48);
	}

	/// <summary>
	/// <see cref="WriteDigits"/> as a call, for the rare zero padded integer layout (integers beyond the exact integer
	/// path, i.e. at least 2^53 / 2^24), so that it does not carry another inlined copy.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void WriteDigitsOutOfLine(ref byte p, ulong value, int count) => WriteDigits(ref p, value, count);

	/// <summary>Writes exactly <paramref name="count"/> (1..20) digits of <paramref name="value"/>, zero padded.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteDigits(ref byte p, ulong value, int count)
	{
		if (count > 8)
		{
			var high = value / 100_000_000;
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, count - 8), EightDigits((uint)(value - high * 100_000_000)));
			count -= 8;
			if (count > 8)
			{
				var top = high / 100_000_000;
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, count - 8), EightDigits((uint)(high - top * 100_000_000)));
				count -= 8;
				high = top;
			}

			WriteUpTo8Digits(ref p, (uint)high, count);
		}
		else
		{
			WriteUpTo8Digits(ref p, (uint)value, count);
		}
	}

	/// <summary>Writes exactly <paramref name="count"/> (1..8) digits of <paramref name="value"/>, zero padded.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteUpTo8Digits(ref byte p, uint value, int count)
	{
		if (count > 4)
		{
			// Two overlapping 4-byte stores cover any length from 5 to 8.
			var x = EightDigits(value) >> (8 * (8 - count));
			Unsafe.WriteUnaligned(ref p, (uint)x);
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, count - 4), (uint)(x >> (8 * (count - 4))));
		}
		else
		{
			var h = value / 100;
			var x = (Pair(h) | ((uint)Pair(value - h * 100) << 16)) >> (8 * (4 - count));
			if (count >= 2)
			{
				Unsafe.WriteUnaligned(ref p, (ushort)x);
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, count - 2), (ushort)(x >> (8 * (count - 2))));
			}
			else
			{
				p = (byte)x;
			}
		}
	}
}
