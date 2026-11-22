using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Security;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>A row of <see cref="PoolStringsTests"/>' sheet, read as text: each member a column's string.</summary>
[CsvRecord]
public partial class PooledRow
{
	/// <summary>A shared string.</summary>
	public string? Shared { get; set; }

	/// <summary>An inline string, some with white space around it.</summary>
	public string? Inline { get; set; }

	/// <summary>An inline string of its own in each row.</summary>
	public string? Unique { get; set; }

	/// <summary>A number, in two styles.</summary>
	public string? Number { get; set; }

	/// <summary>A boolean.</summary>
	public string? Flag { get; set; }

	/// <summary>A date.</summary>
	public string? Day { get; set; }

	/// <summary>An error cell, or a formula's string.</summary>
	public string? Error { get; set; }

	/// <summary>A text of more than 128 bytes, shared or inline.</summary>
	public string? Long { get; set; }

	/// <summary>A shared string in one of three styles.</summary>
	public string? Styled { get; set; }

	/// <summary>An inline string, a null value, a one-byte value, or an empty cell.</summary>
	public string? Note { get; set; }
}

/// <summary>A row of <see cref="PoolStringsTests"/>' sheet with its typed cells read as values.</summary>
[CsvRecord]
public partial class PooledTypedRow
{
	/// <summary>A shared string.</summary>
	public string? Shared { get; set; }

	/// <summary>A number.</summary>
	public double Number { get; set; }

	/// <summary>A boolean.</summary>
	public bool Flag { get; set; }

	/// <summary>A date.</summary>
	public DateTime Day { get; set; }
}

/// <summary>
/// <see cref="ExcelReaderOptions.PoolStrings"/> (Phase 9's step 4): every path that gives text gives the same text with
/// pooling on and off (fields, records, dynamic records, <see cref="CsvDataReader"/>, <c>ParallelEnumerate</c>, the
/// conversion to CSV), over a generated sheet of shared and inline strings, typed cells, error cells and styled cells, in
/// several readings, and over the corpus; equal values of a column are one instance when on and distinct when off, while a
/// typed or styled cell's text is made for each read; and the default, the override of <see cref="ExcelReaderOptions.Csv"/>'s
/// <see cref="CsvReaderOptions.PoolStrings"/>, and the options' equality.
/// </summary>
/// <param name="output">Shows the lines each reading gave each way, and how each way ended.</param>
public class PoolStringsTests(ITestOutputHelper output)
{
	/// <summary>The rows after the header: more than 512, so that the column of unique values switches its pool off part way.</summary>
	private const int RowCount = 600;

	/// <summary>A text of more than the 128 bytes the pool keeps, shared and inline.</summary>
	private static readonly string LongA = "long " + new string('a', 140);

	/// <summary>Another text of more than 128 bytes, inline.</summary>
	private static readonly string LongB = "long " + new string('b', 140);

	/// <summary>
	/// The shared strings: the first header name, <c>Shared</c>'s four values (one not ASCII), <c>Styled</c>'s value, and
	/// <see cref="LongA"/>.
	/// </summary>
	private static readonly string[] SharedTexts = ["Shared", "apple", "pear", "plum", "Größe", "abc", LongA];

	/// <summary>
	/// The readings compared: raw and formatted (in two cultures), null values, error cells as null and failing every read,
	/// trimming, no header, and a header found in a range of columns.
	/// </summary>
	private static readonly (string Name, ExcelReaderOptions Options)[] Readings =
	[
		("raw", new()),
		("formatted", new() { Text = ExcelText.Formatted }),
		("formatted in de-DE with null values", new() { Text = ExcelText.Formatted, Csv = new() { NullValues = ["NA", "pear"], FormatProvider = CultureInfo.GetCultureInfo("de-DE") } }),
		("error cells as null, null values and trimming", new() { ErrorCells = ExcelErrorCells.Null, Csv = new() { NullValues = ["NA", "pear"], TrimWhitespace = true } }),
		("error cells failing", new() { ErrorCells = ExcelErrorCells.Error }),
		("no header", new() { Csv = new() { HasHeader = false } }),
		("a header found in columns B to D", new() { Range = "B:D", HeaderRow = ExcelHeaderRow.Find(["Inline", "Unique"]) }),
	];

	/// <summary>The readings of the corpus: every row a record, raw and formatted; and with a header, formatted and trimmed, and with formulas as text.</summary>
	private static readonly (string Name, ExcelReaderOptions Options)[] CorpusReadings =
	[
		("raw", new() { Csv = new() { HasHeader = false, SkipEmptyLines = false } }),
		("formatted", new() { Text = ExcelText.Formatted, Csv = new() { HasHeader = false, SkipEmptyLines = false } }),
		("formatted and trimmed with a header", new() { Text = ExcelText.Formatted, Csv = new() { TrimWhitespace = true } }),
		("formulas as text and error cells as null with a header", new() { Formulas = ExcelFormulas.FormulaText, ErrorCells = ExcelErrorCells.Null }),
	];

	/// <summary>The sheet, made once.</summary>
	private static readonly Lazy<TestPackage> Book = new(Package);

	[Fact]
	public void PoolsByDefault()
	{
		Assert.True(ExcelReaderOptions.Default.PoolStrings);
		Assert.True(new ExcelReaderOptions().PoolStrings);

		// A CSV reader's default stays off.
		Assert.False(CsvReaderOptions.Default.PoolStrings);
		Assert.False(ExcelReaderOptions.Default.Csv.PoolStrings);

		using var reader = ExcelReader.Create(Book.Value.ToStream());
		Assert.True(reader.Options.PoolStrings);
		Assert.True(RepeatsOneInstance(reader));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PoolsWithACallersCsvOptions(bool async)
	{
		// The guide's way to set the culture: a Csv of the caller's own, whose PoolStrings is off.
		var csv = new CsvReaderOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
		Assert.False(csv.PoolStrings);

		using var workbook = await Workbooks.OpenAsync(Book.Value, async);
		using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = csv });
		Assert.True(reader.Options.PoolStrings);
		Assert.Equal(csv with { PoolStrings = true }, reader.Options);
		Assert.True(RepeatsOneInstance(reader));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DoesNotPoolWhenTurnedOffWhateverCsvSays(bool async)
	{
		var csv = new CsvReaderOptions { PoolStrings = true, HasHeader = true };
		using var workbook = await Workbooks.OpenAsync(Book.Value, async);
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { PoolStrings = false, Csv = csv }))
		{
			Assert.False(reader.Options.PoolStrings);
			Assert.Equal(csv with { PoolStrings = false }, reader.Options);
			Assert.False(RepeatsOneInstance(reader));
		}

		// The workbook's own options are the default of its readers, and ExcelReader's.
		using (var off = await Workbooks.OpenAsync(Book.Value, async, new ExcelReaderOptions { PoolStrings = false }))
		using (var reader = await off.OpenReaderAsync(async))
		{
			Assert.False(reader.Options.PoolStrings);
			Assert.False(RepeatsOneInstance(reader));
		}

		var unpooled = new ExcelReaderOptions { PoolStrings = false };
		using (var reader = async ? await ExcelReader.CreateAsync(Book.Value.ToStream(), unpooled) : ExcelReader.Create(Book.Value.ToStream(), unpooled))
		{
			Assert.False(reader.Options.PoolStrings);
			Assert.False(RepeatsOneInstance(reader));
		}
	}

	[Fact]
	public void KeepsTheCsvOptionsWhenTheyAgree()
	{
		var csv = new CsvReaderOptions { PoolStrings = true, HasHeader = false };
		Assert.Same(csv, new ExcelReaderOptions { Csv = csv }.ReaderCsv());
		Assert.Same(CsvReaderOptions.Default, new ExcelReaderOptions { PoolStrings = false }.ReaderCsv());

		var copy = ExcelReaderOptions.Default.ReaderCsv();
		Assert.Equal(CsvReaderOptions.Default with { PoolStrings = true }, copy);
		Assert.Equal(csv with { PoolStrings = false }, new ExcelReaderOptions { Csv = csv, PoolStrings = false }.ReaderCsv());

		// The reader gets them as they are.
		using var workbook = ExcelWorkbook.Open(Book.Value.ToStream());
		using (var reader = workbook.OpenReader(new ExcelReaderOptions { Csv = csv }))
		{
			Assert.Same(csv, reader.Options);
		}

		using (var reader = workbook.OpenReader(new ExcelReaderOptions { PoolStrings = false }))
		{
			Assert.Same(CsvReaderOptions.Default, reader.Options);
		}
	}

	[Fact]
	public void ComparesAndCopiesPoolStrings()
	{
		var off = new ExcelReaderOptions { PoolStrings = false };
		Assert.NotEqual(ExcelReaderOptions.Default, off);
		Assert.Equal(new ExcelReaderOptions { PoolStrings = false }, off);
		Assert.Equal(new ExcelReaderOptions { PoolStrings = false }.GetHashCode(), off.GetHashCode());
		Assert.Equal(ExcelReaderOptions.Default, off with { PoolStrings = true });
		Assert.False((ExcelReaderOptions.Default with { PoolStrings = false }).PoolStrings);
		Assert.False((off with { Sheet = "Data" }).PoolStrings);
		Assert.Contains("PoolStrings = False", off.ToString(), StringComparison.Ordinal);
		Assert.Contains("PoolStrings = True", ExcelReaderOptions.Default.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesTheSameTextEveryWayPooledAndNot(bool async)
	{
		foreach (var (name, options) in Readings)
		{
			var pooled = await ReadEveryWayAsync(Book.Value, options with { PoolStrings = true }, async);
			var unpooled = await ReadEveryWayAsync(Book.Value, options with { PoolStrings = false }, async);
			AssertSameLines(name, unpooled, pooled);
			output.WriteLine($"{name}: {Summary(pooled)}");

			// Every reading reads every row through the fields, whatever the other ways throw.
			Assert.True(pooled.Count(l => l.StartsWith("fields ", StringComparison.Ordinal)) >= RowCount, $"{name}: fewer fields lines than rows.");
		}

		// The lines are of the text Excel shows, and of the values: a few of them, so that the comparison is of something.
		var raw = await ReadEveryWayAsync(Book.Value, new ExcelReaderOptions(), async);
		Assert.Equal(RowCount, raw.Count(l => l.StartsWith("records ", StringComparison.Ordinal)));
		Assert.Contains($"records apple|north|id-0|10|TRUE|2024-01-15|#N/A|{LongA}|abc|NA", raw);
		Assert.Contains("typed apple|10|True|2024-01-15T00:00:00.0000000", raw);
		Assert.Contains("parallel apple|north|id-0|10|TRUE|2024-01-15|#N/A|" + LongA + "|abc|NA", raw);

		var formatted = await ReadEveryWayAsync(Book.Value, new ExcelReaderOptions { Text = ExcelText.Formatted }, async);
		Assert.Contains($"records apple|north|id-0|10.00|TRUE|1/15/2024|#N/A|{LongA}|Note: abc|NA", formatted);
		Assert.Contains($"records pear|south|id-1|1.5|FALSE|1/16/2024|#DIV/0!|{LongB}|abc|", formatted);
		Assert.Contains($"records plum|  south |id-2|-3.00|TRUE|1/15/2024|ok|{LongA}|t:abc|x", formatted);
		Assert.Contains("csv apple,north,id-0,10.00,TRUE,1/15/2024,#N/A," + LongA + ",Note: abc,NA", formatted);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesEqualValuesOfAColumnOneInstanceWhenPooled(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Book.Value, async);

		// Records 0 and 12 hold the same cells in every column but Unique.
		foreach (var pool in new[] { true, false })
		{
			var options = new ExcelReaderOptions { PoolStrings = pool };
			var (first, again) = await TwoRecordsAsync(workbook, options, async);
			var shared = pool ? "one instance" : "distinct instances";
			foreach (var column in new[] { "Shared", "Inline", "Number", "Error", "Note" })
			{
				Assert.Equal(first[column], again[column]);
				Assert.True(ReferenceEquals(first[column], again[column]) == pool, $"{column}, \"{first[column]}\": not {shared}.");
			}

			// A date's text is made for each read, and the pool doesn't keep a value of more than 128 bytes.
			Assert.Equal(first["Day"], again["Day"]);
			Assert.NotSame(first["Day"], again["Day"]);
			Assert.Equal(first["Long"], again["Long"]);
			Assert.NotSame(first["Long"], again["Long"]);

			// Records keep their strings: one instance for each value of a column, or one for each cell.
			using (var reader = await workbook.OpenReaderAsync(async, options))
			{
				var records = reader.GetRecords<PooledRow>().ToList();
				Assert.True(ReferenceEquals(records[0].Shared, records[12].Shared) == pool, $"Records' Shared: not {shared}.");
				Assert.True(ReferenceEquals(records[0].Inline, records[12].Inline) == pool, $"Records' Inline: not {shared}.");
				Assert.Equal(pool ? 1 : RowCount / 4, Instances(records.Select(r => r.Shared!).Where(s => s == "apple")));
			}

			using (var reader = await workbook.OpenReaderAsync(async, options))
			using (var data = new CsvDataReader(reader))
			{
				var names = new List<string>();
				while (async ? await data.ReadAsync() : data.Read())
				{
					names.Add(data.GetString(0));
				}

				Assert.Equal(pool ? 1 : RowCount / 4, Instances(names.Where(s => s == "apple")));
			}

			// The workers of ParallelEnumerate share the reader's pool.
			using (var reader = await workbook.OpenReaderAsync(async, options))
			{
				var names = reader.ParallelEnumerate(r => r.GetString(1), 4).ToList();
				Assert.Equal(RowCount, names.Count);
				Assert.Equal(pool ? 1 : RowCount / 4, Instances(names.Where(s => s == "north")));
			}
		}

		// Under Formatted, a styled cell's text is made for each read too, a column's unstyled text pooled.
		var (shown, shownAgain) = await TwoRecordsAsync(workbook, new ExcelReaderOptions { Text = ExcelText.Formatted }, async);
		Assert.Same(shown["Shared"], shownAgain["Shared"]);
		Assert.Equal("Note: abc", shown["Styled"]);
		Assert.Equal(shown["Styled"], shownAgain["Styled"]);
		Assert.NotSame(shown["Styled"], shownAgain["Styled"]);
		Assert.Equal("10.00", shown["Number"]);
		Assert.Equal(shown["Number"], shownAgain["Number"]);
		Assert.NotSame(shown["Number"], shownAgain["Number"]);
	}

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void ReadsTheCorpusAlikePooledAndNot(string file)
	{
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
		{
			foreach (var (name, options) in CorpusReadings)
			{
				var pooled = ReadSheet(workbook, sheet, options with { PoolStrings = true });
				var unpooled = ReadSheet(workbook, sheet, options with { PoolStrings = false });
				AssertSameLines($"{file}, {sheet.Name}, {name}", unpooled, pooled);
			}
		}
	}

	/// <summary>
	/// The sheet: a header, then <see cref="RowCount"/> rows of ten columns, whose values repeat every 12 rows but for
	/// <c>Unique</c>'s: shared strings; inline strings, one with white space kept around it and one not ASCII; a value of
	/// each row's own; numbers in <c>#,##0.00</c> and in no style; booleans; dates; error cells and a formula's string; texts
	/// of more than 128 bytes; one shared string in three styles (<c>"Note: "@</c>, <c>@</c> and <c>"t:"@</c>); and a null
	/// value, an empty cell, a one-byte value and a text.
	/// </summary>
	/// <returns>The package.</returns>
	private static TestPackage Package()
	{
		var rows = new StringBuilder("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c>");
		var names = new[] { "Inline", "Unique", "Number", "Flag", "Day", "Error", "Long", "Styled", "Note" };
		for (var i = 0; i < names.Length; i++)
		{
			rows.Append(Inline($"{(char)('B' + i)}1", names[i]));
		}

		rows.Append("</row>");
		var inline = new[] { "north", "south", "  south ", "éast" };
		var numbers = new[] { "10", "1.5", "-3" };
		var styles = new[] { 4, 15, 12 };
		for (var i = 0; i < RowCount; i++)
		{
			var r = (i + 2).ToString(CultureInfo.InvariantCulture);
			rows.Append($"<row r=\"{r}\">");
			rows.Append($"<c r=\"A{r}\" t=\"s\"><v>{1 + i % 4}</v></c>");
			rows.Append(Inline($"B{r}", inline[i % 4]));
			rows.Append(Inline($"C{r}", $"id-{i}"));
			rows.Append(i % 2 == 0 ? $"<c r=\"D{r}\" s=\"2\"><v>{numbers[i % 3]}</v></c>" : $"<c r=\"D{r}\"><v>{numbers[i % 3]}</v></c>");
			rows.Append($"<c r=\"E{r}\" t=\"b\"><v>{(i + 1) % 2}</v></c>");
			rows.Append($"<c r=\"F{r}\" s=\"1\"><v>{45306 + i % 2}</v></c>");
			rows.Append((i % 3) switch
			{
				0 => $"<c r=\"G{r}\" t=\"e\"><v>#N/A</v></c>",
				1 => $"<c r=\"G{r}\" t=\"e\"><v>#DIV/0!</v></c>",
				_ => $"<c r=\"G{r}\" t=\"str\"><f>\"ok\"</f><v>ok</v></c>",
			});
			rows.Append((i % 3) switch
			{
				0 => $"<c r=\"H{r}\" t=\"s\"><v>6</v></c>",
				1 => Inline($"H{r}", LongB),
				_ => Inline($"H{r}", LongA),
			});
			rows.Append($"<c r=\"I{r}\" t=\"s\" s=\"{styles[i % 3]}\"><v>5</v></c>");
			rows.Append((i % 4) switch
			{
				0 => Inline($"J{r}", "NA"),
				1 => "",
				2 => Inline($"J{r}", "x"),
				_ => Inline($"J{r}", "memo"),
			});
			rows.Append("</row>");
		}

		return TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet(rows.ToString())))
			.Set("xl/styles.xml", FormattedTextTests.Styles())
			.SharedStrings(SharedTexts.Select(t => $"<t>{Sheets.Escape(t)}</t>").ToArray());
	}

	/// <summary>An inline string cell, its white space kept.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <param name="text">The text.</param>
	/// <returns>The cell's XML.</returns>
	private static string Inline(string reference, string text) =>
		$"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Sheets.Escape(text)}</t></is></c>";

	/// <summary>
	/// Reads a sheet with options every way the tests compare, each with a reader of its own, as lines: the fields
	/// (<c>GetString</c>, the indexer, <c>GetStringOrNull</c>, <c>GetChars</c> and <c>FieldEquals</c>) with the header's
	/// names, records with string members and with typed ones, dynamic records, a <see cref="CsvDataReader"/>,
	/// <c>ParallelEnumerate</c> of the fields and of records, and the conversion to CSV (<c>GetTextUtf8</c>). A way that
	/// throws ends with the exception.
	/// </summary>
	/// <param name="package">The workbook.</param>
	/// <param name="options">The options.</param>
	/// <param name="async">Whether to open and read asynchronously, where an asynchronous form exists.</param>
	/// <returns>The lines, each starting with the way it was read.</returns>
	private static async Task<List<string>> ReadEveryWayAsync(TestPackage package, ExcelReaderOptions options, bool async)
	{
		var lines = new List<string>();
		using var workbook = await Workbooks.OpenAsync(package, async);
		await AddAsync(lines, "fields", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			if (reader.Options.HasHeader)
			{
				if (async)
				{
					await reader.ReadHeaderAsync();
				}

				add("header " + string.Join("|", reader.Headers));
			}

			while (async ? await reader.ReadAsync() : reader.Read())
			{
				add($"{reader.LineNumber}: {string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Field(reader, i)))}");
			}
		});

		await AddAsync(lines, "records", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			if (async)
			{
				await foreach (var record in reader.GetRecordsAsync<PooledRow>())
				{
					add(Describe(record));
				}
			}
			else
			{
				foreach (var record in reader.GetRecords<PooledRow>())
				{
					add(Describe(record));
				}
			}
		});

		await AddAsync(lines, "typed", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			foreach (var record in reader.GetRecords<PooledTypedRow>())
			{
				add(FormattableString.Invariant($"{record.Shared}|{record.Number:R}|{record.Flag}|{record.Day:O}"));
			}
		});

		await AddAsync(lines, "dynamic", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			if (async)
			{
				await foreach (var record in reader.GetRecordsAsync<CsvDynamicRecord>())
				{
					add(string.Join("|", record.Select(p => $"{p.Key}={p.Value ?? "(null)"}")));
				}
			}
			else
			{
				foreach (var record in reader.GetRecords<CsvDynamicRecord>())
				{
					add(string.Join("|", record.Select(p => $"{p.Key}={p.Value ?? "(null)"}")));
				}
			}
		});

		await AddAsync(lines, "data", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			using var data = new CsvDataReader(reader);
			add("names " + string.Join("|", Enumerable.Range(0, data.FieldCount).Select(data.GetName)));
			while (async ? await data.ReadAsync() : data.Read())
			{
				add(string.Join("|", Enumerable.Range(0, data.FieldCount).Select(i => data.IsDBNull(i) ? "(null)" : $"{data.GetString(i)}/{data.GetValue(i)}")));
			}
		});

		await AddAsync(lines, "parallel", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			foreach (var fields in reader.ParallelEnumerate(r => string.Join("|", Enumerable.Range(0, r.FieldCount).Select(i => Text(r, i))), 4))
			{
				add(fields);
			}
		});

		await AddAsync(lines, "parallel records", async add =>
		{
			using var reader = await workbook.OpenReaderAsync(async, options);
			foreach (var record in reader.ParallelEnumerate<PooledRow>(4))
			{
				add(Describe(record));
			}
		});

		await AddAsync(lines, "csv", async add =>
		{
			var csv = new MemoryStream();
			var settings = new ExcelToCsvOptions { Excel = options };
			if (async)
			{
				await ExcelConvert.ToCsvAsync(package.ToStream(), csv, settings);
			}
			else
			{
				ExcelConvert.ToCsv(package.ToStream(), csv, settings);
			}

			foreach (var line in Encoding.UTF8.GetString(csv.ToArray()).Split('\n'))
			{
				add(line.TrimEnd('\r'));
			}
		});

		return lines;
	}

	/// <summary>
	/// Adds the lines of one way of reading, each after the way's name, and then the exception that ended it, if it threw
	/// one a reading may throw: a value that can't be read (an error cell failing every read), or a column a record lacks.
	/// </summary>
	/// <param name="lines">The lines.</param>
	/// <param name="way">The way's name.</param>
	/// <param name="read">Reads, giving each line to the action it is passed.</param>
	/// <returns>A task that completes when the way has been read.</returns>
	private static async Task AddAsync(List<string> lines, string way, Func<Action<string>, Task> read)
	{
		try
		{
			await read(line => lines.Add($"{way} {line}"));
		}
		catch (Exception e) when (e is FormatException or KeyNotFoundException)
		{
			lines.Add($"{way} threw {e.GetType().Name}: {e.Message}");
		}
	}

	/// <summary>
	/// A field read every way its text can be read: <c>GetString</c>, which the indexer's string, <c>GetChars</c> and
	/// <c>FieldEquals</c> agree with; <c>GetStringOrNull</c>; and whether it is <c>apple</c>. An error cell that fails every
	/// read gives its exception's message, and fails the indexer's string too.
	/// </summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <param name="index">The field.</param>
	/// <returns>The field's line.</returns>
	private static string Field(CsvReader reader, int index)
	{
		string text;
		try
		{
			text = reader.GetString(index);
		}
		catch (FormatException e)
		{
			Assert.Throws<FormatException>(() => reader[index].ToString());

			return "failed: " + e.Message;
		}

		var chars = new char[text.Length];
		var length = reader.GetChars(index, chars);
		Assert.Equal(text, reader[index].ToString());
		Assert.Equal(text, new string(chars, 0, length));
		Assert.True(reader.FieldEquals(index, Encoding.UTF8.GetBytes(text)), $"Field {index}, \"{text}\": FieldEquals disagrees with GetString.");

		return $"{text}/{reader.GetStringOrNull(index) ?? "(null)"}/{reader.FieldEquals(index, "apple"u8)}";
	}

	/// <summary>A field's text, or its exception's message when it fails every read.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <param name="index">The field.</param>
	/// <returns>The text.</returns>
	private static string Text(CsvReader reader, int index)
	{
		try
		{
			return reader.GetString(index);
		}
		catch (FormatException e)
		{
			return "failed: " + e.Message;
		}
	}

	/// <summary>A record's members, joined with <c>|</c>, a null one as <c>(null)</c>.</summary>
	/// <param name="record">The record.</param>
	/// <returns>The text.</returns>
	private static string Describe(PooledRow record) => string.Join(
		"|",
		new[] { record.Shared, record.Inline, record.Unique, record.Number, record.Flag, record.Day, record.Error, record.Long, record.Styled, record.Note }
			.Select(m => m ?? "(null)"));

	/// <summary>Reads a sheet's records, each its line number and its fields, the header first; and what it throws, if it throws.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The lines, and the exception's type and message last.</returns>
	private static List<string> ReadSheet(ExcelWorkbook workbook, ExcelSheet sheet, ExcelReaderOptions options)
	{
		var lines = new List<string>();
		try
		{
			using var reader = workbook.OpenReader(sheet, options);
			if (reader.Options.HasHeader)
			{
				lines.Add("header " + string.Join("|", reader.Headers));
			}

			while (reader.Read())
			{
				lines.Add($"{reader.LineNumber}: {string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => $"{Text(reader, i)}/{reader[i].ToString()}"))}");
			}
		}
		catch (Exception e) when (FuzzTests.IsExpected(e))
		{
			lines.Add($"{e.GetType().Name}: {e.Message}");
		}

		return lines;
	}

	/// <summary>Reads records 0 and 12 of the sheet, which hold the same cells but for <c>Unique</c>'s, each field by its column's name.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="async">Whether to open and read asynchronously.</param>
	/// <returns>The two records' fields.</returns>
	private static async Task<(Dictionary<string, string> First, Dictionary<string, string> Again)> TwoRecordsAsync(ExcelWorkbook workbook, ExcelReaderOptions options, bool async)
	{
		using var reader = await workbook.OpenReaderAsync(async, options);
		var records = new List<Dictionary<string, string>>();
		while (records.Count <= 12 && (async ? await reader.ReadAsync() : reader.Read()))
		{
			records.Add(reader.Headers.Select((name, i) => (name, i)).ToDictionary(c => c.name, c => reader.GetString(c.i)));
		}

		return (records[0], records[12]);
	}

	/// <summary>Whether a reader's equal values of a column are one instance: records 0 and 12's <c>Shared</c> and <c>Inline</c>, which agree.</summary>
	/// <param name="reader">The reader, before its first record.</param>
	/// <returns>Whether they are one instance.</returns>
	private static bool RepeatsOneInstance(CsvReader reader)
	{
		var shared = new List<string>();
		var inline = new List<string>();
		while (shared.Count <= 12 && reader.Read())
		{
			shared.Add(reader.GetString(0));
			inline.Add(reader.GetString(1));
		}

		Assert.Equal(shared[0], shared[12]);
		Assert.Equal(inline[0], inline[12]);
		var one = ReferenceEquals(shared[0], shared[12]);
		Assert.Equal(one, ReferenceEquals(inline[0], inline[12]));

		return one;
	}

	/// <summary>The number of distinct instances among strings.</summary>
	/// <param name="texts">The strings.</param>
	/// <returns>The number of instances.</returns>
	private static int Instances(IEnumerable<string> texts) => texts.Distinct(Identity.Instance).Count();

	/// <summary>The lines of each way of a reading, counted, and the exception that ended a way, if one did.</summary>
	/// <param name="lines">The lines.</param>
	/// <returns>Such as <c>fields 601, records 600, data threw FormatException</c>.</returns>
	private static string Summary(List<string> lines)
	{
		var ways = new List<string>();
		foreach (var way in new[] { "fields", "records", "typed", "dynamic", "data", "parallel", "parallel records", "csv" })
		{
			var mine = lines.Where(l => l.StartsWith(way + " ", StringComparison.Ordinal) && !l.StartsWith(way + " records ", StringComparison.Ordinal)).ToList();
			var threw = mine.LastOrDefault(l => l.StartsWith($"{way} threw ", StringComparison.Ordinal));
			ways.Add(threw is null ? $"{way} {mine.Count}" : $"{way} {mine.Count - 1} then {threw.Substring(way.Length + 1, Math.Min(threw.Length - way.Length - 1, 60))}");
		}

		return string.Join(", ", ways);
	}

	/// <summary>Asserts that two readings' lines are the same, naming the first that differs.</summary>
	/// <param name="name">The reading, for the message.</param>
	/// <param name="unpooled">The lines read with pooling off.</param>
	/// <param name="pooled">The lines read with pooling on.</param>
	private static void AssertSameLines(string name, List<string> unpooled, List<string> pooled)
	{
		for (var i = 0; i < Math.Max(unpooled.Count, pooled.Count); i++)
		{
			var off = i < unpooled.Count ? unpooled[i] : "(no line)";
			var on = i < pooled.Count ? pooled[i] : "(no line)";
			Assert.True(off == on, $"{name}: line {i + 1} of {unpooled.Count} and {pooled.Count}: unpooled \"{off}\", pooled \"{on}\".");
		}
	}

	/// <summary>Compares strings by instance.</summary>
	private sealed class Identity : IEqualityComparer<string>
	{
		/// <summary>The comparer.</summary>
		public static readonly Identity Instance = new();

		/// <inheritdoc/>
		public bool Equals(string? x, string? y) => ReferenceEquals(x, y);

		/// <inheritdoc/>
		public int GetHashCode(string obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
