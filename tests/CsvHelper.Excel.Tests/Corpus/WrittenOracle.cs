using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using CsvHelper.Excel.Corpus;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>What a cell of a written workbook should be, as Excel reads it.</summary>
internal enum ExpectedKind
{
	/// <summary>Text: <c>Value2</c> is <see cref="ExpectedCell.Text"/>.</summary>
	Text,

	/// <summary>A boolean.</summary>
	Boolean,

	/// <summary>A number in General or in a format of the writer's own: <c>Value2</c> is <see cref="ExpectedCell.Number"/>.</summary>
	Number,

	/// <summary>
	/// A value in its member's format, whose text Excel shows as .NET does (the translation's oracle): a typed cell of the
	/// value its text reads back as (<see cref="ExpectedCell.Typed"/>), or a text cell of the text.
	/// </summary>
	Formatted,

	/// <summary>A formula, which Excel computes.</summary>
	Formula,
}

/// <summary>A cell of a written workbook as Excel should read it, from the value the content set wrote.</summary>
internal sealed class ExpectedCell
{
	/// <summary>What it is.</summary>
	public ExpectedKind Kind { get; init; }

	/// <summary>A text cell's text, or a formatted value's .NET text.</summary>
	public string? Text { get; init; }

	/// <summary>A number's <c>Value2</c>, or the serial a formatted value's text reads back as (<see cref="double.NaN"/> when it doesn't).</summary>
	public double Number { get; init; }

	/// <summary>
	/// The type of Excel's <c>Value</c> for a number: <c>number</c>, or <c>date</c> for a value with a day, a month or a
	/// year; <see langword="null"/> for any of them and <c>currency</c>, for a number in its column's Excel format, which
	/// <see cref="Shows"/> checks.
	/// </summary>
	public string? NumberType { get; init; } = "number";

	/// <summary>
	/// Whether a formatted value is a typed cell: its format is one Excel shows the same, and its text reads back, from
	/// 1900-01-01, as a value that formats to the same text. Otherwise it is text.
	/// </summary>
	public bool Typed { get; init; }

	/// <summary>A boolean's value.</summary>
	public bool Boolean { get; init; }

	/// <summary>The formula, <c>=</c> first.</summary>
	public string? Formula { get; init; }

	/// <summary>The text Excel shows, where .NET's text or the content set says it; <see langword="null"/> where it doesn't.</summary>
	public string? Shows { get; init; }

	/// <summary>What the cell holds, for messages: the column's format and culture, and the value's type.</summary>
	public string Source { get; init; } = "";
}

/// <summary>What the writer put in a sheet's parts that Excel's answers are compared with.</summary>
/// <param name="Codes">Each cell's number format code, by row and 0-based column.</param>
/// <param name="Widths">Each column's width, by 0-based column.</param>
/// <param name="Hidden">The hidden columns, 0-based.</param>
/// <param name="Fitted">The columns auto-fit sized (<c>bestFit</c>), 0-based.</param>
internal sealed record WrittenParts(Dictionary<(long Row, int Column), string> Codes, Dictionary<int, double> Widths, HashSet<int> Hidden, HashSet<int> Fitted);

/// <summary>
/// The written set's workbooks as Excel should read them, by rules of their own: every cell from the value the content set
/// wrote and its column's format and culture, as Phase 4's "What a field becomes" says, each sheet's look from its
/// settings, as Phase 5's "What a sheet gets" says, and the widths and number formats from the workbook's own parts.
/// </summary>
internal static class WrittenOracle
{
	/// <summary>Milliseconds in a day.</summary>
	private const double MillisecondsPerDay = 86_400_000;

	/// <summary>Excel's first date.</summary>
	private static readonly DateTime January1900 = new(1900, 1, 1);

	/// <summary>The first day after Excel's 1900-02-29, which never was: serials count from 1899-12-30 on, and from 1899-12-31 before.</summary>
	private static readonly DateTime March1900 = new(1900, 3, 1);

	/// <summary>The last millisecond Excel shows, which later ticks round to.</summary>
	private static readonly DateTime LastMillisecond = new(9999, 12, 31, 23, 59, 59, 999);

	/// <summary>
	/// The set's formats that Excel can't show as .NET does, so that their values are text, by format and culture (empty for
	/// the writer's, the invariant one), from the rules of Phase 4's "Formats": <c>M</c> and a date without a year, offsets
	/// (<c>K</c>, <c>zzz</c>), <c>F</c>, four digits of a second's fraction, <c>y</c>, a 12-hour hour without <c>tt</c> and a
	/// 24-hour one with it, a month where Excel reads minutes, an era, and months in the genitive.
	/// </summary>
	private static readonly HashSet<(string Format, string Culture)> Untranslated =
	[
		("M", ""), ("dd MMMM", ""), ("yyyy-MM-ddK", ""), ("HH:mm:ss.FFF", ""), ("HH:mm:ss.ffff", ""), ("%y", ""), ("hh:mm", ""), ("HH:mm tt", ""),
		("yyyy HH:MM", ""), ("yyyy g", ""), ("yyyy-MM-dd HH:mm zzz", ""), ("d MMMM yyyy", "pl-PL"), ("dd. MMM yyyy", "de-DE"), ("d MMMM yyyy", "ru-RU"),
		("dddd", "fr-FR"),
	];

	/// <summary>The built-in number formats the writer uses by id.</summary>
	internal static readonly Dictionary<uint, string> BuiltIn = new()
	{
		[0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%", [11] = "0.00E+00", [12] = "# ?/?",
		[13] = "# ??/??", [46] = "[h]:mm:ss", [48] = "##0.0E+0", [49] = "@",
	};

	/// <summary>
	/// The cells of each Excel sheet a workbook becomes, by sheet name, then by row and 0-based column: each sheet's title in
	/// row 1 when it has one, its header next (named as its table's columns are, in a table), repeated with the title in the
	/// sheets <see cref="ExcelRowLimit.NewSheet"/> adds, and its rows after it, a value in a column's Excel format showing
	/// the text its look says.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The cells.</returns>
	public static List<(string Sheet, Dictionary<(long Row, int Column), ExpectedCell> Cells)> Cells(WrittenWorkbook workbook)
	{
		var result = new List<(string, Dictionary<(long, int), ExpectedCell>)>();
		var options = workbook.Options;
		foreach (var sheet in workbook.Sheets)
		{
			var settings = Settings(workbook, sheet);
			var columns = ColumnOptions(workbook, sheet);
			var header = options.Csv.HasHeader ? sheet.Rows[0] : null;
			var firstRow = settings.Title is null ? 1 : 2;
			var shows = sheet.Columns.Select(c => c.Name).ToList().IndexOf("Shows");
			var data = 0;
			foreach (var (name, chunk) in Chunks(workbook, sheet))
			{
				var cells = new Dictionary<(long, int), ExpectedCell>();
				if (settings.Title is { } title)
				{
					cells[(1, 0)] = Text(title, $"{new SheetLocation(name).Cell(0, 1)} the title");
				}

				var lines = header is null ? chunk : [header, .. chunk];
				for (var r = 0; r < lines.Count; r++)
				{
					var row = firstRow + r;
					var isHeader = header is not null && r == 0;
					for (var c = 0; c < lines[r].Count; c++)
					{
						var column = sheet.Columns[c];
						var value = lines[r][c];
						var source = $"{new SheetLocation(name).Cell(c, row)} [{column.Format}] [{column.Culture}] {value?.GetType().Name ?? "null"}";
						var expected = isHeader
							? settings.Table is null ? Expect(value, null, null, options, source) : Text(sheet.Look.TableColumns?[c] ?? (string)value!, source + " a table's column")
							: columns[c]?.Format is { } code
								? InFormat(value, code, sheet.Look.Shows.TryGetValue(column.Name, out var texts) ? texts[data + r - (header is null ? 0 : 1)] : null, options, source + $" in {code}")
								: Expect(value, column.Format, column.Culture, options, source);
						if (expected is { Kind: ExpectedKind.Formula })
						{
							Assert.True(shows >= 0, $"{sheet.Name} has a formula, and no Shows column for the text Excel shows of it.");
							expected = new ExpectedCell { Kind = ExpectedKind.Formula, Formula = expected.Formula, Shows = lines[r][shows] as string, Source = source };
						}

						if (expected is not null)
						{
							cells[(row, c)] = expected;
						}
					}
				}

				data += chunk.Count;
				result.Add((name, cells));
			}
		}

		return result;
	}

	/// <summary>
	/// The sheets a sheet of the set becomes, with their data rows: each holds the workbook's <see cref="WrittenWorkbook.MaxRows"/>
	/// less its title's and header's rows, and the content set must name each.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <returns>Each sheet's name and data rows.</returns>
	public static List<(string Name, List<IReadOnlyList<object?>> Rows)> Chunks(WrittenWorkbook workbook, WrittenSheet sheet)
	{
		var hasHeader = workbook.Options.Csv.HasHeader;
		var data = sheet.Rows.Skip(hasHeader ? 1 : 0).ToList();
		var perSheet = workbook.MaxRows is { } rows ? rows - (hasHeader ? 1 : 0) - (Settings(workbook, sheet).Title is null ? 0 : 1) : int.MaxValue;
		var chunks = Enumerable.Range(0, Math.Max(1, (data.Count + perSheet - 1) / perSheet)).Select(i => data.Skip(i * perSheet).Take(perSheet).ToList()).ToList();
		Assert.True(chunks.Count == sheet.Names.Count, $"{sheet.Name} becomes {chunks.Count} sheets, and the content set names {sheet.Names.Count}.");

		return [.. chunks.Select((c, i) => (sheet.Names[i], c))];
	}

	/// <summary>
	/// A sheet's settings, as the plan's "Where the settings come from" says: the options it is added with, else its record
	/// type's <see cref="ExcelSheetAttribute"/>, else the workbook's <see cref="ExcelWriterOptions.Sheets"/>, each whole.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <returns>The settings.</returns>
	public static ExcelSheetOptions Settings(WrittenWorkbook workbook, WrittenSheet sheet)
	{
		if (sheet.Look.Options is { } given)
		{
			return given;
		}

		if (sheet.Attribute is { } attribute)
		{
			return new ExcelSheetOptions
			{
				Title = attribute.Title,
				BoldHeader = attribute.BoldHeader,
				FreezeHeader = attribute.FreezeHeader,
				FreezeColumns = attribute.FreezeColumns,
				AutoFilter = attribute.AutoFilter,
				Table = attribute.Table ? new ExcelTableOptions { Name = attribute.TableName, Style = attribute.TableStyle } : null,
				AutoFit = attribute.AutoFit,
				MaxAutoFitWidth = attribute.MaxAutoFitWidth,
			};
		}

		return workbook.Options.Sheets;
	}

	/// <summary>
	/// Each column's options, as the plan says: its entry in the settings' <see cref="ExcelSheetOptions.Columns"/> by its
	/// header's name, else its member's <see cref="ExcelColumnAttribute"/> (a width of 0 for none), each whole.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <returns>Each column's options, or <see langword="null"/> for none.</returns>
	public static ExcelColumnOptions?[] ColumnOptions(WrittenWorkbook workbook, WrittenSheet sheet)
	{
		var given = Settings(workbook, sheet).Columns;

		return
		[
			.. sheet.Columns.Select(c => given is not null && given.TryGetValue(c.Name, out var options)
				? options
				: c.Excel is { } attribute
					? new ExcelColumnOptions { Width = attribute.Width == 0 ? null : attribute.Width, Format = attribute.Format, Wrap = attribute.Wrap, Hidden = attribute.Hidden }
					: null),
		];
	}

	/// <summary>
	/// A field's cell in its column's Excel format: a number, a date (its serial, rounded to the millisecond), a boolean or
	/// text, as without the format, showing the text the content set says.
	/// </summary>
	/// <param name="value">The value written.</param>
	/// <param name="code">The column's format code.</param>
	/// <param name="shows">The text Excel shows, or <see langword="null"/> where the content set doesn't say.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="source">What the cell holds, for messages.</param>
	/// <returns>The cell, or <see langword="null"/> for none.</returns>
	private static ExpectedCell? InFormat(object? value, string code, string? shows, ExcelWriterOptions options, string source)
	{
		var plain = value switch
		{
			DateTime date => new ExpectedCell { Kind = ExpectedKind.Number, Number = Serial(Round(date)), Source = source },
			_ => Expect(value, null, null, options, source),
		};
		if (plain is null)
		{
			return null;
		}

		Assert.True(plain.Kind == ExpectedKind.Text || shows is not null, $"{source}: the content set doesn't say what Excel shows in {code}.");

		return plain.Kind switch
		{
			ExpectedKind.Number => new ExpectedCell { Kind = ExpectedKind.Number, Number = plain.Number, NumberType = null, Shows = shows, Source = source },
			ExpectedKind.Boolean => new ExpectedCell { Kind = ExpectedKind.Boolean, Boolean = plain.Boolean, Shows = shows, Source = source },
			ExpectedKind.Text => new ExpectedCell { Kind = ExpectedKind.Text, Text = plain.Text, Shows = shows, Source = source },
			_ => plain,
		};
	}

	/// <summary>A field's cell, from its value, its column's format and culture, and the options.</summary>
	/// <param name="value">The value written.</param>
	/// <param name="format">The column's format, or <see langword="null"/>.</param>
	/// <param name="culture">The column's culture's name, or <see langword="null"/> for the writer's.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="source">What the cell holds, for messages.</param>
	/// <returns>The cell, or <see langword="null"/> for none.</returns>
	public static ExpectedCell? Expect(object? value, string? format, string? culture, ExcelWriterOptions options, string source)
	{
		var provider = culture is null ? options.Csv.FormatProvider : CultureInfo.GetCultureInfo(culture);
		var isDefault = format is null or "" or "O" or "o";
		var translated = format is not null && !Untranslated.Contains((format, culture ?? ""));
		switch (value)
		{
			case null:
				return null;
			case string text:
				return text.Length == 0 ? null : Text(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)), source);
			case byte[] utf8:
				return Text(Encoding.UTF8.GetString(utf8), source);
			case bool flag:
				return new ExpectedCell { Kind = ExpectedKind.Boolean, Boolean = flag, Shows = flag ? "TRUE" : "FALSE", Source = source };
			case ExcelFormula formula:
				return formula == default ? null : new ExpectedCell { Kind = ExpectedKind.Formula, Formula = "=" + formula.Formula, Source = source };
			case DateTime date when isDefault:
				return Date(date, date.ToString("O", CultureInfo.InvariantCulture), options, source);
			case DateTime date:
				return Formatted(date.ToString(format, provider), t => DateTime.TryParseExact(t, format!, provider, DateTimeStyles.NoCurrentDateDefault, out var r) && r.ToString(format, provider) == t ? r : null, translated, source);
			case DateTimeOffset offset when isDefault:
				return options.DateTimeOffsets switch
				{
					ExcelDateTimeOffsets.Utc => Date(offset.UtcDateTime, offset.ToString("O", CultureInfo.InvariantCulture), options, source),
					ExcelDateTimeOffsets.ClockTime => Date(offset.DateTime, offset.ToString("O", CultureInfo.InvariantCulture), options, source),
					_ => Text(offset.ToString("O", CultureInfo.InvariantCulture), source),
				};
			case DateTimeOffset offset:
				// The clock time the text shows, read as a DateTime's: DateTimeOffset's parsing takes a time alone as today's.
				return options.DateTimeOffsets == ExcelDateTimeOffsets.ClockTime
					? Formatted(offset.ToString(format, provider), t => DateTime.TryParseExact(t, format!, provider, DateTimeStyles.NoCurrentDateDefault, out var r) && r.ToString(format, provider) == t ? r : null, translated, source)
					: Text(offset.ToString(format, provider), source);
			case TimeSpan duration when format is null or "" or "c" or "t" or "T":
				return Duration(duration, options, source);
			case TimeSpan duration:
				return Text(duration.ToString(format, provider), source);
#if NET
			case DateOnly date when isDefault:
				return Date(date.ToDateTime(TimeOnly.MinValue), date.ToString("O", CultureInfo.InvariantCulture), options, source);
			case DateOnly date:
				return Formatted(date.ToString(format, provider), t => DateOnly.TryParseExact(t, format!, provider, DateTimeStyles.None, out var r) && r.ToString(format, provider) == t ? r.ToDateTime(TimeOnly.MinValue) : null, translated, source);
			case TimeOnly time when isDefault:
				return Time(time.Ticks, source);
			case TimeOnly time:
				return Formatted(time.ToString(format, provider), t => TimeOnly.TryParseExact(t, format!, provider, DateTimeStyles.None, out var r) && r.ToString(format, provider) == t ? new DateTime(r.Ticks) : null, translated, source);
			case Half half:
				return Float(double.Parse(half.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), half.ToString(CultureInfo.InvariantCulture), source);
			case Int128 or UInt128:
				return Integer((IFormattable)value, provider, options, source);
#endif
			case double number:
				return Float(number, number.ToString("R", CultureInfo.InvariantCulture), source);
			case float number:
				return Float(double.Parse(Shortest(number), CultureInfo.InvariantCulture), number.ToString(CultureInfo.InvariantCulture), source);
			case sbyte or byte or short or ushort or int or uint or long or ulong or decimal:
				return Integer((IFormattable)value, provider, options, source);
			case Uri uri:
				// The URI as given, whether a link or not; the written set's URIs written by hand are the same as their text.
				return Text(uri.OriginalString, source);
			default:
				return Text(value.ToString()!, source);
		}
	}

	/// <summary>
	/// Each Excel sheet's look, as the plan's "What a sheet gets" says, from each sheet's settings and the values written:
	/// the frozen pane over the header's rows and the frozen columns; the filter over the header and the rows below it,
	/// without a table; the table, named as its look says; the title's merge over the header's columns, and the ranges
	/// merged by hand; a link in each cell of a URI the policy takes, outside the header; the hidden columns; the title's row
	/// and the rows whose text wraps (a line break, or more characters than its column's width) taller than standard; and
	/// each cell's look from row 1 to the row below the used range: the title in bold Calibri 14, the header bold but in a
	/// styled table, a link's cell in the <c>Hyperlink</c> style, and a column's wrap in its cells, and its format and wrap
	/// in a cell not written.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="used">The used range of each sheet as Excel answered it, by name: the last row and 0-based column.</param>
	/// <returns>Each sheet's name and look.</returns>
	public static List<(string Sheet, SheetLook Look)> Looks(WrittenWorkbook workbook, IReadOnlyDictionary<string, (long Row, int Column)> used)
	{
		var result = new List<(string, SheetLook)>();
		var cells = Cells(workbook).ToDictionary(s => s.Sheet, s => s.Cells);
		foreach (var sheet in workbook.Sheets)
		{
			var settings = Settings(workbook, sheet);
			var columns = ColumnOptions(workbook, sheet);
			var count = sheet.Rows[0].Count;
			var last = CellReference.ColumnName(count - 1);
			var header = settings.Title is null ? 1 : 2;
			var chunks = Chunks(workbook, sheet);
			for (var i = 0; i < chunks.Count; i++)
			{
				var (name, rows) = chunks[i];
				var lastRow = header + rows.Count;
				var written = cells[name];
				var merged = new List<string>();
				if (settings.Title is not null && count > 1)
				{
					merged.Add($"A1:{last}1");
				}

				if (i == 0)
				{
					merged.AddRange(sheet.Look.Merges.Select(m => m.Range));
				}

				var links = new List<SheetLink>();
				var tall = new List<long>();
				if (settings.Title is not null)
				{
					tall.Add(1);
				}

				for (var r = 0; r < rows.Count; r++)
				{
					var row = header + 1 + r;
					for (var c = 0; c < rows[r].Count; c++)
					{
						if (rows[r][c] is Uri uri && Takes(workbook.Options.Links, uri))
						{
							var (address, subAddress) = LinkOf(uri);
							links.Add(new SheetLink(CellReference.ColumnName(c) + row.ToString(CultureInfo.InvariantCulture), address, subAddress));
						}

						if (columns[c] is { Wrap: true } wrap && rows[r][c] is string text && (text.Contains('\n') || text.Length > (wrap.Width ?? double.MaxValue)))
						{
							tall.Add(row);
						}
					}
				}

				var looks = new Dictionary<string, string>(StringComparer.Ordinal);
				var (usedRow, usedColumn) = used[name];
				for (var row = 1L; row <= usedRow + 1; row++)
				{
					for (var c = 0; c <= usedColumn; c++)
					{
						var column = c < columns.Length ? columns[c] : null;
						var reference = CellReference.ColumnName(c) + row.ToString(CultureInfo.InvariantCulture);
						string[] parts = !written.ContainsKey((row, c))
							? [column is { Wrap: true } ? "wrap" : "", column?.Format is { } code ? "format " + ExcelCode(code) : ""]
							: row == 1 && settings.Title is not null
								? ["bold", "size 14"]
								: row == header
									? [settings.BoldHeader && settings.Table?.Style is null ? "bold" : ""]
									: [links.Any(l => l.Cell == reference) ? "Hyperlink" : "", column is { Wrap: true } ? "wrap" : ""];
						var look = string.Join(", ", parts.Where(p => p.Length > 0));
						if (look.Length > 0)
						{
							looks[reference] = look;
						}
					}
				}

				result.Add((name, new SheetLook
				{
					Freeze = (settings.FreezeHeader ? header : 0, settings.FreezeColumns) is var (frozenRows, frozenColumns) && (frozenRows > 0 || frozenColumns > 0) ? (frozenRows, frozenColumns) : null,
					Filter = settings.AutoFilter && settings.Table is null ? $"A{header}:{last}{Math.Max(lastRow, header)}" : null,
					Tables = settings.Table is { } table
						?
						[
							new SheetTable(
								sheet.Look.Tables![i], $"A{header}:{last}{Math.Max(lastRow, header + 1)}", StyleName(table.Style), table.BandedRows, table.FilterButtons,
								sheet.Look.TableColumns ?? [.. sheet.Rows[0].Select(n => (string)n!)]),
						]
						: [],
					Merged = merged,
					Links = links,
					HiddenColumns = [.. columns.Select((o, c) => (o, c)).Where(x => x.o is { Hidden: true }).Select(x => CellReference.ColumnName(x.c))],
					TallRows = tall,
					Cells = looks,
				}));
			}
		}

		return result;
	}

	/// <summary>
	/// The hidden names Excel gives the workbook's filters, as Excel writes them: <c>_FilterDatabase</c> of each filtered
	/// sheet, over the filter's range, with the sheet's name quoted as in formulas.
	/// </summary>
	/// <param name="looks">Each sheet's look (<see cref="Looks"/>).</param>
	/// <returns>The names.</returns>
	public static List<WrittenName> FilterNames(IEnumerable<(string Sheet, SheetLook Look)> looks) =>
	[
		.. looks.Where(l => l.Look.Filter is not null).Select(l =>
		{
			var sheet = SheetLocation.Quote(l.Sheet);
			var corners = l.Look.Filter!.Split(':');

			return new WrittenName(sheet + "!_FilterDatabase", $"={sheet}!{Absolute(corners[0])}:{Absolute(corners[1])}", false);
		}),
	];

	/// <summary>
	/// A format code as Excel's <c>NumberFormat</c> gives it: a dollar sign in quotes, which needs none, without them. The
	/// written set's codes need no other change.
	/// </summary>
	/// <param name="code">The code written.</param>
	/// <returns>The code Excel gives.</returns>
	public static string ExcelCode(string code) => code.Replace("\"$\"", "$");

	/// <summary>Whether a policy makes a URI a link: <see cref="ExcelLinks.Web"/> an absolute http, https or mailto URI, <see cref="ExcelLinks.All"/> any.</summary>
	/// <param name="links">The policy.</param>
	/// <param name="uri">The URI.</param>
	/// <returns>Whether it is a link.</returns>
	private static bool Takes(ExcelLinks links, Uri uri) => links switch
	{
		ExcelLinks.All => true,
		ExcelLinks.Web => uri.IsAbsoluteUri && uri.Scheme is "http" or "https" or "mailto",
		_ => false,
	};

	/// <summary>
	/// A link's <c>Address</c> and <c>SubAddress</c> as Excel shows them, by the test's own rules: a jump within the
	/// workbook (<c>#Other!B2</c>) has no address; a relative URI's address is its text; a file's is its local path; and
	/// another's is its absolute form, its fragment the sub-address. Escaped letters past ASCII show as the letters.
	/// </summary>
	/// <param name="uri">The URI.</param>
	/// <returns>The address and sub-address.</returns>
	internal static (string? Address, string? SubAddress) LinkOf(Uri uri)
	{
		if (!uri.IsAbsoluteUri)
		{
			return uri.OriginalString.StartsWith("#", StringComparison.Ordinal) ? (null, uri.OriginalString.Substring(1)) : (PastAscii(uri.OriginalString), null);
		}

		if (uri.IsFile)
		{
			return (uri.LocalPath, null);
		}

		var absolute = uri.AbsoluteUri;
		var hash = absolute.IndexOf('#');

		return hash < 0 ? (PastAscii(absolute), null) : (PastAscii(absolute.Substring(0, hash)), absolute.Substring(hash + 1));
	}

	/// <summary>A text with its escapes of UTF-8 past ASCII (<c>%C3%A9</c>) decoded, and its escapes of ASCII kept.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text decoded.</returns>
	private static string PastAscii(string text)
	{
		var result = new StringBuilder();
		var bytes = new List<byte>();
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '%' && i + 2 < text.Length && byte.TryParse(text.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b) && b >= 0x80)
			{
				bytes.Add(b);
				i += 2;

				continue;
			}

			result.Append(Encoding.UTF8.GetString([.. bytes]));
			bytes.Clear();
			result.Append(text[i]);
		}

		return result.Append(Encoding.UTF8.GetString([.. bytes])).ToString();
	}

	/// <summary>A table style's name as Excel gives it: a built-in's in Excel's case, or <see langword="null"/> for none.</summary>
	/// <param name="style">The style given.</param>
	/// <returns>The name.</returns>
	private static string? StyleName(string? style)
	{
		if (style is null)
		{
			return null;
		}

		foreach (var kind in new[] { "Light", "Medium", "Dark" })
		{
			var prefix = "TableStyle" + kind;
			if (style.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return prefix + style.Substring(prefix.Length);
			}
		}

		return style;
	}

	/// <summary>A cell reference made absolute, as Excel writes a defined name's.</summary>
	/// <param name="reference">The reference, such as <c>A1</c>.</param>
	/// <returns>The reference, such as <c>$A$1</c>.</returns>
	private static string Absolute(string reference)
	{
		var letters = new string([.. reference.TakeWhile(char.IsLetter)]);

		return "$" + letters + "$" + reference.Substring(letters.Length);
	}

	/// <summary>
	/// The width Excel gives a column at a DPI, as <c>ColumnWidth</c> shows it: a width in characters of the maximum digit
	/// width becomes whole pixels, less the padding, in digits, to two decimals. Calibri 11's digit is 7 pixels at 96 DPI
	/// and 11 at 144, the padding 5 and 7, and a column without a width 64 and 96 pixels.
	/// </summary>
	/// <param name="width">The width written, or <see langword="null"/> for the default.</param>
	/// <param name="dpi">The DPI.</param>
	/// <returns>The width Excel shows.</returns>
	public static double ExcelWidth(double? width, int dpi)
	{
		var (digit, padding, standard) = dpi switch
		{
			96 => (7, 5, 64),
			144 => (11, 7, 96),
			_ => throw new InvalidOperationException($"Excel's widths are known at 96 and 144 DPI, and the answers were made at {dpi}."),
		};
		var pixels = width is { } w ? (int)Math.Truncate((256 * w + Math.Truncate(128.0 / digit)) / 256 * digit) : standard;

		return Math.Round((pixels - padding) / (double)digit, 2, MidpointRounding.AwayFromZero);
	}

	/// <summary>
	/// The number format code and the columns the writer gave each sheet, from the workbook's parts (through the Open XML
	/// SDK): each cell's code by row and 0-based column, and each column's width, whether it is hidden, and whether auto-fit
	/// sized it (<c>bestFit</c>), 0-based.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's parts, by name.</returns>
	public static Dictionary<string, WrittenParts> Parts(byte[] workbook)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), false);
		var part = document.WorkbookPart!;
		var stylesheet = part.WorkbookStylesPart!.Stylesheet!;
		var custom = stylesheet.NumberingFormats?.Elements<NumberingFormat>().ToDictionary(n => n.NumberFormatId!.Value, n => n.FormatCode!.Value!) ?? [];
		var formats = stylesheet.CellFormats!.Elements<CellFormat>().Select(f => f.NumberFormatId?.Value ?? 0).ToList();
		var result = new Dictionary<string, WrittenParts>();
		foreach (var sheet in part.Workbook!.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
		{
			var worksheet = ((WorksheetPart)part.GetPartById(sheet.Id!)).Worksheet!;
			var codes = new Dictionary<(long, int), string>();
			foreach (var cell in worksheet.Descendants<Cell>())
			{
				var id = formats[(int)(cell.StyleIndex?.Value ?? 0)];
				var reference = cell.CellReference!.Value!;
				var letters = new string([.. reference.TakeWhile(char.IsLetter)]);
				codes[(long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture), letters.Aggregate(0, (c, l) => c * 26 + (l - 'A' + 1)) - 1)] =
					custom.TryGetValue(id, out var code) ? code : BuiltIn[id];
			}

			var widths = new Dictionary<int, double>();
			var hidden = new HashSet<int>();
			var fitted = new HashSet<int>();
			foreach (var column in worksheet.Descendants<Column>())
			{
				for (var c = (int)column.Min!.Value - 1; c < column.Max!.Value; c++)
				{
					widths[c] = column.Width!.Value;
					if (column.Hidden?.Value == true)
					{
						hidden.Add(c);
					}

					if (column.BestFit?.Value == true)
					{
						fitted.Add(c);
					}
				}
			}

			result[DecodeName(sheet.Name!.Value!)] = new WrittenParts(codes, widths, hidden, fitted);
		}

		return result;
	}

	/// <summary>A sheet's name as Excel reads it: its <c>_xHHHH_</c> escapes decoded, which the Open XML SDK leaves.</summary>
	/// <param name="name">The name as the SDK gives it.</param>
	/// <returns>The name.</returns>
	private static string DecodeName(string name) => CorpusFiles.DecodeEscapes(name);

	/// <summary>A text cell.</summary>
	/// <param name="text">Its text.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Text(string text, string source) => new() { Kind = ExpectedKind.Text, Text = text, Source = source };

	/// <summary>
	/// A number of a floating-point type: text for <c>NaN</c> and the infinities, which Excel can't open as numbers, and the
	/// value otherwise.
	/// </summary>
	/// <param name="number">The value, as a double.</param>
	/// <param name="text">Its invariant text.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Float(double number, string text, string source) =>
		double.IsNaN(number) || double.IsInfinity(number) ? Text(text, source) : new ExpectedCell { Kind = ExpectedKind.Number, Number = number, Source = source };

	/// <summary>
	/// An integer or a decimal: past 15 significant digits (from the first digit that isn't 0 to the last), as
	/// <see cref="ExcelWriterOptions.LongNumbers"/> says, text of the field as written, or the number Excel reads its digits as.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="provider">The column's culture, which the field's text is in.</param>
	/// <param name="options">The options.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Integer(IFormattable value, IFormatProvider provider, ExcelWriterOptions options, string source)
	{
		var invariant = value.ToString(null, CultureInfo.InvariantCulture);
		var digits = invariant.Where(char.IsDigit).SkipWhile(d => d == '0').Reverse().SkipWhile(d => d == '0').Count();
		if (digits > 15 && options.LongNumbers == ExcelLongNumbers.Text)
		{
			return Text(value.ToString(null, provider), source);
		}

		return new ExpectedCell { Kind = ExpectedKind.Number, Number = double.Parse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture), Source = source };
	}

	/// <summary>
	/// A date and time in the writer's own format: its serial, rounded to the millisecond, in <c>yyyy-mm-dd</c> at midnight,
	/// with seconds when it has no milliseconds, and with milliseconds otherwise; before 1900-01-01, as
	/// <see cref="ExcelWriterOptions.OutOfRangeDates"/> says, text of the field as written.
	/// </summary>
	/// <param name="date">The clock time.</param>
	/// <param name="written">The field's text.</param>
	/// <param name="options">The options.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Date(DateTime date, string written, ExcelWriterOptions options, string source)
	{
		var rounded = Round(date);
		if (rounded < January1900)
		{
			return Text(written, source);
		}

		var shown = rounded.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : rounded.Millisecond == 0 ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss.fff";

		return new ExpectedCell { Kind = ExpectedKind.Number, Number = Serial(rounded), NumberType = "date", Shows = rounded.ToString(shown, CultureInfo.InvariantCulture), Source = source };
	}

	/// <summary>A time of day: a fraction of a day, to the millisecond, the last millisecond at most.</summary>
	/// <param name="ticks">Its ticks.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Time(long ticks, string source)
	{
		var milliseconds = Math.Min((ticks + 5_000) / 10_000, 86_399_999);
		var time = new DateTime(milliseconds * 10_000);

		return new ExpectedCell
		{
			Kind = ExpectedKind.Number, Number = milliseconds / MillisecondsPerDay,
			Shows = time.ToString(milliseconds % 1000 == 0 ? "HH:mm:ss" : "HH:mm:ss.fff", CultureInfo.InvariantCulture), Source = source,
		};
	}

	/// <summary>
	/// A duration: days, to the millisecond, shown in hours as <c>[h]:mm:ss</c>; a negative one of half a millisecond or more,
	/// as <see cref="ExcelWriterOptions.OutOfRangeDates"/> says, text of the field as written.
	/// </summary>
	/// <param name="duration">The duration.</param>
	/// <param name="options">The options.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Duration(TimeSpan duration, ExcelWriterOptions options, string source)
	{
		if (duration.Ticks <= -5_000)
		{
			return Text(duration.ToString("c", CultureInfo.InvariantCulture), source);
		}

		var milliseconds = duration.Ticks < 0 ? 0 : (duration.Ticks + 5_000) / 10_000;
		var shows = $"{milliseconds / 3_600_000}:{milliseconds / 60_000 % 60:00}:{milliseconds / 1000 % 60:00}" + (milliseconds % 1000 == 0 ? "" : $".{milliseconds % 1000:000}");

		return new ExpectedCell { Kind = ExpectedKind.Number, Number = milliseconds / MillisecondsPerDay, Shows = shows, Source = source };
	}

	/// <summary>
	/// A value in its member's format: .NET's text, which Excel shows whether the cell is text or typed, and the serial the
	/// text reads back as, with the format and culture, by the parser of the value's type: a time of day alone for a format
	/// without a date.
	/// </summary>
	/// <param name="text">.NET's text of the value.</param>
	/// <param name="parse">
	/// Reads the text back with the format and culture, as a date on 0001-01-01 when it has no date; <see langword="null"/>
	/// when it can't, or when the value doesn't format to the same text.
	/// </param>
	/// <param name="translated">Whether Excel shows the format as .NET does.</param>
	/// <param name="source">What it holds.</param>
	/// <returns>The cell.</returns>
	private static ExpectedCell Formatted(string text, Func<string, DateTime?> parse, bool translated, string source)
	{
		var number = double.NaN;
		var type = "number";
		if (parse(text) is { } read)
		{
			if (read.Date == DateTime.MinValue)
			{
				number = Round(read).TimeOfDay.TotalMilliseconds / MillisecondsPerDay;
			}
			else if (Round(read) >= January1900)
			{
				number = Serial(Round(read));
				type = "date";
			}
		}

		return new ExpectedCell { Kind = ExpectedKind.Formatted, Text = text, Number = number, NumberType = type, Typed = translated && !double.IsNaN(number), Shows = text, Source = source };
	}

	/// <summary>A date rounded to the millisecond, half up, and to Excel's last millisecond at most.</summary>
	/// <param name="date">The date.</param>
	/// <returns>The rounded date.</returns>
	private static DateTime Round(DateTime date)
	{
		var milliseconds = date.Ticks / 10_000 + (date.Ticks % 10_000 >= 5_000 ? 1 : 0);

		return milliseconds * 10_000 > LastMillisecond.Ticks ? LastMillisecond : new DateTime(milliseconds * 10_000);
	}

	/// <summary>A date's serial in the 1900 date system, which counts 1900-02-29.</summary>
	/// <param name="date">The date, rounded to the millisecond, from 1900-01-01.</param>
	/// <returns>The serial.</returns>
	private static double Serial(DateTime date) =>
		(date - (date < March1900 ? new DateTime(1899, 12, 31) : new DateTime(1899, 12, 30))).Ticks / 10_000 / MillisecondsPerDay;

	/// <summary>
	/// A float's shortest text that reads back as it, which the writer copies into the cell: .NET Framework's
	/// <see cref="float.ToString()"/> isn't, so the digits are tried from one up. (A text past the largest float reads as
	/// infinity on .NET, and fails on .NET Framework.)
	/// </summary>
	/// <param name="number">The float.</param>
	/// <returns>The text.</returns>
	private static string Shortest(float number)
	{
		for (var digits = 1; digits < 9; digits++)
		{
			var text = ((double)number).ToString("E" + (digits - 1), CultureInfo.InvariantCulture);
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == number)
			{
				return text;
			}
		}

		return ((double)number).ToString("E8", CultureInfo.InvariantCulture);
	}
}
