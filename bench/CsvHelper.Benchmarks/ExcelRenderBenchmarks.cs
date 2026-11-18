using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// What <see cref="ExcelText.Formatted"/>'s renderer costs a cell: <see cref="Count"/> numbers each shown in a code as Excel
/// shows them (<c>NumberText.TryWrite</c>, into UTF-8 on the stack), in the invariant culture (Excel en-US) and the 1900
/// date system. The time is per cell (<see cref="BenchmarkAttribute.OperationsPerInvoke"/>).
/// </summary>
/// <remarks>
/// <para>
/// The codes: General; <c>#,##0.00</c>; <c>yyyy-mm-dd hh:mm:ss</c>, the orders' dates' (<see cref="ExcelOrders.DateFormat"/>);
/// <c># ?/?</c>; and a conditional code, <see cref="ConditionalCode"/>, which shows millions and thousands with a letter.
/// Each has numbers of its own, from a fixed seed, every one of which it shows (<see cref="Setup"/> checks).
/// </para>
/// <para>
/// Two references, for scale: <see cref="Parse"/>, the stored text of General's numbers parsed as a reader parses a cell's
/// value before it renders it (core's <c>CsvParse.TryDouble</c>), which a read under <c>Formatted</c> pays besides the
/// renderer; and <see cref="DotNet"/>, the same numbers written by .NET's <see cref="double.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
/// the shortest text that reads back as the same number.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
public class ExcelCellRender
{
	/// <summary>The numbers each code shows: a call renders them all.</summary>
	public const int Count = 1024;

	/// <summary>A conditional code: millions as <c>12.3M</c>, thousands as <c>12.3K</c>, and the rest as integers.</summary>
	public const string ConditionalCode = "[>=1000000]0.0,,\"M\";[>=1000]0.0,\"K\";0";

	/// <summary>The room for a cell's text: core's formatter renders on the stack in as much.</summary>
	private const int Room = 512;

	/// <summary>General, compiled.</summary>
	private static readonly FormatCode General = FormatCode.Compile("General");

	/// <summary><c>#,##0.00</c>, compiled.</summary>
	private static readonly FormatCode Grouped = FormatCode.Compile("#,##0.00");

	/// <summary><c>yyyy-mm-dd hh:mm:ss</c>, compiled.</summary>
	private static readonly FormatCode DateTimeCode = FormatCode.Compile("yyyy-mm-dd hh:mm:ss");

	/// <summary><c># ?/?</c>, compiled.</summary>
	private static readonly FormatCode FractionCode = FormatCode.Compile("# ?/?");

	/// <summary><see cref="ConditionalCode"/>, compiled.</summary>
	private static readonly FormatCode Conditional = FormatCode.Compile(ConditionalCode);

	/// <summary>General's numbers.</summary>
	private double[] generalValues = [];

	/// <summary>General's numbers' stored text, as UTF-8: each number's shortest round-trip text, as a sheet holds it.</summary>
	private byte[][] generalStored = [];

	/// <summary><c>#,##0.00</c>'s numbers.</summary>
	private double[] groupedValues = [];

	/// <summary><c>yyyy-mm-dd hh:mm:ss</c>'s numbers.</summary>
	private double[] dateValues = [];

	/// <summary><c># ?/?</c>'s numbers.</summary>
	private double[] fractionValues = [];

	/// <summary><see cref="ConditionalCode"/>'s numbers.</summary>
	private double[] conditionalValues = [];

	/// <summary>
	/// Makes each code's numbers, from a fixed seed, and checks that the code shows every one of them, so that each call
	/// renders all <see cref="Count"/>:
	/// <list type="bullet">
	/// <item><description>General: integers below a million, amounts of two decimals, doubles below 1,000 in full, and doubles from 10⁻⁸ to 10¹⁵ (some in exponents); a quarter negative.</description></item>
	/// <item><description><c>#,##0.00</c>: doubles in full from −200,000 to 800,000.</description></item>
	/// <item><description><c>yyyy-mm-dd hh:mm:ss</c>: the serials of 1990 to 2030, each at a time of day in full.</description></item>
	/// <item><description><c># ?/?</c>: doubles in full below 100.</description></item>
	/// <item><description>The conditional code: from 1 to 10⁹, evenly over the powers of ten, so a third in each section.</description></item>
	/// </list>
	/// It checks too that each of General's stored texts parses back to its number, so that <see cref="Parse"/> times the
	/// parse a read makes.
	/// </summary>
	/// <exception cref="InvalidOperationException">A code doesn't show one of its numbers, or a stored text doesn't parse back to its number.</exception>
	[GlobalSetup]
	public void Setup()
	{
		var random = new Random(20261117);

		generalValues = new double[Count];
		for (var i = 0; i < Count; i++)
		{
			var value = (i % 4) switch
			{
				0 => random.Next(0, 1_000_000),
				1 => Math.Round(random.NextDouble() * 10_000, 2),
				2 => random.NextDouble() * 1_000,
				_ => Math.Pow(10, random.Next(-8, 16)) * random.NextDouble(),
			};
			generalValues[i] = random.Next(4) == 0 ? -value : value;
		}

		generalStored = generalValues.Select(v => Encoding.UTF8.GetBytes(v.ToString("R", CultureInfo.InvariantCulture))).ToArray();
		for (var i = 0; i < Count; i++)
		{
			if (!CsvParse.TryDouble(generalStored[i], CultureInfo.InvariantCulture, out var parsed) || parsed != generalValues[i])
			{
				throw new InvalidOperationException($"{Encoding.UTF8.GetString(generalStored[i])} doesn't parse back to {generalValues[i]:R}.");
			}
		}

		groupedValues = Fill(random, r => (r.NextDouble() - 0.2) * 1_000_000);
		dateValues = Fill(random, r => 32874 + r.NextDouble() * 14974);
		fractionValues = Fill(random, r => r.NextDouble() * 100);
		conditionalValues = Fill(random, r => Math.Pow(10, r.NextDouble() * 9));

		Check(General, generalValues);
		Check(Grouped, groupedValues);
		Check(DateTimeCode, dateValues);
		Check(FractionCode, fractionValues);
		Check(Conditional, conditionalValues);
	}

	/// <summary>General's numbers, each in General.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("General")]
	public long Render_General() => Render(General, generalValues);

	/// <summary>Amounts in <c>#,##0.00</c>.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Grouped")]
	public long Render_Grouped() => Render(Grouped, groupedValues);

	/// <summary>Dates and times in <c>yyyy-mm-dd hh:mm:ss</c>.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("DateTime")]
	public long Render_DateTime() => Render(DateTimeCode, dateValues);

	/// <summary>Numbers in <c># ?/?</c>.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Fraction")]
	public long Render_Fraction() => Render(FractionCode, fractionValues);

	/// <summary>Numbers in <see cref="ConditionalCode"/>.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Conditional")]
	public long Render_Conditional() => Render(Conditional, conditionalValues);

	/// <summary>A reference: General's numbers' stored text parsed, as a reader parses a cell's value before rendering it.</summary>
	/// <returns>A sum of the numbers' bits, so that the parse isn't dropped.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Parse")]
	public long Parse()
	{
		var total = 0L;
		foreach (var stored in generalStored)
		{
			if (CsvParse.TryDouble(stored, CultureInfo.InvariantCulture, out var value))
			{
				total += BitConverter.DoubleToInt64Bits(value);
			}
		}

		return total;
	}

	/// <summary>A reference: General's numbers written by .NET as their shortest round-trip text, as UTF-8.</summary>
	/// <returns>The bytes of their texts.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("DotNet")]
	public long DotNet()
	{
		Span<byte> buffer = stackalloc byte[Room];
		var total = 0L;
		foreach (var value in generalValues)
		{
			if (value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
			{
				total += written;
			}
		}

		return total;
	}

	/// <summary>Renders numbers in a code, as the reader's formatter does, into UTF-8 on the stack.</summary>
	/// <param name="code">The code.</param>
	/// <param name="values">The numbers.</param>
	/// <returns>The bytes of their texts.</returns>
	private static long Render(FormatCode code, double[] values)
	{
		Span<byte> buffer = stackalloc byte[Room];
		var culture = DisplayCulture.Invariant;
		var total = 0L;
		foreach (var value in values)
		{
			var text = new ShownText(buffer);
			if (NumberText.TryWrite(code, value, culture, false, ref text))
			{
				total += text.Length;
			}
		}

		return total;
	}

	/// <summary>Makes <see cref="Count"/> numbers.</summary>
	/// <param name="random">The numbers' source.</param>
	/// <param name="next">Makes a number.</param>
	/// <returns>The numbers.</returns>
	private static double[] Fill(Random random, Func<Random, double> next)
	{
		var values = new double[Count];
		for (var i = 0; i < Count; i++)
		{
			values[i] = next(random);
		}

		return values;
	}

	/// <summary>Checks that a code shows each of its numbers, in the room it is given.</summary>
	/// <param name="code">The code.</param>
	/// <param name="values">The numbers.</param>
	/// <exception cref="InvalidOperationException">The code doesn't show a number.</exception>
	private static void Check(FormatCode code, double[] values)
	{
		Span<byte> buffer = stackalloc byte[Room];
		foreach (var value in values)
		{
			var text = new ShownText(buffer);
			if (!NumberText.TryWrite(code, value, DisplayCulture.Invariant, false, ref text) || !text.Fits)
			{
				throw new InvalidOperationException($"{code.Code} doesn't show {value:R}.");
			}
		}
	}
}
