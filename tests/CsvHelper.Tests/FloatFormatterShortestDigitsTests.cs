using System.Globalization;
using System.Text;
using Xunit;

namespace CsvHelper.Tests;

/// <summary>
/// <see cref="FloatFormatter.ShortestDigits"/>: the digits, and the power of ten of the last, that
/// <see cref="FloatFormatter.TryFormat(double, Span{byte}, out int)"/> writes for a double, read back from its text: for
/// random doubles of every binary exponent, subnormals, every power of two (2^-25 and 2^-958 as the runtime formats them),
/// integers below and above 2^53 with and without trailing zeros, and the largest and smallest doubles.
/// </summary>
public class FloatFormatterShortestDigitsTests
{
	/// <summary>The fraction bits of a double.</summary>
	private const ulong FractionMask = 0x000F_FFFF_FFFF_FFFFUL;

	/// <summary>2^53: integers below it are formatted exactly.</summary>
	private const ulong TwoTo53 = 1UL << 53;

	[Fact]
	public void TakesTheDigitsOfRandomDoublesOfEveryExponent()
	{
		var random = new Random(20261123);
		var checker = new Checker();
		for (var biased = 0UL; biased <= 0x7FE; biased++)
		{
			// The smallest and largest fractions of each exponent, then random ones.
			if (biased != 0)
			{
				checker.Check(FromBits(biased << 52));
			}

			checker.Check(FromBits((biased << 52) | 1));
			checker.Check(FromBits((biased << 52) | FractionMask));
			for (var i = 0; i < 200; i++)
			{
				var fraction = NextBits(random) & FractionMask;
				if (biased == 0 && fraction == 0)
				{
					continue;
				}

				checker.Check(FromBits((biased << 52) | fraction));
			}
		}

		// Random bits of any exponent, with fewer fraction bits now and then, as short decimals have.
		for (var i = 0; i < 300_000; i++)
		{
			var bits = NextBits(random) & 0x7FFF_FFFF_FFFF_FFFFUL;
			bits &= ~((1UL << random.Next(0, 53)) - 1);
			if (bits != 0 && bits < 0x7FF0_0000_0000_0000UL)
			{
				checker.Check(FromBits(bits));
			}
		}

		checker.AssertNoFailures();
		Assert.True(checker.Count > 700_000, $"Only {checker.Count} doubles were checked.");
	}

	[Fact]
	public void TakesTheDigitsOfSubnormals()
	{
		var random = new Random(3241);
		var checker = new Checker();
		for (var bits = 1UL; bits <= 100_000; bits++)
		{
			checker.Check(FromBits(bits));
		}

		// Up to the smallest normal double, and past it.
		for (var bits = 0x000F_FFFF_FFFF_0000UL; bits <= 0x0010_0000_0001_0000UL; bits++)
		{
			checker.Check(FromBits(bits));
		}

		for (var i = 0; i < 200_000; i++)
		{
			var bits = (NextBits(random) & FractionMask) >> random.Next(0, 52);
			if (bits != 0)
			{
				checker.Check(FromBits(bits));
			}
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void TakesTheDigitsOfEveryPowerOfTwo()
	{
		var checker = new Checker();
		for (var power = -1074; power <= 1023; power++)
		{
			var value = PowerOfTwo(power);
			checker.Check(value);
			checker.Check(FromBits(Bits(value) + 1));
			if (power > -1074)
			{
				checker.Check(FromBits(Bits(value) - 1));
			}
		}

		checker.AssertNoFailures();
	}

	[Fact]
	public void TakesTheQuirkyPowersOfTwoAsTheRuntimeFormatsThem()
	{
		// 2^-25 and 2^-958, which .NET 10 runtimes up to at least 10.0.12 format one digit short (see PowerOfTwoQuirk).
		var cases = new[]
		{
			(Power: -25, Quirk: (Digits: 2_980_232_238_769_531UL, Exponent: -23), Correct: (Digits: 29_802_322_387_695_312UL, Exponent: -24)),
			(Power: -958, Quirk: (Digits: 4_104_536_801_298_376UL, Exponent: -304), Correct: (Digits: 41_045_368_012_983_762UL, Exponent: -305)),
		};
		foreach (var (power, quirk, correct) in cases)
		{
			var value = PowerOfTwo(power);
			var digits = FloatFormatter.ShortestDigits(value, out var exponent);

			Assert.Equal(FromText(Text(value)), (digits, exponent));
#if NET
			// As the runtime formats it, whichever of the two that is.
			Assert.Equal(FromText(value.ToString("R", CultureInfo.InvariantCulture)), (digits, exponent));
			Assert.True((digits, exponent) == quirk || (digits, exponent) == correct, $"2^{power}: {digits}, {exponent}");
#else
			// No .NET 10 BCL to follow: the correct shortest digits.
			Assert.Equal(correct, (digits, exponent));
#endif
		}
	}

	[Fact]
	public void TakesTheDigitsOfIntegers()
	{
		var random = new Random(53);
		var checker = new Checker();
		for (var i = 1UL; i <= 100_000; i++)
		{
			checker.Check(i);
		}

		var power = 1UL;
		for (var zeros = 0; zeros <= 19; zeros++, power *= 10)
		{
			// Powers of ten and their multiples, below 2^53 (exact, trailing zeros taken off) and above it.
			checker.Check(power);
			checker.Check(9.0 * power);
			checker.Check(7.0 * power);
			for (var i = 0; i < 2_000; i++)
			{
				var multiple = (NextBits(random) >> random.Next(11, 64)) | 1;
				checker.Check(multiple * (double)power);
				if (multiple <= (TwoTo53 - 1) / power)
				{
					checker.Check((double)(multiple * power));
				}
			}
		}

		for (var i = 0; i < 200_000; i++)
		{
			// Below 2^53, then from 2^52 to 2^53 (whose exponent puts them past the exact path), then above 2^53.
			checker.Check((double)((NextBits(random) >> 11) >> random.Next(0, 53) | 1));
			checker.Check((double)((NextBits(random) & ((1UL << 52) - 1)) | (1UL << 52)));
			checker.Check((double)(NextBits(random) | TwoTo53));
		}

		for (var offset = 0UL; offset <= 5_000; offset++)
		{
			checker.Check((double)(TwoTo53 - 1 - offset));
			checker.Check((double)(TwoTo53 + offset * 2));
		}

		for (var exponent = 16; exponent <= 308; exponent++)
		{
			checker.Check(double.Parse("1E" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
			checker.Check(double.Parse("1.23E" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
		}

		checker.AssertNoFailures();
	}

	[Theory]
	[InlineData(1.0, 1UL, 0)]
	[InlineData(0.1, 1UL, -1)]
	[InlineData(123.45, 12345UL, -2)]
	[InlineData(1200.0, 12UL, 2)]
	[InlineData(1e15, 1UL, 15)]
	[InlineData(1e16, 1UL, 16)]
	[InlineData(1e17, 1UL, 17)]
	[InlineData(9007199254740990.0, 900719925474099UL, 1)]
	[InlineData(9007199254740991.0, 9007199254740991UL, 0)]
	[InlineData(9007199254740992.0, 9007199254740992UL, 0)]
	[InlineData(0.30000000000000004, 30000000000000004UL, -17)]
	[InlineData(4.505630514819934, 4505630514819934UL, -15)]
	[InlineData(double.MaxValue, 17976931348623157UL, 292)]
	[InlineData(double.Epsilon, 5UL, -324)]
	[InlineData(2.2250738585072014E-308, 22250738585072014UL, -324)]
	[InlineData(2.225073858507201E-308, 2225073858507201UL, -323)]
	public void TakesTheDigitsOfTheEdges(double value, ulong digits, int exponent)
	{
		Assert.Equal((digits, exponent), (FloatFormatter.ShortestDigits(value, out var actual), actual));
		Assert.Equal(FromText(Text(value)), (digits, exponent));
	}

	/// <summary>A double from its bits.</summary>
	/// <param name="bits">The bits.</param>
	/// <returns>The double.</returns>
	private static double FromBits(ulong bits) => BitConverter.Int64BitsToDouble((long)bits);

	/// <summary>A double's bits.</summary>
	/// <param name="value">The double.</param>
	/// <returns>The bits.</returns>
	private static ulong Bits(double value) => (ulong)BitConverter.DoubleToInt64Bits(value);

	/// <summary>A power of two, from its bits, on every target.</summary>
	/// <param name="power">The power, from -1074 to 1023.</param>
	/// <returns>The double.</returns>
	private static double PowerOfTwo(int power) => FromBits(power < -1022 ? 1UL << (power + 1074) : (ulong)(power + 1023) << 52);

	/// <summary>64 random bits.</summary>
	/// <param name="random">The random numbers.</param>
	/// <returns>The bits.</returns>
	private static ulong NextBits(Random random)
	{
		var bytes = new byte[8];
		random.NextBytes(bytes);

		return BitConverter.ToUInt64(bytes, 0);
	}

	/// <summary>The text <see cref="FloatFormatter.TryFormat(double, Span{byte}, out int)"/> writes.</summary>
	/// <param name="value">The double.</param>
	/// <returns>The text.</returns>
	private static string Text(double value)
	{
		var buffer = new byte[FloatFormatter.MaxDoubleLength];
		Assert.True(FloatFormatter.TryFormat(value, buffer, out var written));

		return Encoding.ASCII.GetString(buffer, 0, written);
	}

	/// <summary>A positive number's text read back: its digits without leading or trailing zeros, and the last one's power of ten.</summary>
	/// <param name="text">The text: <c>123.45</c>, <c>0.0001</c>, <c>1E-05</c>, <c>1.5E+300</c>.</param>
	/// <returns>The digits and the power.</returns>
	private static (ulong Digits, int Exponent) FromText(string text)
	{
		var e = text.IndexOf('E');
		var power = e < 0 ? 0 : int.Parse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
		var mantissa = e < 0 ? text : text.Substring(0, e);
		var point = mantissa.IndexOf('.');
		var integerDigits = point < 0 ? mantissa.Length : point;
		var significant = mantissa.Replace(".", "").TrimEnd('0');

		return (ulong.Parse(significant.TrimStart('0'), CultureInfo.InvariantCulture), integerDigits - significant.Length + power);
	}

	/// <summary>Checks doubles' digits against their text's, and keeps the first failures.</summary>
	private sealed class Checker
	{
		private readonly List<string> failures = [];
		private long mismatches;

		/// <summary>How many doubles were checked.</summary>
		public long Count { get; private set; }

		/// <summary>Checks a double's digits: those of its text, 1 to 17 of them, the last not 0.</summary>
		/// <param name="value">The double, positive, finite and not 0.</param>
		public void Check(double value)
		{
			Count++;
			var expected = FromText(Text(value));
			var digits = FloatFormatter.ShortestDigits(value, out var exponent);
			if ((digits, exponent) != expected || digits == 0 || digits % 10 == 0 || digits >= 100_000_000_000_000_000UL)
			{
				mismatches++;
				if (failures.Count < 25)
				{
					failures.Add($"0x{Bits(value):X16} ({Text(value)}): expected {expected.Digits}, {expected.Exponent}; got {digits}, {exponent}");
				}
			}
		}

		/// <summary>Fails with the first mismatches, if there were any.</summary>
		public void AssertNoFailures()
		{
			Assert.True(mismatches == 0, $"{mismatches} mismatches of {Count} doubles (the first {failures.Count}):\n" + string.Join("\n", failures));
		}
	}
}
