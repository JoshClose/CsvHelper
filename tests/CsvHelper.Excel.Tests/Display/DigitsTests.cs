using System.Globalization;
using System.Numerics;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// A double's 15 significant digits and their rounding to the places shown (<see cref="Digits"/>), against what Excel 365
/// (16.0 build 20527, en-US) showed through COM on 2026-11-16 in <c>0.00000000000000E+00</c>: doubles whose shortest
/// text has 16 digits ending in 5, a little above or below it, and doubles that are exactly the half between two 15-digit
/// numbers. The probes are in <c>Csv\_bench\results\phase8\step2\digits</c> (<c>make_digits.py</c>, <c>make_ties.py</c>,
/// <c>make_ties2.py</c>, and the design's <c>excel_probe.ps1</c>).
/// </summary>
public class DigitsTests
{
	/// <summary>Each double, as its shortest text, and Excel's text for it in <c>0.00000000000000E+00</c>.</summary>
	private const string ExcelAnswers = """
		5.432396130376745E-300	5.43239613037675E-300
		1.755706016255885E-300	1.75570601625588E-300
		8.278167416263685E-100	8.27816741626369E-100
		5.632934792483085E-100	5.63293479248308E-100
		4.118319746502315E-20	4.11831974650232E-20
		9.347932214626045E-20	9.34793221462604E-20
		2.450121706345195E-05	2.45012170634520E-05
		3.745187405867315E-05	3.74518740586731E-05
		0.9090246733265885	9.09024673326589E-01
		0.7544045009063455	7.54404500906345E-01
		5.359963548410695	5.35996354841070E+00
		4.176738922987485	4.17673892298748E+00
		77.90357002693295	7.79035700269330E+01
		32.11205380808165	3.21120538080816E+01
		51062.70960337945	5.10627096033795E+04
		41840.56547433285	4.18405654743328E+04
		82328191418.30705	8.23281914183071E+10
		48684663117.68245	4.86846631176824E+10
		5.578728413040275E+16	5.57872841304028E+16
		5.407080135634625E+16	5.40708013563462E+16
		2.656152305426605E+20	2.65615230542661E+20
		7.473575781818135E+20	7.47357578181813E+20
		1.498571748248225E+100	1.49857174824823E+100
		9.935958434508155E+100	9.93595843450815E+100
		1.069538963016035E+300	1.06953896301604E+300
		3.728140346982225E+300	3.72814034698222E+300
		-5.432396130376745E-300	-5.43239613037675E-300
		-1.755706016255885E-300	-1.75570601625588E-300
		-8.278167416263685E-100	-8.27816741626369E-100
		-5.632934792483085E-100	-5.63293479248308E-100
		123928122675506.5	1.23928122675506E+14
		5019641914031425	5.01964191403142E+15
		98795675504731.25	9.87956755047312E+13
		390351278050801.5	3.90351278050801E+14
		67060186125772.25	6.70601861257722E+13
		48868067851288.75	4.88680678512887E+13
		4883105239016.625	4.88310523901662E+12
		2941706411316.875	2.94170641131687E+12
		447265247959.3125	4.47265247959312E+11
		475725045114.6875	4.75725045114687E+11
		29512582121.15625	2.95125821211562E+10
		108369842601.4375	1.08369842601437E+11
		14101590357.21875	1.41015903572187E+10
		2570949962455925	2.57094996245592E+15
		1.374429428015545E+16	1.37442942801554E+16
		8599524471528855	8.59952447152885E+15
		1.684560558026415E+16	1.68456055802641E+16
		-98795675504731.25	-9.87956755047312E+13
		-390351278050801.5	-3.90351278050801E+14
		-4883105239016.625	-4.88310523901662E+12
		-447265247959.3125	-4.47265247959312E+11
		-2570949962455925	-2.57094996245592E+15
		-1.684560558026415E+16	-1.68456055802641E+16
		790506287307431.5	7.90506287307431E+14
		946912487111805.5	9.46912487111805E+14
		581639778737.1875	5.81639778737187E+11
		838044125417130.5	8.38044125417130E+14
		13770738407275.25	1.37707384072752E+13
		1792539438912295	1.79253943891229E+15
		17694774608041.75	1.76947746080417E+13
		8531472062184945	8.53147206218494E+15
		43931969474902.25	4.39319694749022E+13
		""";

	/// <summary>
	/// The doubles, exactly the half between two 15-digit numbers, that Excel takes up where 634 of the 638 probed go down:
	/// no rule found explains them, so they are listed differences.
	/// </summary>
	private const string ExcelTakesUp = """
		548368913012129.5	5.48368913012130E+14
		506320692698959.5	5.06320692698960E+14
		5187285576.265625	5.18728557626563E+09
		-5187285576.265625	-5.18728557626563E+09
		""";

	/// <summary>Each double is taken to Excel's 15 digits: by its exact value, and an exact half down.</summary>
	[Fact]
	public void TakesFifteenDigitsAsExcelDoes()
	{
		foreach (var (value, text) in Lines(ExcelAnswers))
		{
			Assert.True(text == Scientific(value), $"{value.ToString("R", CultureInfo.InvariantCulture)}: Excel {text}, ours {Scientific(value)}.");
		}
	}

	/// <summary>The exact halves Excel takes up go down, one digit below Excel's: the listed differences.</summary>
	[Fact]
	public void ExactHalvesExcelTakesUpGoDown()
	{
		foreach (var (value, text) in Lines(ExcelTakesUp))
		{
			var ours = Scientific(value);
			Assert.NotEqual(text, ours);
			Assert.Equal(Reference(value), (Digits.Of(value).Significand.ToString(CultureInfo.InvariantCulture), Digits.Of(value).Exponent));
		}
	}

	/// <summary>Random doubles of every magnitude, and exact halves, match the exact value rounded to 15 digits, halves down.</summary>
	[Fact]
	public void MatchesTheExactValueRounded()
	{
		var random = new Random(81523);
		var bytes = new byte[8];
		var checkedHalves = 0;
		for (var i = 0; i < 60_000; i++)
		{
			double value;
			if (i % 3 == 0)
			{
				// Any bits: every magnitude, subnormals among them.
				random.NextBytes(bytes);
				value = BitConverter.ToDouble(bytes, 0);
				if (double.IsNaN(value) || double.IsInfinity(value))
				{
					continue;
				}
			}
			else if (i % 3 == 1)
			{
				// A decimal of 15 to 17 digits, as a workbook stores numbers.
				var digits = (random.NextDouble() * 9e16) + 1e16;
				value = Math.Floor(digits) * Math.Pow(10, random.Next(-30, 30) - 16);
			}
			else
			{
				// A binary fraction: t / 2^j, often exactly the half between two 15-digit numbers.
				value = (random.NextDouble() * 9e15) / Math.Pow(2, random.Next(0, 60));
				value = Math.Floor(value * 1024) / 1024;
			}

			var digitsOf = Digits.Of(value);
			if (value == 0 || Math.Abs(value) < 2.2250738585072014E-308)
			{
				// Excel holds a subnormal double as 0.
				Assert.True(digitsOf.IsZero, value.ToString("R", CultureInfo.InvariantCulture));

				continue;
			}

			var expected = Reference(value);
			Assert.True(expected == (digitsOf.Significand.ToString(CultureInfo.InvariantCulture), digitsOf.Exponent),
				$"{value.ToString("R", CultureInfo.InvariantCulture)}: exact {expected}, ours ({digitsOf.Significand}, {digitsOf.Exponent}).");
			checkedHalves += IsExactHalf(value) ? 1 : 0;
		}

		Assert.True(checkedHalves > 100, $"Only {checkedHalves} exact halves were checked.");
	}

	/// <summary>The edges of doubles: zero of either sign, values that aren't finite, subnormals (Excel's 0), the smallest normal and the largest.</summary>
	[Fact]
	public void TakesTheEdges()
	{
		Assert.True(Digits.Of(0).IsZero);
		Assert.True(Digits.Of(-0.0).IsZero);
		Assert.True(Digits.Of(double.NaN).IsZero);
		Assert.True(Digits.Of(double.PositiveInfinity).IsZero);
		Assert.True(Digits.Of(double.Epsilon).IsZero);
		Assert.True(Digits.Of(-2.2250738585072009E-308).IsZero);
		Assert.Equal("1.79769313486232E+308", Scientific(double.MaxValue));
		Assert.Equal("2.22507385850720E-308", Scientific(2.2250738585072014E-308));
		Assert.Equal("1.00000000000000E+15", Scientific(999999999999999.9));
		Assert.Equal("3.00000000000000E-01", Scientific(0.30000000000000004));
		Assert.Equal("1.00000000000000E+00", Scientific(1));
		Assert.Equal("1.00000000000000E+16", Scientific(1e16));
		Assert.Equal("1.23456789012346E+17", Scientific(123456789012345678));
	}

	/// <summary>
	/// Every power of two matches the exact value rounded: 137 of them have a shortest text of 16 digits ending in 5, and in
	/// three (2^298, 2^847 and 2^848) the decimal, just below, takes a 32-bit word fewer than the double when compared.
	/// </summary>
	[Fact]
	public void TakesEveryPowerOfTwo()
	{
		for (var power = -1022; power <= 1023; power++)
		{
			var value = Math.Pow(2, power);
			var digits = Digits.Of(value);
			Assert.True(Reference(value) == (digits.Significand.ToString(CultureInfo.InvariantCulture), digits.Exponent), $"2^{power}");
		}

		Assert.Equal("5.09258994083622E+89", Scientific(Math.Pow(2, 298)));
	}

	/// <summary>
	/// The digits are those of the text path they replaced (<see cref="DigitsReference"/>: core's text formatted and read
	/// back), for random doubles of every magnitude and the cases above: Excel's answers, the halves it takes up, the edges,
	/// every power of two and its neighbours, and the numbers rounded.
	/// </summary>
	[Fact]
	public void TakesTheTextPathsDigits()
	{
		var values = new List<double>
		{
			0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, -2.2250738585072009E-308,
			2.2250738585072014E-308, -2.2250738585072014E-308, double.MaxValue, -double.MaxValue, 999999999999999.9,
			0.30000000000000004, 1, 1e16, 123456789012345678, 1234.5, 0.5, 1e20, 2.675, 1.005, 0.285, 8.345, 1.5, 2.5, 0.125,
			999.995, 0.004, 0.005, 0.0004, 0.0006, 9.95, 45306.99999999, 1250, 49, 50, 9.87654321e-5, 0.99999999999,
		};
		values.AddRange(Lines(ExcelAnswers).Select(line => line.Value));
		values.AddRange(Lines(ExcelTakesUp).Select(line => line.Value));
		for (var power = -1074; power <= 1023; power++)
		{
			var bits = power < -1022 ? 1L << (power + 1074) : (long)(power + 1023) << 52;
			values.Add(BitConverter.Int64BitsToDouble(bits));
			values.Add(BitConverter.Int64BitsToDouble(bits + 1));
			values.Add(-BitConverter.Int64BitsToDouble(bits - 1));
		}

		var random = new Random(7023);
		var bytes = new byte[8];
		for (var i = 0; i < 300_000; i++)
		{
			switch (i % 4)
			{
				case 0:
					// Any bits: every magnitude, subnormals and values that aren't finite among them.
					random.NextBytes(bytes);
					values.Add(BitConverter.ToDouble(bytes, 0));
					break;
				case 1:
					// A decimal of 15 to 17 digits, as a workbook stores numbers.
					values.Add(Math.Floor((random.NextDouble() * 9e16) + 1e16) * Math.Pow(10, random.Next(-30, 30) - 16));
					break;
				case 2:
					// A binary fraction, often exactly the half between two 15-digit numbers.
					values.Add(Math.Floor((random.NextDouble() * 9e15) / Math.Pow(2, random.Next(0, 60)) * 1024) / 1024);
					break;
				default:
					// An integer, with trailing zeros now and then.
					values.Add(Math.Floor(random.NextDouble() * 1e15) * Math.Pow(10, random.Next(0, 8)));
					break;
			}
		}

		var failures = new List<string>();
		foreach (var value in values)
		{
			var digits = Digits.Of(value);
			var expected = DigitsReference.Of(value);
			if ((digits.Significand, digits.Count, digits.Exponent) != expected && failures.Count < 20)
			{
				failures.Add($"{value.ToString("R", CultureInfo.InvariantCulture)}: the text path {expected}, ours ({digits.Significand}, {digits.Count}, {digits.Exponent})");
			}
		}

		Assert.True(failures.Count == 0, string.Join("\n", failures));
		Assert.True(values.Count > 300_000);
	}

	/// <summary>Rounding to the places shown is half away from zero, on the 15 digits: Excel's answers from the design.</summary>
	/// <param name="value">The number.</param>
	/// <param name="decimals">The places.</param>
	/// <param name="expected">The rounded number.</param>
	[Theory]
	[InlineData(2.675, 2, "2.68")]
	[InlineData(1.005, 2, "1.01")]
	[InlineData(0.285, 2, "0.29")]
	[InlineData(8.345, 2, "8.35")]
	[InlineData(0.5, 0, "1")]
	[InlineData(1.5, 0, "2")]
	[InlineData(2.5, 0, "3")]
	[InlineData(0.125, 2, "0.13")]
	[InlineData(999.995, 2, "1000")]
	[InlineData(0.004, 2, "0")]
	[InlineData(0.005, 2, "0.01")]
	[InlineData(0.0004, 2, "0")]
	[InlineData(0.0006, 3, "0.001")]
	[InlineData(9.95, 1, "10")]
	[InlineData(45306.99999999, 0, "45307")]
	[InlineData(123456789012345678, 0, "123456789012346000")]
	[InlineData(0.30000000000000004, 15, "0.3")]
	[InlineData(1234.5, -2, "1200")]
	[InlineData(1250, -2, "1300")]
	[InlineData(49, -2, "0")]
	[InlineData(50, -2, "100")]
	[InlineData(9.87654321e-5, 10, "0.0000987654")]
	[InlineData(0.99999999999, 10, "1")]
	public void RoundsHalfAwayFromZero(double value, int decimals, string expected)
	{
		Assert.Equal(expected, Plain(Digits.Of(value).Round(decimals)));
		Assert.Equal(expected, Plain(Digits.Of(-value).Round(decimals)));
	}

	/// <summary>Scaling moves the digits by powers of ten, as <c>%</c> and scaling commas do.</summary>
	[Fact]
	public void Scales()
	{
		Assert.Equal("1.2", Plain(Digits.Of(1234.5).Scale(-3).Round(1)));
		Assert.Equal("50", Plain(Digits.Of(0.5).Scale(2)));
		Assert.Equal("0.0012345", Plain(Digits.Of(1234.5).Scale(-6)));
		Assert.True(Digits.Of(0).Scale(5).IsZero);
	}

	/// <summary>Each digit is read at its power of ten, and the integer part's length counted.</summary>
	[Fact]
	public void ReadsDigits()
	{
		var digits = Digits.Of(1234.5);
		Assert.Equal(1, digits.DigitAt(3));
		Assert.Equal(4, digits.DigitAt(0));
		Assert.Equal(5, digits.DigitAt(-1));
		Assert.Equal(0, digits.DigitAt(-2));
		Assert.Equal(0, digits.DigitAt(4));
		Assert.Equal(4, digits.IntegerLength);
		Assert.Equal(5, digits.Count);
		Assert.Equal(3, digits.Exponent);
		Assert.Equal(0, Digits.Of(0.5).IntegerLength);
		Assert.Equal(0, Digits.Of(0).IntegerLength);
		Assert.Equal(0, Digits.Of(0).DigitAt(0));
		Assert.Equal(1, Digits.Of(1e20).DigitAt(20));
		Assert.Equal(21, Digits.Of(1e20).IntegerLength);
	}

	// .NET 10 only: on .NET 8, the unoptimized (tier 0) code of core's double formatting allocates until the JIT optimizes
	// it, as ExcelToCsvTests found for its parsing; with tiered compilation off (DOTNET_TieredCompilation=0), .NET 8
	// allocates nothing here either (checked 2026-11-16). For the same reason, it runs in Release builds only.
#if NET10_0_OR_GREATER
	/// <summary>Taking and rounding digits allocates nothing, exact halves among them.</summary>
	[OptimizedFact]
	public void AllocatesNothing()
	{
		var values = new[] { 2.675, 123928122675506.5, 5.432396130376745E-300, 1e300, double.Epsilon, 0.1 };
		var sum = 0L;
		foreach (var value in values)
		{
			sum += Digits.Of(value).Round(2).Count;
		}

		// Two passes are measured, and the smaller kept: the runtime allocates once on the thread at a moment the JIT's
		// tiering sets (see ExcelToCsvTests.AllocatesTheSameForAnyNumberOfRows), which can fall in one pass but not in both.
		Assert.Equal(0, Math.Min(Allocated(), Allocated()));
		Assert.True(sum > 0);

		long Allocated()
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 1000; i++)
			{
				foreach (var value in values)
				{
					sum += Digits.Of(value).Round(2).Count;
				}
			}

			return GC.GetAllocatedBytesForCurrentThread() - before;
		}
	}
#endif

	/// <summary>Reads lines of a double and Excel's text.</summary>
	/// <param name="table">The lines, tab-separated.</param>
	/// <returns>The pairs.</returns>
	private static IEnumerable<(double Value, string Text)> Lines(string table)
	{
		foreach (var line in table.Split('\n'))
		{
			var parts = line.Trim().Split('\t');
			if (parts.Length == 2)
			{
				yield return (double.Parse(parts[0], CultureInfo.InvariantCulture), parts[1]);
			}
		}
	}

	/// <summary>A double's 15 digits as <c>0.00000000000000E+00</c> shows them.</summary>
	/// <param name="value">The double.</param>
	/// <returns>The text.</returns>
	private static string Scientific(double value)
	{
		var digits = Digits.Of(value);
		var text = digits.Significand.ToString(CultureInfo.InvariantCulture).PadRight(15, '0');
		var exponent = digits.Exponent;

		return (value < 0 ? "-" : "") + text.Substring(0, 1) + "." + text.Substring(1) + (exponent < 0 ? "E-" : "E+") +
			Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
	}

	/// <summary>Rounded digits in decimals, without trailing zeros.</summary>
	/// <param name="digits">The digits.</param>
	/// <returns>The text: <c>0</c> for zero.</returns>
	private static string Plain(Digits digits)
	{
		if (digits.IsZero)
		{
			return "0";
		}

		var text = digits.Significand.ToString(CultureInfo.InvariantCulture);
		if (digits.Exponent >= text.Length - 1)
		{
			return text + new string('0', digits.Exponent - text.Length + 1);
		}

		if (digits.Exponent >= 0)
		{
			return text.Substring(0, digits.Exponent + 1) + "." + text.Substring(digits.Exponent + 1);
		}

		return "0." + new string('0', -digits.Exponent - 1) + text;
	}

	/// <summary>The exact value of a double's absolute value in decimal: its digits and the power of ten of the first.</summary>
	/// <param name="value">The double.</param>
	/// <returns>Every digit, and the power.</returns>
	private static (string Digits, int Exponent) Exact(double value)
	{
		var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
		var biased = (int)(bits >> 52) & 0x7FF;
		var fraction = bits & 0xF_FFFF_FFFF_FFFF;
		var significand = biased == 0 ? fraction : fraction | (1L << 52);
		var exponent = (biased == 0 ? 1 : biased) - 1075;
		var integer = exponent >= 0 ? new BigInteger(significand) << exponent : new BigInteger(significand) * BigInteger.Pow(5, -exponent);
		var text = integer.ToString(CultureInfo.InvariantCulture);

		return (text, text.Length - 1 - Math.Max(0, -exponent));
	}

	/// <summary>
	/// The reference: the exact value rounded to 15 significant digits, and exactly the half down, without trailing zeros.
	/// </summary>
	/// <param name="value">The double, not zero.</param>
	/// <returns>The significant digits and the power of ten of the first.</returns>
	private static (string Digits, int Exponent) Reference(double value)
	{
		var (text, exponent) = Exact(value);
		if (text.Length > Digits.MaxSignificant)
		{
			var rest = text.Substring(Digits.MaxSignificant);
			var up = rest[0] > '5' || (rest[0] == '5' && rest.Substring(1).Any(c => c != '0'));
			var kept = BigInteger.Parse(text.Substring(0, Digits.MaxSignificant), CultureInfo.InvariantCulture) + (up ? 1 : 0);
			text = kept.ToString(CultureInfo.InvariantCulture);
			if (text.Length > Digits.MaxSignificant)
			{
				exponent++;
			}
		}

		return (text.TrimEnd('0'), exponent);
	}

	/// <summary>Whether a double is exactly the half between two 15-digit numbers.</summary>
	/// <param name="value">The double.</param>
	/// <returns>Whether its exact value has 16 significant digits ending in 5.</returns>
	private static bool IsExactHalf(double value)
	{
		var digits = Exact(value).Digits.TrimEnd('0');

		return digits.Length == 16 && digits[15] == '5';
	}
}
