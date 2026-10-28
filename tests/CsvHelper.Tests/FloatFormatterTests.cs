using System.Globalization;
using System.Numerics;
using System.Text;
using Xunit;

namespace CsvHelper.Tests;

/// <summary>
/// <see cref="FloatFormatter"/> must produce exactly the bytes of the BCL's
/// <c>TryFormat(Span&lt;byte&gt;, out int, default, CultureInfo.InvariantCulture)</c>.
/// </summary>
public class FloatFormatterTests
{
	private const int BufferSize = 64;
	private const byte Sentinel = 0xCC;

#if NET
	/// <summary>Compares the formatter against the BCL and collects (a bounded number of) mismatches.</summary>
	private sealed class Checker
	{
		private readonly byte[] expected = new byte[BufferSize];
		private readonly byte[] actual = new byte[BufferSize];
		private readonly List<string> failures = [];
		private long mismatches;
		public long Count;

		public void Check(double value)
		{
			Count++;
			var okE = value.TryFormat(expected, out var we, default, CultureInfo.InvariantCulture);
			actual.AsSpan().Fill(Sentinel);
			var okA = FloatFormatter.TryFormat(value, actual, out var wa);
			if (!okE || !okA || we != wa || !expected.AsSpan(0, we).SequenceEqual(actual.AsSpan(0, wa))
				|| wa > FloatFormatter.MaxDoubleLength || actual.AsSpan(wa).ContainsAnyExcept(Sentinel))
			{
				Fail($"double 0x{BitConverter.DoubleToUInt64Bits(value):X16}: expected \"{Encoding.UTF8.GetString(expected, 0, we)}\" got \"{(okA ? Encoding.UTF8.GetString(actual, 0, wa) : "<false>")}\"");
			}
		}

		public void Check(float value)
		{
			Count++;
			var okE = value.TryFormat(expected, out var we, default, CultureInfo.InvariantCulture);
			actual.AsSpan().Fill(Sentinel);
			var okA = FloatFormatter.TryFormat(value, actual, out var wa);
			if (!okE || !okA || we != wa || !expected.AsSpan(0, we).SequenceEqual(actual.AsSpan(0, wa))
				|| wa > FloatFormatter.MaxSingleLength || actual.AsSpan(wa).ContainsAnyExcept(Sentinel))
			{
				Fail($"float 0x{BitConverter.SingleToUInt32Bits(value):X8}: expected \"{Encoding.UTF8.GetString(expected, 0, we)}\" got \"{(okA ? Encoding.UTF8.GetString(actual, 0, wa) : "<false>")}\"");
			}
		}

		/// <summary>Checks the value, its negation and a few neighbouring representable values.</summary>
		public void CheckAround(double value, int neighbours = 2)
		{
			double up = value, down = value;
			Check(value);
			Check(-value);
			for (var i = 0; i < neighbours; i++)
			{
				up = Math.BitIncrement(up);
				down = Math.BitDecrement(down);
				Check(up);
				Check(-up);
				Check(down);
				Check(-down);
			}
		}

		public void CheckAround(float value, int neighbours = 2)
		{
			float up = value, down = value;
			Check(value);
			Check(-value);
			for (var i = 0; i < neighbours; i++)
			{
				up = MathF.BitIncrement(up);
				down = MathF.BitDecrement(down);
				Check(up);
				Check(-up);
				Check(down);
				Check(-down);
			}
		}

		private void Fail(string message)
		{
			mismatches++;
			if (failures.Count < 25) failures.Add(message);
		}

		public void AssertNoFailures()
		{
			Assert.True(mismatches == 0, $"{mismatches} mismatches out of {Count} values (first {failures.Count} shown):\n" + string.Join("\n", failures));
		}
	}
#endif

	// --------------------------------------------------------------------------------------------------------------
	// Hand-picked values
	// --------------------------------------------------------------------------------------------------------------

	public static TheoryData<double, string> KnownDoubles => new()
	{
		{ 0.0, "0" },
		{ -0.0, "-0" },
		{ double.NaN, "NaN" },
		{ double.PositiveInfinity, "Infinity" },
		{ double.NegativeInfinity, "-Infinity" },
		{ double.Epsilon, "5E-324" },
		{ -double.Epsilon, "-5E-324" },
		{ double.MaxValue, "1.7976931348623157E+308" },
		{ double.MinValue, "-1.7976931348623157E+308" },
		{ 0.1, "0.1" },
		{ 1.0 / 3, "0.3333333333333333" },
		{ 3.141592653589793, "3.141592653589793" },
		{ 1234.5678, "1234.5678" },
		{ 1.2345678901234567E-05, "1.2345678901234568E-05" },
		{ 0.0001, "0.0001" },
		{ 0.00001, "1E-05" },
		{ 1e16, "10000000000000000" },
		{ 1e17, "1E+17" },
		{ 12345678901234567.0, "12345678901234568" },
		{ 123456789012345678.0, "1.2345678901234568E+17" },
		{ 1e23, "1E+23" },
		{ 9007199254740992.0, "9007199254740992" },
		{ -60.0, "-60" },
		{ 2.2250738585072014E-308, "2.2250738585072014E-308" },
		{ 1e-100, "1E-100" },
		{ 1e300, "1E+300" },
	};

	[Theory]
	[MemberData(nameof(KnownDoubles))]
	public void KnownDoubleOutputs(double value, string expected)
	{
		Span<byte> buffer = stackalloc byte[32];
		Assert.True(FloatFormatter.TryFormat(value, buffer, out var written));
		Assert.Equal(expected, Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray()));
#if NET
		Assert.Equal(expected, value.ToString(CultureInfo.InvariantCulture));
#endif
	}

	public static TheoryData<float, string> KnownSingles => new()
	{
		{ 0f, "0" },
		{ -0f, "-0" },
		{ float.NaN, "NaN" },
		{ float.PositiveInfinity, "Infinity" },
		{ float.NegativeInfinity, "-Infinity" },
		{ float.Epsilon, "1E-45" },
		{ float.MaxValue, "3.4028235E+38" },
		{ float.MinValue, "-3.4028235E+38" },
		{ 0.52913445f, "0.52913445" },
		{ 123.45678f, "123.45678" },
		{ -0.001234f, "-0.001234" },
		{ 0.1f, "0.1" },
		{ 1f / 3, "0.33333334" },
		{ 1e7f, "10000000" },
		{ 1e8f, "100000000" },
		{ 1e9f, "1E+09" },
		{ 16777216f, "16777216" },
		{ 0.0001f, "0.0001" },
		{ 0.00001f, "1E-05" },
		{ 3e10f, "3E+10" },
		{ 1.17549435E-38f, "1.1754944E-38" },
	};

	[Theory]
	[MemberData(nameof(KnownSingles))]
	public void KnownSingleOutputs(float value, string expected)
	{
		Span<byte> buffer = stackalloc byte[32];
		Assert.True(FloatFormatter.TryFormat(value, buffer, out var written));
		Assert.Equal(expected, Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray()));
#if NET
		Assert.Equal(expected, value.ToString(CultureInfo.InvariantCulture));
#endif
	}

#if NET
	private static IEnumerable<double> DoubleEdgeCases()
	{
		yield return 0.0;
		yield return -0.0;
		yield return double.Epsilon;
		yield return double.MaxValue;
		yield return double.MinValue;
		yield return double.NaN;
		yield return BitConverter.UInt64BitsToDouble(0x7FF0_0000_0000_0001); // signalling NaN
		yield return BitConverter.UInt64BitsToDouble(0xFFF8_0000_0000_0000); // negative quiet NaN
		yield return BitConverter.UInt64BitsToDouble(0xFFFF_FFFF_FFFF_FFFF);
		yield return BitConverter.UInt64BitsToDouble(0x7FF7_FFFF_FFFF_FFFF);
		yield return double.PositiveInfinity;
		yield return double.NegativeInfinity;
		yield return BitConverter.UInt64BitsToDouble(0x0010_0000_0000_0000); // smallest normal
		yield return BitConverter.UInt64BitsToDouble(0x000F_FFFF_FFFF_FFFF); // largest subnormal
		yield return BitConverter.UInt64BitsToDouble(0x0000_0000_0000_0002);
		yield return BitConverter.UInt64BitsToDouble(0x0000_0000_0000_0003);
		yield return BitConverter.UInt64BitsToDouble(0x0000_0000_0000_000A);
		yield return BitConverter.UInt64BitsToDouble(0x0000_0000_0000_0015);
		yield return 0.1;
		yield return 0.2;
		yield return 0.3;
		yield return 0.1 + 0.2;
		yield return 1.0 / 3;
		yield return 2.0 / 3;
		yield return Math.PI;
		yield return Math.E;
		yield return Math.Sqrt(2);
		yield return 1234.5678;
		yield return 1.2345678901234567E-05;
		yield return 123456789012345.0;    // 15 digits
		yield return 1234567890123456.0;   // 16 digits
		yield return 12345678901234567.0;  // 17 digits
		yield return 0.123456789012345;    // 15 significant digits
		yield return 0.1234567890123456;   // 16
		yield return 0.12345678901234567;  // 17
		yield return 1.23456789012345E+300;
		yield return 9.87654321098765E-300;
		yield return 1e23;
		yield return 9.999999999999999e22;
		yield return 1.0000000000000001e23;
		yield return 5e-324;
		yield return 1.7976931348623157e308;
		yield return 2.2250738585072014E-308;
		yield return 4.9406564584124654E-324;
		yield return 9007199254740991.0;
		yield return 9007199254740992.0;
		yield return 9007199254740994.0;
		yield return 4503599627370496.5;
		yield return 3000000000000000.5;
		yield return 0.5;
		yield return 0.25;
		yield return 0.125;
		yield return 1.5;
		yield return 2.5;
		yield return 100.0;
		yield return 0.001;
		yield return 0.01;
		yield return 123e-7;
		// Fixed / scientific boundaries: scale -3 / -4 and 17 / 18.
		yield return 0.0001;
		yield return 0.00009999999999999999;
		yield return 0.00012345678901234567;
		yield return 0.000099999;
		yield return 0.00001;
		yield return 99999999999999990.0;
		yield return 99999999999999999.0;
		yield return 1e17;
		yield return 1e16;
		yield return 123456789012345680.0;
		yield return 10000000000000002.0;
		yield return 1e15;
		yield return 1e14;
		// Powers of two, including the asymmetric-interval cases.
		for (var e = -1074; e <= 1023; e += 1) yield return Math.ScaleB(1.0, e);
	}

	private static IEnumerable<float> SingleEdgeCases()
	{
		yield return 0f;
		yield return -0f;
		yield return float.Epsilon;
		yield return float.MaxValue;
		yield return float.MinValue;
		yield return float.NaN;
		yield return BitConverter.UInt32BitsToSingle(0x7F80_0001);
		yield return BitConverter.UInt32BitsToSingle(0xFFC0_0000);
		yield return BitConverter.UInt32BitsToSingle(0xFFFF_FFFF);
		yield return float.PositiveInfinity;
		yield return float.NegativeInfinity;
		yield return BitConverter.UInt32BitsToSingle(0x0080_0000); // smallest normal
		yield return BitConverter.UInt32BitsToSingle(0x007F_FFFF); // largest subnormal
		yield return 0.52913445f;
		yield return 123.45678f;
		yield return -0.001234f;
		yield return 0.1f;
		yield return 0.2f;
		yield return 0.3f;
		yield return 1f / 3;
		yield return 2f / 3;
		yield return MathF.PI;
		yield return MathF.E;
		yield return 3e10f;
		yield return 30000001024f;
		yield return 29999998976f;
		yield return 16777216f;
		yield return 16777215f;
		yield return 8388608f;
		yield return 8388609f;
		yield return 123456784f;
		yield return 12345678f;
		yield return 1234567f;
		yield return 0.0001f;
		yield return 0.00001f;
		yield return 0.000099999f;
		yield return 999999940f;
		yield return 1e9f;
		yield return 1e8f;
		yield return 1.17549435E-38f;
		yield return 7e-45f;
		for (var e = -149; e <= 127; e++) yield return MathF.ScaleB(1f, e);
	}

	[Fact]
	public void DoubleEdgeCasesMatchBcl()
	{
		var checker = new Checker();
		foreach (var v in DoubleEdgeCases())
		{
			if (double.IsFinite(v)) checker.CheckAround(v, 3);
			else checker.Check(v);
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void SingleEdgeCasesMatchBcl()
	{
		var checker = new Checker();
		foreach (var v in SingleEdgeCases())
		{
			if (float.IsFinite(v)) checker.CheckAround(v, 3);
			else checker.Check(v);
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void PowersOfTenAndNeighboursMatchBcl()
	{
		var checker = new Checker();
		for (var e = -323; e <= 308; e++)
		{
			checker.CheckAround(double.Parse("1e" + e, CultureInfo.InvariantCulture), 4);
		}

		for (var e = -45; e <= 38; e++)
		{
			checker.CheckAround(float.Parse("1e" + e, CultureInfo.InvariantCulture), 4);
		}

		// The same through arithmetic, which can land on a neighbour of the correctly rounded value.
		var d = 1.0;
		for (var e = 0; e <= 30; e++, d *= 10) checker.CheckAround(d, 2);
		d = 1;
		for (var e = 0; e >= -30; e--, d /= 10) checker.CheckAround(d, 2);
		var f = 1f;
		for (var e = 0; e <= 30; e++, f *= 10) checker.CheckAround(f, 2);
		f = 1;
		for (var e = 0; e >= -30; e--, f /= 10) checker.CheckAround(f, 2);
		checker.AssertNoFailures();
	}

	[Fact]
	public void IntegersMatchBcl()
	{
		var checker = new Checker();
		for (var i = 0; i <= 200_000; i++)
		{
			checker.Check((double)i);
			checker.Check((float)i);
		}

		for (var bit = 0; bit <= 64; bit++)
		{
			checker.CheckAround(Math.Pow(2, bit), 3);
			checker.CheckAround(MathF.Pow(2, bit), 3);
			checker.CheckAround(Math.Pow(2, bit) - 1, 1);
			checker.CheckAround(MathF.Pow(2, bit) - 1, 1);
		}

		for (var i = -5000L; i <= 5000; i++)
		{
			checker.Check((double)((1L << 53) + i));
			checker.Check((float)((1L << 24) + i));
			checker.Check((double)(100_000_000_000_000_000L + i * 16)); // around 1e17: fixed vs scientific
			checker.Check((float)(1_000_000_000L + i * 64));           // around 1e9
		}

		var rng = new Random(12345);
		for (var i = 0; i < 200_000; i++)
		{
			var v = rng.NextInt64(1L << 53) >> rng.Next(0, 53);
			checker.Check((double)v);
			checker.Check((double)-v);
			checker.Check((float)(v & 0xFF_FFFF));
			checker.Check((float)v);
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void SubnormalDoublesMatchBcl()
	{
		var checker = new Checker();
		for (var bits = 1UL; bits <= 300_000; bits++)
		{
			checker.Check(BitConverter.UInt64BitsToDouble(bits));
		}

		for (var bits = 0x000F_FFFF_FFFF_0000UL; bits <= 0x0010_0000_0001_0000; bits++)
		{
			checker.Check(BitConverter.UInt64BitsToDouble(bits));
		}

		var rng = new Random(777);
		for (var i = 0; i < 300_000; i++)
		{
			checker.Check(BitConverter.UInt64BitsToDouble((ulong)rng.NextInt64(1L << 52) >> rng.Next(0, 52)));
		}

		checker.AssertNoFailures();
	}

	// --------------------------------------------------------------------------------------------------------------
	// Randomized
	// --------------------------------------------------------------------------------------------------------------

	[Fact]
	public void RandomDoubleBitPatternsMatchBcl()
	{
		var checker = new Checker();
		var rng = new Random(20261012);
		for (var i = 0; i < 5_000_000; i++)
		{
			checker.Check(BitConverter.UInt64BitsToDouble((ulong)rng.NextInt64() ^ ((ulong)rng.Next(2) << 63)));
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void RandomSingleBitPatternsMatchBcl()
	{
		var checker = new Checker();
		var rng = new Random(42);
		for (var i = 0; i < 5_000_000; i++)
		{
			checker.Check(BitConverter.UInt32BitsToSingle((uint)rng.NextInt64(1L << 32)));
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void RandomTypicalMagnitudeDoublesMatchBcl()
	{
		// Full-precision values in the range where the fixed notation layouts are used.
		var checker = new Checker();
		var rng = new Random(99);
		for (var i = 0; i < 2_000_000; i++)
		{
			var v = rng.NextDouble() * Math.Pow(10, rng.Next(-6, 19));
			checker.Check(v);
			checker.Check(-v);
			checker.Check((float)v);
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void RandomShortDecimalsMatchBcl()
	{
		// Random digit strings of 1..17 (double) / 1..9 (float) digits and random exponents, parsed with the BCL.
		var checker = new Checker();
		var rng = new Random(1234567);
		var sb = new StringBuilder(40);
		for (var i = 0; i < 1_500_000; i++)
		{
			sb.Clear();
			var digits = rng.Next(1, 18);
			for (var j = 0; j < digits; j++) sb.Append((char)('0' + rng.Next(j == 0 ? 1 : 0, 10)));
			var exponent = (i & 3) == 0 ? rng.Next(-330, 310) : rng.Next(-25, 25);
			sb.Append('e').Append(exponent);
			var text = sb.ToString();
			checker.Check(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
			if (digits <= 9)
			{
				checker.Check(float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
			}
			else
			{
				checker.Check(float.Parse(text.Substring(0, 9) + "e" + (exponent + digits - 9), NumberStyles.Float, CultureInfo.InvariantCulture));
			}
		}

		// Typical CSV-like values: a few integer and fraction digits.
		for (var i = 0; i < 500_000; i++)
		{
			var integer = rng.NextInt64(0, 1_000_000);
			var fractionDigits = rng.Next(0, 9);
			var fraction = rng.NextInt64(0, (long)Math.Pow(10, fractionDigits));
			var text = fractionDigits == 0
				? integer.ToString(CultureInfo.InvariantCulture)
				: integer.ToString(CultureInfo.InvariantCulture) + "." + fraction.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');
			checker.Check(double.Parse(text, CultureInfo.InvariantCulture));
			checker.Check(float.Parse(text, CultureInfo.InvariantCulture));
			checker.Check(double.Parse("0.000" + text.Replace(".", ""), CultureInfo.InvariantCulture));
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void ConsecutiveSingleBitPatternsMatchBcl()
	{
		var checker = new Checker();
		void Sweep(uint start, uint count)
		{
			for (var i = 0U; i < count; i++)
			{
				var bits = start + i;
				checker.Check(BitConverter.UInt32BitsToSingle(bits));
			}
		}

		Sweep(0x0000_0000, 1_048_576);            // tiny subnormals
		Sweep(0x007F_0000, 0x0002_0000);          // subnormal / normal boundary
		Sweep(0x38D1_B717 - 500_000, 1_000_000);  // around 1e-4 (fixed / scientific)
		Sweep(0x3727_C5AC - 250_000, 500_000);    // around 1e-5
		Sweep(0x3F80_0000 - 1_000_000, 2_000_000); // around 1.0
		Sweep(0x4B80_0000 - 262_144, 524_288);    // around 2^24 (integer fast path)
		Sweep(0x4E6E_6B28 - 500_000, 1_000_000);  // around 1e9 (fixed / scientific)
		Sweep(0x7F80_0000 - 1_000_000, 1_000_000 + 2); // up to float.MaxValue and +Infinity
		Sweep(0xBF80_0000 - 250_000, 500_000);    // negative values around -1.0
		checker.AssertNoFailures();
	}

	[Fact]
	public void ConsecutiveDoubleBitPatternsMatchBcl()
	{
		var checker = new Checker();
		void Sweep(ulong start, uint count)
		{
			for (var i = 0U; i < count; i++)
			{
				checker.Check(BitConverter.UInt64BitsToDouble(start + i));
			}
		}

		Sweep(0x3FF0_0000_0000_0000 - 250_000, 500_000); // around 1.0
		Sweep(0x3F1A_36E2_EB1C_432D - 100_000, 200_000); // around 1e-4
		Sweep(0x4376_3457_85D8_A000 - 100_000, 200_000); // around 1e17
		Sweep(0x7FF0_0000_0000_0000 - 200_000, 200_002); // up to double.MaxValue and +Infinity
		checker.AssertNoFailures();
	}

#endif

	// --------------------------------------------------------------------------------------------------------------
	// Destination handling
	// --------------------------------------------------------------------------------------------------------------

	[Fact]
	public void TooSmallDestinationReturnsFalseWithoutWriting()
	{
		double[] doubles =
		[
			0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, -double.MaxValue,
			0.1, -1234.5678, 3.141592653589793, 1.2345678901234567E-05, -1.2345678901234567E-300, 1e16, -12345678901234567.0,
			0.00012345678901234567, -0.0001, 1e23, 42.0, -7.0, 123456789.0,
		];
		float[] floats =
		[
			0f, -0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon, -float.MaxValue,
			0.52913445f, -123.45678f, -0.001234f, 1e8f, 1e9f, -1.17549435E-38f, 16777216f, -0.000123456789f,
		];
		var buffer = new byte[BufferSize];
		foreach (var v in doubles)
		{
			Assert.True(FloatFormatter.TryFormat(v, buffer, out var needed));
			var expected = Encoding.UTF8.GetString(buffer, 0, needed);
			for (var size = 0; size < needed; size++)
			{
				buffer.AsSpan().Fill(Sentinel);
				Assert.False(FloatFormatter.TryFormat(v, buffer.AsSpan(0, size), out var written));
				Assert.Equal(0, written);
				Assert.False(HasOtherThan(buffer, Sentinel));
#if NET
				Assert.False(v.TryFormat(buffer.AsSpan(0, size), out _, default, CultureInfo.InvariantCulture));
#endif
			}

			buffer.AsSpan().Fill(Sentinel);
			Assert.True(FloatFormatter.TryFormat(v, buffer.AsSpan(0, needed), out var exact));
			Assert.Equal(needed, exact);
			Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, exact));
			Assert.False(HasOtherThan(buffer.AsSpan(exact), Sentinel));
		}

		foreach (var v in floats)
		{
			Assert.True(FloatFormatter.TryFormat(v, buffer, out var needed));
			var expected = Encoding.UTF8.GetString(buffer, 0, needed);
			for (var size = 0; size < needed; size++)
			{
				buffer.AsSpan().Fill(Sentinel);
				Assert.False(FloatFormatter.TryFormat(v, buffer.AsSpan(0, size), out var written));
				Assert.Equal(0, written);
				Assert.False(HasOtherThan(buffer, Sentinel));
#if NET
				Assert.False(v.TryFormat(buffer.AsSpan(0, size), out _, default, CultureInfo.InvariantCulture));
#endif
			}

			buffer.AsSpan().Fill(Sentinel);
			Assert.True(FloatFormatter.TryFormat(v, buffer.AsSpan(0, needed), out var exact));
			Assert.Equal(needed, exact);
			Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, exact));
			Assert.False(HasOtherThan(buffer.AsSpan(exact), Sentinel));
		}

		Assert.False(FloatFormatter.TryFormat(1.5, Span<byte>.Empty, out var none));
		Assert.Equal(0, none);
		Assert.False(FloatFormatter.TryFormat(1.5f, Span<byte>.Empty, out none));
		Assert.Equal(0, none);
	}

	[Fact]
	public void ThirtyTwoBytesAreAlwaysEnough()
	{
		// Every output is checked against these maxima above; these are values that reach them.
		Span<byte> buffer = stackalloc byte[32];
		Assert.True(FloatFormatter.TryFormat(-2.2250738585072014E-308, buffer, out var w));
		Assert.Equal("-2.2250738585072014E-308", Encoding.UTF8.GetString(buffer.Slice(0, w).ToArray()));
		Assert.Equal(FloatFormatter.MaxDoubleLength, w);
		Assert.True(FloatFormatter.TryFormat(-1.00000015E-10f, buffer, out w));
		Assert.Equal("-1.00000015E-10", Encoding.UTF8.GetString(buffer.Slice(0, w).ToArray()));
		Assert.Equal(FloatFormatter.MaxSingleLength, w);
		Assert.True(FloatFormatter.MaxDoubleLength <= 32 && FloatFormatter.MaxSingleLength <= 32);
	}

#if NET
	[Fact]
	public void DoublePowerOfTwoDragon4QuirkFollowsRuntime()
	{
		// .NET 10.0.12 and earlier print 2^-25 as "2.980232238769531E-08", which parses back to the next lower double
		// (dotnet/runtime#135582); later servicing releases print "2.9802322387695312E-08". 2^-958 is the only other
		// affected value. The formatter must produce whatever the runtime it runs on produces.
		var checker = new Checker();
		foreach (var v in new[] { Math.ScaleB(1.0, -25), Math.ScaleB(1.0, -958) })
		{
			checker.CheckAround(v, 2);
		}

		checker.AssertNoFailures();
		var bcl = Math.ScaleB(1.0, -25).ToString(CultureInfo.InvariantCulture);
		Assert.True(bcl is "2.980232238769531E-08" or "2.9802322387695312E-08", bcl);
	}
#else
	[Fact]
	public void DoublePowerOfTwoIsTheCorrectShortest()
	{
		// There is no .NET 10 Dragon4 quirk to follow: 2^-25 and 2^-958 get their correct shortest representations.
		Span<byte> buffer = stackalloc byte[32];
		Assert.True(FloatFormatter.TryFormat(BitConverter.Int64BitsToDouble(0x3E60000000000000), buffer, out var w));
		Assert.Equal("2.9802322387695312E-08", Encoding.UTF8.GetString(buffer.Slice(0, w).ToArray()));
		Assert.True(FloatFormatter.TryFormat(BitConverter.Int64BitsToDouble(0x0410000000000000), buffer, out w));
		Assert.Equal("4.1045368012983762E-289", Encoding.UTF8.GetString(buffer.Slice(0, w).ToArray()));
	}
#endif

	// --------------------------------------------------------------------------------------------------------------
	// Internal approximations used by the algorithm, checked exactly over the whole exponent range.
	// --------------------------------------------------------------------------------------------------------------

	[Fact]
	public void LogarithmApproximationsAreExact()
	{
		for (var e = -1100; e <= 1100; e++)
		{
			// floor(e * log10(2)) == largest k with 10^k <= 2^e
			Assert.Equal(FloorLog10(BigPow(2, e)), FloatFormatter.FloorLog10Pow2(e));
			// floor(e * log10(2) - log10(4/3)) == floor(log10(3/4 * 2^e))
			Assert.Equal(FloorLog10(BigPow(2, e) * new BigRational(3, 4)), FloatFormatter.FloorLog10Pow2MinusLog10FourThirds(e));
		}

		for (var e = -350; e <= 350; e++)
		{
			Assert.Equal(FloorLog2(BigPow(10, e)), FloatFormatter.FloorLog2Pow10(e));
		}

		static BigRational BigPow(int b, int e) =>
			e >= 0 ? new BigRational(BigInteger.Pow(b, e), 1) : new BigRational(1, BigInteger.Pow(b, -e));

		static int FloorLog10(BigRational x)
		{
			var k = (int)Math.Floor(BigInteger.Log10(x.Num) - BigInteger.Log10(x.Den)) - 2;
			while (BigPow(10, k + 1) <= x) k++;

			return k;
		}

		static int FloorLog2(BigRational x)
		{
			var k = (int)(BitLength(x.Num) - BitLength(x.Den)) - 2;
			while (BigPow(2, k + 1) <= x) k++;

			return k;
		}
	}

	[Fact]
	public void DigitHelpersAreExact()
	{
		// Digit counts at every power of ten and its neighbours.
		var p = 1UL;
		for (var digits = 1; digits <= 20; digits++)
		{
			Assert.Equal(digits, FloatFormatter.CountDigits(p));
			if (p > 1) Assert.Equal(digits - 1, FloatFormatter.CountDigits(p - 1));
			Assert.Equal(digits, FloatFormatter.CountDigits(p + 1));
			if (digits < 20) p *= 10;
		}

		Assert.Equal(20, FloatFormatter.CountDigits(ulong.MaxValue));
		var rng = new Random(5);
		for (var i = 0; i < 200_000; i++)
		{
			var v = (ulong)rng.NextInt64() >> rng.Next(0, 63);
			if (v == 0) continue;
			Assert.Equal(v.ToString(CultureInfo.InvariantCulture).Length, FloatFormatter.CountDigits(v));
		}

		// Division by 10^j through reciprocals, exact for every dividend below 2^57.
		for (var j = 1; j <= 16; j++)
		{
			var d = (ulong)Math.Pow(10, j);
			var limit = 1UL << 57;
			foreach (var q in new ulong[] { 0, 1, 2, 7, 99, (limit - 1) / d, (limit - 1) / d - 1 })
			{
				foreach (var m in new[] { q * d, q * d + 1, q * d + d - 1, q * d - 1 })
				{
					if (m < limit) Assert.Equal(m / d, FloatFormatter.DivPow10(m, j));
				}
			}

			for (var i = 0; i < 100_000; i++)
			{
				var m = (ulong)rng.NextInt64((long)limit);
				Assert.Equal(m / d, FloatFormatter.DivPow10(m, j));
			}
		}

		// Eight digit conversion.
		Span<byte> bytes = stackalloc byte[8];
		for (var v = 0U; v < 100_000_000; v += v < 1_000_000 ? 1u : 9_973u)
		{
			System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, FloatFormatter.EightDigits(v));
			Assert.Equal(v.ToString("D8", CultureInfo.InvariantCulture), Encoding.ASCII.GetString(bytes));
		}

		System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, FloatFormatter.EightDigits(99_999_999));
		Assert.Equal("99999999", Encoding.ASCII.GetString(bytes));
	}


	private static bool HasOtherThan(ReadOnlySpan<byte> span, byte value)
	{
		foreach (var b in span)
		{
			if (b != value) return true;
		}

		return false;
	}

	private static long BitLength(BigInteger x)
	{
#if NET
		return x.GetBitLength();
#else
		var bytes = x.ToByteArray();
		var length = (bytes.Length - 1) * 8L;
		for (int top = bytes[bytes.Length - 1]; top != 0; top >>= 1) length++;

		return length;
#endif
	}

	private readonly record struct BigRational(BigInteger Num, BigInteger Den)
	{
		public static BigRational operator *(BigRational a, BigRational b) => new(a.Num * b.Num, a.Den * b.Den);
		public static bool operator <=(BigRational a, BigRational b) => a.Num * b.Den <= b.Num * a.Den;
		public static bool operator >=(BigRational a, BigRational b) => a.Num * b.Den >= b.Num * a.Den;
	}
}
