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

	/// <summary>The type of Excel's <c>Value</c> for a number: <c>number</c>, or <c>date</c> for a value with a day, a month or a year.</summary>
	public string NumberType { get; init; } = "number";

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

/// <summary>
/// The written set's workbooks as Excel should read them, by rules of their own: every cell from the value the content set
/// wrote and its column's format and culture, as Phase 4's "What a field becomes" says, and the widths and number formats
/// from the workbook's own parts.
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
	private static readonly Dictionary<uint, string> BuiltIn = new()
	{
		[0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%", [11] = "0.00E+00", [12] = "# ?/?",
		[13] = "# ??/??", [46] = "[h]:mm:ss", [48] = "##0.0E+0", [49] = "@",
	};

	/// <summary>
	/// The cells of each Excel sheet a workbook becomes, by sheet name, then by row and 0-based column: each sheet's header in
	/// row 1, repeated in the sheets <see cref="ExcelRowLimit.NewSheet"/> adds, and its rows after it.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The cells.</returns>
	public static List<(string Sheet, Dictionary<(long Row, int Column), ExpectedCell> Cells)> Cells(WrittenWorkbook workbook)
	{
		var result = new List<(string, Dictionary<(long, int), ExpectedCell>)>();
		var options = workbook.Options;
		foreach (var sheet in workbook.Sheets)
		{
			var header = options.Csv.HasHeader ? sheet.Rows[0] : null;
			var data = sheet.Rows.Skip(header is null ? 0 : 1).ToList();
			var perSheet = workbook.MaxRows is { } rows ? rows - (header is null ? 0 : 1) : int.MaxValue;
			var chunks = Enumerable.Range(0, Math.Max(1, (data.Count + perSheet - 1) / perSheet)).Select(i => data.Skip(i * perSheet).Take(perSheet).ToList()).ToList();
			Assert.True(chunks.Count == sheet.Names.Count, $"{sheet.Name} becomes {chunks.Count} sheets, and the content set names {sheet.Names.Count}.");
			var shows = sheet.Columns.Select(c => c.Name).ToList().IndexOf("Shows");
			for (var i = 0; i < chunks.Count; i++)
			{
				var name = sheet.Names[i];
				var cells = new Dictionary<(long, int), ExpectedCell>();
				var lines = header is null ? chunks[i] : [header, .. chunks[i]];
				for (var r = 0; r < lines.Count; r++)
				{
					var isHeader = header is not null && r == 0;
					for (var c = 0; c < lines[r].Count; c++)
					{
						var column = sheet.Columns[c];
						var source = $"{new SheetLocation(name).Cell(c, r + 1)} [{column.Format}] [{column.Culture}] {lines[r][c]?.GetType().Name ?? "null"}";
						var expected = isHeader ? Expect(lines[r][c], null, null, options, source) : Expect(lines[r][c], column.Format, column.Culture, options, source);
						if (expected is { Kind: ExpectedKind.Formula })
						{
							Assert.True(shows >= 0, $"{sheet.Name} has a formula, and no Shows column for the text Excel shows of it.");
							expected = new ExpectedCell { Kind = ExpectedKind.Formula, Formula = expected.Formula, Shows = lines[r][shows] as string, Source = source };
						}

						if (expected is not null)
						{
							cells[(r + 1, c)] = expected;
						}
					}
				}

				result.Add((name, cells));
			}
		}

		return result;
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
			default:
				return Text(value.ToString()!, source);
		}
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
	/// The number format code and the column widths the writer gave each sheet, from the workbook's parts (through the Open
	/// XML SDK): each cell's code by row and 0-based column, and each column's width, 0-based.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's codes and widths, by name.</returns>
	public static Dictionary<string, (Dictionary<(long Row, int Column), string> Codes, Dictionary<int, double> Widths)> Parts(byte[] workbook)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), false);
		var part = document.WorkbookPart!;
		var stylesheet = part.WorkbookStylesPart!.Stylesheet!;
		var custom = stylesheet.NumberingFormats?.Elements<NumberingFormat>().ToDictionary(n => n.NumberFormatId!.Value, n => n.FormatCode!.Value!) ?? [];
		var formats = stylesheet.CellFormats!.Elements<CellFormat>().Select(f => f.NumberFormatId?.Value ?? 0).ToList();
		var result = new Dictionary<string, (Dictionary<(long, int), string>, Dictionary<int, double>)>();
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
			foreach (var column in worksheet.Descendants<Column>())
			{
				for (var c = column.Min!.Value; c <= column.Max!.Value; c++)
				{
					widths[(int)c - 1] = column.Width!.Value;
				}
			}

			result[DecodeName(sheet.Name!.Value!)] = (codes, widths);
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
