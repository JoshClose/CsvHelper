using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Excel.Corpus;

/// <summary>
/// The <c>written</c> content set, defined once: workbooks CsvHelper.Excel writes. The corpus tool's <c>written</c> command
/// writes each, asks Excel through COM what it reads in it, and commits the answers with the workbook's
/// <see cref="PartsHash"/>; the tests link this file, write the same workbooks again, and compare them with the answers.
/// </summary>
/// <remarks>
/// <para>
/// The workbooks hold every type of Phase 4's "What a field becomes" and its edge values, formatted members in the invariant
/// culture and in others, formulas, escaped and long text, shared strings, <see cref="ExcelRowLimit.NewSheet"/>, and sheet
/// names Excel accepts, in Phase 4's look; and Phase 5's presentation: the default look, titles, frozen panes, columns'
/// formats, widths, wrap and hidden columns, auto-fit, filters, tables, merged cells and links, each sheet's look given by
/// <see cref="WrittenLook"/>. Nothing in them depends on the machine: no time zone, no current culture, no clock.
/// </para>
/// <para>
/// A workbook's bytes depend on the target only through deflate, which differs between runtimes, so a workbook is pinned by
/// its parts (<see cref="PartsHash"/>). The one whose formats read culture data other than the invariant culture's,
/// <c>cultures.xlsx</c>, also depends on the runtime's data (ICU's on .NET, of a version each machine has); its answers
/// carry that data's fingerprint (<see cref="CultureData"/>). It and <c>types-net.xlsx</c> use .NET's newer types, so
/// .NET Framework has neither.
/// </para>
/// </remarks>
internal static class Written
{
	/// <summary>The values of the formatted columns: a morning, an evening and a late night, with seconds and milliseconds.</summary>
	private static readonly DateTime[] Samples = [new(2024, 1, 15, 10, 30, 5, 123), new(2024, 3, 9, 22, 5, 9, 7), new(2024, 9, 25, 23, 58, 58, 888)];

	/// <summary>
	/// Phase 4's look: no bold or frozen header and no auto-fit, as the Phase 4 workbooks were written and answered, so that
	/// Phase 5's defaults leave them, and their pinned parts, as they were. Their options have no links too
	/// (<see cref="ExcelLinks.None"/>), so that a URI is the text it was.
	/// </summary>
	private static readonly ExcelSheetOptions Phase4Look = new() { BoldHeader = false, FreezeHeader = false, AutoFit = 0 };

	/// <summary>The default options with Phase 4's look, and no links.</summary>
	private static readonly ExcelWriterOptions Phase4Options = new() { Sheets = Phase4Look, Links = ExcelLinks.None };

	/// <summary><see cref="ExcelWorkbookWriter"/>'s internal row limit, which a test sets to start new sheets after a few rows.</summary>
	private static readonly PropertyInfo MaxRows = typeof(ExcelWorkbookWriter).GetProperty("MaxRows", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("ExcelWorkbookWriter has no internal MaxRows.");

	/// <summary>The workbooks, in the order the tool writes them.</summary>
	public static IReadOnlyList<WrittenWorkbook> Workbooks { get; } =
	[
		Types(),
#if NET
		NewerTypes(),
#endif
		Options(),
		Text("text.xlsx", ExcelStrings.Inline),
		Text("text-shared.xlsx", ExcelStrings.Shared),
		Formulas(),
		Names(),
		NewSheets(),
		Formats(),
#if NET
		Cultures(),
#endif
		Look(),
		ColumnsLook(),
		AutoFit(),
		Filters(),
		Tables(),
		Merges(),
		Links(),
		AllLinks(),
		NewSheetsLook(),
		Presentation(),
	];

	/// <summary>Writes a workbook synchronously, into memory.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>Its bytes.</returns>
	public static byte[] Write(WrittenWorkbook workbook)
	{
		using var stream = new MemoryStream();
		using (var writer = ExcelWorkbookWriter.Create(stream, workbook.Options, leaveOpen: true))
		{
			Prepare(writer, workbook);
			foreach (var sheet in workbook.Sheets)
			{
				sheet.Write(writer);
			}
		}

		return stream.ToArray();
	}

	/// <summary>Writes a workbook asynchronously, into memory: the same parts as <see cref="Write"/>.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>Its bytes.</returns>
	public static async Task<byte[]> WriteAsync(WrittenWorkbook workbook)
	{
		using var stream = new MemoryStream();
		var writer = ExcelWorkbookWriter.Create(stream, workbook.Options, leaveOpen: true);
		await using (writer)
		{
			Prepare(writer, workbook);
			foreach (var sheet in workbook.Sheets)
			{
				await sheet.WriteAsync(writer);
			}
		}

		return stream.ToArray();
	}

	/// <summary>
	/// The SHA-256 of a workbook's parts, as lowercase hex: each entry's name and its bytes, inflated, with their lengths, in
	/// the package's order. Deflate's output differs between runtimes (zlib-ng on .NET 9 and later, zlib on .NET 8, and
	/// .NET Framework's own), so the file's bytes do too; its parts don't.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The hash.</returns>
	public static string PartsHash(byte[] workbook)
	{
		using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
		using var all = new MemoryStream();
		foreach (var entry in archive.Entries)
		{
			var name = Encoding.UTF8.GetBytes(entry.FullName);
			using var part = new MemoryStream();
			using (var stream = entry.Open())
			{
				stream.CopyTo(part);
			}

			AppendLength(all, name.Length);
			all.Write(name, 0, name.Length);
			AppendLength(all, part.Length);
			part.WriteTo(all);
		}

		return Hash(all.ToArray());
	}

	/// <summary>
	/// A fingerprint of the culture data a workbook's formats and numbers read, as the runtime has it: for each culture, its
	/// id, calendar, date and time patterns, separators, designators and names, and its numbers' signs and separators.
	/// </summary>
	/// <param name="names">The cultures' names.</param>
	/// <returns>The SHA-256 of the data, as lowercase hex.</returns>
	public static string CultureData(IEnumerable<string> names)
	{
		var text = new StringBuilder();
		foreach (var name in names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
		{
			var culture = CultureInfo.GetCultureInfo(name);
			var dates = culture.DateTimeFormat;
			var numbers = culture.NumberFormat;
			string[] fields =
			[
				name, culture.LCID.ToString(CultureInfo.InvariantCulture), dates.Calendar.GetType().FullName ?? "",
				dates.ShortDatePattern, dates.LongDatePattern, dates.ShortTimePattern, dates.LongTimePattern, dates.FullDateTimePattern,
				dates.MonthDayPattern, dates.YearMonthPattern, dates.RFC1123Pattern, dates.SortableDateTimePattern,
				dates.UniversalSortableDateTimePattern, dates.DateSeparator, dates.TimeSeparator, dates.AMDesignator, dates.PMDesignator,
				string.Join("|", dates.MonthNames), string.Join("|", dates.AbbreviatedMonthNames), string.Join("|", dates.MonthGenitiveNames),
				string.Join("|", dates.AbbreviatedMonthGenitiveNames), string.Join("|", dates.DayNames), string.Join("|", dates.AbbreviatedDayNames),
				numbers.NegativeSign, numbers.PositiveSign, numbers.NumberDecimalSeparator, numbers.NumberGroupSeparator, numbers.NaNSymbol,
				numbers.PositiveInfinitySymbol, numbers.NegativeInfinitySymbol,
			];
			foreach (var field in fields)
			{
				text.Append(field).Append('\n');
			}
		}

		return Hash(Encoding.UTF8.GetBytes(text.ToString()));
	}

	/// <summary>Sets a workbook's row limit for the test of <see cref="ExcelRowLimit.NewSheet"/>, before its first sheet.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="workbook">The workbook.</param>
	private static void Prepare(ExcelWorkbookWriter writer, WrittenWorkbook workbook)
	{
		if (workbook.MaxRows is { } rows)
		{
			MaxRows.SetValue(writer, rows);
		}
	}

	/// <summary>Appends a length to a hash's input, as 8 bytes, least significant first.</summary>
	/// <param name="stream">The input.</param>
	/// <param name="length">The length.</param>
	private static void AppendLength(MemoryStream stream, long length)
	{
		for (var i = 0; i < 8; i++)
		{
			stream.WriteByte((byte)(length >> (8 * i)));
		}
	}

	/// <summary>The SHA-256 of bytes, as lowercase hex.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash.</returns>
	private static string Hash(byte[] bytes)
	{
		using var sha = SHA256.Create();

		return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
	}

	/// <summary>A text of characters given by their code points.</summary>
	/// <param name="codes">The code points.</param>
	/// <returns>The text.</returns>
	private static string Chars(params int[] codes) => string.Concat(codes.Select(char.ConvertFromUtf32));

	/// <summary>A text of UTF-16 code units, lone surrogates and noncharacters included.</summary>
	/// <param name="units">The code units.</param>
	/// <returns>The text.</returns>
	private static string Units(params int[] units) => new(units.Select(u => (char)u).ToArray());

	/// <summary>A record whose every member of a value's type holds the value.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="value">The value.</param>
	/// <returns>The record.</returns>
	private static T Filled<T>(object value)
		where T : new()
	{
		var record = new T();
		foreach (var property in typeof(T).GetProperties().Where(p => p.PropertyType == value.GetType()))
		{
			property.SetValue(record, value);
		}

		return record;
	}

	/// <summary>"types.xlsx": every type of "What a field becomes" that .NET Framework has, at its edges.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Types()
	{
		var guid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");
		WrittenNumber[] numbers =
		[
			new() { Case = "zero", Int64 = 0, UInt64 = 0, Int32 = 0, Decimal = 0m, Double = 0, Single = 0 },
			new() { Case = "negative", Int64 = -42, Int32 = -42, Decimal = -1234.5m, Double = -1234.5, Single = -2.5f },
			new() { Case = "negative zero", Double = -0.0, Single = -0.0f },
			new() { Case = "15 digits", Int64 = 123456789012345, UInt64 = 123456789012345, Decimal = 123456789012345m, Double = 123456789012345 },
			new() { Case = "16 digits", Int64 = 1234567890123456, UInt64 = 1234567890123456, Decimal = 1234567890123456m, Double = 1234567890123456 },
			new() { Case = "zeros at the end", Int64 = 1000000000000000000, UInt64 = 10000000000000000000, Decimal = 1.50m, Double = 1E+300 },
			new() { Case = "largest", Int64 = long.MaxValue, UInt64 = ulong.MaxValue, Int32 = int.MaxValue, Decimal = decimal.MaxValue, Double = double.MaxValue, Single = float.MaxValue },
			new() { Case = "least", Int64 = long.MinValue, Int32 = int.MinValue, Decimal = decimal.MinValue, Double = double.MinValue, Single = float.MinValue },
			new() { Case = "17 digits", Decimal = 0.1m, Double = 0.30000000000000004, Single = 0.1f },
			new() { Case = "1 and an ulp", Double = 1.0000000000000002 },
			new() { Case = "small", Decimal = 0.00001m, Double = 1E-05, Single = 1E-05f },
			new() { Case = "large", Double = 1.5E+20, Single = 1.5E+20f },
			new() { Case = "least normal", Double = 2.2250738585072014E-308, Single = float.Epsilon },
			new() { Case = "subnormal", Double = double.Epsilon },
			new() { Case = "not a number", Double = double.NaN, Single = float.NaN },
			new() { Case = "infinities", Double = double.PositiveInfinity, Single = float.NegativeInfinity },
		];
		WrittenInteger[] integers =
		[
			new() { Case = "least", SByte = sbyte.MinValue, Byte = byte.MinValue, Int16 = short.MinValue, UInt16 = ushort.MinValue, UInt32 = uint.MinValue },
			new() { Case = "largest", SByte = sbyte.MaxValue, Byte = byte.MaxValue, Int16 = short.MaxValue, UInt16 = ushort.MaxValue, UInt32 = uint.MaxValue },
		];
		WrittenDate[] dates =
		[
			new() { Case = "milliseconds", DateTime = new(2024, 12, 31, 23, 59, 59, 999), TimeSpan = TimeSpan.FromDays(1000) + TimeSpan.FromMilliseconds(5), Offset = new(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)) },
			new() { Case = "midnight", DateTime = new(2024, 1, 15), TimeSpan = TimeSpan.Zero, Offset = new(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(-5)) },
			new() { Case = "seconds", DateTime = new(2024, 1, 15, 10, 30, 0), TimeSpan = new(1, 12, 0, 0), Offset = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero) },
			new() { Case = "Excel's first day", DateTime = new(1900, 1, 1), TimeSpan = TimeSpan.FromMilliseconds(500) },
			new() { Case = "before 1900-02-29", DateTime = new(1900, 2, 28), TimeSpan = TimeSpan.FromHours(-1) },
			new() { Case = "after 1900-02-29", DateTime = new(1900, 3, 1, 0, 0, 0, 1), TimeSpan = TimeSpan.FromTicks(-4999) },
			new() { Case = "Excel's last second", DateTime = new(9999, 12, 31, 23, 59, 59) },
			new() { Case = "DateTime's last tick", DateTime = DateTime.MaxValue },
			new() { Case = "before 1900", DateTime = new(1899, 12, 31) },
			new() { Case = "DateTime's first", DateTime = DateTime.MinValue },
			new() { Case = "UTC", DateTime = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc) },
			new() { Case = "local", DateTime = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Local) },
			new() { Case = "less than half a millisecond", DateTime = new DateTime(2024, 1, 15, 10, 30, 0).AddTicks(4999) },
			new() { Case = "half a millisecond", DateTime = new DateTime(2024, 1, 15, 10, 30, 0).AddTicks(5000) },
		];
		WrittenOther[] others =
		[
			new() { Case = "set", Flag = true, Letter = 'x', Id = guid, Day = DayOfWeek.Monday, Link = new Uri("https://example.com/a?b=c&d=e"), Note = "plain" },
			new() { Case = "other", Flag = false, Letter = '0', Id = Guid.Empty, Day = DayOfWeek.Sunday, Link = new Uri("urn:isbn:0451450523"), Note = "" },
			new() { Case = "null" },
		];
		WrittenWidths[] widths =
		[
			new() { Case = "narrow first", When = new(2024, 1, 15), Took = TimeSpan.FromHours(1) },
			new() { Case = "wider after", When = new(2024, 1, 15, 10, 30, 0, 123), Took = TimeSpan.MaxValue },
		];

		return new WrittenWorkbook(
			"types.xlsx",
			"every type that .NET Framework has, at its edges, with the default options",
			Phase4Options,
			[
				WrittenSheet.Records("Numbers", numbers),
				WrittenSheet.Records("Integers", integers),
				WrittenSheet.Records("Dates", dates),
				WrittenSheet.Records("Other", others),
				WrittenSheet.Records("Widths", widths),
			]);
	}

#if NET
	/// <summary>"types-net.xlsx": .NET's newer types, and their formats in the invariant culture.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook NewerTypes()
	{
		WrittenNewer[] newer =
		[
			new() { Case = "first", Date = new(2024, 1, 15), Time = new(23, 59, 59, 999), Half = (Half)1.5, Int128 = 42, UInt128 = 42UL },
			new() { Case = "Excel's first day", Date = new(1900, 1, 1), Time = TimeOnly.MinValue, Half = Half.MaxValue, Int128 = Int128.MaxValue, UInt128 = UInt128.MaxValue },
			new() { Case = "before 1900", Date = DateOnly.MinValue, Time = new(10, 30), Half = Half.NaN, Int128 = Int128.MinValue, UInt128 = UInt128.MinValue },
			new() { Case = "noon", Date = new(1900, 2, 28), Time = new(12, 0), Half = Half.Epsilon, Int128 = 123456789012345, UInt128 = 1234567890123456UL },
			new() { Case = "midnight's last tick", Date = DateOnly.MaxValue, Time = TimeOnly.MaxValue, Half = Half.NegativeInfinity },
		];

		return new WrittenWorkbook(
			"types-net.xlsx",
			".NET's newer types (DateOnly, TimeOnly, Half, Int128, UInt128), and DateOnly's and TimeOnly's formats in the invariant culture",
			Phase4Options,
			[
				WrittenSheet.Records("Newer", newer),
				WrittenSheet.Records("Formats", Samples.Select(s => new WrittenNewerFormats { Day = DateOnly.FromDateTime(s), Rfc = DateOnly.FromDateTime(s), Long = DateOnly.FromDateTime(s), Clock = TimeOnly.FromDateTime(s), Precise = TimeOnly.FromDateTime(s), Short = TimeOnly.FromDateTime(s), TimeRfc = TimeOnly.FromDateTime(s) }).ToArray()),
			]);
	}
#endif

	/// <summary>"options.xlsx": <see cref="ExcelLongNumbers.Number"/> and <see cref="ExcelDateTimeOffsets.Utc"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Options()
	{
		WrittenNumber[] numbers =
		[
			new() { Case = "16 digits", Int64 = 1234567890123456, UInt64 = 1234567890123456, Decimal = 1234567890123456m },
			new() { Case = "17 digits", Int64 = 12345678901234567, UInt64 = 12345678901234567, Decimal = 1234567890123456.7m },
			new() { Case = "largest", Int64 = long.MaxValue, UInt64 = ulong.MaxValue, Decimal = decimal.MaxValue },
			new() { Case = "least", Int64 = long.MinValue, Decimal = decimal.MinValue },
		];
		WrittenOffset[] offsets =
		[
			new() { Case = "milliseconds", Offset = new(2024, 1, 15, 10, 30, 0, 123, TimeSpan.FromHours(2)), Formatted = new(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)) },
			new() { Case = "west", Offset = new(2024, 1, 15, 22, 30, 0, TimeSpan.FromHours(-5)), Formatted = new(2024, 1, 15, 22, 30, 0, TimeSpan.FromHours(-5)) },
			new() { Case = "UTC", Offset = new(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), Formatted = new(2024, 1, 15, 0, 0, 0, TimeSpan.Zero) },
			new() { Case = "before 1900 in UTC", Offset = new(1900, 1, 1, 0, 30, 0, TimeSpan.FromHours(1)), Formatted = new(1900, 1, 1, 0, 30, 0, TimeSpan.FromHours(1)) },
		];

		return new WrittenWorkbook(
			"options.xlsx",
			"LongNumbers = Number and DateTimeOffsets = Utc",
			new ExcelWriterOptions { Sheets = Phase4Look, Links = ExcelLinks.None, LongNumbers = ExcelLongNumbers.Number, DateTimeOffsets = ExcelDateTimeOffsets.Utc },
			[WrittenSheet.Records("Numbers", numbers), WrittenSheet.Records("Offsets", offsets)]);
	}

	/// <summary>"text.xlsx" and "text-shared.xlsx": texts that need escaping, and the longest cell, written by hand.</summary>
	/// <param name="name">The workbook's name.</param>
	/// <param name="strings">Where its strings go.</param>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Text(string name, ExcelStrings strings)
	{
		object?[][] rows =
		[
			["Case", "Text"],
			["tab", "a\tb"],
			["LF", "a\nb"],
			["CR", "a\rb"],
			["CR LF", "a\r\nb"],
			["C0 controls", Units([.. Enumerable.Range(0, 32)])],
			["C1 controls", "a" + Units(0x7F, 0x80, 0x85, 0x9F) + "b"],
			["an escape", "_x0041_"],
			["an escape in lowercase", "_x004a_"],
			["a capital X", "_X0041_"],
			["no closing underscore", "_x0041"],
			["not hex", "_x004G_"],
			["an escaped escape", "_x005F_x0041_"],
			["noncharacters", "a" + Units(0xFFFE) + "b" + Units(0xFFFF)],
			["an emoji", Chars(0x1F600)],
			["Japanese", "日本語"],
			["entities", "&<>\"'"],
			["spaces at the ends", "  padded  "],
			["tabs at the ends", "\tx\t"],
			["LFs at the ends", "\nx\n"],
			["a lone surrogate", "a" + Units(0xD800) + "b"],
			["invalid UTF-8", new byte[] { (byte)'a', 0xFF, (byte)'b' }],
			["leading zeros", "00123"],
			["a decimal", "1.5"],
			["an exponent", "1E5"],
			["a sign", "-1"],
			["a point", ".5"],
			["a formula", "=1+1"],
			["an error", "#N/A"],
			["a boolean", "TRUE"],
			["a date", "2024-01-15"],
			["the longest", new string('a', 32765) + Chars(0x1F600)],
		];

		return new WrittenWorkbook(
			name,
			$"texts that need escaping, numbers stored as text, and a cell of 32,767 code units, with {strings} strings",
			new ExcelWriterOptions { Sheets = Phase4Look, Links = ExcelLinks.None, Strings = strings },
			[WrittenSheet.Fields("Text", rows)]);
	}

	/// <summary>"formulas.xlsx": formulas of every kind, written by hand and by a record writer, each beside the text Excel shows.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Formulas()
	{
		static ExcelFormula F(string formula) => new(formula);

		object?[][] calc =
		[
			["Case", "A", "B", "Formula", "Shows"],
			["a sum", 2, 3, F("B2+C2"), "5"],
			["given with =", 5, 7, F("=B3*C3"), "35"],
			["a condition", null, null, F("IF(B2>1,\"big\",\"small\")"), "big"],
			["escapes and entities", null, null, F("\"a_x0041_b\"&\"<&>\""), "a_x0041_b<&>"],
			["line breaks", null, null, F("IF(B2>0,\r\n  \"x\r\ny\",\r\n  2)"), "x\r\ny"],
			["control characters", null, null, F("\"a" + Units(1) + "b\"&\"" + Units(0xFFFE) + "\""), "a" + Units(1) + "b" + Units(0xFFFE)],
			["a newer function", null, null, F("_xlfn.XLOOKUP(2,B2:B3,C2:C3)"), "3"],
			["its own sheet", null, null, F("SUM(Calc!B2:B3)"), "7"],
			["a quoted sheet", null, null, F("'Q1 Data'!A1&\"!\""), "quoted!"],
			["an emoji", null, null, F("LEN(\"" + Chars(0x1F600) + "\")"), "1"],
			["not ASCII", null, null, F("\"日本\"&\"é\""), "日本é"],
			["an array constant", null, null, F("SUM({1,2;3,4})"), "10"],
			["an unknown function", null, null, F("NOSUCHFUNC(1)"), "#NAME?"],
			["a missing sheet", null, null, F("Nope!A1"), "#REF!"],
			["a percent", null, null, F("B2%"), "0.02"],
			["an empty argument", null, null, F("SUM(1,)"), "1"],
			["text that looks like a number", null, null, F("\"00123\""), "00123"],
		];
		WrittenSum[] sums =
		[
			new() { A = 1, B = 2, Total = F("A2+B2"), Shows = "3" },
			new() { A = 3, B = 4, Total = F("SUM(A3:B3)*2"), Shows = "14" },
			new() { A = 5, B = 6 },
		];

		return new WrittenWorkbook(
			"formulas.xlsx",
			"formulas written by hand and by a record writer, each beside the text Excel shows for it",
			Phase4Options,
			[WrittenSheet.Fields("Calc", calc), WrittenSheet.Fields("Q1 Data", [["quoted"]]), WrittenSheet.Records("Records", sums)]);
	}

	/// <summary>"names.xlsx": sheet names Excel opens, each sheet holding its name and its place.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Names()
	{
		string[] names =
		[
			"Q&A <1>", "\"Quoted\"", "a_x0041_b", "History", "日本", " spaced ", "TRUE", "A1", "R1C1", "123", "=a", "+a", "!#$%&;,",
			"Bob's", "ab", "a" + Units(0x200B) + "b", Units(0xFF21), new string('x', 29) + Chars(0x1F600),
		];

		return new WrittenWorkbook(
			"names.xlsx",
			"sheet names Excel opens: escapes, entities, quotes, spaces, references, a zero-width space, a full-width letter, 31 code units",
			Phase4Options,
			[.. names.Select((n, i) => WrittenSheet.Fields(n, [[n], [i + 1]]))]);
	}

	/// <summary>"newsheet.xlsx": <see cref="ExcelRowLimit.NewSheet"/> after two data rows a sheet, with shared strings.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook NewSheets()
	{
		const string Long = "abcdefghijklmnopqrstuvwxyz12345";
		var orders = Enumerable.Range(0, 7).Select(i => new WrittenOrder { Name = "n" + i, When = new DateTime(2024, 1, 15 + i, 10, 30, 0), Code = i == 4 ? "00123" : null });
		object?[][] totals =
		[
			["Total", "Value", "Shows"],
			["t1", new ExcelFormula("ROW()*10+1"), "21"],
			["t2", new ExcelFormula("ROW()*10+2"), "32"],
			["t3", new ExcelFormula("ROW()*10+3"), "23"],
		];

		return new WrittenWorkbook(
			"newsheet.xlsx",
			"RowLimit = NewSheet, a sheet's rows limited to 3 (the header and two records), with shared strings",
			new ExcelWriterOptions { Sheets = Phase4Look, Links = ExcelLinks.None, Strings = ExcelStrings.Shared, RowLimit = ExcelRowLimit.NewSheet },
			[
				WrittenSheet.Records("Orders", orders.ToArray(), ["Orders", "Orders (2)", "Orders (3)", "Orders (4)"]),
				WrittenSheet.Fields(Long, totals, [Long, "abcdefghijklmnopqrstuvwxyz1 (2)"]),
			],
			maxRows: 3);
	}

	/// <summary>"formats.xlsx": date and time formats in the invariant culture, with <see cref="ExcelDateTimeOffsets.ClockTime"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Formats()
	{
		var offsets = Samples.Select(s => Filled<WrittenClockOffsets>(new DateTimeOffset(s, TimeSpan.FromHours(2))));
		TimeSpan[] durations = [new(1, 12, 30, 5, 123), TimeSpan.FromSeconds(1), new(2, 3, 4)];

		return new WrittenWorkbook(
			"formats.xlsx",
			"[CsvFormat] dates, times, offsets and durations in the invariant culture, standard and custom, with DateTimeOffsets = ClockTime",
			new ExcelWriterOptions { Sheets = Phase4Look, Links = ExcelLinks.None, DateTimeOffsets = ExcelDateTimeOffsets.ClockTime },
			[
				WrittenSheet.Records("Standard", Samples.Select(s => Filled<WrittenStandardFormats>(s)).ToArray()),
				WrittenSheet.Records("Custom", Samples.Select(s => Filled<WrittenCustomFormats>(s)).ToArray()),
				WrittenSheet.Records("Offsets", offsets.ToArray()),
				WrittenSheet.Records("Durations", durations.Select(d => Filled<WrittenDurationFormats>(d)).ToArray()),
				WrittenSheet.Records("Early", new[] { new DateTime(1899, 12, 31), new DateTime(1900, 1, 1) }.Select(d => Filled<WrittenEarly>(d)).ToArray()),
			]);
	}

#if NET
	/// <summary>"cultures.xlsx": formats and numbers in cultures other than the invariant one.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Cultures()
	{
		var kinds = Samples.Select(s => new WrittenCultureKinds
		{
			UsDate = DateOnly.FromDateTime(s), UsLongDate = DateOnly.FromDateTime(s), GermanDate = DateOnly.FromDateTime(s), GermanLongDate = DateOnly.FromDateTime(s),
			FrenchDate = DateOnly.FromDateTime(s), FrenchLongDate = DateOnly.FromDateTime(s), FrenchMonth = DateOnly.FromDateTime(s),
			JapaneseDate = DateOnly.FromDateTime(s), JapaneseLongDate = DateOnly.FromDateTime(s),
			UsTime = TimeOnly.FromDateTime(s), UsLongTime = TimeOnly.FromDateTime(s), GermanTime = TimeOnly.FromDateTime(s), GermanLongTime = TimeOnly.FromDateTime(s),
			FrenchTime = TimeOnly.FromDateTime(s), FrenchLongTime = TimeOnly.FromDateTime(s), JapaneseTime = TimeOnly.FromDateTime(s), JapaneseLongTime = TimeOnly.FromDateTime(s),
			GermanOffset = new DateTimeOffset(s, TimeSpan.FromHours(1)), FrenchOffset = new DateTimeOffset(s, TimeSpan.FromHours(1)), JapaneseOffset = new DateTimeOffset(s, TimeSpan.FromHours(9)),
		});
		WrittenCultureNumbers[] numbers =
		[
			new() { Case = "negative", German = -1234.5m, GermanDouble = -1234.5, French = -1234.5, Swedish = -1234.5, Arabic = -1234.5, Persian = -1234.5, Invariant = -1234.5, GermanLong = -1234567 },
			new() { Case = "large", German = 1234567890.25m, GermanDouble = 1.5E+20, French = 1.5E+20, Swedish = 1.5E+20, Arabic = 1.5E+20, Persian = 1.5E+20, Invariant = 1.5E+20, GermanLong = 123456789012345 },
			new() { Case = "small", German = 0.001m, GermanDouble = 1E-05, French = -1E-05, Swedish = -1E-05, Arabic = 1E-05, Persian = -1E-05, Invariant = 1E-05, GermanLong = 0 },
		];

		return new WrittenWorkbook(
			"cultures.xlsx",
			"[CsvFormat] and [CsvCulture] in en-US, de-DE, fr-FR, ja-JP and other cultures: standard and custom date formats, DateOnly, TimeOnly, offsets with DateTimeOffsets = ClockTime, and numbers",
			new ExcelWriterOptions { Sheets = Phase4Look, Links = ExcelLinks.None, DateTimeOffsets = ExcelDateTimeOffsets.ClockTime },
			[
				WrittenSheet.Records("Standard", Samples.Select(s => Filled<WrittenCultureStandard>(s)).ToArray()),
				WrittenSheet.Records("Custom", Samples.Select(s => Filled<WrittenCultureCustom>(s)).ToArray()),
				WrittenSheet.Records("Kinds", kinds.ToArray()),
				WrittenSheet.Records("Numbers", numbers),
			]);
	}
#endif

	/// <summary>Orders of every kind of cell the default look shows: text, numbers, dates, booleans, durations and links.</summary>
	/// <returns>Five orders, the first the narrowest in most columns.</returns>
	private static WrittenLookOrder[] Orders() =>
	[
		new() { Id = 1, Customer = "Ada", Placed = new(2024, 1, 15), Total = 12.5m, Paid = true, Took = TimeSpan.FromHours(1), Site = new Uri("https://example.com/orders/1"), Note = "first" },
		new() { Id = 22, Customer = "Grace Hopper", Placed = new(2024, 1, 16, 10, 30, 5), Total = 1234567.891m, Paid = false, Took = new(1, 2, 3, 4, 500), Site = new Uri("https://example.com/orders/22") },
		new() { Id = 333, Customer = "Ken", Placed = new(2024, 2, 29, 23, 59, 59, 999), Total = -0.5m, Paid = true, Took = TimeSpan.FromDays(400), Site = new Uri("mailto:ken@example.com"), Note = "a note longer than the others" },
		new() { Id = 4444, Customer = "名前", Placed = new(2024, 12, 31, 12, 0, 0), Total = 0m, Took = TimeSpan.Zero, Note = "" },
		new() { Id = 55555, Customer = "Zoë", Placed = new(1900, 3, 1), Total = 99999999.99m, Paid = true, Took = TimeSpan.FromMinutes(90), Site = new Uri("https://example.com/orders/55555?x=1#top"), Note = "last" },
	];

	/// <summary>"look.xlsx": the default look: a bold, frozen header and auto-fit over 100 rows, with titles and frozen columns.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Look()
	{
		var orders = Orders();

		return new WrittenWorkbook(
			"look.xlsx",
			"the default options: a bold, frozen header, auto-fit over 100 rows and links for http, https and mailto; with a title over a frozen column, a title over one column, and columns frozen without the header",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Orders", orders),
				WrittenSheet.Records("Titled", orders.Take(3).ToArray(), look: new() { Options = new() { Title = "Orders for January 2024", FreezeColumns = 1 } }),
				WrittenSheet.Fields("One column", [["Name"], ["a"], ["b"]], look: new() { Options = new() { Title = "A title over one column, which isn't merged" } }),
				WrittenSheet.Fields("Columns", [["A", "B", "C"], [1, 2, 3]], look: new() { Options = new() { BoldHeader = false, FreezeHeader = false, FreezeColumns = 2 } }),
			]);
	}

	/// <summary>"columns.xlsx": columns' formats, widths, wrap and hidden, from <see cref="ExcelColumnAttribute"/> and <see cref="ExcelSheetOptions.Columns"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook ColumnsLook()
	{
		WrittenColumns[] rows =
		[
			new()
			{
				Case = "first", Amount = 1234.5m, Rate = 0.125, Big = 1.5E+20, Price = 1234.5m, Part = 1.5, Code = 123, When = new(2024, 1, 15, 10, 30, 5, 123), Paid = true, Zip = "00123",
				Notes = "short", Secret = "hidden", Wide = "wide",
			},
			new()
			{
				Case = "second", Amount = 0m, Rate = 1, Big = 12345, Price = -42m, Part = 0.25, Code = 0, When = new(1900, 1, 1), Paid = false, Zip = "007",
				Notes = "a line\nand another line", Secret = "s", Wide = "",
			},
			new()
			{
				Case = "third", Amount = -0.125m, Rate = -0.0123, Big = -0.001, Price = 0m, Part = 2.75, Code = 1234567, When = new(2024, 12, 31, 23, 59, 59), Paid = true, Zip = "",
				Notes = "This note is long enough to wrap across several lines in a column twenty characters wide.", Secret = "x", Wide = "text",
			},
		];
		var shows = new Dictionary<string, IReadOnlyList<string>>
		{
			["Amount"] = ["1,234.50", "0.00", "-0.13"],
			["Rate"] = ["12.5%", "100.0%", "-1.2%"],
			["Big"] = ["1.50E+20", "1.23E+04", "-1.00E-03"],
			["Price"] = ["$1,234.50", "-$42.00", "$0.00"],
			["Part"] = ["1 1/2", " 1/4", "2 3/4"],
			["Code"] = ["000123", "000000", "1234567"],
			["When"] = ["15/01/2024 10:30", "01/01/1900 00:00", "31/12/2024 23:59"],
			["Paid"] = ["Paid: TRUE", "Paid: FALSE", "Paid: TRUE"],
		};
		var columns = new Dictionary<string, ExcelColumnOptions>
		{
			["Case"] = new() { Hidden = true },
			["Amount"] = new() { Format = "0.00" },
			["Notes"] = new(),
			["Secret"] = new() { Width = 8 },
		};
		object?[][] mixed =
		[
			["Case", "Value"],
			["number", 1234567],
			["text", "n/a"],
			["boolean", true],
			["date", new DateTime(2024, 1, 15)],
			["decimal", 2.5m],
		];

		return new WrittenWorkbook(
			"columns.xlsx",
			"columns' formats (numbers, percents, exponents, currency, fractions, leading zeros, a date's, a boolean's text section, text), widths, wrap and hidden columns, from [ExcelColumn] and from ExcelSheetOptions.Columns, which replaces it whole",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Attributes", rows, look: new() { Shows = shows }),
				WrittenSheet.Records("Options", rows, look: new() { Options = new() { Columns = columns }, Shows = new Dictionary<string, IReadOnlyList<string>>(shows) { ["Amount"] = ["1234.50", "0.00", "-0.13"] } }),
				WrittenSheet.Fields(
					"Mixed",
					mixed,
					look: new() { Options = new() { Columns = new Dictionary<string, ExcelColumnOptions> { ["Value"] = new() { Format = "#,##0" } } }, Shows = new Dictionary<string, IReadOnlyList<string>> { ["Value"] = ["1,234,567", "n/a", "TRUE", "45,306", "3"] } }),
			]);
	}

	/// <summary>"autofit.xlsx": auto-fit's widths of every kind of cell.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook AutoFit()
	{
		WrittenWidths[] widths =
		[
			new() { Case = "narrow first", When = new(2024, 1, 15), Took = TimeSpan.FromHours(1) },
			new() { Case = "wider after", When = new(2024, 1, 15, 10, 30, 0, 123), Took = TimeSpan.MaxValue },
		];
		WrittenGeneral[] general =
		[
			new() { Case = "small", Value = 0.000123456789 },
			new() { Case = "pi", Value = 3.14159265358979 },
			new() { Case = "negative", Value = -1234567.891 },
			new() { Case = "decimals", Value = 123456.123456 },
			new() { Case = "large", Value = 1.5E+20 },
			new() { Case = "tiny", Value = 1E-05 },
			new() { Case = "11 digits", Value = 12345678901 },
			new() { Case = "12 digits", Value = 123456789012 },
			new() { Case = "a half", Value = -0.5 },
			new() { Case = "17 digits", Value = 0.30000000000000004 },
		];
		object?[][] text =
		[
			["Short", "Long", "Lines", "Wide"],
			["a", "This text is a hundred characters long, which is more than the sixty that auto-fit takes at most.", "first line\nsecond, longer line", "日本語のテキスト" + Chars(0x1F600)],
			["b", "short", "one line", "ascii"],
		];
		object?[][] capped =
		[
			["Name", "Value"],
			["short", 1.5],
			["a name of thirty characters ok", 123456.5],
			["past the held rows, a much longer name", 2.5],
		];
		object?[][] header =
		[
			["A header much wider than its cells", "B"],
			[1, 2],
			[3, 4],
		];
		object?[][] formulas =
		[
			["A", "B", "Sum", "Shows"],
			[1, 2, new ExcelFormula("A2+B2"), "3"],
			[10, 20, new ExcelFormula("A3*B3"), "200"],
		];

		return new WrittenWorkbook(
			"autofit.xlsx",
			"auto-fit's widths: later values wider than the first, General numbers, text capped at 60 characters and on one line, a capped sheet that measures two records, a bold header wider than its cells, formulas, and dates at midnight",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Widths", widths),
				WrittenSheet.Records("General", general),
				WrittenSheet.Fields("Text", text),
				WrittenSheet.Fields("Capped", capped, look: new() { Options = new() { AutoFit = 2, MaxAutoFitWidth = 20 } }),
				WrittenSheet.Fields("Header", header),
				WrittenSheet.Fields("Formulas", formulas),
				WrittenSheet.Records("Midnight", new WrittenMidnight[] { new() { Day = new(2024, 1, 15) }, new() { Day = new(2024, 1, 16) } }),
			]);
	}

	/// <summary>"filters.xlsx": autofilters and their hidden names.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Filters()
	{
		var orders = Orders();
		var filter = new ExcelSheetOptions { AutoFilter = true };

		return new WrittenWorkbook(
			"filters.xlsx",
			"autofilters, with their hidden _FilterDatabase names: over the rows, under a title in a sheet whose name needs quotes, over a header alone with an empty field at its end, and with a frozen column in a sheet whose name reads as an escape",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Orders", orders, look: new() { Options = filter }),
				WrittenSheet.Records("Bob's orders", orders.Take(3).ToArray(), look: new() { Options = filter with { Title = "Bob's orders" } }),
				WrittenSheet.Fields("Header", [["Name", "Value", ""]], look: new() { Options = filter }),
				WrittenSheet.Fields("a_x0041_b", [["A", "B"], [1, 2]], look: new() { Options = filter with { FreezeColumns = 1 } }),
			]);
	}

	/// <summary>"tables.xlsx": tables.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Tables()
	{
		var orders = Orders();
		var table = new ExcelSheetOptions { Table = new() };
		object?[][] renamed =
		[
			["Name", "Name", "Name2", "", 2024, true, "Two\nLines", " Name", "NAME"],
			["a", "b", "c", "d", "e", "f", "g", "h", "i"],
		];

		return new WrittenWorkbook(
			"tables.xlsx",
			"tables: named after their sheets or given, in a style, in one given in lower case or in none, without stripes or buttons, header cells renamed as Excel names them, over a header alone, under a title, with AutoFilter, and from [ExcelSheet]",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Orders", orders.Take(3).ToArray(), look: new() { Options = table, Tables = ["Orders"] }),
				WrittenSheet.Records("Q1 Orders", orders.Take(2).ToArray(), look: new() { Options = new() { Table = new() { Style = "tablestylelight9", BandedRows = false, FilterButtons = false } }, Tables = ["Q1_Orders"] }),
				WrittenSheet.Fields("2024", [["Year", "Total"], [2024, 1.5]], look: new() { Options = new() { Table = new() { Style = null } }, Tables = ["_2024"] }),
				WrittenSheet.Fields("Renamed", renamed, look: new() { Options = table, Tables = ["Renamed"], TableColumns = ["Name", "Name3", "Name2", "Column4", "2024", "True", "Two\nLines", " Name", "NAME5"] }),
				WrittenSheet.Fields("Header", [["A", "B", "C"]], look: new() { Options = table, Tables = ["Header"] }),
				WrittenSheet.Records("Titled", orders.Take(2).ToArray(), look: new() { Options = table with { Title = "A titled table" }, Tables = ["Titled"] }),
				WrittenSheet.Fields("Filtered", [["Name", "Value"], ["a", 1], ["b", 2]], look: new() { Options = table with { AutoFilter = true }, Tables = ["Filtered"] }),
				WrittenSheet.Records("Sales", new WrittenSale[] { new() { Region = "North", Amount = 1234.5m }, new() { Region = "South", Amount = 99.99m } }, look: new() { Tables = ["Sales"], Shows = new Dictionary<string, IReadOnlyList<string>> { ["Amount"] = ["1,234.50", "99.99"] } }),
			]);
	}

	/// <summary>"merges.xlsx": merged cells.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Merges()
	{
		object?[][] report =
		[
			["Region", "Q1", "Q2", "Q3", "Notes"],
			["North", 10, 20, 30, "steady"],
			[null, 11, 21, 31, null],
			["South", 5, 6, 7, "growing"],
			[null, 8, 9, 10, null],
			["Total", 34, 56, 78, null],
		];
		object?[][] beside =
		[
			["Name", "Value", "Code"],
			["a", 1, "x"],
			["b", 2, "y"],
			["c", 3, "z"],
		];

		return new WrittenWorkbook(
			"merges.xlsx",
			"merged cells: down a column before the header and while the rows are written, down and across columns after them, past the rows, under a title, and beside a table",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Fields("Report", report, look: new() { Options = new() { Title = "Quarterly report" }, Merges = [(0, "A3:A4"), (3, "A5:A6"), (6, "E3:E6"), (6, "B9:D10")] }),
				WrittenSheet.Fields("Beside", beside, look: new() { Options = new() { Table = new() }, Tables = ["Beside"], Merges = [(4, "E2:F3"), (4, "D6:D7")] }),
			]);
	}

	/// <summary>Sites of the URIs a policy takes or leaves as text.</summary>
	/// <param name="uris">The URIs.</param>
	/// <returns>A site for each.</returns>
	private static WrittenLink[] Sites(params (string Name, Uri? Link)[] uris) => [.. uris.Select(u => new WrittenLink { Name = u.Name, Link = u.Link })];

	/// <summary>"links.xlsx": links by the default policy, <see cref="ExcelLinks.Web"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Links()
	{
		var sites = Sites(
			("https", new Uri("https://example.com/a?b=1&c=2#frag")),
			("mailto", new Uri("mailto:someone@example.com?subject=Hi%20there")),
			("upper case", new Uri("HTTP://Example.COM")),
			("again", new Uri("https://example.com/a?b=1&c=2#frag")),
			("past ASCII", new Uri("https://example.com/caf%C3%A9")),
			("file", new Uri("file:///C:/Temp/report.txt")),
			("relative", new Uri("docs/report.txt", UriKind.Relative)),
			("urn", new Uri("urn:isbn:0451450523")),
			("none", null));
		object?[][] header =
		[
			[new Uri("https://example.com/header"), "Name"],
			[new Uri("https://example.com/row"), "a"],
		];

		return new WrittenWorkbook(
			"links.xlsx",
			"links by the default policy, Web: http, https and mailto URIs are links in the Hyperlink style, one shared by two cells, and file, relative and urn URIs text; in a table, in a column's format and wrap, and a header's URI as text",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records("Sites", sites),
				WrittenSheet.Records("Table", sites, look: new() { Options = new() { Table = new() }, Tables = ["Table"] }),
				WrittenSheet.Records("Styled", sites, look: new() { Options = new() { Columns = new Dictionary<string, ExcelColumnOptions> { ["Link"] = new() { Format = "@", Wrap = true } } } }),
				WrittenSheet.Fields("Header", header),
			]);
	}

	/// <summary>"links-all.xlsx": links by <see cref="ExcelLinks.All"/>.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook AllLinks()
	{
		var sites = Sites(
			("share", new Uri(@"\\server\share\a b.txt")),
			("path", new Uri(@"C:\Temp\réport.txt")),
			("relative", new Uri("docs/r%C3%A9sum%C3%A9.txt", UriKind.Relative)),
			("jump", new Uri("#Other!B2", UriKind.Relative)),
			("ftp", new Uri("ftp://example.com/file.txt")),
			("https", new Uri("https://example.com/")));

		return new WrittenWorkbook(
			"links-all.xlsx",
			"links by ExcelLinks.All: a share, a Windows path, a relative path with escaped letters, a jump to a cell of the workbook, and ftp and https",
			new ExcelWriterOptions { Links = ExcelLinks.All },
			[WrittenSheet.Records("Sites", sites), WrittenSheet.Fields("Other", [["Name", "Value"], ["target", 1]])]);
	}

	/// <summary>"newsheet-look.xlsx": <see cref="ExcelRowLimit.NewSheet"/> with a look, each new sheet repeating it.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook NewSheetsLook()
	{
		object?[][] filtered =
		[
			["Name", "Value"],
			["a", 1],
			["b", 22],
			["c", 333],
			["d", 4444],
			["e", 55555],
		];

		return new WrittenWorkbook(
			"newsheet-look.xlsx",
			"RowLimit = NewSheet, a sheet's rows limited to 4 (a title, the header and two records): each new sheet repeats the title and its merge, the bold, frozen header, the table or the filter, and the widths",
			new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet },
			[
				WrittenSheet.Records("Orders", Orders(), ["Orders", "Orders (2)", "Orders (3)"], new() { Options = new() { Title = "Orders", Table = new() }, Tables = ["Orders", "Orders2", "Orders3"] }),
				WrittenSheet.Fields("Filtered", filtered, ["Filtered", "Filtered (2)", "Filtered (3)"], new() { Options = new() { Title = "Filtered", AutoFilter = true, FreezeColumns = 1 } }),
			],
			maxRows: 4);
	}

	/// <summary>"presentation.xlsx": every setting together.</summary>
	/// <returns>The workbook.</returns>
	private static WrittenWorkbook Presentation()
	{
		WrittenReportLine[] lines =
		[
			new() { Region = "North", Product = "Widgets", Units = 12345, Revenue = 98765.4m, Share = 0.4567, Updated = new(2024, 1, 15), Link = new Uri("https://example.com/north"), Notes = "steady", Internal = "a" },
			new() { Region = "South", Product = "Gadgets and gizmos", Units = 7, Revenue = 12.5m, Share = 0.0012, Updated = new(2024, 2, 29), Link = new Uri("mailto:south@example.com"), Notes = "growing\nfast", Internal = "b" },
			new() { Region = "East", Product = "Widgets", Units = 0, Revenue = -150m, Share = 1, Updated = new(2024, 12, 31), Notes = "A note long enough to wrap in a column twenty-four characters wide.", Internal = "c" },
		];
		object?[][] summary =
		[
			["Region", "Total", "Site"],
			["North", 98765.4, new Uri("https://example.com/north")],
			[null, 1.5, new Uri("https://example.com/north/2")],
			["South", 12.5, null],
		];
		var report = new ExcelSheetOptions
		{
			Title = "Sales report",
			FreezeColumns = 1,
			Table = new() { Name = "Report" },
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["Units"] = new() { Format = "#,##0" },
				["Revenue"] = new() { Format = "\"$\"#,##0.00;[Red]-\"$\"#,##0.00" },
				["Share"] = new() { Format = "0.00%" },
				["Updated"] = new() { Format = "yyyy-mm-dd" },
				["Notes"] = new() { Wrap = true, Width = 24 },
				["Internal"] = new() { Hidden = true },
			},
		};

		return new WrittenWorkbook(
			"presentation.xlsx",
			"every setting together: a title, a bold header frozen with a column, a table, columns' formats, wrap, widths and hidden, links, cells merged beside the table, and auto-fit; and a filtered sheet with a title, links and a range merged down a column",
			new ExcelWriterOptions(),
			[
				WrittenSheet.Records(
					"Report",
					lines,
					look: new()
					{
						Options = report,
						Merges = [(4, "K3:L4"), (4, "K6:K7")],
						Tables = ["Report"],
						Shows = new Dictionary<string, IReadOnlyList<string>>
						{
							["Units"] = ["12,345", "7", "0"],
							["Revenue"] = ["$98,765.40", "$12.50", "-$150.00"],
							["Share"] = ["45.67%", "0.12%", "100.00%"],
							["Updated"] = ["2024-01-15", "2024-02-29", "2024-12-31"],
						},
					}),
				WrittenSheet.Fields("Summary", summary, look: new() { Options = new() { Title = "Summary", AutoFilter = true, FreezeColumns = 1 }, Merges = [(1, "A3:A4")] }),
			]);
	}
}

/// <summary>A workbook of the written set.</summary>
internal sealed class WrittenWorkbook
{
	/// <summary>Creates a workbook.</summary>
	/// <param name="name">Its file name, such as <c>types.xlsx</c>.</param>
	/// <param name="description">What it holds.</param>
	/// <param name="options">The options it is written with.</param>
	/// <param name="sheets">Its sheets, in tab order.</param>
	/// <param name="maxRows">The rows a sheet holds before <see cref="ExcelWriterOptions.RowLimit"/> applies, or <see langword="null"/> for Excel's.</param>
	public WrittenWorkbook(string name, string description, ExcelWriterOptions options, IReadOnlyList<WrittenSheet> sheets, int? maxRows = null)
	{
		Name = name;
		Description = description;
		Options = options;
		Sheets = sheets;
		MaxRows = maxRows;
		Cultures = [.. sheets.SelectMany(s => s.Columns).Select(c => c.Culture).OfType<string>().Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal)];
	}

	/// <summary>Its file name, such as <c>types.xlsx</c>.</summary>
	public string Name { get; }

	/// <summary>What it holds.</summary>
	public string Description { get; }

	/// <summary>The options it is written with.</summary>
	public ExcelWriterOptions Options { get; }

	/// <summary>Its sheets, in tab order.</summary>
	public IReadOnlyList<WrittenSheet> Sheets { get; }

	/// <summary>The rows a sheet holds before <see cref="ExcelWriterOptions.RowLimit"/> applies, or <see langword="null"/> for Excel's.</summary>
	public int? MaxRows { get; }

	/// <summary>The cultures other than the invariant one that its columns' formats and numbers read, by name.</summary>
	public IReadOnlyList<string> Cultures { get; }

	/// <inheritdoc/>
	public override string ToString() => Name;
}

/// <summary>
/// A sheet of the written set: its rows of values, the header first, and its columns' formats and cultures, written by a
/// record writer or field by field.
/// </summary>
internal sealed class WrittenSheet
{
	/// <summary>Creates a sheet.</summary>
	/// <param name="name">The name it is added with.</param>
	/// <param name="names">The sheets it becomes, in tab order: itself, and those <see cref="ExcelRowLimit.NewSheet"/> adds.</param>
	/// <param name="columns">Its columns, in order.</param>
	/// <param name="rows">Its rows of values, the header first.</param>
	/// <param name="look">Its look: the options it is added with, its merged ranges, and what Excel should show.</param>
	/// <param name="attribute">Its record type's <see cref="ExcelSheetAttribute"/>, or <see langword="null"/>.</param>
	/// <param name="write">Writes it.</param>
	/// <param name="writeAsync">Writes it asynchronously.</param>
	private WrittenSheet(
		string name, IReadOnlyList<string>? names, IReadOnlyList<WrittenColumn> columns, IReadOnlyList<IReadOnlyList<object?>> rows, WrittenLook look,
		ExcelSheetAttribute? attribute, Action<ExcelWorkbookWriter> write, Func<ExcelWorkbookWriter, Task> writeAsync)
	{
		Name = name;
		Names = names ?? [name];
		Columns = columns;
		Rows = rows;
		Look = look;
		Attribute = attribute;
		Write = write;
		WriteAsync = writeAsync;
	}

	/// <summary>The name it is added with.</summary>
	public string Name { get; }

	/// <summary>The sheets it becomes, in tab order: itself, and those <see cref="ExcelRowLimit.NewSheet"/> adds.</summary>
	public IReadOnlyList<string> Names { get; }

	/// <summary>Its columns, in order.</summary>
	public IReadOnlyList<WrittenColumn> Columns { get; }

	/// <summary>Its rows of values, the header first: records' members, or the fields written by hand.</summary>
	public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

	/// <summary>Its look: the options it is added with, its merged ranges, and what Excel should show.</summary>
	public WrittenLook Look { get; }

	/// <summary>Its record type's <see cref="ExcelSheetAttribute"/>, or <see langword="null"/>.</summary>
	public ExcelSheetAttribute? Attribute { get; }

	/// <summary>Writes it.</summary>
	public Action<ExcelWorkbookWriter> Write { get; }

	/// <summary>Writes it asynchronously.</summary>
	public Func<ExcelWorkbookWriter, Task> WriteAsync { get; }

	/// <summary>
	/// A sheet of records, written by <typeparamref name="T"/>'s record writer with its header: a column per public property,
	/// in declaration order, with its <see cref="CsvFormatAttribute"/>, <see cref="CsvCultureAttribute"/> and
	/// <see cref="ExcelColumnAttribute"/>. Without merged ranges it is written by <see cref="ExcelWorkbookWriter.WriteSheet"/>;
	/// with them, by <see cref="ExcelWorkbookWriter.AddSheet{T}"/> and <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/>,
	/// the ranges merged before the header (0) or after the records.
	/// </summary>
	/// <typeparam name="T">The record type, of public properties.</typeparam>
	/// <param name="name">The sheet's name.</param>
	/// <param name="records">The records.</param>
	/// <param name="names">The sheets it becomes, or <see langword="null"/> for itself alone.</param>
	/// <param name="look">Its look, or <see langword="null"/> for none: the workbook's <see cref="ExcelWriterOptions.Sheets"/>, or <typeparamref name="T"/>'s attribute.</param>
	/// <returns>The sheet.</returns>
	public static WrittenSheet Records<T>(string name, IReadOnlyList<T> records, IReadOnlyList<string>? names = null, WrittenLook? look = null)
	{
		var given = look ?? WrittenLook.None;
		var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
		var columns = properties
			.Select(p => new WrittenColumn(p.Name, p.GetCustomAttribute<CsvFormatAttribute>()?.Format, p.GetCustomAttribute<CsvCultureAttribute>()?.Name, p.GetCustomAttribute<ExcelColumnAttribute>()))
			.ToList();
		var rows = new List<IReadOnlyList<object?>> { properties.Select(p => (object?)p.Name).ToList() };
		rows.AddRange(records.Select(r => properties.Select(p => p.GetValue(r)).ToList()));
		if (given.Merges.Any(m => m.Before != 0 && m.Before != rows.Count))
		{
			throw new InvalidOperationException($"{name}: a sheet of records merges ranges before its header or after its records.");
		}

		void Write(ExcelWorkbookWriter workbook)
		{
			if (given.Merges.Count == 0)
			{
				workbook.WriteSheet(name, records, given.Options);

				return;
			}

			using var writer = workbook.AddSheet<T>(name, given.Options);
			Merge(writer, given, 0);
			writer.WriteRecords(records);
			Merge(writer, given, rows.Count);
		}

		async Task WriteAsync(ExcelWorkbookWriter workbook)
		{
			if (given.Merges.Count == 0)
			{
				await workbook.WriteSheetAsync(name, records, given.Options);

				return;
			}

			var writer = workbook.AddSheet<T>(name, given.Options);
			await using (writer)
			{
				Merge(writer, given, 0);
				await writer.WriteRecordsAsync(records);
				Merge(writer, given, rows.Count);
			}
		}

		return new WrittenSheet(name, names, columns, rows, given, typeof(T).GetCustomAttribute<ExcelSheetAttribute>(), Write, WriteAsync);
	}

	/// <summary>
	/// A sheet written field by field: <see cref="CsvWriter.WriteField(object)"/> for each value, and
	/// <see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/> for a byte array, UTF-8 that need not be valid; each of its
	/// look's ranges merged before the row it names.
	/// </summary>
	/// <param name="name">The sheet's name.</param>
	/// <param name="rows">Its rows of values, the header first.</param>
	/// <param name="names">The sheets it becomes, or <see langword="null"/> for itself alone.</param>
	/// <param name="look">Its look, or <see langword="null"/> for none: the workbook's <see cref="ExcelWriterOptions.Sheets"/>.</param>
	/// <returns>The sheet.</returns>
	public static WrittenSheet Fields(string name, IReadOnlyList<IReadOnlyList<object?>> rows, IReadOnlyList<string>? names = null, WrittenLook? look = null)
	{
		var given = look ?? WrittenLook.None;
		var columns = Enumerable.Range(0, rows.Max(r => r.Count)).Select(i => new WrittenColumn(rows[0].ElementAtOrDefault(i) as string ?? "", null, null)).ToList();

		void Write(ExcelWorkbookWriter workbook)
		{
			using var writer = workbook.AddSheet(name, given.Options);
			for (var i = 0; i < rows.Count; i++)
			{
				Merge(writer, given, i);
				WriteRow(writer, rows[i]);
				writer.EndRecord();
			}

			Merge(writer, given, rows.Count);
		}

		async Task WriteAsync(ExcelWorkbookWriter workbook)
		{
			var writer = workbook.AddSheet(name, given.Options);
			await using (writer)
			{
				for (var i = 0; i < rows.Count; i++)
				{
					Merge(writer, given, i);
					WriteRow(writer, rows[i]);
					await writer.EndRecordAsync();
				}

				Merge(writer, given, rows.Count);
			}
		}

		return new WrittenSheet(name, names, columns, rows, given, null, Write, WriteAsync);
	}

	/// <summary>Merges a look's ranges that come before a row.</summary>
	/// <param name="writer">The sheet's writer.</param>
	/// <param name="look">The look.</param>
	/// <param name="before">The row's index among the sheet's rows, the header's 0; their count for the ranges after them.</param>
	private static void Merge(CsvWriter writer, WrittenLook look, int before)
	{
		foreach (var (_, range) in look.Merges.Where(m => m.Before == before))
		{
			writer.MergeCells(range);
		}
	}

	/// <summary>Writes a row's values as fields.</summary>
	/// <param name="writer">The sheet's writer.</param>
	/// <param name="row">The values.</param>
	private static void WriteRow(CsvWriter writer, IReadOnlyList<object?> row)
	{
		foreach (var value in row)
		{
			if (value is byte[] utf8)
			{
				writer.WriteField(new ReadOnlySpan<byte>(utf8));
			}
			else
			{
				writer.WriteField(value);
			}
		}
	}

	/// <inheritdoc/>
	public override string ToString() => Name;
}

/// <summary>A column of a written sheet.</summary>
/// <param name="name">Its header's name.</param>
/// <param name="format">Its member's format, or <see langword="null"/>.</param>
/// <param name="culture">Its member's culture's name (empty for the invariant culture), or <see langword="null"/> for the writer's.</param>
/// <param name="excel">Its member's <see cref="ExcelColumnAttribute"/>, or <see langword="null"/>.</param>
internal sealed class WrittenColumn(string name, string? format, string? culture, ExcelColumnAttribute? excel = null)
{
	/// <summary>Its header's name.</summary>
	public string Name => name;

	/// <summary>Its member's format, or <see langword="null"/>.</summary>
	public string? Format => format;

	/// <summary>Its member's culture's name (empty for the invariant culture), or <see langword="null"/> for the writer's.</summary>
	public string? Culture => culture;

	/// <summary>Its member's <see cref="ExcelColumnAttribute"/>, or <see langword="null"/>.</summary>
	public ExcelColumnAttribute? Excel => excel;
}

/// <summary>
/// A written sheet's look: the options it is added with, the ranges merged on its writer, and what Excel should show that
/// the values written don't say: the text of values in a column's Excel format, and the names Excel gives its tables and
/// their columns.
/// </summary>
internal sealed class WrittenLook
{
	/// <summary>No look: the sheet is added without options, and merges nothing.</summary>
	public static WrittenLook None { get; } = new();

	/// <summary>The options the sheet is added with, or <see langword="null"/> for its record type's attribute, else the workbook's.</summary>
	public ExcelSheetOptions? Options { get; init; }

	/// <summary>
	/// The ranges merged on the sheet's writer, each before the row of an index among the sheet's rows (the header's 0), or
	/// after them (their count).
	/// </summary>
	public IReadOnlyList<(int Before, string Range)> Merges { get; init; } = [];

	/// <summary>
	/// The text Excel shows of each data row's value in a column whose Excel format is a code (<see cref="ExcelColumnOptions.Format"/>),
	/// by the column's header name, in the order of the rows across the sheets it becomes.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>> Shows { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

	/// <summary>The names of its tables, one for each sheet it becomes, or <see langword="null"/> without a table.</summary>
	public IReadOnlyList<string>? Tables { get; init; }

	/// <summary>
	/// The names Excel gives its table's columns, which the header's cells hold, or <see langword="null"/> when they are the
	/// header's fields.
	/// </summary>
	public IReadOnlyList<string>? TableColumns { get; init; }
}

/// <summary>Numbers of each common type: a row per edge case.</summary>
[CsvRecord]
public sealed partial class WrittenNumber
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A <see cref="long"/>.</summary>
	public long? Int64 { get; set; }

	/// <summary>A <see cref="ulong"/>.</summary>
	public ulong? UInt64 { get; set; }

	/// <summary>An <see cref="int"/>.</summary>
	public int? Int32 { get; set; }

	/// <summary>A <see cref="decimal"/>.</summary>
	public decimal? Decimal { get; set; }

	/// <summary>A <see cref="double"/>.</summary>
	public double? Double { get; set; }

	/// <summary>A <see cref="float"/>.</summary>
	public float? Single { get; set; }
}

/// <summary>The smaller integer types, at their ends.</summary>
[CsvRecord]
public sealed partial class WrittenInteger
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>An <see cref="sbyte"/>.</summary>
	public sbyte? SByte { get; set; }

	/// <summary>A <see cref="byte"/>.</summary>
	public byte? Byte { get; set; }

	/// <summary>A <see cref="short"/>.</summary>
	public short? Int16 { get; set; }

	/// <summary>A <see cref="ushort"/>.</summary>
	public ushort? UInt16 { get; set; }

	/// <summary>A <see cref="uint"/>.</summary>
	public uint? UInt32 { get; set; }
}

/// <summary>Dates, durations and offsets in their default formats: the first row the widest, which sets the widths.</summary>
[CsvRecord]
public sealed partial class WrittenDate
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A date and time.</summary>
	public DateTime? DateTime { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan? TimeSpan { get; set; }

	/// <summary>An offset: text, by <see cref="ExcelDateTimeOffsets.Text"/>.</summary>
	public DateTimeOffset? Offset { get; set; }
}

/// <summary>Booleans and the types written as text.</summary>
[CsvRecord]
public sealed partial class WrittenOther
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A boolean.</summary>
	public bool? Flag { get; set; }

	/// <summary>A character.</summary>
	public char? Letter { get; set; }

	/// <summary>A Guid.</summary>
	public Guid? Id { get; set; }

	/// <summary>An enum.</summary>
	public DayOfWeek? Day { get; set; }

	/// <summary>A URI: its text, in Phase 4's workbooks, which have no links.</summary>
	public Uri? Link { get; set; }

	/// <summary>A string, or an empty one, which is no cell.</summary>
	public string? Note { get; set; }
}

/// <summary>
/// Widths from the first data record only: a later value that needs more than the first shows <c>########</c> in Excel,
/// until Phase 5's auto-fit.
/// </summary>
[CsvRecord]
public sealed partial class WrittenWidths
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A date at midnight, then one with milliseconds.</summary>
	public DateTime? When { get; set; }

	/// <summary>A short duration, then <see cref="TimeSpan.MaxValue"/>, which Excel can't show as a duration, so it is text.</summary>
	public TimeSpan? Took { get; set; }
}

/// <summary>Offsets in their default format and in a format, with <see cref="ExcelDateTimeOffsets.Utc"/>.</summary>
[CsvRecord]
public sealed partial class WrittenOffset
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>The serial of its UTC time.</summary>
	public DateTimeOffset? Offset { get; set; }

	/// <summary>A format's text shows a clock time, which only <see cref="ExcelDateTimeOffsets.ClockTime"/> types: text.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm")]
	public DateTimeOffset Formatted { get; set; }
}

/// <summary>Formulas written by a record writer, each beside the text Excel shows.</summary>
[CsvRecord]
public sealed partial class WrittenSum
{
	/// <summary>A number.</summary>
	public int A { get; set; }

	/// <summary>Another.</summary>
	public int B { get; set; }

	/// <summary>A formula over them, or <see langword="default"/>, which is no cell.</summary>
	public ExcelFormula Total { get; set; }

	/// <summary>The text Excel shows for <see cref="Total"/>.</summary>
	public string Shows { get; set; } = "";
}

/// <summary>An order, for <see cref="ExcelRowLimit.NewSheet"/>.</summary>
[CsvRecord]
public sealed partial class WrittenOrder
{
	/// <summary>Its name.</summary>
	public string Name { get; set; } = "";

	/// <summary>When it was placed.</summary>
	public DateTime When { get; set; }

	/// <summary>A code that looks like a number, in one order, or none.</summary>
	public string? Code { get; set; }
}

/// <summary>The standard date and time formats in the invariant culture, but <c>U</c>, which depends on the machine's time zone.</summary>
[CsvRecord]
public sealed partial class WrittenStandardFormats
{
	/// <summary><c>d</c>.</summary>
	[CsvFormat("d")]
	public DateTime ShortDate { get; set; }

	/// <summary><c>D</c>.</summary>
	[CsvFormat("D")]
	public DateTime LongDate { get; set; }

	/// <summary><c>f</c>.</summary>
	[CsvFormat("f")]
	public DateTime Full { get; set; }

	/// <summary><c>F</c>.</summary>
	[CsvFormat("F")]
	public DateTime FullLong { get; set; }

	/// <summary><c>g</c>.</summary>
	[CsvFormat("g")]
	public DateTime General { get; set; }

	/// <summary><c>G</c>.</summary>
	[CsvFormat("G")]
	public DateTime GeneralLong { get; set; }

	/// <summary><c>M</c>: no year, so text.</summary>
	[CsvFormat("M")]
	public DateTime MonthDay { get; set; }

	/// <summary><c>R</c>.</summary>
	[CsvFormat("R")]
	public DateTime Rfc1123 { get; set; }

	/// <summary><c>s</c>.</summary>
	[CsvFormat("s")]
	public DateTime Sortable { get; set; }

	/// <summary><c>t</c>.</summary>
	[CsvFormat("t")]
	public DateTime ShortTime { get; set; }

	/// <summary><c>T</c>.</summary>
	[CsvFormat("T")]
	public DateTime LongTime { get; set; }

	/// <summary><c>u</c>.</summary>
	[CsvFormat("u")]
	public DateTime Universal { get; set; }

	/// <summary><c>y</c>.</summary>
	[CsvFormat("y")]
	public DateTime YearMonth { get; set; }
}

/// <summary>Custom date and time formats in the invariant culture: those Excel can show, and those it can't, which are text.</summary>
[CsvRecord]
public sealed partial class WrittenCustomFormats
{
	/// <summary><c>yyyy-MM-dd</c>.</summary>
	[CsvFormat("yyyy-MM-dd")]
	public DateTime C01 { get; set; }

	/// <summary><c>dd/MM/yy</c>.</summary>
	[CsvFormat("dd/MM/yy")]
	public DateTime C02 { get; set; }

	/// <summary><c>d.M.yyyy</c>.</summary>
	[CsvFormat("d.M.yyyy")]
	public DateTime C03 { get; set; }

	/// <summary><c>HH:mm:ss.fff</c>.</summary>
	[CsvFormat("HH:mm:ss.fff")]
	public DateTime C04 { get; set; }

	/// <summary><c>H:mm:ss.ff</c>.</summary>
	[CsvFormat("H:mm:ss.ff")]
	public DateTime C05 { get; set; }

	/// <summary><c>HH:mm:ss.f</c>.</summary>
	[CsvFormat("HH:mm:ss.f")]
	public DateTime C06 { get; set; }

	/// <summary><c>h:mm tt</c>.</summary>
	[CsvFormat("h:mm tt")]
	public DateTime C07 { get; set; }

	/// <summary><c>hh:mm t</c>.</summary>
	[CsvFormat("hh:mm t")]
	public DateTime C08 { get; set; }

	/// <summary><c>mm:ss</c>.</summary>
	[CsvFormat("mm:ss")]
	public DateTime C09 { get; set; }

	/// <summary><c>%H</c>.</summary>
	[CsvFormat("%H")]
	public DateTime C10 { get; set; }

	/// <summary>A quoted literal that isn't ASCII.</summary>
	[CsvFormat("yyyy'年'")]
	public DateTime C11 { get; set; }

	/// <summary>An escaped literal.</summary>
	[CsvFormat("yyyy\\-MM")]
	public DateTime C12 { get; set; }

	/// <summary>A literal in double quotes.</summary>
	[CsvFormat("yyyy\"x\"MM")]
	public DateTime C13 { get; set; }

	/// <summary>A double quote, quoted.</summary>
	[CsvFormat("yyyy'\"'MM")]
	public DateTime C14 { get; set; }

	/// <summary>A literal letter, quoted.</summary>
	[CsvFormat("'Q' yyyy")]
	public DateTime C15 { get; set; }

	/// <summary>A letter that isn't a token, as a literal.</summary>
	[CsvFormat("yyyy-MM-ddTHH:mm")]
	public DateTime C16 { get; set; }

	/// <summary>An abbreviated month name.</summary>
	[CsvFormat("MMM d, yyyy")]
	public DateTime C17 { get; set; }

	/// <summary>An abbreviated day name and a month name.</summary>
	[CsvFormat("ddd MMMM yyyy")]
	public DateTime C18 { get; set; }

	/// <summary>A semicolon, which separates Excel's sections.</summary>
	[CsvFormat("yyyy;MM")]
	public DateTime C19 { get; set; }

	/// <summary>A quoted letter between an hour and its minutes.</summary>
	[CsvFormat("HH 'h' mm")]
	public DateTime C20 { get; set; }

	/// <summary>Day and month names in full.</summary>
	[CsvFormat("dddd, MMMM dd, yyyy")]
	public DateTime C21 { get; set; }

	/// <summary>A year and a month, read back on the 1st.</summary>
	[CsvFormat("yyyy-MM")]
	public DateTime C22 { get; set; }

	/// <summary>No year: text.</summary>
	[CsvFormat("dd MMMM")]
	public DateTime C23 { get; set; }

	/// <summary><c>K</c>, an offset: text.</summary>
	[CsvFormat("yyyy-MM-ddK")]
	public DateTime C24 { get; set; }

	/// <summary><c>F</c>: text.</summary>
	[CsvFormat("HH:mm:ss.FFF")]
	public DateTime C25 { get; set; }

	/// <summary>Four digits of a second's fraction, which Excel refuses: text.</summary>
	[CsvFormat("HH:mm:ss.ffff")]
	public DateTime C26 { get; set; }

	/// <summary><c>y</c>: text.</summary>
	[CsvFormat("%y")]
	public DateTime C27 { get; set; }

	/// <summary>A 12-hour hour without <c>tt</c>: text.</summary>
	[CsvFormat("hh:mm")]
	public DateTime C28 { get; set; }

	/// <summary>A 24-hour hour with <c>tt</c>: text.</summary>
	[CsvFormat("HH:mm tt")]
	public DateTime C29 { get; set; }

	/// <summary>A month where Excel reads minutes: text.</summary>
	[CsvFormat("yyyy HH:MM")]
	public DateTime C30 { get; set; }

	/// <summary>An era: text.</summary>
	[CsvFormat("yyyy g")]
	public DateTime C31 { get; set; }

	/// <summary>A date and a time to the second.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm:ss")]
	public DateTime C32 { get; set; }

	/// <summary>A date and a 12-hour time.</summary>
	[CsvFormat("M/d/yyyy h:mm tt")]
	public DateTime C33 { get; set; }

	/// <summary>A day, a month and a year between points.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime C34 { get; set; }

	/// <summary>A month's name and a year.</summary>
	[CsvFormat("MMMM yyyy")]
	public DateTime C35 { get; set; }

	/// <summary>Literals that aren't ASCII, unquoted.</summary>
	[CsvFormat("yyyy年M月d日")]
	public DateTime C36 { get; set; }
}

/// <summary>Offsets in formats, with <see cref="ExcelDateTimeOffsets.ClockTime"/>: the clock time a format's text shows.</summary>
[CsvRecord]
public sealed partial class WrittenClockOffsets
{
	/// <summary>The default format: the serial of its clock time.</summary>
	public DateTimeOffset Plain { get; set; }

	/// <summary>A custom format.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm")]
	public DateTimeOffset Minutes { get; set; }

	/// <summary><c>R</c>, which shows the UTC time.</summary>
	[CsvFormat("R")]
	public DateTimeOffset Rfc1123 { get; set; }

	/// <summary><c>u</c>, which shows the UTC time.</summary>
	[CsvFormat("u")]
	public DateTimeOffset Universal { get; set; }

	/// <summary><c>O</c>, the default.</summary>
	[CsvFormat("O")]
	public DateTimeOffset Iso { get; set; }

	/// <summary>The offset itself: text.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm zzz")]
	public DateTimeOffset WithOffset { get; set; }
}

/// <summary>Durations in formats: <c>c</c> and <c>t</c> are durations, the others text.</summary>
[CsvRecord]
public sealed partial class WrittenDurationFormats
{
	/// <summary>The default format.</summary>
	public TimeSpan Plain { get; set; }

	/// <summary><c>c</c>.</summary>
	[CsvFormat("c")]
	public TimeSpan Constant { get; set; }

	/// <summary><c>t</c>, another name of <c>c</c>.</summary>
	[CsvFormat("t")]
	public TimeSpan Alias { get; set; }

	/// <summary><c>g</c>: text.</summary>
	[CsvFormat("g")]
	public TimeSpan General { get; set; }

	/// <summary>A custom format: text.</summary>
	[CsvFormat("hh\\:mm\\:ss")]
	public TimeSpan Custom { get; set; }
}

/// <summary>A formatted date before 1900-01-01, which is text by <see cref="ExcelOutOfRangeDates.Text"/>, and Excel's first day.</summary>
[CsvRecord]
public sealed partial class WrittenEarly
{
	/// <summary>A custom format.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime When { get; set; }
}

#if NET
/// <summary>.NET's newer types: the first row the widest, which sets the widths.</summary>
[CsvRecord]
public sealed partial class WrittenNewer
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A date.</summary>
	public DateOnly? Date { get; set; }

	/// <summary>A time of day.</summary>
	public TimeOnly? Time { get; set; }

	/// <summary>A half-precision number.</summary>
	public Half? Half { get; set; }

	/// <summary>A 128-bit integer.</summary>
	public Int128? Int128 { get; set; }

	/// <summary>An unsigned 128-bit integer.</summary>
	public UInt128? UInt128 { get; set; }
}

/// <summary>Dates and times of day in formats, in the invariant culture.</summary>
[CsvRecord]
public sealed partial class WrittenNewerFormats
{
	/// <summary>A custom date format.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateOnly Day { get; set; }

	/// <summary><c>R</c>, which a date writes without its time.</summary>
	[CsvFormat("R")]
	public DateOnly Rfc { get; set; }

	/// <summary><c>D</c>.</summary>
	[CsvFormat("D")]
	public DateOnly Long { get; set; }

	/// <summary>A 12-hour time.</summary>
	[CsvFormat("hh:mm tt")]
	public TimeOnly Clock { get; set; }

	/// <summary>A time to the millisecond.</summary>
	[CsvFormat("HH:mm:ss.fff")]
	public TimeOnly Precise { get; set; }

	/// <summary><c>t</c>.</summary>
	[CsvFormat("t")]
	public TimeOnly Short { get; set; }

	/// <summary><c>R</c>, which a time writes as <c>HH:mm:ss</c>.</summary>
	[CsvFormat("R")]
	public TimeOnly TimeRfc { get; set; }
}

/// <summary>The standard formats with patterns of their own in each of en-US, de-DE, fr-FR and ja-JP.</summary>
[CsvRecord]
public sealed partial class WrittenCultureStandard
{
	/// <summary><c>d</c> in en-US.</summary>
	[CsvFormat("d")]
	[CsvCulture("en-US")]
	public DateTime UsShortDate { get; set; }

	/// <summary><c>D</c> in en-US.</summary>
	[CsvFormat("D")]
	[CsvCulture("en-US")]
	public DateTime UsLongDate { get; set; }

	/// <summary><c>f</c> in en-US.</summary>
	[CsvFormat("f")]
	[CsvCulture("en-US")]
	public DateTime UsFull { get; set; }

	/// <summary><c>F</c> in en-US.</summary>
	[CsvFormat("F")]
	[CsvCulture("en-US")]
	public DateTime UsFullLong { get; set; }

	/// <summary><c>g</c> in en-US.</summary>
	[CsvFormat("g")]
	[CsvCulture("en-US")]
	public DateTime UsGeneral { get; set; }

	/// <summary><c>G</c> in en-US.</summary>
	[CsvFormat("G")]
	[CsvCulture("en-US")]
	public DateTime UsGeneralLong { get; set; }

	/// <summary><c>t</c> in en-US.</summary>
	[CsvFormat("t")]
	[CsvCulture("en-US")]
	public DateTime UsShortTime { get; set; }

	/// <summary><c>T</c> in en-US.</summary>
	[CsvFormat("T")]
	[CsvCulture("en-US")]
	public DateTime UsLongTime { get; set; }

	/// <summary><c>y</c> in en-US.</summary>
	[CsvFormat("y")]
	[CsvCulture("en-US")]
	public DateTime UsYearMonth { get; set; }

	/// <summary><c>d</c> in de-DE.</summary>
	[CsvFormat("d")]
	[CsvCulture("de-DE")]
	public DateTime GermanShortDate { get; set; }

	/// <summary><c>D</c> in de-DE.</summary>
	[CsvFormat("D")]
	[CsvCulture("de-DE")]
	public DateTime GermanLongDate { get; set; }

	/// <summary><c>f</c> in de-DE.</summary>
	[CsvFormat("f")]
	[CsvCulture("de-DE")]
	public DateTime GermanFull { get; set; }

	/// <summary><c>F</c> in de-DE.</summary>
	[CsvFormat("F")]
	[CsvCulture("de-DE")]
	public DateTime GermanFullLong { get; set; }

	/// <summary><c>g</c> in de-DE.</summary>
	[CsvFormat("g")]
	[CsvCulture("de-DE")]
	public DateTime GermanGeneral { get; set; }

	/// <summary><c>G</c> in de-DE.</summary>
	[CsvFormat("G")]
	[CsvCulture("de-DE")]
	public DateTime GermanGeneralLong { get; set; }

	/// <summary><c>t</c> in de-DE.</summary>
	[CsvFormat("t")]
	[CsvCulture("de-DE")]
	public DateTime GermanShortTime { get; set; }

	/// <summary><c>T</c> in de-DE.</summary>
	[CsvFormat("T")]
	[CsvCulture("de-DE")]
	public DateTime GermanLongTime { get; set; }

	/// <summary><c>y</c> in de-DE.</summary>
	[CsvFormat("y")]
	[CsvCulture("de-DE")]
	public DateTime GermanYearMonth { get; set; }

	/// <summary><c>d</c> in fr-FR.</summary>
	[CsvFormat("d")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchShortDate { get; set; }

	/// <summary><c>D</c> in fr-FR.</summary>
	[CsvFormat("D")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchLongDate { get; set; }

	/// <summary><c>f</c> in fr-FR.</summary>
	[CsvFormat("f")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchFull { get; set; }

	/// <summary><c>F</c> in fr-FR.</summary>
	[CsvFormat("F")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchFullLong { get; set; }

	/// <summary><c>g</c> in fr-FR.</summary>
	[CsvFormat("g")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchGeneral { get; set; }

	/// <summary><c>G</c> in fr-FR.</summary>
	[CsvFormat("G")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchGeneralLong { get; set; }

	/// <summary><c>t</c> in fr-FR.</summary>
	[CsvFormat("t")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchShortTime { get; set; }

	/// <summary><c>T</c> in fr-FR.</summary>
	[CsvFormat("T")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchLongTime { get; set; }

	/// <summary><c>y</c> in fr-FR.</summary>
	[CsvFormat("y")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchYearMonth { get; set; }

	/// <summary><c>d</c> in ja-JP.</summary>
	[CsvFormat("d")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseShortDate { get; set; }

	/// <summary><c>D</c> in ja-JP.</summary>
	[CsvFormat("D")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseLongDate { get; set; }

	/// <summary><c>f</c> in ja-JP.</summary>
	[CsvFormat("f")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseFull { get; set; }

	/// <summary><c>F</c> in ja-JP.</summary>
	[CsvFormat("F")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseFullLong { get; set; }

	/// <summary><c>g</c> in ja-JP.</summary>
	[CsvFormat("g")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseGeneral { get; set; }

	/// <summary><c>G</c> in ja-JP.</summary>
	[CsvFormat("G")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseGeneralLong { get; set; }

	/// <summary><c>t</c> in ja-JP.</summary>
	[CsvFormat("t")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseShortTime { get; set; }

	/// <summary><c>T</c> in ja-JP.</summary>
	[CsvFormat("T")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseLongTime { get; set; }

	/// <summary><c>y</c> in ja-JP.</summary>
	[CsvFormat("y")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseYearMonth { get; set; }
}

/// <summary>Custom formats with names, designators and separators of other cultures.</summary>
[CsvRecord]
public sealed partial class WrittenCultureCustom
{
	/// <summary>An abbreviated month in de-DE.</summary>
	[CsvFormat("MMM yyyy")]
	[CsvCulture("de-DE")]
	public DateTime GermanAbbreviated { get; set; }

	/// <summary>Names in full in fr-FR.</summary>
	[CsvFormat("dddd d MMMM yyyy")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchNames { get; set; }

	/// <summary>Abbreviated names in fr-FR.</summary>
	[CsvFormat("ddd d MMM yyyy")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchAbbreviated { get; set; }

	/// <summary>A designator in de-DE.</summary>
	[CsvFormat("MMMM yyyy h tt")]
	[CsvCulture("de-DE")]
	public DateTime GermanDesignator { get; set; }

	/// <summary>A date and a time in de-DE.</summary>
	[CsvFormat("dd.MM.yyyy HH:mm")]
	[CsvCulture("de-DE")]
	public DateTime GermanDateTime { get; set; }

	/// <summary>A month without a day in pl-PL: the nominative.</summary>
	[CsvFormat("MMMM yyyy")]
	[CsvCulture("pl-PL")]
	public DateTime PolishMonth { get; set; }

	/// <summary>Names in full in en-GB.</summary>
	[CsvFormat("dddd d MMMM yyyy")]
	[CsvCulture("en-GB")]
	public DateTime BritishNames { get; set; }

	/// <summary>A culture's separator in sv-SE.</summary>
	[CsvFormat("yyyy/MM/dd")]
	[CsvCulture("sv-SE")]
	public DateTime SwedishSlashes { get; set; }

	/// <summary>An abbreviated month in ja-JP, which Excel shows otherwise.</summary>
	[CsvFormat("MMM yyyy'年'")]
	[CsvCulture("ja-JP")]
	public DateTime JapaneseAbbreviated { get; set; }

	/// <summary>A month with a day in pl-PL: the genitive, which Excel doesn't show, so text.</summary>
	[CsvFormat("d MMMM yyyy")]
	[CsvCulture("pl-PL")]
	public DateTime PolishGenitive { get; set; }

	/// <summary>An abbreviated month with a day in de-DE.</summary>
	[CsvFormat("dd. MMM yyyy")]
	[CsvCulture("de-DE")]
	public DateTime GermanDayAbbreviated { get; set; }

	/// <summary>A month in full in de-DE.</summary>
	[CsvFormat("MMMM yyyy")]
	[CsvCulture("de-DE")]
	public DateTime GermanMonth { get; set; }

	/// <summary>A month with a day in ru-RU: the genitive, so text.</summary>
	[CsvFormat("d MMMM yyyy")]
	[CsvCulture("ru-RU")]
	public DateTime RussianGenitive { get; set; }

	/// <summary>A day's name alone in fr-FR: no year, so text.</summary>
	[CsvFormat("dddd")]
	[CsvCulture("fr-FR")]
	public DateTime FrenchDay { get; set; }
}

/// <summary>Dates, times of day and offsets in cultures' standard formats.</summary>
[CsvRecord]
public sealed partial class WrittenCultureKinds
{
	/// <summary>A date in en-US's <c>d</c>.</summary>
	[CsvFormat("d")]
	[CsvCulture("en-US")]
	public DateOnly UsDate { get; set; }

	/// <summary>A date in en-US's <c>D</c>.</summary>
	[CsvFormat("D")]
	[CsvCulture("en-US")]
	public DateOnly UsLongDate { get; set; }

	/// <summary>A date in de-DE's <c>d</c>.</summary>
	[CsvFormat("d")]
	[CsvCulture("de-DE")]
	public DateOnly GermanDate { get; set; }

	/// <summary>A date in de-DE's <c>D</c>.</summary>
	[CsvFormat("D")]
	[CsvCulture("de-DE")]
	public DateOnly GermanLongDate { get; set; }

	/// <summary>A date in fr-FR's <c>d</c>.</summary>
	[CsvFormat("d")]
	[CsvCulture("fr-FR")]
	public DateOnly FrenchDate { get; set; }

	/// <summary>A date in fr-FR's <c>D</c>.</summary>
	[CsvFormat("D")]
	[CsvCulture("fr-FR")]
	public DateOnly FrenchLongDate { get; set; }

	/// <summary>A date in fr-FR's <c>y</c>.</summary>
	[CsvFormat("y")]
	[CsvCulture("fr-FR")]
	public DateOnly FrenchMonth { get; set; }

	/// <summary>A date in ja-JP's <c>d</c>.</summary>
	[CsvFormat("d")]
	[CsvCulture("ja-JP")]
	public DateOnly JapaneseDate { get; set; }

	/// <summary>A date in ja-JP's <c>D</c>.</summary>
	[CsvFormat("D")]
	[CsvCulture("ja-JP")]
	public DateOnly JapaneseLongDate { get; set; }

	/// <summary>A time in en-US's <c>t</c>.</summary>
	[CsvFormat("t")]
	[CsvCulture("en-US")]
	public TimeOnly UsTime { get; set; }

	/// <summary>A time in en-US's <c>T</c>.</summary>
	[CsvFormat("T")]
	[CsvCulture("en-US")]
	public TimeOnly UsLongTime { get; set; }

	/// <summary>A time in de-DE's <c>t</c>.</summary>
	[CsvFormat("t")]
	[CsvCulture("de-DE")]
	public TimeOnly GermanTime { get; set; }

	/// <summary>A time in de-DE's <c>T</c>.</summary>
	[CsvFormat("T")]
	[CsvCulture("de-DE")]
	public TimeOnly GermanLongTime { get; set; }

	/// <summary>A time in fr-FR's <c>t</c>.</summary>
	[CsvFormat("t")]
	[CsvCulture("fr-FR")]
	public TimeOnly FrenchTime { get; set; }

	/// <summary>A time in fr-FR's <c>T</c>.</summary>
	[CsvFormat("T")]
	[CsvCulture("fr-FR")]
	public TimeOnly FrenchLongTime { get; set; }

	/// <summary>A time in ja-JP's <c>t</c>.</summary>
	[CsvFormat("t")]
	[CsvCulture("ja-JP")]
	public TimeOnly JapaneseTime { get; set; }

	/// <summary>A time in ja-JP's <c>T</c>.</summary>
	[CsvFormat("T")]
	[CsvCulture("ja-JP")]
	public TimeOnly JapaneseLongTime { get; set; }

	/// <summary>An offset in de-DE's <c>g</c>: its clock time.</summary>
	[CsvFormat("g")]
	[CsvCulture("de-DE")]
	public DateTimeOffset GermanOffset { get; set; }

	/// <summary>An offset in fr-FR's <c>D</c>.</summary>
	[CsvFormat("D")]
	[CsvCulture("fr-FR")]
	public DateTimeOffset FrenchOffset { get; set; }

	/// <summary>An offset in ja-JP's <c>T</c>.</summary>
	[CsvFormat("T")]
	[CsvCulture("ja-JP")]
	public DateTimeOffset JapaneseOffset { get; set; }
}

/// <summary>Numbers in their members' cultures: their default text there, which the sink reads with the culture's signs.</summary>
[CsvRecord]
public sealed partial class WrittenCultureNumbers
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A decimal in de-DE.</summary>
	[CsvCulture("de-DE")]
	public decimal? German { get; set; }

	/// <summary>A double in de-DE.</summary>
	[CsvCulture("de-DE")]
	public double? GermanDouble { get; set; }

	/// <summary>A double in fr-FR.</summary>
	[CsvCulture("fr-FR")]
	public double? French { get; set; }

	/// <summary>A double in sv-SE, whose minus is U+2212.</summary>
	[CsvCulture("sv-SE")]
	public double? Swedish { get; set; }

	/// <summary>A double in ar-SA, whose signs have a letter mark.</summary>
	[CsvCulture("ar-SA")]
	public double? Arabic { get; set; }

	/// <summary>A double in fa-IR.</summary>
	[CsvCulture("fa-IR")]
	public double? Persian { get; set; }

	/// <summary>A double in the invariant culture, named.</summary>
	[CsvCulture("")]
	public double? Invariant { get; set; }

	/// <summary>An integer in de-DE.</summary>
	[CsvCulture("de-DE")]
	public long? GermanLong { get; set; }
}
#endif

/// <summary>An order of the default look: text, numbers, a date and time, a boolean, a duration and a link.</summary>
[CsvRecord]
public sealed partial class WrittenLookOrder
{
	/// <summary>Its number.</summary>
	public int Id { get; set; }

	/// <summary>Who placed it.</summary>
	public string Customer { get; set; } = "";

	/// <summary>When it was placed.</summary>
	public DateTime Placed { get; set; }

	/// <summary>Its total.</summary>
	public decimal Total { get; set; }

	/// <summary>Whether it is paid.</summary>
	public bool Paid { get; set; }

	/// <summary>How long it took.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>Its page, a link by the default policy, or none.</summary>
	public Uri? Site { get; set; }

	/// <summary>A note, or none.</summary>
	public string? Note { get; set; }
}

/// <summary>Columns of every kind of Excel format, width, wrap and hidden, by <see cref="ExcelColumnAttribute"/>.</summary>
[CsvRecord]
public sealed partial class WrittenColumns
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>A decimal with thousands and two decimals.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Amount { get; set; }

	/// <summary>A percent with a decimal.</summary>
	[ExcelColumn(Format = "0.0%")]
	public double Rate { get; set; }

	/// <summary>Scientific notation.</summary>
	[ExcelColumn(Format = "0.00E+00")]
	public double Big { get; set; }

	/// <summary>A currency, negative in red.</summary>
	[ExcelColumn(Format = "\"$\"#,##0.00;[Red]-\"$\"#,##0.00")]
	public decimal Price { get; set; }

	/// <summary>A fraction.</summary>
	[ExcelColumn(Format = "# ?/?")]
	public double Part { get; set; }

	/// <summary>Leading zeros.</summary>
	[ExcelColumn(Format = "000000")]
	public int Code { get; set; }

	/// <summary>A date in the column's format, in place of its own.</summary>
	[ExcelColumn(Format = "dd/mm/yyyy hh:mm")]
	public DateTime When { get; set; }

	/// <summary>A boolean, in the format's text section.</summary>
	[ExcelColumn(Format = "0;-0;0;\"Paid: \"@")]
	public bool Paid { get; set; }

	/// <summary>Text that looks like a number, in the text format.</summary>
	[ExcelColumn(Format = "@")]
	public string Zip { get; set; } = "";

	/// <summary>Text that wraps in a column of 20 characters.</summary>
	[ExcelColumn(Width = 20, Wrap = true)]
	public string Notes { get; set; } = "";

	/// <summary>A hidden column.</summary>
	[ExcelColumn(Hidden = true)]
	public string Secret { get; set; } = "";

	/// <summary>A column of 30 characters.</summary>
	[ExcelColumn(Width = 30)]
	public string Wide { get; set; } = "";
}

/// <summary>Numbers in General, which auto-fit measures as Excel shows them.</summary>
[CsvRecord]
public sealed partial class WrittenGeneral
{
	/// <summary>What the row holds.</summary>
	public string Case { get; set; } = "";

	/// <summary>The number.</summary>
	public double Value { get; set; }
}

/// <summary>Dates at midnight, whose column keeps the width of a date and time.</summary>
[CsvRecord]
public sealed partial class WrittenMidnight
{
	/// <summary>The date.</summary>
	public DateTime Day { get; set; }
}

/// <summary>Sales, in a table that <see cref="ExcelSheetAttribute"/> asks for.</summary>
[CsvRecord]
[ExcelSheet(Table = true, TableName = "Sales", TableStyle = "TableStyleDark1", FreezeColumns = 1)]
public sealed partial class WrittenSale
{
	/// <summary>The region.</summary>
	public string Region { get; set; } = "";

	/// <summary>The amount, with thousands and two decimals.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Amount { get; set; }
}

/// <summary>A named URI, which the policy makes a link or leaves as text.</summary>
[CsvRecord]
public sealed partial class WrittenLink
{
	/// <summary>What the URI is.</summary>
	public string Name { get; set; } = "";

	/// <summary>The URI, or none.</summary>
	public Uri? Link { get; set; }
}

/// <summary>A line of a report with every setting: formats by <see cref="ExcelSheetOptions.Columns"/>, a link, wrap and a hidden column.</summary>
[CsvRecord]
public sealed partial class WrittenReportLine
{
	/// <summary>The region.</summary>
	public string Region { get; set; } = "";

	/// <summary>The product.</summary>
	public string Product { get; set; } = "";

	/// <summary>Units sold.</summary>
	public int Units { get; set; }

	/// <summary>Revenue.</summary>
	public decimal Revenue { get; set; }

	/// <summary>The share of the total.</summary>
	public double Share { get; set; }

	/// <summary>When it was updated.</summary>
	public DateTime Updated { get; set; }

	/// <summary>The region's page, or none.</summary>
	public Uri? Link { get; set; }

	/// <summary>Notes, which wrap.</summary>
	public string Notes { get; set; } = "";

	/// <summary>A hidden column.</summary>
	public string Internal { get; set; } = "";
}
