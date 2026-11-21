using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// What <see cref="ExcelConvert.ToExcel(Stream, Stream, CsvToExcelOptions)"/>'s type inference costs a field:
/// <see cref="Count"/> CSV fields of a kind, each matched against the types, as each field of the first 1,000 records
/// is to decide its column's type (<c>ColumnTypes.Matches</c>), or put into the form the sink reads for its column's
/// type, as each field of a typed column is (<c>ColumnTypes.Format</c>), in the CSV reader's default culture and at the
/// workbook's default options. The time is per field (<see cref="BenchmarkAttribute.OperationsPerInvoke"/>).
/// </summary>
/// <remarks>
/// <para>
/// The kinds, each like a column of the orders that <c>ExcelOrdersToExcel</c> converts (<see cref="OrderRecord"/>),
/// from a fixed seed, in the text CsvHelper writes for them:
/// <list type="bullet">
/// <item><description>Number: short numbers, amounts to the cent below 100,000 (<c>Total</c>'s, <c>52341.27</c>), of at most 15 significant digits.</description></item>
/// <item><description>
/// Double: doubles below 50 in their shortest round-trip text (<c>Weight</c>'s, <c>4.505630514819934</c>), each of 16
/// or 17 significant digits, so that each is checked to be its double's own text (<c>IsDoubleText</c>), the largest
/// part of the orders' inference (Phase 9's decision 40).
/// </description></item>
/// <item><description>DateTime: dates and times from 2020 to 2024 to the millisecond, in ISO 8601 as .NET's <c>"O"</c> format writes them (<c>OrderDate</c>'s, <c>2021-03-04T12:34:56.7890000</c>).</description></item>
/// <item><description>Bool: <c>True</c> and <c>False</c> (<c>Paid</c>'s), three in four true.</description></item>
/// </list>
/// </para>
/// <para>
/// <see cref="Setup"/> checks that each field matches its kind's type and has a form, handed over as the type its kind
/// expects (a double's for the doubles, a decimal's for the short numbers), so that each call takes the path its name
/// says.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
public class ExcelColumnTypes
{
	/// <summary>The fields of each kind: a call matches or forms them all.</summary>
	public const int Count = 1024;

	/// <summary>The most significant digits of a number that isn't checked to be its double's own text.</summary>
	private const int MaxShortDigits = 15;

	/// <summary>The fields' types, as the conversion makes them at its defaults.</summary>
	private readonly ColumnTypes types = new(CsvReaderOptions.Default.FormatProvider, ExcelWriterOptions.Default);

	/// <summary>The short numbers, as UTF-8.</summary>
	private byte[][] numbers = [];

	/// <summary>The doubles of 16 or 17 significant digits, as UTF-8.</summary>
	private byte[][] doubles = [];

	/// <summary>The dates and times, as UTF-8.</summary>
	private byte[][] dateTimes = [];

	/// <summary>The booleans, as UTF-8.</summary>
	private byte[][] bools = [];

	/// <summary>
	/// Makes each kind's fields, from a fixed seed, and checks that each field matches its kind's type and has a form,
	/// handed over as the type its kind expects.
	/// </summary>
	/// <exception cref="InvalidOperationException">A field doesn't match its kind's type, or has no form, or is handed over as another type.</exception>
	[GlobalSetup]
	public void Setup()
	{
		var random = new Random(20261120);
		var start = new DateTime(2020, 1, 1);

		numbers = Fill(random, r => (r.Next(100, 10_000_000) / 100m).ToString(CultureInfo.InvariantCulture));
		doubles = Fill(random, NextLongDouble);
		dateTimes = Fill(random, r => start.AddSeconds(r.Next(0, 5 * 365 * 24 * 3600)).AddMilliseconds(r.Next(1000)).ToString("O", CultureInfo.InvariantCulture));
		bools = Fill(random, r => r.Next(4) != 0 ? "True" : "False");

		Check(numbers, ColumnType.Number, typeof(decimal));
		Check(doubles, ColumnType.Number, typeof(double));
		Check(dateTimes, ColumnType.DateTime, typeof(DateTime));
		Check(bools, ColumnType.Bool, typeof(bool));
	}

	/// <summary>Short numbers matched against the types.</summary>
	/// <returns>How many match a type: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Number")]
	public long Matches_Number() => Match(numbers);

	/// <summary>Short numbers put into a number column's form: their invariant text.</summary>
	/// <returns>How many have a form: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Number")]
	public long Format_Number() => Form(numbers, ColumnType.Number);

	/// <summary>Doubles of 16 or 17 significant digits matched against the types, each checked to be its double's own text.</summary>
	/// <returns>How many match a type: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Double")]
	public long Matches_Double() => Match(doubles);

	/// <summary>
	/// Doubles of 16 or 17 significant digits put into a number column's form, each checked to be its double's own text,
	/// and so handed over as a double's.
	/// </summary>
	/// <returns>How many have a form: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Double")]
	public long Format_Double() => Form(doubles, ColumnType.Number);

	/// <summary>Dates and times matched against the types.</summary>
	/// <returns>How many match a type: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("DateTime")]
	public long Matches_DateTime() => Match(dateTimes);

	/// <summary>Dates and times put into a date and time column's form: a date and time's <c>"O"</c> text.</summary>
	/// <returns>How many have a form: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("DateTime")]
	public long Format_DateTime() => Form(dateTimes, ColumnType.DateTime);

	/// <summary>Booleans matched against the types.</summary>
	/// <returns>How many match a type: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Bool")]
	public long Matches_Bool() => Match(bools);

	/// <summary>Booleans put into a boolean column's form: <c>True</c> or <c>False</c>.</summary>
	/// <returns>How many have a form: all of them.</returns>
	[Benchmark(OperationsPerInvoke = Count)]
	[BenchmarkCategory("Bool")]
	public long Format_Bool() => Form(bools, ColumnType.Bool);

	/// <summary>Makes <see cref="Count"/> fields.</summary>
	/// <param name="random">The fields' source.</param>
	/// <param name="next">Makes a field's text.</param>
	/// <returns>The fields, as UTF-8.</returns>
	private static byte[][] Fill(Random random, Func<Random, string> next)
	{
		var fields = new byte[Count][];
		for (var i = 0; i < Count; i++)
		{
			fields[i] = Encoding.UTF8.GetBytes(next(random));
		}

		return fields;
	}

	/// <summary>
	/// Makes a double below 50, as the orders' <c>Weight</c>, in its shortest round-trip text, drawing again until that
	/// has more than 15 significant digits.
	/// </summary>
	/// <param name="random">The doubles' source.</param>
	/// <returns>The text.</returns>
	private static string NextLongDouble(Random random)
	{
		while (true)
		{
			var text = (random.NextDouble() * 50).ToString("R", CultureInfo.InvariantCulture);
			if (SignificantDigits(text) > MaxShortDigits)
			{
				return text;
			}
		}
	}

	/// <summary>Counts a number's significant digits, from the first that isn't 0 to the last that isn't, as the conversion counts them.</summary>
	/// <param name="text">The number's invariant text.</param>
	/// <returns>The count.</returns>
	private static int SignificantDigits(string text)
	{
		var mantissa = text.Split('E')[0].Replace("-", "").Replace(".", "");

		return mantissa.Trim('0').Length;
	}

	/// <summary>Matches fields against the types, as the conversion does to decide a column's type.</summary>
	/// <param name="fields">The fields.</param>
	/// <returns>How many match a type.</returns>
	private long Match(byte[][] fields)
	{
		var count = 0L;
		foreach (var field in fields)
		{
			if (types.Matches(field) != 0)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>Puts fields into the form the sink reads for a column's type, as the conversion does for each field.</summary>
	/// <param name="fields">The fields.</param>
	/// <param name="type">The column's type.</param>
	/// <returns>How many have a form.</returns>
	private long Form(byte[][] fields, ColumnType type)
	{
		Span<byte> room = stackalloc byte[ColumnTypes.MaxFormLength];
		var count = 0L;
		foreach (var field in fields)
		{
			if (types.Format(field, type, room, out _) >= 0)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Checks that each field matches a type, as the narrowest of the types it matches, and has a form for it, handed over
	/// as a type.
	/// </summary>
	/// <param name="fields">The fields.</param>
	/// <param name="type">The column's type they should match.</param>
	/// <param name="cellType">The type their forms should be handed over as.</param>
	/// <exception cref="InvalidOperationException">A field doesn't match the type, or has no form, or is handed over as another type.</exception>
	private void Check(byte[][] fields, ColumnType type, Type cellType)
	{
		Span<byte> room = stackalloc byte[ColumnTypes.MaxFormLength];
		foreach (var field in fields)
		{
			var text = Encoding.UTF8.GetString(field);
			if (ColumnTypes.Narrowest(types.Matches(field)) != type)
			{
				throw new InvalidOperationException($"{text} doesn't match {type}.");
			}

			if (types.Format(field, type, room, out var handed) < 0 || handed != cellType)
			{
				throw new InvalidOperationException($"{text} has no {type} form handed over as a {cellType.Name}.");
			}
		}
	}
}
