using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using nietras.SeparatedValues;

namespace CsvHelper.Benchmarks;

/// <summary>A record whose dates, times and durations are in exact formats.</summary>
[CsvRecord]
public sealed partial class FormattedDate
{
	/// <summary>A day, <c>yyyyMMdd</c>.</summary>
	[CsvFormat("yyyyMMdd")]
	public DateTime Day { get; set; }

	/// <summary>A time with an offset, <c>yyyyMMddHHmmzzz</c>.</summary>
	[CsvFormat("yyyyMMddHHmmzzz")]
	public DateTimeOffset At { get; set; }

	/// <summary>A due date, <c>dd.MM.yyyy</c>.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateOnly Due { get; set; }

	/// <summary>A time of day, <c>HH.mm</c>.</summary>
	[CsvFormat("HH.mm")]
	public TimeOnly Time { get; set; }

	/// <summary>A duration, <c>hh:mm:ss</c>.</summary>
	[CsvFormat("hh\\:mm\\:ss")]
	public TimeSpan Took { get; set; }

	/// <summary>Rows of the five columns, from a fixed seed, with a header.</summary>
	/// <param name="rows">The number of rows.</param>
	/// <returns>The CSV text.</returns>
	public static string Generate(int rows)
	{
		var random = new Random(42);
		var text = new StringBuilder("Day,At,Due,Time,Took\n");
		var start = new DateTime(2000, 1, 1);
		for (var i = 0; i < rows; i++)
		{
			var day = start.AddDays(random.Next(0, 9000));
			var at = new DateTimeOffset(day.AddMinutes(random.Next(0, 1440)), TimeSpan.FromHours(random.Next(-11, 13)));
			var took = TimeSpan.FromSeconds(random.Next(0, 86400));
			text.Append(day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append(',')
				.Append(at.ToString("yyyyMMddHHmmzzz", CultureInfo.InvariantCulture)).Append(',')
				.Append(day.AddDays(30).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append(',')
				.Append(at.ToString("HH.mm", CultureInfo.InvariantCulture)).Append(',')
				.Append(took.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture)).Append('\n');
		}

		return text.ToString();
	}
}

/// <summary>
/// Dates, times and durations in exact formats, which the other benchmarks don't read: Records = every member through
/// the generated mapper's <c>[CsvFormat]</c> parsing; ParseExact = the reader's <c>ParseExact&lt;T&gt;</c> for each date
/// column, with Sep parsing the same columns as a control.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FormattedDatesRead : ReadBenchmarkBase
{
	private const int Rows = 200_000;
	private static readonly string[] DueFormats = ["yyyy-MM-dd", "dd.MM.yyyy"];

	protected override string Generate() => FormattedDate.Generate(Rows);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public long CsvHelper_Records()
	{
		using var reader = CsvReader.Create(NewStream());
		var sum = 0L;
		foreach (var record in reader.GetRecords<FormattedDate>())
		{
			sum += record.Day.Ticks + record.At.Ticks + record.Due.DayNumber + record.Time.Ticks + record.Took.Ticks;
		}

		return sum;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("ParseExact")]
	public long CsvHelper_ParseExact()
	{
		using var reader = CsvReader.Create(NewStream());
		var sum = 0L;
		while (reader.Read())
		{
			sum += reader.ParseExact<DateTime>(0, "yyyyMMdd").Ticks + reader.ParseExact<DateTimeOffset>(1, "yyyyMMddHHmmzzz").Ticks +
				   reader.ParseExact<DateTime>(2, DueFormats).Ticks;
		}

		return sum;
	}

	[Benchmark]
	[BenchmarkCategory("ParseExact")]
	public long Sep_ParseExact()
	{
		using var reader = Sep.Reader().From(NewStream());
		var sum = 0L;
		foreach (var row in reader)
		{
			sum += DateTime.ParseExact(row[0].Span, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).Ticks +
				   DateTimeOffset.ParseExact(row[1].Span, "yyyyMMddHHmmzzz", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).Ticks +
				   DateTime.ParseExact(row[2].Span, DueFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).Ticks;
		}

		return sum;
	}
}
