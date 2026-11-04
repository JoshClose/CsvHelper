using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper.Linq;

namespace CsvHelper.Tests;

/// <summary>A record whose dates, times and durations have formats, which apply to text cells only.</summary>
[CsvRecord]
public partial class FormattedCellRecord
{
	/// <summary>A date cell, or text in the format.</summary>
	[CsvFormat("yyyyMMdd")]
	public DateTime When { get; set; }

	/// <summary>A duration cell, or text in the format.</summary>
	[CsvFormat("hh\\:mm")]
	public TimeSpan Took { get; set; }

#if NET
	/// <summary>A date cell, or text in the format.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateOnly Day { get; set; }

	/// <summary>A date cell, or text in the format.</summary>
	[CsvFormat("HH.mm")]
	public TimeOnly Time { get; set; }
#endif
}

/// <summary>A record read from typed cells: each member from a cell of the kind its type converts.</summary>
[CsvRecord]
public partial class TypedCellRecord
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A date cell.</summary>
	public DateTime When { get; set; }

	/// <summary>A date cell, offset zero.</summary>
	public DateTimeOffset At { get; set; }

	/// <summary>A duration cell.</summary>
	public TimeSpan Took { get; set; }

#if NET
	/// <summary>A date cell's day.</summary>
	public DateOnly Day { get; set; }

	/// <summary>A date cell's time of day.</summary>
	public TimeOnly Time { get; set; }
#endif

	/// <summary>A number cell with 17 digits, rounded to 15.</summary>
	public decimal Total { get; set; }

	/// <summary>A number cell written as <c>42.0</c>.</summary>
	public int Count { get; set; }

	/// <summary>A number cell.</summary>
	public long Id { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>A date cell read as text.</summary>
	public string Text { get; set; } = "";

	/// <summary>An empty cell.</summary>
	public int? Missing { get; set; }
}

// Typed reading's conversions (docs/plans/excel.md, Phase 2, "What a cell reads as"), through a record source whose
// cells have kinds: every target and kind, through every accessor.
public class CellConversionTests
{
	private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

	private static readonly CsvCellOptions Date1904 = new() { Date1904 = true };

	private static (string, CsvCellKind) Text(string text) => (text, CsvCellKind.Text);

	private static (string, CsvCellKind) Number(string text) => (text, CsvCellKind.Number);

	private static (string, CsvCellKind) Date(string text) => (text, CsvCellKind.DateNumber);

	private static (string, CsvCellKind) Duration(string text) => (text, CsvCellKind.Duration);

	private static (string, CsvCellKind) Bool(string text) => (text, CsvCellKind.Bool);

	private static (string, CsvCellKind) Error(string text) => (text, CsvCellKind.Error);

	private static (string, CsvCellKind) Empty() => ("", CsvCellKind.Empty);

	/// <summary>A reader on a record of one cell, of a source without a header.</summary>
	private static CsvReader One((string Text, CsvCellKind Kind) cell, CsvCellOptions? cells = null, CsvReaderOptions? options = null)
	{
		var reader = Rows(cells, options ?? new CsvReaderOptions { HasHeader = false, FormatProvider = German }, new FakeRow(1, cell));
		Assert.True(reader.Read());

		return reader;
	}

	/// <summary>A reader over rows of a source called Data.</summary>
	private static CsvReader Rows(CsvCellOptions? cells, CsvReaderOptions? options, params FakeRow[] rows) =>
		new(new FakeRecordSource("Data", rows) { Cells = cells ?? CsvCellOptions.Default }, options ?? new CsvReaderOptions { HasHeader = false });

	private static T Parse<T>((string, CsvCellKind) cell, CsvCellOptions? cells = null)
#if NET
		where T : ISpanParsable<T>
#endif
	{
		using var reader = One(cell, cells);

		return reader.Parse<T>(0);
	}

	private static string GetString((string, CsvCellKind) cell, CsvCellOptions? cells = null)
	{
		using var reader = One(cell, cells);

		return reader.GetString(0);
	}

	private static TException Fails<T, TException>((string, CsvCellKind) cell, CsvCellOptions? cells = null)
#if NET
		where T : ISpanParsable<T>
#endif
		where TException : Exception
	{
		using var reader = One(cell, cells);
		Assert.False(reader.TryParse<T>(0, out _));

		return Assert.Throws<TException>(() => reader.Parse<T>(0));
	}

	[Fact]
	public void NumberCellsConvertForEachTarget()
	{
		Assert.Equal("1.5", GetString(Number("1.5")));
		Assert.Equal("0.30000000000000004", GetString(Number("0.30000000000000004")));
		Assert.Equal("42.0", GetString(Number("42.0")));
		Assert.Equal(1.5, Parse<double>(Number("1.5")));
		Assert.Equal(0.30000000000000004, Parse<double>(Number("0.30000000000000004")));
		Assert.Equal(1.5f, Parse<float>(Number("1.5")));

		// decimal and integer targets round to 15 significant digits, as Excel shows numbers; null reads them exactly.
		Assert.Equal(0.3m, Parse<decimal>(Number("0.30000000000000004")));
		Assert.Equal(0.30000000000000004m, Parse<decimal>(Number("0.30000000000000004"), new CsvCellOptions { SignificantDigits = null }));
		Assert.Equal(1234.5m, Parse<decimal>(Number("1234.5")));
		Assert.Equal(42, Parse<int>(Number("42")));
		Assert.Equal(42, Parse<int>(Number("42.0")));
		Assert.Equal(1000, Parse<int>(Number("1E3")));
		Assert.Equal(3, Parse<int>(Number("3.0000000000000004")));
		Assert.Equal(-7, Parse<int>(Number("-7")));
		Assert.Equal(int.MinValue, Parse<int>(Number("-2147483648")));
		Assert.Equal(12345678901234600L, Parse<long>(Number("12345678901234567")));
		Assert.Equal(12345678901234567L, Parse<long>(Number("12345678901234567"), new CsvCellOptions { SignificantDigits = null }));
		Assert.Equal((short)-300, Parse<short>(Number("-300")));
		Assert.Equal((byte)255, Parse<byte>(Number("255")));
		Assert.Equal((sbyte)-128, Parse<sbyte>(Number("-128")));
		Assert.Equal((ushort)65535, Parse<ushort>(Number("65535")));
		Assert.Equal(4294967295u, Parse<uint>(Number("4294967295")));
		Assert.Equal(0ul, Parse<ulong>(Number("-0")));
		Assert.Equal(ulong.MaxValue, Parse<ulong>(Number("18446744073709551615"), new CsvCellOptions { SignificantDigits = null }));
		Assert.Equal("'1.5' is not a whole number, so it is not a valid Int32.", Fails<int, FormatException>(Number("1.5")).Message);
		Assert.Equal("'2147483648' is outside the range of Int32.", Fails<int, OverflowException>(Number("2147483648")).Message);
		Fails<byte, OverflowException>(Number("256"));
		Fails<byte, OverflowException>(Number("-1"));
		Fails<ulong, OverflowException>(Number("-1"));
		// 18446744073709551615 at 15 digits is 18446744073709600000, past ulong's range.
		Fails<ulong, OverflowException>(Number("18446744073709551615"));
		Fails<long, OverflowException>(Number("1E+19"));

		// 1 is true and 0 false; other numbers aren't booleans.
		Assert.True(Parse<bool>(Number("1")));
		Assert.True(Parse<bool>(Number("1.0")));
		Assert.False(Parse<bool>(Number("0")));
		Assert.False(Parse<bool>(Number("-0")));
		Assert.Equal("'2' is not a valid boolean: a number cell is true when it is 1 and false when it is 0.", Fails<bool, FormatException>(Number("2")).Message);

		// A number in a date target is a serial: the column holds dates.
		Assert.Equal(new DateTime(2024, 1, 15, 12, 0, 0), Parse<DateTime>(Number("45306.5")));
		Assert.Equal(DateTimeKind.Unspecified, Parse<DateTime>(Number("45306.5")).Kind);
		Assert.Equal(new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), Parse<DateTimeOffset>(Number("45306.5")));
		Assert.Equal(TimeSpan.FromHours(36), Parse<TimeSpan>(Number("1.5")));
#if NET
		Assert.Equal(new DateOnly(2024, 1, 15), Parse<DateOnly>(Number("45306.5")));
		Assert.Equal(new TimeOnly(12, 0), Parse<TimeOnly>(Number("45306.5")));
#endif

		// Anything else parses the stored text with the invariant culture, as in Phase 1.
		Assert.Equal(DayOfWeek.Tuesday, ParseEnum(Number("2")));
	}

	private static DayOfWeek ParseEnum((string, CsvCellKind) cell)
	{
		using var reader = One(cell);

		return reader.Parse<DayOfWeek>(0);
	}

	[Fact]
	public void DateCellsConvertAndReadAsIso8601()
	{
		// The text comes from the value: a day, a time alone (a serial below 1), or both, with milliseconds when there are any.
		Assert.Equal("2024-01-15", GetString(Date("45306")));
		Assert.Equal("2024-01-15T12:00:00", GetString(Date("45306.5")));
		Assert.Equal("2024-01-15T06:00:00", GetString(Date("45306.25")));
		Assert.Equal("12:00:00", GetString(Date("0.5")));
		Assert.Equal("00:00:00", GetString(Date("0")));
		Assert.Equal("2024-01-15T00:00:00.25", GetString(Date(Serial(45306, 250))));
		Assert.Equal("2024-01-15T00:00:00.007", GetString(Date(Serial(45306, 7))));
		Assert.Equal("2024-01-15T23:59:59.999", GetString(Date(Serial(45306, 86_399_999))));
		// Rounded to the millisecond, as Excel shows it: 0.999999999 is the next day.
		Assert.Equal("1900-01-01", GetString(Date("0.999999999")));

		// Excel's 1900 date system counts a 1900-02-29 that never existed.
		Assert.Equal("1900-01-01", GetString(Date("1")));
		Assert.Equal("1900-02-28", GetString(Date("59")));
		Assert.Equal("1900-02-29", GetString(Date("60")));
		Assert.Equal("1900-02-29T12:00:00", GetString(Date("60.5")));
		Assert.Equal("1900-03-01", GetString(Date("61")));
		Assert.Equal(new DateTime(1899, 12, 31, 12, 0, 0), Parse<DateTime>(Date("0.5")));
		Assert.Equal(new DateTime(1900, 1, 1), Parse<DateTime>(Date("1")));
		Assert.Equal(new DateTime(1900, 2, 28), Parse<DateTime>(Date("59")));
		Assert.Equal(new DateTime(1900, 3, 1), Parse<DateTime>(Date("61")));
		Assert.Equal(
			"'60' is 1900-02-29 in Excel's 1900 date system, a day that doesn't exist, so it is not a valid DateTime.",
			Fails<DateTime, FormatException>(Date("60")).Message);
		Fails<DateTimeOffset, FormatException>(Date("60.5"));
#if NET
		Fails<DateOnly, FormatException>(Date("60"));
		// Its time of day is a time all the same.
		Assert.Equal(new TimeOnly(12, 0), Parse<TimeOnly>(Date("60.5")));
#endif

		// The limits: 0 to 9999-12-31 23:59:59.999. Outside them, the text is the stored text.
		Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, 999), Parse<DateTime>(Date("2958465.99999999")));
		Assert.Equal("9999-12-31T23:59:59.999", GetString(Date("2958465.99999999")));
		Assert.Equal("2958466", GetString(Date("2958466")));
		Assert.Equal("-1", GetString(Date("-1")));
		Assert.Equal(
			"'-1' is not a valid DateTime: Excel's dates are serials from 0 to 2958465.99999999 (9999-12-31).",
			Fails<DateTime, FormatException>(Date("-1")).Message);
		Fails<DateTime, FormatException>(Date("2958466"));
		Fails<DateTime, FormatException>(Date("1E+300"));
		Fails<DateTime, FormatException>(Date("NaN"));
#if NET
		Fails<TimeOnly, FormatException>(Date("-0.5"));
#endif

		// Numeric targets get the serial: rounded for decimal and integers, which must be whole.
		Assert.Equal(45306.5, Parse<double>(Date("45306.5")));
		Assert.Equal(45306.5m, Parse<decimal>(Date("45306.5")));
		Assert.Equal(45306, Parse<int>(Date("45306")));
		Fails<int, FormatException>(Date("45306.5"));
		Assert.Equal("The cell is a date, so it is not a valid Boolean.", Fails<bool, FormatException>(Date("1")).Message);
	}

	[Fact]
	public void The1904DateSystemStartsIn1904()
	{
		Assert.Equal(new DateTime(1904, 1, 1), Parse<DateTime>(Date("0"), Date1904));
		Assert.Equal(new DateTime(1904, 1, 2), Parse<DateTime>(Date("1"), Date1904));
		Assert.Equal(new DateTime(1904, 3, 1), Parse<DateTime>(Date("60"), Date1904));
		Assert.Equal(new DateTime(2024, 1, 15), Parse<DateTime>(Date("43844"), Date1904));
		Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, 999), Parse<DateTime>(Date("2957003.99999999"), Date1904));
		Assert.Equal("1904-01-02", GetString(Date("1"), Date1904));
		Assert.Equal("1904-02-29", GetString(Date("59"), Date1904));
		Assert.Equal("12:00:00", GetString(Date("0.5"), Date1904));
		Assert.Equal("2957004", GetString(Date("2957004"), Date1904));
		Assert.Equal(
			"'-1' is not a valid DateTime: Excel's dates are serials from 0 to 2957003.99999999 (9999-12-31) in the 1904 date system.",
			Fails<DateTime, FormatException>(Date("-1"), Date1904).Message);
		Fails<DateTime, FormatException>(Date("2957004"), Date1904);
	}

	[Fact]
	public void DurationCellsConvertAndReadAsElapsedTime()
	{
		Assert.Equal("36:00:00", GetString(Duration("1.5")));
		Assert.Equal("00:00:00", GetString(Duration("0")));
		Assert.Equal("-01:30:00", GetString(Duration("-0.0625")));
		Assert.Equal("00:00:01", GetString(Duration(Serial(0, 1000))));
		Assert.Equal("00:00:00.5", GetString(Duration(Serial(0, 500))));
		Assert.Equal("24000:00:00", GetString(Duration("1000")));
		Assert.Equal("1E+20", GetString(Duration("1E+20")));
		Assert.Equal(TimeSpan.FromHours(36), Parse<TimeSpan>(Duration("1.5")));
		Assert.Equal(TimeSpan.FromMinutes(-90), Parse<TimeSpan>(Duration("-0.0625")));
		Assert.Equal("'1E+20' is outside the range of TimeSpan.", Fails<TimeSpan, OverflowException>(Duration("1E+20")).Message);
		// Date targets read it as the serial it is.
		Assert.Equal(new DateTime(1900, 1, 1, 12, 0, 0), Parse<DateTime>(Duration("1.5")));
		Assert.Equal(1.5m, Parse<decimal>(Duration("1.5")));
		Assert.Equal("The cell is a duration, so it is not a valid Boolean.", Fails<bool, FormatException>(Duration("1")).Message);
	}

	[Fact]
	public void BooleanCellsConvertAndReadAsTrueOrFalse()
	{
		Assert.Equal("TRUE", GetString(Bool("1")));
		Assert.Equal("FALSE", GetString(Bool("0")));
		Assert.True(Parse<bool>(Bool("1")));
		Assert.False(Parse<bool>(Bool("0")));
		Assert.Equal(1, Parse<int>(Bool("1")));
		Assert.Equal(0m, Parse<decimal>(Bool("0")));
		Assert.Equal(1.0, Parse<double>(Bool("1")));
		Assert.Equal("The cell is a boolean, so it is not a valid DateTime.", Fails<DateTime, FormatException>(Bool("1")).Message);
		Fails<TimeSpan, FormatException>(Bool("0"));

		// A boolean cell is 1 or 0, whatever the reader's true and false values.
		using var reader = One(Bool("1"), options: new CsvReaderOptions { HasHeader = false, TrueValues = ["Ja"], FalseValues = ["Nein"] });
		Assert.True(reader.Parse<bool>(0));
	}

	[Fact]
	public void ErrorCellsReadAsTheirTextAndFailForOtherTargets()
	{
		using (var reader = One(Error("#N/A")))
		{
			Assert.Equal("#N/A", reader.GetString(0));
			Assert.Equal("#N/A", reader.Parse<string>(0));
			Assert.Equal("The cell holds the error #N/A, so it is not a valid Int32.", Assert.Throws<FormatException>(() => reader.Parse<int>(0)).Message);
			Assert.Equal("The cell holds the error #N/A, so it is not a valid Double.", Assert.Throws<FormatException>(() => reader.Parse<double>(0)).Message);
			Assert.Throws<FormatException>(() => reader.Parse<DateTime>(0));
			Assert.Throws<FormatException>(() => reader.Parse<Guid>(0));
			Assert.Throws<FormatException>(() => reader.Parse<bool>(0));
			Assert.Throws<FormatException>(() => reader.ParseExact<DateTime>(0, "yyyy"));
			Assert.False(reader.TryParse<double>(0, out _));
			Assert.Null(reader[0].ParseOrNull<int>());
		}

		// Null values can name them, as for text.
		using (var reader = One(Error("#N/A"), options: new CsvReaderOptions { HasHeader = false, NullValues = ["#N/A"] }))
		{
			Assert.Null(reader.GetStringOrNull(0));
			Assert.True(reader.IsNull(0));
		}

		// Values left out of messages.
		using (var reader = One(Error("#N/A"), options: new CsvReaderOptions { HasHeader = false, IncludeValuesInExceptions = false }))
		{
			var e = Assert.Throws<FormatException>(() => reader.Parse<int>(0));
			Assert.Equal("The cell holds an error, so it is not a valid Int32 (IncludeValuesInExceptions is off, so its value is not shown).", e.Message);
		}
	}

	[Fact]
	public void ErrorCellsCanFailEveryRead()
	{
		var cells = new CsvCellOptions { ThrowOnErrorCells = true };
		var rows = new[]
		{
			new FakeRow(1, Text("Name"), Text("Quantity")),
			new FakeRow(2, Text("a"), Number("1")),
			new FakeRow(3, Text("b"), Error("#DIV/0!")),
		};
		using (var reader = Rows(cells, new CsvReaderOptions(), rows))
		{
			Assert.True(reader.Read());
			Assert.True(reader.Read());
			// Strings too, naming the cell; the stored form is still there.
			Assert.Equal("Data!B3 holds the error #DIV/0!.", Assert.Throws<FormatException>(() => reader.GetString(1)).Message);
			Assert.Throws<FormatException>(() => reader.GetStringOrNull(1));
			Assert.Throws<FormatException>(() => reader.Parse<string>(1));
			Assert.Throws<FormatException>(() => reader[1].ToString());
			Assert.Throws<FormatException>(() => reader.Parse<int>(1));
			Assert.Equal("#DIV/0!"u8.ToArray(), reader.GetSpan(1).ToArray());
			Assert.Equal("b", reader.GetString(0));
		}

		// Records: the cell goes to OnRecordError, and a dynamic record's value fails when it is read.
		var errors = new List<CsvRecordError>();
		var options = new CsvReaderOptions
		{
			OnRecordError = error =>
			{
				errors.Add(error);

				return true;
			},
		};
		using (var reader = Rows(cells, options, rows))
		{
			Assert.Equal(["a"], reader.GetRecords<CellQuantity>().Select(r => r.Name));
		}

		Assert.Equal("Data!B3", Assert.Single(errors).Location);
		using (var reader = Rows(cells, new CsvReaderOptions(), rows))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().ToList();
			Assert.Equal("1", records[0]["Quantity"]);
			Assert.Equal("The cell holds the error #DIV/0!.", Assert.Throws<FormatException>(() => records[1]["Quantity"]).Message);
			Assert.Throws<FormatException>(() => records[1].Parse<string>("Quantity"));
		}

		using (var reader = Rows(cells, new CsvReaderOptions(), rows))
		using (var data = new CsvDataReader(reader))
		{
			Assert.True(data.Read());
			Assert.True(data.Read());
			Assert.Throws<FormatException>(() => data.GetValue(1));
		}

		// A header cell is a name, not a value: an error there names its column with its text.
		using (var reader = Rows(cells, new CsvReaderOptions(), new FakeRow(1, Text("Name"), Error("#REF!")), new FakeRow(2, Text("a"), Number("1"))))
		{
			Assert.True(reader.Read());
			Assert.Equal(["Name", "#REF!"], reader.Headers);
			Assert.Equal("1", reader.GetString("#REF!"));
		}
	}

	[Fact]
	public void TextCellsReadAsCsvTextDoes()
	{
		using var reader = Rows(null, new CsvReaderOptions { HasHeader = false, FormatProvider = German, TrueValues = ["Ja"], FalseValues = ["Nein"] },
			new FakeRow(1, Text("1,5"), Text("15.01.2024"), Text("Ja"), Text("20240115"), Empty()));
		Assert.True(reader.Read());
		Assert.Equal(1.5, reader.Parse<double>(0));
		Assert.Equal(1.5m, reader.Parse<decimal>(0));
		Assert.Equal(new DateTime(2024, 1, 15), reader.Parse<DateTime>(1));
		Assert.True(reader.Parse<bool>(2));
		Assert.Equal(new DateTime(2024, 1, 15), reader.ParseExact<DateTime>(3, "yyyyMMdd"));
		Assert.Equal("", reader.GetString(4));
		Assert.Null(reader[4].ParseOrNull<int>());
		Assert.Throws<FormatException>(() => reader.Parse<int>(4));
	}

	[Fact]
	public void ParseExactConvertsNumberAndDateCellsWithoutTheFormat()
	{
		using var reader = Rows(null, null, new FakeRow(1, Date("45306.5"), Number("45306"), Text("20240115"), Bool("1"), Duration("1.5")));
		Assert.True(reader.Read());
		var noon = new DateTime(2024, 1, 15, 12, 0, 0);
		Assert.Equal(noon, reader.ParseExact<DateTime>(0, "yyyyMMdd"));
		Assert.Equal(noon, reader.ParseExact<DateTime>(0, "yyyyMMdd", German));
		Assert.Equal(noon, reader.ParseExact<DateTime>(0, ["yyyyMMdd", "dd.MM.yyyy"]));
		Assert.Equal(noon, reader.ParseExact<DateTime>(0, ["yyyyMMdd"], German));
		Assert.Equal(new DateTimeOffset(noon, TimeSpan.Zero), reader.ParseExact<DateTimeOffset>(0, "yyyyMMdd"));
		Assert.Equal(new DateTime(2024, 1, 15), reader.ParseExact<DateTime>(1, "yyyyMMdd"));
		Assert.Equal(new DateTime(2024, 1, 15), reader.ParseExact<DateTime>(2, "yyyyMMdd"));
		Assert.Equal(new DateTime(2024, 1, 15), reader.ParseExact<DateTime>(2, ["dd.MM.yyyy", "yyyyMMdd"], German));
		Assert.Equal(new DateTime(1900, 1, 1, 12, 0, 0), reader.ParseExact<DateTime>(4, "yyyyMMdd"));
		Assert.Throws<FormatException>(() => reader.ParseExact<DateTime>(3, "yyyyMMdd"));
		Assert.Throws<NotSupportedException>(() => reader.ParseExact<int>(1, "0"));
	}

	[Fact]
	public void ParseExactReadsDateCellsForTimesAndDurations()
	{
		using var reader = Rows(null, null, new FakeRow(1, Date("45306.5"), Duration("0.0625"), Text("01:30"), Text("15.01.2024"), Bool("1")));
		Assert.True(reader.Read());
		Assert.Equal(new TimeSpan(45306, 12, 0, 0), reader.ParseExact<TimeSpan>(0, "hh\\:mm"));
		Assert.Equal(TimeSpan.FromMinutes(90), reader.ParseExact<TimeSpan>(1, "hh\\:mm"));
		Assert.Equal(TimeSpan.FromMinutes(90), reader.ParseExact<TimeSpan>(1, ["c"], German));
		Assert.Equal(TimeSpan.FromMinutes(90), reader.ParseExact<TimeSpan>(2, "hh\\:mm"));
		Assert.Throws<FormatException>(() => reader.ParseExact<TimeSpan>(4, "hh\\:mm"));
#if NET
		Assert.Equal(new DateOnly(2024, 1, 15), reader.ParseExact<DateOnly>(0, "dd.MM.yyyy"));
		Assert.Equal(new DateOnly(2024, 1, 15), reader.ParseExact<DateOnly>(3, ["dd.MM.yyyy"], German));
		Assert.Equal(new TimeOnly(12, 0), reader.ParseExact<TimeOnly>(0, "HH.mm"));
		Assert.Equal(new TimeOnly(1, 30), reader.ParseExact<TimeOnly>(1, "HH.mm", German));
		Assert.Throws<FormatException>(() => reader.ParseExact<DateOnly>(4, "dd.MM.yyyy"));
#endif

		// Generated mappers: [CsvFormat] applies to the text cells of the second row.
		var rows = new[]
		{
#if NET
			new FakeRow(1, Text("When"), Text("Took"), Text("Day"), Text("Time")),
			new FakeRow(2, Date("45306.5"), Duration("0.0625"), Date("45306.5"), Date("45306.5")),
			new FakeRow(3, Text("20240115"), Text("01:30"), Text("15.01.2024"), Text("12.00")),
#else
			new FakeRow(1, Text("When"), Text("Took")),
			new FakeRow(2, Date("45306.5"), Duration("0.0625")),
			new FakeRow(3, Text("20240115"), Text("01:30")),
#endif
		};
		using var records = Rows(null, new CsvReaderOptions(), rows);
		var read = records.GetRecords<FormattedCellRecord>().ToList();
		Assert.Equal([new DateTime(2024, 1, 15, 12, 0, 0), new DateTime(2024, 1, 15)], read.Select(r => r.When));
		Assert.All(read, r => Assert.Equal(TimeSpan.FromMinutes(90), r.Took));
#if NET
		Assert.All(read, r => Assert.Equal(new DateOnly(2024, 1, 15), r.Day));
		Assert.All(read, r => Assert.Equal(new TimeOnly(12, 0), r.Time));
#endif
	}

	[Fact]
	public void FieldEqualsComparesTheText()
	{
		using (var csv = CsvReader.FromText("a,\"b\"\"c\",  d  ,é\n", new CsvReaderOptions { HasHeader = false, TrimWhitespace = true }))
		{
			Assert.True(csv.Read());
			Assert.True(csv.FieldEquals(0, "a"u8));
			Assert.False(csv.FieldEquals(0, "A"u8));
			Assert.True(csv.FieldEquals(1, "b\"c"u8));
			Assert.True(csv.FieldEquals(2, "d"u8));
			Assert.True(csv.FieldEquals(3, "é"u8));
			Assert.True(csv.FieldEquals(9, ""u8));
			Assert.Throws<ArgumentOutOfRangeException>(() => csv.FieldEquals(-1, ""u8));
		}

		using (var reader = Rows(null, null, new FakeRow(1, Date("45306.5"), Bool("1"), Number("1.5"), Text("é"), Error("#N/A"), Empty())))
		{
			Assert.True(reader.Read());
			Assert.True(reader.FieldEquals(0, "2024-01-15T12:00:00"u8));
			Assert.False(reader.FieldEquals(0, "45306.5"u8));
			Assert.False(reader.FieldEquals(0, "2024-01-15T12:00:0é"u8));
			Assert.True(reader.FieldEquals(1, "TRUE"u8));
			Assert.False(reader.FieldEquals(1, "1"u8));
			Assert.True(reader.FieldEquals(2, "1.5"u8));
			Assert.True(reader.FieldEquals(3, "é"u8));
			Assert.True(reader.FieldEquals(4, "#N/A"u8));
			Assert.True(reader.FieldEquals(5, ""u8));
		}

		using var strict = Rows(new CsvCellOptions { ThrowOnErrorCells = true }, null, new FakeRow(1, Error("#N/A")));
		Assert.True(strict.Read());
		Assert.Throws<FormatException>(() => strict.FieldEquals(0, "#N/A"u8));
	}

	[Fact]
	public void LinqComparesTheTextOfTypedCells()
	{
		var rows = new[]
		{
			new FakeRow(1, Text("Name"), Text("When"), Text("Paid")),
			new FakeRow(2, Text("a"), Date("45306"), Bool("1")),
			new FakeRow(3, Text("b"), Date("45307"), Bool("0")),
			new FakeRow(4, Text("c"), Date("45306"), Bool("0")),
		};
		using (var reader = Rows(null, new CsvReaderOptions(), rows))
		{
			var query = reader.Query().Where(r => r["Paid"] == "TRUE" || r["When"] == "2024-01-16").Select(r => r["Name"]);
			// The comparison runs on the reader's fields, not on rows.
			Assert.Equal(["Paid", "When"], Assert.Single(CsvQueryable.Explain(query).Sources).FilterMembers);
			Assert.Equal(["a", "b"], query.ToList());
		}

		using (var reader = Rows(null, new CsvReaderOptions(), rows))
		{
			Assert.Equal(["b", "c"], reader.Query().Where(r => "2024-01-15" != r["When"] || r["Paid"] != "TRUE").Select(r => r["Name"]).ToList());
		}
	}

	[Fact]
	public void TextAccessorsGiveTheTextAndByteAccessorsTheStoredForm()
	{
		using var reader = Rows(null, new CsvReaderOptions(),
			new FakeRow(1, Text("Name"), Date("45306"), Bool("1"), Duration("1.5")),
			new FakeRow(2, Text("x"), Date("45306.5"), Bool("0"), Duration("0.25")));

		// A date in the header names its column.
		Assert.Equal(["Name", "2024-01-15", "TRUE", "36:00:00"], reader.Headers);
		Assert.Equal(1, reader.GetFieldIndex("2024-01-15"));
		Assert.True(reader.Read());
		Assert.Equal("2024-01-15T12:00:00", reader.GetString(1));
		Assert.Equal("2024-01-15T12:00:00", reader.GetString("2024-01-15"));
		Assert.Equal("2024-01-15T12:00:00", reader.GetStringOrNull(1));
		Assert.Equal("2024-01-15T12:00:00", reader.Parse<string>(1));
		Assert.Equal("2024-01-15T12:00:00", reader[1].ToString());
		Assert.Equal("2024-01-15T12:00:00", reader[1].Parse<string>());
		Assert.Equal("2024-01-15T12:00:00", reader.Parse(1, (text, _) => text));
		Assert.True(reader.TryParse<string>(1, out var text) && text == "2024-01-15T12:00:00");
		Assert.Equal(["x", "2024-01-15T12:00:00", "FALSE", "06:00:00"], reader.Current.ToArray());
		Assert.Equal("FALSE", reader.Current.ToDictionary()["TRUE"]);
		var chars = new char[32];
		Assert.Equal("2024-01-15T12:00:00", new string(chars, 0, reader.GetChars(1, chars)));
		Assert.Equal("x", new string(chars, 0, reader.GetChars(0, chars)));
		Assert.Throws<ArgumentException>(() => reader.GetChars(1, new char[5]));

		// The stored form.
		Assert.Equal("45306.5"u8.ToArray(), reader.GetSpan(1).ToArray());
		Assert.Equal("45306.5"u8.ToArray(), reader.GetRawSpan(1).ToArray());
		Assert.Equal("45306.5"u8.ToArray(), reader[1].Span.ToArray());
		Assert.Equal("0"u8.ToArray(), reader.Current.GetSpan(2).ToArray());
		Assert.Equal("x,45306.5,0,0.25", Encoding.UTF8.GetString(reader.RawRecord.ToArray()));
	}

	[Fact]
	public void DynamicRecordsGiveTheTextAndConvert()
	{
		var rows = new[]
		{
			new FakeRow(1, Text("Name"), Text("When"), Text("Paid"), Text("Total"), Text("Took")),
			new FakeRow(2, Text("a"), Date("45306.5"), Bool("1"), Number("0.30000000000000004"), Duration("1.5")),
			new FakeRow(3, Text("b"), Date("1"), Bool("0"), Number("3"), Duration("0")),
		};
		List<CsvDynamicRecord> records;
		using (var reader = Rows(null, new CsvReaderOptions(), rows))
		{
			records = reader.GetRecords<CsvDynamicRecord>().ToList();
		}

		Assert.Equal("2024-01-15T12:00:00", records[0]["When"]);
		Assert.Equal("TRUE", records[0]["Paid"]);
		Assert.Equal("36:00:00", records[0]["Took"]);
		Assert.Equal("0.30000000000000004", records[0]["Total"]);
		Assert.Equal(new DateTime(2024, 1, 15, 12, 0, 0), records[0].Parse<DateTime>("When"));
		Assert.Equal("2024-01-15T12:00:00", records[0].Parse<string>("When"));
		Assert.Equal(0.3m, records[0].Parse<decimal>("Total"));
		Assert.Equal(TimeSpan.FromHours(36), records[0].Parse<TimeSpan>("Took"));
		Assert.True(records[0].Parse<bool>("Paid"));
		Assert.Equal(3, records[1].Parse<int>("Total"));
		Assert.Equal("Name: b, When: 1900-01-01, Paid: FALSE, Total: 3, Took: 00:00:00", records[1].ToString());
		Assert.Equal("1900-01-01", records[1].Detach()["When"]);
		Assert.Equal(new DateTime(1900, 1, 1), records[1].Detach().Parse<DateTime>("When"));
		Assert.Equal("TRUE", records[0].ToDictionary()["Paid"]);
		dynamic first = records[0];
		Assert.Equal("TRUE", (string)first.Paid);

		// Records of a source in the 1904 date system have their own schema.
		using (var reader = Rows(Date1904, new CsvReaderOptions(), rows))
		{
			Assert.Equal("1904-01-02", reader.GetRecords<CsvDynamicRecord>().Last()["When"]);
		}
	}

	[Fact]
	public async Task GeneratedMappersReadTypedCells()
	{
		var rows = new List<FakeRow>
		{
#if NET
			new(1, Text("Name"), Text("When"), Text("At"), Text("Took"), Text("Total"), Text("Count"), Text("Id"), Text("Paid"), Text("Text"), Text("Missing"), Text("Day"), Text("Time")),
#else
			new(1, Text("Name"), Text("When"), Text("At"), Text("Took"), Text("Total"), Text("Count"), Text("Id"), Text("Paid"), Text("Text"), Text("Missing")),
#endif
		};
		for (var i = 0; i < 1000; i++)
		{
			var serial = (45306 + i).ToString(CultureInfo.InvariantCulture) + ".5";
#if NET
			rows.Add(new FakeRow(i + 2, Text($"n{i}"), Date(serial), Date(serial), Duration("1.5"), Number("0.30000000000000004"), Number("42.0"), Number(i.ToString(CultureInfo.InvariantCulture)), Bool(i % 2 == 0 ? "1" : "0"), Date(serial), Empty(), Date(serial), Date(serial)));
#else
			rows.Add(new FakeRow(i + 2, Text($"n{i}"), Date(serial), Date(serial), Duration("1.5"), Number("0.30000000000000004"), Number("42.0"), Number(i.ToString(CultureInfo.InvariantCulture)), Bool(i % 2 == 0 ? "1" : "0"), Date(serial), Empty()));
#endif
		}

		void Check(IReadOnlyList<TypedCellRecord> records)
		{
			Assert.Equal(1000, records.Count);
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				var when = new DateTime(2024, 1, 15, 12, 0, 0).AddDays(i);
				Assert.Equal($"n{i}", r.Name);
				Assert.Equal(when, r.When);
				Assert.Equal(new DateTimeOffset(when, TimeSpan.Zero), r.At);
				Assert.Equal(TimeSpan.FromHours(36), r.Took);
				Assert.Equal(0.3m, r.Total);
				Assert.Equal(42, r.Count);
				Assert.Equal(i, r.Id);
				Assert.Equal(i % 2 == 0, r.Paid);
				Assert.Equal(when.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), r.Text);
				Assert.Null(r.Missing);
#if NET
				Assert.Equal(DateOnly.FromDateTime(when), r.Day);
				Assert.Equal(new TimeOnly(12, 0), r.Time);
#endif
			}
		}

		using (var reader = new CsvReader(new FakeRecordSource("Data", rows, rowsPerFill: 37), null))
		{
			Check(reader.GetRecords<TypedCellRecord>().ToList());
		}

		await using (var reader = new CsvReader(new FakeRecordSource("Data", rows, rowsPerFill: 37), null))
		{
			var records = new List<TypedCellRecord>();
			await foreach (var record in reader.GetRecordsAsync<TypedCellRecord>())
			{
				records.Add(record);
			}

			Check(records);
		}

		using (var reader = new CsvReader(new FakeRecordSource("Data", rows, rowsPerFill: 37), null))
		{
			Check(reader.ParallelEnumerate<TypedCellRecord>(4).ToList());
		}

		// A worker parses as the reader does, and locates its errors.
		using (var reader = new CsvReader(new FakeRecordSource("Data", rows, rowsPerFill: 37), null))
		{
			var values = reader.ParallelEnumerate(r => (r.Parse<DateTime>(1), r.GetString(7), r.Parse<int>(5)), 4).ToList();
			Assert.Equal(new DateTime(2024, 1, 16, 12, 0, 0), values[1].Item1);
			Assert.Equal(["TRUE", "FALSE"], values.Take(2).Select(v => v.Item2));
			Assert.All(values, v => Assert.Equal(42, v.Item3));
		}

		var errors = new List<CsvRecordError>();
		var options = new CsvReaderOptions
		{
			OnRecordError = error =>
			{
				errors.Add(error);

				return true;
			},
		};
		rows[5] = rows[5] with { Cells = [.. rows[5].Cells.Take(1), Date("60"), .. rows[5].Cells.Skip(2)] };
		using (var reader = new CsvReader(new FakeRecordSource("Data", rows), options))
		{
			Assert.Equal(999, reader.GetRecords<TypedCellRecord>().Count());
		}

		var only = Assert.Single(errors);
		Assert.Equal("Data!B6", only.Location);
		Assert.Contains("1900-02-29", only.Exception.InnerException?.Message ?? only.Exception.Message);
	}

	[Fact]
	public void DataReadersConvertWithTheirTypedGetters()
	{
		using var reader = Rows(null, new CsvReaderOptions(),
			new FakeRow(1, Text("When"), Text("Total"), Text("Count"), Text("Paid"), Text("Small")),
			new FakeRow(2, Date("45306.5"), Number("0.30000000000000004"), Number("42.0"), Bool("1"), Number("300.00000000000006")));
		using var data = new CsvDataReader(reader);
		Assert.True(data.Read());
		Assert.Equal(new DateTime(2024, 1, 15, 12, 0, 0), data.GetDateTime(0));
		Assert.Equal("2024-01-15T12:00:00", data.GetValue(0));
		Assert.Equal("2024-01-15T12:00:00", data.GetString(0));
		Assert.Equal(0.3m, data.GetDecimal(1));
		Assert.Equal(0.30000000000000004, data.GetDouble(1));
		Assert.Equal(42, data.GetInt32(2));
		Assert.Equal(42L, data.GetInt64(2));
		Assert.Equal((short)300, data.GetInt16(4));
		Assert.True(data.GetBoolean(3));
		Assert.Equal("TRUE", data.GetValue(3));
		var chars = new char[32];
		Assert.Equal(19, data.GetChars(0, 0, chars, 0, 32));
	}

	[Fact]
	public void SerialDatesMatchAnIndependentImplementation()
	{
		// Excel's 1900 system is the OLE Automation date from serial 61 on, and a day later before serial 60.
		static DateTime? Expected1900(double serial)
		{
			if (serial < 0 || serial >= 2958466 || (serial >= 59.9999999 && serial < 61))
			{
				return null;
			}

			return DateTime.FromOADate(serial < 60 ? serial + 1 : serial);
		}

		var serials = new List<double>();
		foreach (var day in new[] { 0, 1, 2, 58, 59, 61, 62, 1462, 45306, 2958464, 2958465 })
		{
			// Every millisecond around the day's ends.
			for (var ms = -1500; ms <= 1500; ms++)
			{
				if (day * 86_400_000L + ms >= 0)
				{
					serials.Add(double.Parse(Serial(day, ms), CultureInfo.InvariantCulture));
				}
			}
		}

		// Whole milliseconds, then any doubles. FromOADate adds its epoch to the milliseconds before it truncates them, so
		// for a serial within a rounding error of half a millisecond it can be a millisecond off: those compare within one.
		var random = new Random(20261103);
		for (var i = 0; i < 20_000; i++)
		{
			serials.Add(random.Next(0, 2958466) + (random.Next(0, 86_400_000) / 86_400_000.0));
		}

		var wholeMilliseconds = serials.Count;
		for (var i = 0; i < 20_000; i++)
		{
			serials.Add(random.NextDouble() * 2958466);
		}

		foreach (var date1904 in new[] { false, true })
		{
			var cells = new CsvCellOptions { Date1904 = date1904 };
			var rows = serials.Select((s, i) => new FakeRow(i + 1, Date(s.ToString("R", CultureInfo.InvariantCulture)))).ToArray();
			using var reader = Rows(cells, null, rows);
			var index = 0;
			while (reader.Read())
			{
				var exact = index < wholeMilliseconds;
				var serial = serials[index++];
				var expected = date1904
					? (serial + 1462 < 2958466 ? DateTime.FromOADate(serial + 1462) : (DateTime?)null)
					: Expected1900(serial);
				var converted = reader.TryParse<DateTime>(0, out var date);
				if (expected is null)
				{
					// The edges where rounding crosses serial 60, or 9999-12-31, are left to the cases above.
					continue;
				}

				Assert.True(converted, $"{serial:R} ({(date1904 ? 1904 : 1900)})");
				if (exact)
				{
					Assert.Equal(expected, date);
				}
				else
				{
					Assert.InRange((date - expected.Value).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
				}

				// The millisecond rounding is exact: the serial's milliseconds, rounded once.
				var milliseconds = (long)Math.Round(serial * 86_400_000, MidpointRounding.AwayFromZero);
				Assert.Equal(milliseconds % 86_400_000, (long)date.TimeOfDay.TotalMilliseconds);
				var text = reader.GetString(0);
				var timeAlone = Math.Round(serial * 86_400_000, MidpointRounding.AwayFromZero) < 86_400_000;
				var expectedText = timeAlone
					? date.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture)
					: date.TimeOfDay == TimeSpan.Zero
						? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
						: date.ToString("yyyy-MM-dd'T'HH:mm:ss.FFF", CultureInfo.InvariantCulture);
				Assert.Equal(expectedText, text);
			}
		}
	}

	[Fact]
	public void RoundingMatchesDecimalArithmetic()
	{
		var random = new Random(15);
		for (var digits = 1; digits <= 28; digits++)
		{
			var texts = new List<string>();
			for (var i = 0; i < 300; i++)
			{
				// At least 1E-9, so that decimal holds every digit of the text.
				var value = (0.1 + (0.9 * random.NextDouble())) * Math.Pow(10, random.Next(-8, 17)) * (random.Next(2) == 0 ? -1 : 1);
				texts.Add(value.ToString("R", CultureInfo.InvariantCulture));
				texts.Add(Math.Round(value).ToString("R", CultureInfo.InvariantCulture));
			}

			var cells = new CsvCellOptions { SignificantDigits = digits };
			using var reader = Rows(cells, null, texts.Select((t, i) => new FakeRow(i + 1, Number(t))).ToArray());
			var index = 0;
			while (reader.Read())
			{
				var text = texts[index++];
				var expected = RoundSignificant(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), digits);
				Assert.True(expected == reader.Parse<decimal>(0), $"{text} at {digits} digits: {expected} read as {reader.Parse<decimal>(0)}");
				var whole = expected == decimal.Truncate(expected) && expected >= long.MinValue && expected <= long.MaxValue;
				Assert.True(whole == reader.TryParse<long>(0, out var number), $"{text} at {digits} digits as long");
				if (whole)
				{
					Assert.Equal((long)expected, number);
				}
			}
		}
	}

	[Fact]
	public void RoundingCarriesAndKeepsWithinDecimalsRange()
	{
		static decimal Decimal(string text, int? digits) => Parse<decimal>(Number(text), new CsvCellOptions { SignificantDigits = digits });

		static long Long(string text, int? digits) => Parse<long>(Number(text), new CsvCellOptions { SignificantDigits = digits });

		Assert.Equal(10m, Decimal("9.9999999999999999", 15));
		Assert.Equal(100000000000000000m, Decimal("99999999999999999", 15));
		Assert.Equal(-100000000000000000m, Decimal("-99999999999999999", 15));
		Assert.Equal(123456789012346000000m, Decimal("1.23456789012345678E+20", 15));
		Assert.Equal(3m, Decimal("2.5", 1));
		Assert.Equal(-3m, Decimal("-2.5", 1));
		Assert.Equal(0.13m, Decimal("0.125", 2));
		Assert.Equal(1235m, Decimal("1234.5", 4));
		Assert.Equal(0.0000000000000000000000000001m, Decimal("5E-29", 1));
		Assert.Equal(0m, Decimal("4E-29", 1));
		Assert.Equal(0m, Decimal("1E-300", 1));
		Assert.Equal(10000000000000000000000000000m, Decimal("1E+28", 1));
		Assert.Equal(79228162514264337593543950330m, Decimal("79228162514264337593543950334", 28));
		// decimal.MaxValue's 29 digits round up past it at 28.
		Assert.Throws<OverflowException>(() => Decimal("79228162514264337593543950335", 28));
		Assert.Throws<OverflowException>(() => Decimal("1E+29", 1));
		Assert.Throws<OverflowException>(() => Decimal("1E+300", 15));
		Assert.Equal(3L, Long("2.5", 1));
		Assert.Equal(10000000000000000L, Long("9999999999999999.4", 15));
		Assert.Equal(0L, Long("-0.0", 15));
		Assert.Equal(1000000000000000000L, Long("1E18", 15));
		Assert.Equal(5L, Long("0.5E1", null));
		Assert.Equal(5L, Long("+5", null));
		Assert.Throws<FormatException>(() => Long("1E-300", 15));
		Assert.Throws<FormatException>(() => Long("1.0000000000000000000000000000000000000000001", null));
		Assert.Throws<OverflowException>(() => Long("1E+100000000", 15));
	}

	[Fact]
	public void MessagesLeaveValuesOutWhenAsked()
	{
		var options = new CsvReaderOptions { HasHeader = false, IncludeValuesInExceptions = false };
		string Message<T, TException>((string, CsvCellKind) cell)
#if NET
			where T : ISpanParsable<T>
#endif
			where TException : Exception
		{
			using var reader = One(cell, options: options);

			return Assert.Throws<TException>(() => reader.Parse<T>(0)).Message;
		}

		Assert.Equal("The cell is not a whole number, so it is not a valid Int32 (IncludeValuesInExceptions is off, so its value is not shown).", Message<int, FormatException>(Number("1.5")));
		Assert.Equal("The field is outside the range of Int32 (IncludeValuesInExceptions is off, so its value is not shown).", Message<int, OverflowException>(Number("1E+10")));
		Assert.Equal("The cell is not a valid boolean: a number cell is true when it is 1 and false when it is 0 (IncludeValuesInExceptions is off, so its value is not shown).", Message<bool, FormatException>(Number("2")));
		Assert.Equal("The cell is not a valid DateTime: its serial is a day that doesn't exist in Excel's 1900 date system (IncludeValuesInExceptions is off, so its value is not shown).", Message<DateTime, FormatException>(Date("60")));
		Assert.Equal("The cell is not a valid DateTime: Excel's dates are serials from 0 to 2958465.99999999 (9999-12-31) (IncludeValuesInExceptions is off, so its value is not shown).", Message<DateTime, FormatException>(Date("-1")));
		Assert.Equal("The field is outside the range of TimeSpan (IncludeValuesInExceptions is off, so its value is not shown).", Message<TimeSpan, OverflowException>(Duration("1E+20")));

		using var errors = Rows(new CsvCellOptions { ThrowOnErrorCells = true }, options, new FakeRow(4, Error("#N/A")));
		Assert.True(errors.Read());
		Assert.Equal("Data!A4 holds an error (IncludeValuesInExceptions is off, so its value is not shown).", Assert.Throws<FormatException>(() => errors.GetString(0)).Message);
	}

	[Fact]
	public void SignificantDigitsMustBeFrom1To28()
	{
		Assert.Equal(15, CsvCellOptions.Default.SignificantDigits);
		Assert.Null(new CsvCellOptions { SignificantDigits = null }.SignificantDigits);
		Assert.Equal(28, new CsvCellOptions { SignificantDigits = 28 }.SignificantDigits);
		Assert.Throws<ArgumentOutOfRangeException>(() => new CsvCellOptions { SignificantDigits = 0 });
		Assert.Throws<ArgumentOutOfRangeException>(() => new CsvCellOptions { SignificantDigits = 29 });
		Assert.Equal(new CsvCellOptions { Date1904 = true }, Date1904);
	}

	/// <summary>The shortest text of the serial of <paramref name="day"/> plus <paramref name="milliseconds"/>.</summary>
	private static string Serial(long day, long milliseconds) =>
		((day * 86_400_000 + milliseconds) / 86_400_000.0).ToString("R", CultureInfo.InvariantCulture);

	/// <summary>Rounds to <paramref name="digits"/> significant digits, half away from zero, with decimal arithmetic.</summary>
	private static decimal RoundSignificant(decimal value, int digits)
	{
		if (value == 0)
		{
			return 0;
		}

		// The places after the point to keep: digits less the integer digits, or plus the zeros after the point.
		var places = digits;
		var magnitude = Math.Abs(value);
		while (magnitude >= 1)
		{
			magnitude /= 10;
			places--;
		}

		while (magnitude < 0.1m)
		{
			magnitude *= 10;
			places++;
		}

		if (places >= 0)
		{
			return Math.Round(value, Math.Min(places, 28), MidpointRounding.AwayFromZero);
		}

		var scale = 1m;
		for (var i = 0; i < -places; i++)
		{
			scale *= 10;
		}

		return Math.Round(value / scale, 0, MidpointRounding.AwayFromZero) * scale;
	}
}
