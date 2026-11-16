using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Excel.Corpus;

/// <summary>A text the corpus tool types into a cell of a template's copy in Excel, as typing enters it.</summary>
/// <param name="Cell">The cell, such as <c>B4</c>.</param>
/// <param name="Text">The text typed.</param>
/// <param name="Probe">Whether the text is one the set asks about; otherwise it fills the probe's row with a text that reads.</param>
/// <param name="Reads">
/// For a probe, what the import reads of the cell: its member's value as <see cref="Written.Show"/> writes it, or
/// <see langword="null"/> when the import refuses it, an error naming the cell.
/// </param>
internal sealed record WrittenEntry(string Cell, string Text, bool Probe, string? Reads);

/// <summary>What the import of a filled copy read.</summary>
/// <param name="Records">The records that read, in the sheet's order.</param>
/// <param name="Errors">The errors.</param>
/// <param name="Complete">Whether every record was read.</param>
internal sealed record WrittenImport(IReadOnlyList<object> Records, IReadOnlyList<ExcelImportError> Errors, bool Complete);

/// <summary>
/// Phase 7's templates in the written set: a template of an order (as step 6's probe has it), of every type of the design's
/// table under a title and a password, of .NET's newer types, of lists and prompts of each form, and a prefilled sheet,
/// each with the texts that the corpus tool types into a copy of it in Excel: a matrix for each column, of texts Excel takes
/// and texts it refuses, each in a row of its own whose other cells hold texts that read.
/// </summary>
internal static partial class Written
{
	/// <summary>
	/// The salt of the set's passwords: the bytes 0 to 15, with which the design's probe hashed <c>secret</c>. A property, not
	/// a field: <see cref="Workbooks"/>, in the class's other file, is made first, and the order of a partial class's static
	/// fields across its files isn't defined.
	/// </summary>
	private static byte[] FixedSalt => [.. Enumerable.Range(0, 16).Select(i => (byte)i)];

	/// <summary>
	/// A member's value as the set compares it: dates and times in ISO 8601 with their fractions only when they have them,
	/// a duration as <c>c</c> writes it, a number's shortest text, an enum's names, and the rest as its invariant text.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The text; <c>null</c> for <see langword="null"/>.</returns>
	public static string Show(object? value) => value switch
	{
		null => "null",
		DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
		DateTimeOffset offset => offset.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
		TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
#if NET
		DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
		TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
#endif
		double number => number.ToString("R", CultureInfo.InvariantCulture),
		float number => number.ToString("R", CultureInfo.InvariantCulture),
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "",
	};

	/// <summary>"template.xlsx": a template of an order, step 6's probe's, filled with its matrix.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Template()
	{
		(string, (string, string?)[])[] probes =
		[
			("Id", [("5", "5"), ("1E+3", "1000"), ("-2147483648", "-2147483648"), ("5.5", null), ("abc", null), ("2147483648", null)]),
			("Status", [("Open", "Open"), ("On hold", "OnHold"), ("open", "Open"), ("Pending", null)]),
			("When", [("2024-01-15 10:30", "2024-01-15T10:30:00"), ("1/15/2024", "2024-01-15T00:00:00"), ("45306", "2024-01-15T00:00:00"), ("2024-01-15T10:30:00", "2024-01-15T10:30:00"), ("abc", null)]),
			("Amount", [("-3", "-3"), ("0.1", "0.1"), ("1E+29", null), ("abc", null)]),
			("Paid", [("TRUE", "True"), ("true", "True"), ("FALSE", "False"), ("yes", "True"), ("1", "True")]),
			("Took", [("36:00:00", "1.12:00:00"), ("0.5", "12:00:00"), ("-1:00", "-01:00:00"), ("abc", null)]),
			("Note", [("00123", "00123"), ("1/2", "1/2"), ("=1+1", "=1+1"), ("1.50", "1.50")]),
			("Big", [("999999999999999", "999999999999999"), ("1000000000000000", "1000000000000000"), ("123456789012345678", "123456789012345000")]),
			("Grade", [("a", "a"), ("1", "1"), ("ab", null)]),
			("Region", [("North", "North"), ("north", "north"), ("East", "East")]),
			("Count", [("50", "50"), ("500", null), ("abc", null)]),
		];

		return new WrittenWorkbook(
			"template.xlsx",
			"a template of an order (ExcelTemplate.Create): each column's validation from its member's type, an enum's dropdown, a list and a prompt by attribute, a [CsvValidate] Excel doesn't know, the header locked and the columns below it unlocked; filled in Excel with step 6's matrix",
			new ExcelWriterOptions(),
			[WrittenSheet.Template<WrittenTemplateOrder>("Orders", new ExcelSheetOptions(), Matrix<WrittenTemplateOrder>(1, WrittenTemplateOrder.Good, probes))]);
	}

	/// <summary>"template-types.xlsx": every type of the design's table that every target has, under a title, with a password.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook TemplateTypes()
	{
		(string, (string, string?)[])[] probes =
		[
			("Paid", [("FALSE", "False"), ("maybe", null)]),
			("Shipped", [("Yes", "True"), ("maybe", null)]),
			("Status", [("On hold", "OnHold"), ("Shut", null)]),
			("Access", [("Read, Write", "Read, Write"), ("Nope", null)]),
			("SByte", [("-128", "-128"), ("128", null)]),
			("Byte", [("255", "255"), ("-1", null)]),
			("Short", [("-32768", "-32768"), ("32768", null)]),
			("UShort", [("65535", "65535"), ("65536", null)]),
			("Int", [("2147483647", "2147483647"), ("1.5", null)]),
			("UInt", [("4294967295", "4294967295"), ("-1", null)]),
			("Long", [("-999999999999999", "-999999999999999"), ("abc", null)]),
			("ULong", [("999999999999999", "999999999999999"), ("-1", null)]),
			("Float", [("1.5", "1.5"), ("abc", null)]),
			("Double", [("-1.5E+300", "-1.5E+300"), ("abc", null)]),
			("Decimal", [("79228162514264", "79228162514264"), ("8E+28", null)]),
			("When", [("1/15/2024 10:30", "2024-01-15T10:30:00"), ("abc", null)]),
			("Took", [("36:00:00", "1.12:00:00"), ("abc", null)]),
			("Stamp", [("2024-01-15T10:30:00+01:00", "2024-01-15T10:30:00+01:00"), ("abc", null)]),
			("Grade", [("a", "a"), ("ab", null)]),
			("Name", [("anything at all", "anything at all")]),
			("Key", [("6f9619ff-8b86-d011-b42d-00c04fc964ff", "6f9619ff-8b86-d011-b42d-00c04fc964ff"), ("abc", null)]),
			("Site", [("https://example.com/", "https://example.com/")]),
			("Maybe", [("7", "7"), ("x", null)]),
			("Dotted", [("2024-01-15", "2024-01-15T00:00:00"), ("15.01.2024", "2024-01-15T00:00:00")]),
			("Priced", [("1234.5", "1234.5"), ("abc", null)]),
		];

		return new WrittenWorkbook(
			"template-types.xlsx",
			"a template of every type of the design's table that every target has, booleans' values, flags, offsets as text, a translated [CsvFormat] and a column's own format, under a title, protected with a password (its salt fixed); filled in Excel with a text each column takes and one it refuses",
			new ExcelWriterOptions(),
			[WrittenSheet.Template<WrittenTemplateTypes>("Types", new ExcelSheetOptions { Title = "Every type", Password = "secret" }, Matrix<WrittenTemplateTypes>(2, WrittenTemplateTypes.Good, probes))])
		{
			Salt = FixedSalt,
		};
	}

#if NET
	/// <summary>"template-types-net.xlsx": .NET's newer types of the design's table, <see cref="DateOnly"/> and <see cref="TimeOnly"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook TemplateTypesNet()
	{
		(string, (string, string?)[])[] probes =
		[
			("Day", [("2024-01-15", "2024-01-15"), ("2024-01-15 10:30", "2024-01-15"), ("2024-01-15T10:30:00", "2024-01-15"), ("abc", null)]),
			("Time", [("10:30", "10:30:00"), ("23:59:59", "23:59:59"), ("abc", null)]),
		];

		return new WrittenWorkbook(
			"template-types-net.xlsx",
			"a template of DateOnly and TimeOnly: dates that take a time, read as their dates, and times of day; filled in Excel",
			new ExcelWriterOptions(),
			[WrittenSheet.Template<WrittenTemplateTypesNet>("Types", new ExcelSheetOptions(), Matrix<WrittenTemplateTypesNet>(1, WrittenTemplateTypesNet.Good, probes))]);
	}
#endif

	/// <summary>"template-lists.xlsx": lists of each form and prompts, the template's sheet named Lists, so its lists sheet is Lists2.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook TemplateLists()
	{
		(string, (string, string?)[])[] probes =
		[
			("Color", [("Red", "Red"), ("red", "red"), ("Purple", "Purple")]),
			("Quote", [("a\"b", "a\"b"), ("a", "a")]),
			("Region", [("North, East", "North, East"), ("north, east", "north, east"), ("East", "East")]),
			("Padded", [(" padded ", " padded "), ("padded", "padded")]),
			("Many", [("Region07", "Region07"), ("region07", "region07"), ("Region31", "Region31")]),
			("Repeated", [("y", "y"), ("z", "z")]),
			("TheCustomersPreferredDeliveryRegion", [("Far", "Far"), ("Mars", "Mars")]),
			("Notes", [("anything", "anything")]),
		];

		return new WrittenWorkbook(
			"template-lists.xlsx",
			"a template of lists of each form: inline, matching in case, a quote counted once, repeats listed once; in the very hidden lists sheet, matching ignoring case, those with commas, spaces at either end, or of more than 255 characters; prompts, a title cut to 32 characters, a line break, and a prompt alone; filled in Excel",
			new ExcelWriterOptions(),
			[WrittenSheet.Template<WrittenTemplateLists>("Lists", new ExcelSheetOptions(), Matrix<WrittenTemplateLists>(1, WrittenTemplateLists.Good, probes))]);
	}

	/// <summary>"template-prefilled.xlsx": a sheet of orders to edit, validated and protected under a title, with a password.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook TemplatePrefilled()
	{
		WrittenTemplateOrder[] orders =
		[
			new() { Id = 1, Status = WrittenTemplateStatus.Open, When = new DateTime(2024, 1, 15, 10, 30, 0), Amount = 9.5m, Paid = true, Took = TimeSpan.FromHours(1.5), Note = "first", Big = 123456789012345, Grade = 'A', Region = "North", Count = 5 },
			new() { Id = 2, Status = WrittenTemplateStatus.OnHold, When = new DateTime(2024, 1, 16), Amount = 0.25m, Paid = false, Took = TimeSpan.FromHours(36), Note = "00123", Big = 1, Grade = 'B', Region = "South", Count = 0 },
			new() { Id = 3, Status = WrittenTemplateStatus.Closed, When = new DateTime(2024, 1, 17), Amount = 2m, Paid = true, Took = TimeSpan.Zero, Note = "", Big = 0, Grade = 'C', Region = "North", Count = 100 },
		];

		// Edits: a status no list has, a long of 16 digits, a date changed, and an order added below the others.
		List<WrittenEntry> edits =
		[
			new("B4", "Pending", true, null),
			new("H5", "1000000000000000", true, "1000000000000000"),
			new("C3", "2024-02-01", true, "2024-02-01T00:00:00"),
			.. Row<WrittenTemplateOrder>(6, WrittenTemplateOrder.Good, "Count", "7", "7"),
		];

		return new WrittenWorkbook(
			"template-prefilled.xlsx",
			"a sheet of orders to edit and import again: WriteSheet<T> under a title, validated and protected with a password (its salt fixed), the records' cells unlocked; edited in Excel",
			new ExcelWriterOptions(),
			[WrittenSheet.Records("Orders", orders, look: new() { Options = new() { Title = "Orders", Validate = true, Protect = true, Password = "secret" } }, fill: edits)])
		{
			Salt = FixedSalt,
		};
	}

	/// <summary>
	/// A template's matrix: each probe text in a row of its own from the row below the header, whose other cells hold the
	/// columns' good texts, and an empty row after each column's probes, which the import skips.
	/// </summary>
	/// <typeparam name="T">The record type, whose public properties are the columns, in order.</typeparam>
	/// <param name="headerRow">The header's row.</param>
	/// <param name="good">A text each column takes and reads, by its header name.</param>
	/// <param name="probes">Each column's probe texts, with what the import reads of each.</param>
	/// <returns>The entries.</returns>
	private static List<WrittenEntry> Matrix<T>(int headerRow, IReadOnlyDictionary<string, string> good, (string Column, (string Text, string? Reads)[] Texts)[] probes)
	{
		var entries = new List<WrittenEntry>();
		var row = headerRow + 1;
		foreach (var (column, texts) in probes)
		{
			foreach (var (text, reads) in texts)
			{
				entries.AddRange(Row<T>(row, good, column, text, reads));
				row++;
			}

			row++;
		}

		return entries;
	}

	/// <summary>A row of a matrix: a probe text in its column, and each other column's good text.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="row">The row.</param>
	/// <param name="good">A text each column takes and reads, by its header name.</param>
	/// <param name="column">The probe's column's header name.</param>
	/// <param name="text">The probe text.</param>
	/// <param name="reads">What the import reads of it, or <see langword="null"/> when it refuses it.</param>
	/// <returns>The row's entries.</returns>
	private static IEnumerable<WrittenEntry> Row<T>(int row, IReadOnlyDictionary<string, string> good, string column, string text, string? reads)
	{
		var names = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.Name).ToList();
		if (!names.Contains(column))
		{
			throw new InvalidOperationException($"{typeof(T).Name} has no column {column}.");
		}

		for (var c = 0; c < names.Count; c++)
		{
			var cell = ColumnLetters(c) + row.ToString(CultureInfo.InvariantCulture);

			yield return names[c] == column ? new WrittenEntry(cell, text, true, reads) : new WrittenEntry(cell, good[names[c]], false, null);
		}
	}

	/// <summary>A 0-based column's letters, such as <c>A</c> or <c>AB</c>.</summary>
	/// <param name="column">The column.</param>
	/// <returns>The letters.</returns>
	private static string ColumnLetters(int column)
	{
		var letters = "";
		for (var n = column + 1; n > 0; n = (n - 1) / 26)
		{
			letters = (char)('A' + (n - 1) % 26) + letters;
		}

		return letters;
	}
}

/// <summary>An order's state, for the templates.</summary>
public enum WrittenTemplateStatus
{
	/// <summary>Open.</summary>
	Open,

	/// <summary>On hold, written by its other name.</summary>
	[CsvEnumName("On hold")]
	OnHold,

	/// <summary>Closed.</summary>
	Closed,
}

/// <summary>What a user may do, as flags: no dropdown.</summary>
[Flags]
public enum WrittenTemplateAccess
{
	/// <summary>Nothing.</summary>
	None = 0,

	/// <summary>Read.</summary>
	Read = 1,

	/// <summary>Write.</summary>
	Write = 2,
}

/// <summary>An order, as step 6's probe had it: a column of each common kind of validation, a list, and a check Excel doesn't know.</summary>
[CsvRecord]
public sealed partial class WrittenTemplateOrder
{
	/// <summary>A text each column takes and reads.</summary>
	internal static readonly Dictionary<string, string> Good = new()
	{
		["Id"] = "1", ["Status"] = "Open", ["When"] = "2024-01-15", ["Amount"] = "9.5", ["Paid"] = "TRUE", ["Took"] = "10:30",
		["Note"] = "x", ["Big"] = "123", ["Grade"] = "a", ["Region"] = "North", ["Count"] = "5",
	};

	/// <summary>Whole numbers.</summary>
	public int Id { get; set; }

	/// <summary>A dropdown of the enum's names.</summary>
	public WrittenTemplateStatus Status { get; set; }

	/// <summary>A date and time.</summary>
	public DateTime When { get; set; }

	/// <summary>Numbers.</summary>
	public decimal Amount { get; set; }

	/// <summary>A dropdown of booleans.</summary>
	public bool Paid { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>Text.</summary>
	public string Note { get; set; } = "";

	/// <summary>Whole numbers of 15 digits.</summary>
	public long Big { get; set; }

	/// <summary>One character.</summary>
	public char Grade { get; set; }

	/// <summary>A list, which the import doesn't check.</summary>
	[ExcelColumn(List = new[] { "North", "South" }, Prompt = "The order's region.")]
	public string Region { get; set; } = "";

	/// <summary>Whole numbers, which the import checks from 0 to 100.</summary>
	[CsvValidate(nameof(IsSmall), Message = "Count must be 0 to 100.")]
	public int Count { get; set; }

	/// <summary>Whether a count is from 0 to 100.</summary>
	/// <param name="value">The count.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsSmall(int value) => value is >= 0 and <= 100;
}

/// <summary>Every type of the design's table that every target has.</summary>
[CsvRecord]
public sealed partial class WrittenTemplateTypes
{
	/// <summary>A text each column takes and reads.</summary>
	internal static readonly Dictionary<string, string> Good = new()
	{
		["Paid"] = "TRUE", ["Shipped"] = "Yes", ["Status"] = "Open", ["Access"] = "Read", ["SByte"] = "1", ["Byte"] = "1", ["Short"] = "1",
		["UShort"] = "1", ["Int"] = "1", ["UInt"] = "1", ["Long"] = "1", ["ULong"] = "1", ["Float"] = "1", ["Double"] = "1", ["Decimal"] = "1",
		["When"] = "2024-01-15", ["Took"] = "10:30", ["Stamp"] = "2024-01-15T10:30:00+01:00", ["Grade"] = "a", ["Name"] = "x",
		["Key"] = "6f9619ff-8b86-d011-b42d-00c04fc964ff", ["Site"] = "https://example.com/", ["Maybe"] = "1", ["Dotted"] = "2024-01-15", ["Priced"] = "1",
	};

	/// <summary>A dropdown of booleans.</summary>
	public bool Paid { get; set; }

	/// <summary>A dropdown of its first values.</summary>
	[CsvBooleanValues(new[] { "Yes" }, new[] { "No" })]
	public bool Shipped { get; set; }

	/// <summary>A dropdown of the enum's names as written.</summary>
	public WrittenTemplateStatus Status { get; set; }

	/// <summary>Flags: no validation.</summary>
	public WrittenTemplateAccess Access { get; set; }

	/// <summary>Whole numbers.</summary>
	public sbyte SByte { get; set; }

	/// <summary>Whole numbers.</summary>
	public byte Byte { get; set; }

	/// <summary>Whole numbers.</summary>
	public short Short { get; set; }

	/// <summary>Whole numbers.</summary>
	public ushort UShort { get; set; }

	/// <summary>Whole numbers.</summary>
	public int Int { get; set; }

	/// <summary>Whole numbers.</summary>
	public uint UInt { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public long Long { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public ulong ULong { get; set; }

	/// <summary>Numbers.</summary>
	public float Float { get; set; }

	/// <summary>Numbers.</summary>
	public double Double { get; set; }

	/// <summary>Numbers.</summary>
	public decimal Decimal { get; set; }

	/// <summary>A date and time.</summary>
	public DateTime When { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>Text, as DateTimeOffsets is by default.</summary>
	public DateTimeOffset Stamp { get; set; }

	/// <summary>One character.</summary>
	public char Grade { get; set; }

	/// <summary>Text.</summary>
	public string Name { get; set; } = "";

	/// <summary>Text.</summary>
	public Guid Key { get; set; }

	/// <summary>Text.</summary>
	public Uri? Site { get; set; }

	/// <summary>Whole numbers, or none.</summary>
	public int? Maybe { get; set; }

	/// <summary>A format Phase 4 translates.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime Dotted { get; set; }

	/// <summary>Numbers, in the column's own format.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Priced { get; set; }
}

#if NET
/// <summary>.NET's newer types of the design's table.</summary>
[CsvRecord]
public sealed partial class WrittenTemplateTypesNet
{
	/// <summary>A text each column takes and reads.</summary>
	internal static readonly Dictionary<string, string> Good = new() { ["Day"] = "2024-01-15", ["Time"] = "10:30" };

	/// <summary>A date, which takes a date and time.</summary>
	public DateOnly Day { get; set; }

	/// <summary>A time of day.</summary>
	public TimeOnly Time { get; set; }
}
#endif

/// <summary>Lists of each form, and prompts.</summary>
[CsvRecord]
public sealed partial class WrittenTemplateLists
{
	/// <summary>A text each column takes and reads.</summary>
	internal static readonly Dictionary<string, string> Good = new()
	{
		["Color"] = "Red", ["Quote"] = "c", ["Region"] = "South, West", ["Padded"] = "plain", ["Many"] = "Region01", ["Repeated"] = "x",
		["TheCustomersPreferredDeliveryRegion"] = "Near", ["Notes"] = "n",
	};

	/// <summary>Inline, matching in case.</summary>
	[ExcelColumn(List = new[] { "Red", "Green", "Blue" })]
	public string Color { get; set; } = "";

	/// <summary>Inline, a quote doubled.</summary>
	[ExcelColumn(List = new[] { "a\"b", "c" })]
	public string Quote { get; set; } = "";

	/// <summary>In the lists sheet: values with commas.</summary>
	[ExcelColumn(List = new[] { "North, East", "South, West" })]
	public string Region { get; set; } = "";

	/// <summary>In the lists sheet: a value with spaces at either end.</summary>
	[ExcelColumn(List = new[] { " padded ", "plain" })]
	public string Padded { get; set; } = "";

	/// <summary>In the lists sheet: 30 values, of more than 255 characters.</summary>
	[ExcelColumn(List = new[]
	{
		"Region01", "Region02", "Region03", "Region04", "Region05", "Region06", "Region07", "Region08", "Region09", "Region10",
		"Region11", "Region12", "Region13", "Region14", "Region15", "Region16", "Region17", "Region18", "Region19", "Region20",
		"Region21", "Region22", "Region23", "Region24", "Region25", "Region26", "Region27", "Region28", "Region29", "Region30",
	})]
	public string Many { get; set; } = "";

	/// <summary>Inline, a value repeated, listed once.</summary>
	[ExcelColumn(List = new[] { "x", "y", "x" })]
	public string Repeated { get; set; } = "";

	/// <summary>A header of 35 characters, its prompt's title cut to 32; a prompt over two lines.</summary>
	[ExcelColumn(List = new[] { "Near", "Far" }, Prompt = "Where we deliver.\nPick one.")]
	public string TheCustomersPreferredDeliveryRegion { get; set; } = "";

	/// <summary>A prompt alone, a validation of any value.</summary>
	[ExcelColumn(Prompt = "Anything you like.")]
	public string Notes { get; set; } = "";
}
