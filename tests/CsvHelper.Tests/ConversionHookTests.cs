using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// The internal hooks of CsvHelper.Excel's conversions (Phase 6): a field's text as UTF-8 (<c>GetTextUtf8</c>), a typed
/// cell's text and a number's rounded text (<see cref="CsvCells"/>), and a UTF-8 field with its type
/// (<c>CsvWriter.WriteField(ReadOnlySpan&lt;byte&gt;, Type)</c>).
/// </summary>
public class ConversionHookTests
{
	private static (string, CsvCellKind) Text(string text) => (text, CsvCellKind.Text);

	private static (string, CsvCellKind) Number(string text) => (text, CsvCellKind.Number);

	private static (string, CsvCellKind) Date(string text) => (text, CsvCellKind.DateNumber);

	private static (string, CsvCellKind) Duration(string text) => (text, CsvCellKind.Duration);

	private static (string, CsvCellKind) Bool(string text) => (text, CsvCellKind.Bool);

	private static (string, CsvCellKind) Error(string text) => (text, CsvCellKind.Error);

	private static (string, CsvCellKind) Empty() => ("", CsvCellKind.Empty);

	/// <summary>A reader over a source of one row per cell, called Data, without a header.</summary>
	private static CsvReader Cells(CsvCellOptions? cells, params (string, CsvCellKind)[] column) =>
		new(
			new FakeRecordSource("Data", column.Select((c, i) => new FakeRow(i + 1, c)).ToArray()) { Cells = cells ?? CsvCellOptions.Default },
			new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false });

	/// <summary>The text <c>GetTextUtf8</c> gives for a field, as a string.</summary>
	private static string TextOf(CsvReader reader, int index, bool roundNumbers)
	{
		Span<byte> buffer = stackalloc byte[64];

		return Encoding.UTF8.GetString(reader.GetTextUtf8(index, buffer, roundNumbers).ToArray());
	}

	/// <summary>What <see cref="CsvCells.TryRound"/> gives, or <see langword="null"/> when it leaves the stored text.</summary>
	private static string? Round(string text, int keep, int room = 64)
	{
		var destination = new byte[room];

		return CsvCells.TryRound(Encoding.UTF8.GetBytes(text), keep, destination, out var length) ? Encoding.ASCII.GetString(destination, 0, length) : null;
	}

	/// <summary>Writes with a CSV writer and gives the CSV.</summary>
	private static string Write(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			write(writer);
		}

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	[Fact]
	public void TextIsTheTextGetStringGets()
	{
		(string, CsvCellKind)[] cells =
		[
			Text("a,b"), Text("Äpfel 😀"), Text(""), Number("1.5"), Number("0.30000000000000004"), Number("1.2345678901234568E+17"),
			Date("45306"), Date("45306.438024594907"), Date("0.4375"), Date("60"), Date("61"), Date("1"), Date("0"), Date("2958465.9999999884"),
			Date("2958466"), Date("-1"), Date("abc"), Duration("1.5"), Duration("-0.0625"), Duration("0.000011574074074074073"),
			Duration("10675198.999999988"), Duration("1E+300"), Bool("1"), Bool("0"), Bool("TRUE"), Bool("2"), Error("#N/A"), Empty(),
		];

		foreach (var date1904 in new[] { false, true })
		{
			using var reader = Cells(new CsvCellOptions { Date1904 = date1904 }, cells);
			var row = 0;
			while (reader.Read())
			{
				var expected = reader.GetString(0);
				Assert.True(expected == TextOf(reader, 0, roundNumbers: false), $"{cells[row]} ({(date1904 ? 1904 : 1900)})");
				if (cells[row].Item2 != CsvCellKind.Number)
				{
					Assert.Equal(expected, TextOf(reader, 0, roundNumbers: true));
				}

				row++;
			}

			Assert.Equal(cells.Length, row);
		}
	}

	[Fact]
	public void TextOfRandomDatesAndDurationsIsTheTextGetStringGets()
	{
		var random = new Random(20261112);
		var cells = new List<(string, CsvCellKind)>();
		for (var i = 0; i < 20_000; i++)
		{
			cells.Add(Date((random.NextDouble() * 2958466).ToString("R", CultureInfo.InvariantCulture)));
			cells.Add(Date((random.Next(0, 2958466) + (random.Next(0, 86_400_000) / 86_400_000.0)).ToString("R", CultureInfo.InvariantCulture)));
			cells.Add(Duration(((random.NextDouble() - 0.5) * 2 * 10_675_199).ToString("R", CultureInfo.InvariantCulture)));
			cells.Add(Duration(((random.NextDouble() - 0.5) * 4).ToString("R", CultureInfo.InvariantCulture)));
		}

		foreach (var date1904 in new[] { false, true })
		{
			using var reader = Cells(new CsvCellOptions { Date1904 = date1904 }, cells.ToArray());
			while (reader.Read())
			{
				Assert.Equal(reader.GetString(0), TextOf(reader, 0, roundNumbers: true));
			}
		}
	}

	[Fact]
	public void TextOfCsvIsTheFieldsValue()
	{
		using var reader = CsvReader.FromText("a,\"b,\"\"c\"\"\",0.30000000000000004,45306,,1\r\n", new CsvReaderOptions { HasHeader = false });
		Assert.True(reader.Read());
		for (var i = 0; i < reader.FieldCount; i++)
		{
			var span = Encoding.UTF8.GetString(reader.GetSpan(i).ToArray());
			Assert.Equal(span, TextOf(reader, i, roundNumbers: false));
			Assert.Equal(span, TextOf(reader, i, roundNumbers: true));
		}

		Assert.Equal("b,\"c\"", TextOf(reader, 1, roundNumbers: true));
	}

	[Fact]
	public void NumbersRoundToTheSourcesSignificantDigits()
	{
		(string, CsvCellKind)[] cells = [Number("0.30000000000000004"), Number("1234567.8910000001"), Number("1.5"), Number("123456789012345")];
		using (var reader = Cells(null, cells))
		{
			var rounded = new List<string>();
			var stored = new List<string>();
			while (reader.Read())
			{
				rounded.Add(TextOf(reader, 0, roundNumbers: true));
				stored.Add(TextOf(reader, 0, roundNumbers: false));
			}

			Assert.Equal(["0.3", "1234567.891", "1.5", "123456789012345"], rounded);
			Assert.Equal(cells.Select(c => c.Item1), stored);
		}

		// SignificantDigits = null keeps the stored text, and fewer digits round further.
		using (var reader = Cells(new CsvCellOptions { SignificantDigits = null }, cells))
		{
			while (reader.Read())
			{
				Assert.Equal(reader.GetString(0), TextOf(reader, 0, roundNumbers: true));
			}
		}

		using (var reader = Cells(new CsvCellOptions { SignificantDigits = 3 }, cells))
		{
			var rounded = new List<string>();
			while (reader.Read())
			{
				rounded.Add(TextOf(reader, 0, roundNumbers: true));
			}

			Assert.Equal(["0.3", "1230000", "1.5", "123000000000000"], rounded);
		}
	}

	[Theory]
	[InlineData("0.30000000000000004", "0.3")]
	[InlineData("-0.30000000000000004", "-0.3")]
	[InlineData("1234567.8910000001", "1234567.891")]
	[InlineData("3.1415926535897931", "3.14159265358979")]
	[InlineData("1.0000000000000001E-5", "1E-5")]
	[InlineData("1.2345678901234568E+17", "1.23456789012346E+17")]
	[InlineData("1.2345678901234567E-05", "1.23456789012346E-05")]
	[InlineData("1.2345678901234567E+020", "1.23456789012346E+020")]
	[InlineData("9.9999999999999999E+09", "1E+10")]
	[InlineData("1.2345678901234567e+17", "1.23456789012346e+17")]
	[InlineData("1.2345678901234567E17", "1.23456789012346E17")]
	[InlineData("9.9999999999999999E+99", "1E+100")]
	[InlineData("9.9999999999999999E-100", "1E-99")]
	[InlineData("123456789012345678", "123456789012346000")]
	[InlineData("999999999999999999", "1000000000000000000")]
	[InlineData("9.9999999999999999", "10")]
	[InlineData("0.000012345678901234567", "0.0000123456789012346")]
	[InlineData("0.99999999999999999", "1")]
	[InlineData("1.5000000000000000", "1.5")]
	[InlineData("1234567890123456789012345678901234567890123456789", "1234567890123460000000000000000000000000000000000")]
	public void NumbersRoundInTheirOwnNotation(string stored, string expected)
	{
		Assert.Equal(expected, Round(stored, 15));
	}

	[Theory]
	[InlineData("123456789012345")]
	[InlineData("-123456789012345")]
	[InlineData("0.123456789012345")]
	[InlineData("1.23456789012345E+17")]
	[InlineData("1.50")]
	[InlineData("0.0000000000000000000001")]
	[InlineData("abc")]
	[InlineData("1.2345678901234567x")]
	[InlineData("")]
	public void NumbersOfFewerDigitsAndOtherTextKeepTheStoredText(string stored)
	{
		Assert.Null(Round(stored, 15));
	}

	[Fact]
	public void RoundedTextThatDoesNotFitKeepsTheStoredText()
	{
		Assert.Equal("0.3", Round("0.30000000000000004", 15, room: 3));
		Assert.Null(Round("0.30000000000000004", 15, room: 2));
		Assert.Equal("-0.3", Round("-0.30000000000000004", 15, room: 4));
		Assert.Null(Round("-0.30000000000000004", 15, room: 3));
		Assert.Equal("1234567.891", Round("1234567.8910000001", 15, room: 11));
		Assert.Null(Round("1234567.8910000001", 15, room: 10));
		Assert.Equal("0.0000123456789012346", Round("0.000012345678901234567", 15, room: 21));
		Assert.Null(Round("0.000012345678901234567", 15, room: 20));
		Assert.Equal("1000000000000000000", Round("999999999999999999", 15, room: 19));
		Assert.Null(Round("999999999999999999", 15, room: 18));
		Assert.Equal("1E+100", Round("9.9999999999999999E+99", 15, room: 6));
		Assert.Null(Round("9.9999999999999999E+99", 15, room: 5));
		Assert.Equal("-1.23456789012346E-05", Round("-1.2345678901234567E-05", 15, room: 21));
		Assert.Null(Round("-1.2345678901234567E-05", 15, room: 20));
	}

	[Fact]
	public void RoundedTextReadsAsDecimalTargetsReadTheCell()
	{
		var random = new Random(6);
		for (var digits = 1; digits <= 28; digits++)
		{
			var texts = new List<string>();
			for (var i = 0; i < 300; i++)
			{
				// At least 1E-9, so that decimal holds every digit of the text.
				var value = (0.1 + (0.9 * random.NextDouble())) * Math.Pow(10, random.Next(-8, 17)) * (random.Next(2) == 0 ? -1 : 1);
				texts.Add(value.ToString("R", CultureInfo.InvariantCulture));
				texts.Add(value.ToString("E16", CultureInfo.InvariantCulture));
				texts.Add(value.ToString("F17", CultureInfo.InvariantCulture));
			}

			using var reader = Cells(new CsvCellOptions { SignificantDigits = digits }, texts.Select(Number).ToArray());
			var index = 0;
			while (reader.Read())
			{
				var text = texts[index++];
				var converted = TextOf(reader, 0, roundNumbers: true);
				var value = decimal.Parse(converted, NumberStyles.Float, CultureInfo.InvariantCulture);
				Assert.True(value == reader.Parse<decimal>(0), $"{text} at {digits} digits: {converted}, read as {reader.Parse<decimal>(0)}");
				Assert.Equal(text.Contains('E'), converted.Contains('E'));
				var mantissa = converted.Split('E')[0];
				if (converted != text)
				{
					Assert.True(!mantissa.Contains('.') || !mantissa.EndsWith("0", StringComparison.Ordinal), $"{text} at {digits} digits: {converted}");
				}
			}
		}
	}

	[Fact]
	public void ErrorCellsFailAsGetStringDoes()
	{
		using (var reader = Cells(new CsvCellOptions { ThrowOnErrorCells = true }, Text("x"), Error("#N/A")))
		{
			Assert.True(reader.Read());
			Assert.Equal("x", TextOf(reader, 0, roundNumbers: true));
			Assert.True(reader.Read());
			var expected = Assert.Throws<FormatException>(() => reader.GetString(0));
			var e = Assert.Throws<FormatException>(() => TextOf(reader, 0, roundNumbers: true));
			Assert.Equal(expected.Message, e.Message);
			Assert.Equal("Data!A2 holds the error #N/A.", e.Message);
		}

		using (var reader = Cells(null, Error("#N/A")))
		{
			Assert.True(reader.Read());
			Assert.Equal("#N/A", TextOf(reader, 0, roundNumbers: true));
		}
	}

	[Fact]
	public void TypedFieldsWriteCsvAsTheirBytesDo()
	{
		string[] values = ["a", "", "a,b", "say \"hi\"", "line\nbreak", " padded ", "=1+1", "-5", "Äpfel"];
		foreach (var options in new[]
		{
			new CsvWriterOptions(),
			new CsvWriterOptions { Quoting = CsvQuoting.Always },
			new CsvWriterOptions { Mode = CsvMode.Escape },
			new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [12, 12] },
		})
		{
			foreach (var value in values)
			{
				var expected = Outcome(() => Write(w => Fields(w, value, (writer, utf8) => writer.WriteField(utf8)), options));
				var typed = Outcome(() => Write(w => Fields(w, value, (writer, utf8) => writer.WriteField(utf8, typeof(string))), options));
				Assert.Equal(expected, typed);
			}
		}

		static void Fields(CsvWriter writer, string value, Action<CsvWriter, byte[]> write)
		{
			write(writer, Encoding.UTF8.GetBytes(value));
			write(writer, Encoding.UTF8.GetBytes("next"));
			writer.EndRecord();
		}

		static string Outcome(Func<string> write)
		{
			try
			{
				return write();
			}
			catch (Exception e)
			{
				return $"{e.GetType().Name}: {e.Message}";
			}
		}
	}

	[Fact]
	public void TypedFieldsGiveTheirTypeToInjectionProtectionAndShouldQuote()
	{
		// Escape quotes what it protects; a number isn't text, so it isn't protected.
		var escape = new CsvWriterOptions { Injection = CsvInjection.Escape };
		Assert.Equal("\"'-5\",-5\r\n", Write(w =>
		{
			w.WriteField("-5"u8, typeof(string));
			w.WriteField("-5"u8, typeof(double));
			w.EndRecord();
		}, escape));

		var types = new List<Type>();
		var quote = new CsvWriterOptions
		{
			ShouldQuote = (field, type, index, record) =>
			{
				types.Add(type);

				return type == typeof(decimal);
			},
		};
		Assert.Equal("1.5,\"1.5\"\r\n", Write(w =>
		{
			w.WriteField("1.5"u8, typeof(string));
			w.WriteField("1.5"u8, typeof(decimal));
			w.EndRecord();
		}, quote));
		Assert.Equal([typeof(string), typeof(decimal)], types);
	}

	[Fact]
	public void TypedFieldsReachACellSinkWithTheirType()
	{
		var sink = new FakeCellSink();
		using (var writer = new CsvWriter(sink, null))
		{
			writer.WriteField("1.50"u8, typeof(decimal));
			writer.WriteField("2024-01-15T10:30:00.0000000"u8, typeof(DateTime));
			writer.WriteField("a,b"u8, typeof(string));
			writer.WriteField(""u8, typeof(bool));
			writer.EndRecord();
		}

		var record = Assert.Single(sink.Records);
		Assert.Equal(
			[new FakeCell(typeof(decimal), "1.50"), new FakeCell(typeof(DateTime), "2024-01-15T10:30:00.0000000"), new FakeCell(typeof(string), "a,b"), new FakeCell(typeof(bool), "")],
			record.Cells);
	}
}
