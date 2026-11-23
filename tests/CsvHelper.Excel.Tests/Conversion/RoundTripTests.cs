using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// CSV converted to a sheet and back to CSV (Phase 6): each field comes back as it was, but a typed cell's, which comes back
/// as the reader's text of the same value (a number's invariant text, <c>2024-01-15 10:30</c> as <c>2024-01-15T10:30:00</c>,
/// <c>true</c> as <c>TRUE</c>). Each workbook passes the Open XML SDK's validator, and ClosedXML and ExcelDataReader read
/// the values inference typed. A seeded property test does it for random columns of each type, some with a near miss,
/// which makes its column text.
/// </summary>
public class RoundTripTests
{
	/// <summary>A field of each form, and a date and time before 1900 in a column of dates and times.</summary>
	private const string FieldsCsv =
		"Name,Qty,Price,Exp,Day,At,Precise,Early,Paid,Code,Note\r\n" +
		"apple,3,1.50,1e5,2024-01-15,2024-01-15 10:30,2024-01-15T10:30:00.1234567,1899-12-31 10:30,true,00123,\"a, b\"\r\n" +
		"pear,-4,-0.25,2.5E-3,2024-02-29,2024-02-29,2024-02-29T23:59:59.9996,2024-01-15 10:30,False,123,\r\n";

	/// <summary>Fields near a type's form that match none with the default options: text in any column.</summary>
	private static readonly string[] NearMisses =
	[
		"00123", "007", "+5", ".5", "5.", "1.", "1e", "1E+", "1,5", "1,234", "$5", "50%", " 5", "5 ", "0x10", "NaN", "Infinity", "-",
		"1234567890123456", "1E400", "1E-400", "2024-02-30", "2023-02-29", "1900-02-29", "2024-13-01", "2024-1-15", "2024/01/15",
		"1/15/2024", "2024-01-15T24:00", "2024-01-15T10:60", "2024-01-15T10:30:60", "2024-01-15T10:30:00.12345678", "2024-01-15t10:30",
		"2024-01-15T10:30Z", "2024-01-15T10:30:00+01:00", "10:30", "36:00:00", "tru", "truee", "yes", "t", "#N/A", "=1+1", "@SUM(1)",
	];

	/// <summary>Text that is near no type.</summary>
	private static readonly string[] Words = ["apple", "x y", "a, b", "say \"hi\"", "two\nlines", "Äpfel", "日本語"];

	/// <summary>Booleans in each case.</summary>
	private static readonly string[] Booleans = ["true", "false", "TRUE", "FALSE", "True", "False", "tRuE", "fAlSe"];

	/// <summary>The property test's seeds; an odd seed converts asynchronously.</summary>
	public static TheoryData<int> Seeds
	{
		get
		{
			var data = new TheoryData<int>();
			for (var seed = 0; seed < 40; seed++)
			{
				data.Add(seed);
			}

			return data;
		}
	}

	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesEachFieldBack(bool async)
	{
		// A number comes back as its cell's text, its exponent's e as E; a date and time as the reader's ISO 8601 text of it,
		// rounded to the millisecond (into the next day), and one before 1900, a text cell, as it was; a boolean in capitals;
		// text as it was.
		Assert.Equal(
			"Name,Qty,Price,Exp,Day,At,Precise,Early,Paid,Code,Note\r\n" +
			"apple,3,1.50,1E5,2024-01-15,2024-01-15T10:30:00,2024-01-15T10:30:00.123,1899-12-31 10:30,TRUE,00123,\"a, b\"\r\n" +
			"pear,-4,-0.25,2.5E-3,2024-02-29,2024-02-29,2024-03-01,2024-01-15T10:30:00,FALSE,123,\r\n",
			await RoundTripAsync(FieldsCsv, async));

		// A culture's numbers come back in the invariant culture's form.
		var german = new CsvToExcelOptions { Csv = new CsvReaderOptions { Separator = ';', FormatProvider = new CultureInfo("de-DE") } };
		Assert.Equal("Preis,Menge\r\n1.5,-2\r\n-0.25,3\r\n", await RoundTripAsync("Preis;Menge\r\n1,5;-2\r\n-0,25;3\r\n", async, german));

		// Text cells give every field back.
		Assert.Equal(FieldsCsv, await RoundTripAsync(FieldsCsv, async, new CsvToExcelOptions { InferTypes = 0 }));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MakesAColumnWithANearMissText(bool async)
	{
		// Each near miss below a field of the type it is near: every column is text, each field as it was.
		var (header, pairs, csv) = NearMissCsv();
		var workbook = await ToExcelAsync(csv, async);

		AssertSame("near misses", "ClosedXML", NearMissCells(header, pairs), Written.ClosedXmlValues(workbook));
		Assert.Equal([[.. header], [.. pairs.Select(p => p.Valid)], [.. pairs.Select(p => p.Miss)]], Records(await ToCsvAsync(workbook, async)));
	}

	[GridCraftFact]
	public async Task GridCraftReadsTheNearMisses()
	{
		var (header, pairs, csv) = NearMissCsv();

		AssertSame("near misses", "GridCraft", NearMissCells(header, pairs), GridCraft.Values(await ToExcelAsync(csv, false)));
	}

	[GridCraftFact]
	public async Task GridCraftReadsEachField()
	{
		// GivesEachFieldBack's cells: numbers, dates and dates and times to the millisecond (into the next day), the date and
		// time before 1900 as its text, booleans, and text.
		object?[][] rows =
		[
			["Name", "Qty", "Price", "Exp", "Day", "At", "Precise", "Early", "Paid", "Code", "Note"],
			["apple", 3d, 1.5, 1e5, new DateTime(2024, 1, 15), new DateTime(2024, 1, 15, 10, 30, 0), new DateTime(2024, 1, 15, 10, 30, 0, 123), "1899-12-31 10:30", true, "00123", "a, b"],
			["pear", -4d, -0.25, 2.5e-3, new DateTime(2024, 2, 29), new DateTime(2024, 2, 29), new DateTime(2024, 3, 1), new DateTime(2024, 1, 15, 10, 30, 0), false, "123", null],
		];
		var expected = rows.SelectMany((row, r) => row.Select((cell, c) => (Cell: cell, Column: c)).Where(x => x.Cell is not null).Select(x => $"{CellReference.ColumnName(x.Column)}{r + 1}={ClosedXmlValue(x.Cell!)}")).ToList();

		AssertSame("each field", "GridCraft", expected, GridCraft.Values(await ToExcelAsync(FieldsCsv, false)));
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task GivesRandomColumnsBack(int seed)
	{
		var (description, rows, columns, csv) = RandomCsv(seed);
		var async = seed % 2 == 1;
		var workbook = await ToExcelAsync(csv, async);

		Assert.True(Written.ValidationErrors(workbook).Count == 0, description + ": " + string.Join("; ", Written.ValidationErrors(workbook)));

		// ClosedXML's values, cell by cell. ClosedXML reads a line feed in a cell's text as CR LF, which the cell doesn't hold
		// (ExcelDataReader reads it as written).
		AssertSame(description, "ClosedXML", RandomCells(rows, columns), [.. Written.ClosedXmlValues(workbook).Select(v => v.Replace("\r\n", "\n"))]);

		// ExcelDataReader's values, row by row.
		var dataReader = new List<string> { string.Join("|", ["String:id", .. columns.Select((_, i) => $"String:c{i + 1}")]) };
		dataReader.AddRange(Enumerable.Range(0, rows).Select(r => string.Join("|", [$"String:r{r + 1}", .. columns.Select(c => c.Cells[r] is { } cell ? DataReaderValue(cell) : "")])));
		AssertSame(description, "ExcelDataReader", dataReader, Written.ExcelDataReaderValues(workbook));

		// And back to CSV, record by record.
		List<string[]> back = [["id", .. columns.Select((_, i) => $"c{i + 1}")]];
		back.AddRange(Enumerable.Range(0, rows).Select(r => (string[])[$"r{r + 1}", .. columns.Select(c => c.Back[r])]));
		var records = Records(await ToCsvAsync(workbook, async));
		Assert.Equal(back.Count, records.Count);
		foreach (var (want, got) in back.Zip(records, (w, g) => (w, g)))
		{
			Assert.Equal(want, got);
		}
	}

	[GridCraftTheory]
	[MemberData(nameof(Seeds))]
	public async Task GridCraftReadsTheRandomColumns(int seed)
	{
		var (description, rows, columns, csv) = RandomCsv(seed);

		AssertSame(description, "GridCraft", RandomCells(rows, columns), GridCraft.Values(await ToExcelAsync(csv, false)));
	}

	/// <summary>Each near miss below a field of the type it is near, a column each.</summary>
	/// <returns>The columns' names, each column's field and near miss, and the CSV.</returns>
	private static (List<string> Header, List<(string Valid, string Miss)> Pairs, string Csv) NearMissCsv()
	{
		(string Valid, string[] Misses)[] types =
		[
			("123", ["00123", "007", "+5", ".5", "5.", "1.", "1e", "1E+", "1,5", "1,234", "$5", "50%", " 5", "5 ", "0x10", "NaN", "Infinity", "-", "--1", "1.5.5", "1234567890123456", "1E308", "1E400", "1E-308", "1E-400"]),
			("2024-01-15", ["2024-02-30", "2023-02-29", "1900-02-29", "2024-13-01", "2024-00-10", "2024-1-15", "24-01-15", "2024/01/15", "1/15/2024", "2024_01_15", "0000-01-01"]),
			("2024-01-15 10:30", ["2024-01-15T24:00", "2024-01-15T10:60", "2024-01-15T10:30:60", "2024-01-15T10:30:5", "2024-01-15T10:30:00.12345678", "2024-01-15T10:30:00.", "2024-01-15t10:30", "2024-01-15  10:30", "2024-01-15T1030", "2024-01-15T10:30Z", "2024-01-15T10:30:00+01:00", "10:30", "36:00:00"]),
			("true", ["tru", "truee", "yes", "t", "f", "1", "0", "true ", "trüe"]),
		];
		var pairs = types.SelectMany(t => t.Misses.Select(m => (t.Valid, Miss: m))).ToList();
		var header = pairs.Select((_, i) => $"c{i + 1}").ToList();
		var csv = string.Join(",", header) + "\r\n" + string.Join(",", pairs.Select(p => p.Valid)) + "\r\n" + string.Join(",", pairs.Select(p => Quoted(p.Miss))) + "\r\n";

		return (header, pairs, csv);
	}

	/// <summary>The near misses' cells, each a text cell of its field, as <see cref="Written.ClosedXmlValues"/> gives them.</summary>
	/// <param name="header">The columns' names.</param>
	/// <param name="pairs">Each column's field and near miss.</param>
	/// <returns>The cells.</returns>
	private static List<string> NearMissCells(List<string> header, List<(string Valid, string Miss)> pairs)
	{
		List<string> expected = [.. header.Select((h, i) => $"{CellReference.ColumnName(i)}1=Text:{h}")];
		expected.AddRange(pairs.Select((p, i) => $"{CellReference.ColumnName(i)}2=Text:{p.Valid}"));
		expected.AddRange(pairs.Select((p, i) => $"{CellReference.ColumnName(i)}3=Text:{p.Miss}"));

		return expected;
	}

	/// <summary>A seed's random columns.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>A description for messages, the number of records, the columns and the CSV.</returns>
	private static (string Description, int Rows, List<Column> Columns, string Csv) RandomCsv(int seed)
	{
		var random = new Random(seed);
		var rows = random.Next(1, 25);
		var columns = Enumerable.Range(0, random.Next(1, 9)).Select(_ => RandomColumn(random, rows)).ToList();
		var csv = new StringBuilder("id");
		foreach (var (_, i) in columns.Select((c, i) => (c, i)))
		{
			csv.Append(",c").Append(i + 1);
		}

		csv.Append("\r\n");
		for (var r = 0; r < rows; r++)
		{
			// The first column, of text, gives every record a value, so that none is an empty row, which a reader skips.
			csv.Append('r').Append(r + 1);
			foreach (var column in columns)
			{
				csv.Append(',').Append(Quoted(column.Fields[r] ?? ""));
			}

			csv.Append("\r\n");
		}

		var description = $"seed {seed}: " + string.Join(", ", columns.Select((c, i) => $"c{i + 1} {c.Type}"));

		return (description, rows, columns, csv.ToString());
	}

	/// <summary>A seed's random columns' cells, row by row, as <see cref="Written.ClosedXmlValues"/> gives them.</summary>
	/// <param name="rows">The number of records.</param>
	/// <param name="columns">The columns.</param>
	/// <returns>The cells.</returns>
	private static List<string> RandomCells(int rows, List<Column> columns)
	{
		var expected = new List<string> { "A1=Text:id" };
		expected.AddRange(columns.Select((_, i) => $"{CellReference.ColumnName(i + 1)}1=Text:c{i + 1}"));
		for (var r = 0; r < rows; r++)
		{
			expected.Add($"A{r + 2}=Text:r{r + 1}");
			expected.AddRange(columns.Select((c, i) => (c.Cells[r], i)).Where(c => c.Item1 is not null).Select(c => $"{CellReference.ColumnName(c.i + 1)}{r + 2}={ClosedXmlValue(c.Item1!)}"));
		}

		return expected;
	}

	/// <summary>Converts CSV to a workbook and back to CSV, as the mode says.</summary>
	private static async Task<string> RoundTripAsync(string csv, bool async, CsvToExcelOptions? options = null) =>
		await ToCsvAsync(await ToExcelAsync(csv, async, options), async);

	/// <summary>Converts CSV to a workbook through the stream overloads, as the mode says, and checks the workbook's zip (<see cref="Written.Checked(byte[])"/>).</summary>
	private static async Task<byte[]> ToExcelAsync(string csv, bool async, CsvToExcelOptions? options = null)
	{
		using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
		using var output = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToExcelAsync(input, output, options);
		}
		else
		{
			ExcelConvert.ToExcel(input, output, options);
		}

		return Written.Checked(output.ToArray());
	}

	/// <summary>Converts a workbook's first sheet to CSV through the stream overloads, as the mode says.</summary>
	private static async Task<string> ToCsvAsync(byte[] workbook, bool async)
	{
		using var input = new MemoryStream(workbook);
		using var output = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToCsvAsync(input, output);
		}
		else
		{
			ExcelConvert.ToCsv(input, output);
		}

		return Encoding.UTF8.GetString(output.ToArray());
	}

	/// <summary>Asserts that a reader read what was expected, naming the first value that differs.</summary>
	private static void AssertSame(string description, string reader, IReadOnlyList<string> expected, IReadOnlyList<string> read)
	{
		for (var i = 0; i < Math.Max(expected.Count, read.Count); i++)
		{
			var (want, got) = (i < expected.Count ? expected[i] : "nothing", i < read.Count ? read[i] : "nothing");
			Assert.True(want == got, $"{description}: expected {CellValue(want)}, {reader} {CellValue(got)}");
		}
	}

	/// <summary>A value for a message, its line breaks shown.</summary>
	private static string CellValue(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n");

	/// <summary>A CSV's records, every line a record, the header's too.</summary>
	private static List<string[]> Records(string csv)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(csv), new CsvReaderOptions { HasHeader = false });
		var records = new List<string[]>();
		while (reader.Read())
		{
			records.Add([.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)]);
		}

		return records;
	}

	/// <summary>A field as CSV writes it: quoted when it has a comma, a quote or a line break.</summary>
	private static string Quoted(string field) => field.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;

	/// <summary>A cell's value as <see cref="Written.ClosedXmlValues"/> gives it.</summary>
	private static string ClosedXmlValue(object cell) => cell switch
	{
		double number => "Number:" + number.ToString("R", CultureInfo.InvariantCulture),
		DateTime date => "DateTime:" + Rounded(date).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
		bool flag => flag ? "Boolean:TRUE" : "Boolean:FALSE",
		_ => "Text:" + cell,
	};

	/// <summary>A cell's value as <see cref="Written.ExcelDataReaderValues"/> gives it.</summary>
	private static string DataReaderValue(object cell) => cell switch
	{
		double number => "Double:" + number.ToString("R", CultureInfo.InvariantCulture),
		DateTime date => "DateTime:" + Rounded(date).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
		bool flag => flag ? "Boolean:True" : "Boolean:False",
		_ => "String:" + cell,
	};

	/// <summary>A date and time rounded to the millisecond, half up, as a cell holds it.</summary>
	private static DateTime Rounded(DateTime date) => new((date.Ticks / 10_000 + (date.Ticks % 10_000 >= 5_000 ? 1 : 0)) * 10_000);

	/// <summary>
	/// A random column: its fields (null for an empty one), the cells inference should make of them, and the text they
	/// come back as. A typed column has a near miss in one in four, which makes it text.
	/// </summary>
	private static Column RandomColumn(Random random, int rows)
	{
		var kind = random.Next(5);
		var miss = kind < 4 && random.Next(4) == 0 ? random.Next(rows) : -1;
		var fields = new string?[rows];
		var cells = new object?[rows];
		var back = new string[rows];
		for (var r = 0; r < rows; r++)
		{
			if (r != miss && random.Next(8) == 0)
			{
				back[r] = "";

				continue;
			}

			(fields[r], cells[r], back[r]) = r == miss ? Text(NearMisses[random.Next(NearMisses.Length)]) : kind switch
			{
				0 => RandomNumber(random),
				1 => RandomDate(random),
				2 => RandomDateTime(random),
				3 => RandomBoolean(random),
				_ => Text(random.Next(2) == 0 ? NearMisses[random.Next(NearMisses.Length)] : Words[random.Next(Words.Length)]),
			};
		}

		if (miss >= 0)
		{
			// The near miss is held with the rest: every field is a text cell of its text.
			for (var r = 0; r < rows; r++)
			{
				(cells[r], back[r]) = (fields[r], fields[r] ?? "");
			}
		}

		var type = miss >= 0 ? "text, a near miss" : new[] { "number", "date", "date and time", "boolean", "text" }[kind];

		return new Column(type, fields, cells, back);
	}

	/// <summary>A text field, its cell and its text back.</summary>
	private static (string, object?, string) Text(string text) => (text, text, text);

	/// <summary>
	/// A number of 1 to 15 significant digits, its point anywhere or after zeros, negative in one in three and with an
	/// exponent in one in four (<c>e</c> or <c>E</c>, signed or not); it comes back in its cell's text, with <c>E</c>.
	/// </summary>
	private static (string, object?, string) RandomNumber(Random random)
	{
		var digits = new StringBuilder();
		digits.Append((char)('1' + random.Next(9)));
		for (var count = random.Next(15); count > 0; count--)
		{
			digits.Append((char)('0' + random.Next(10)));
		}

		var mantissa = random.Next(4) switch
		{
			0 => "0." + new string('0', random.Next(4)) + digits,
			1 => digits.ToString(),
			_ when digits.Length > 1 => digits.Insert(random.Next(1, digits.Length), '.').ToString(),
			_ => digits.ToString(),
		};
		var field = (random.Next(3) == 0 ? "-" : "") + mantissa;
		if (random.Next(4) == 0)
		{
			field += (random.Next(2) == 0 ? "e" : "E") + new[] { "", "+", "-" }[random.Next(3)] + random.Next(100).ToString(CultureInfo.InvariantCulture);
		}

		return (field, double.Parse(field, NumberStyles.Float, CultureInfo.InvariantCulture), field.Replace('e', 'E'));
	}

	/// <summary>A date from 1900-03-01, past the serials ClosedXML and ExcelDataReader read a day off, to 9998.</summary>
	private static (string, object?, string) RandomDate(Random random)
	{
		var date = new DateTime(1900, 3, 1).AddDays(random.Next((new DateTime(9998, 12, 31) - new DateTime(1900, 3, 1)).Days));
		var field = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		return (field, date, field);
	}

	/// <summary>
	/// A date and time (<c>T</c> or a space, <c>HH:mm</c>, then seconds and a fraction of 1 to 7 digits or not), or a date
	/// alone in one in six; it comes back as the reader's text of it rounded to the millisecond.
	/// </summary>
	private static (string, object?, string) RandomDateTime(Random random)
	{
		var (field, value, _) = RandomDate(random);
		var date = (DateTime)value!;
		if (random.Next(6) > 0)
		{
			date = date.AddHours(random.Next(24)).AddMinutes(random.Next(60));
			field += (random.Next(2) == 0 ? "T" : " ") + date.ToString("HH:mm", CultureInfo.InvariantCulture);
			if (random.Next(3) > 0)
			{
				date = date.AddSeconds(random.Next(60));
				field += date.ToString(":ss", CultureInfo.InvariantCulture);
				if (random.Next(2) == 0)
				{
					var fraction = random.Next(1, 8);
					var ticks = random.Next((int)Math.Pow(10, fraction));
					field += "." + ticks.ToString(new string('0', fraction), CultureInfo.InvariantCulture);
					date = date.AddTicks(ticks * (long)Math.Pow(10, 7 - fraction));
				}
			}
		}

		// The reader's text: the date alone at midnight, else with seconds, and milliseconds without the zeros at their end.
		var rounded = Rounded(date);
		var text = rounded.ToString(rounded.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
		if (rounded.Millisecond != 0)
		{
			text += "." + rounded.Millisecond.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
		}

		return (field, date, text);
	}

	/// <summary>A boolean in any case; it comes back in capitals.</summary>
	private static (string, object?, string) RandomBoolean(Random random)
	{
		var field = Booleans[random.Next(Booleans.Length)];
		var value = field.Length == 4;

		return (field, value, value ? "TRUE" : "FALSE");
	}

	/// <summary>A random column of the property test.</summary>
	/// <param name="Type">What it holds, for messages.</param>
	/// <param name="Fields">Its fields, <see langword="null"/> for an empty one.</param>
	/// <param name="Cells">The cells inference should make: a double, a date and time, a boolean, text, or none.</param>
	/// <param name="Back">The text each field comes back as.</param>
	private sealed record Column(string Type, string?[] Fields, object?[] Cells, string[] Back);
}
