using System.Text;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>A cell's value as a reader gives it: its text, and what kind of value it is.</summary>
/// <param name="text">The text: a number in invariant form, a boolean as <c>1</c> or <c>0</c>, an error such as <c>#N/A</c>.</param>
/// <param name="kind">What it is.</param>
internal readonly struct CellValue(string text, SheetCellKind kind) : IEquatable<CellValue>
{
	/// <summary>The text.</summary>
	public string Text => text;

	/// <summary>What it is.</summary>
	public SheetCellKind Kind => kind;

	/// <summary>Whether two values are the same text and kind.</summary>
	/// <param name="left">A value.</param>
	/// <param name="right">Another.</param>
	/// <returns>Whether they are equal.</returns>
	public static bool operator ==(CellValue left, CellValue right) => left.Equals(right);

	/// <summary>Whether two values differ.</summary>
	/// <param name="left">A value.</param>
	/// <param name="right">Another.</param>
	/// <returns>Whether they differ.</returns>
	public static bool operator !=(CellValue left, CellValue right) => !left.Equals(right);

	/// <inheritdoc/>
	public bool Equals(CellValue other) => Text == other.Text && Kind == other.Kind;

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is CellValue other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => (Text, Kind).GetHashCode();

	/// <inheritdoc/>
	public override string ToString() => $"{Kind}:{Show(Text)}";

	/// <summary>Text for a failure message: control characters escaped, and long text cut short.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text to show.</returns>
	public static string Show(string text)
	{
		var shown = string.Concat(text.Select(c => c < ' ' ? $"<{(int)c:X2}>" : c.ToString()));

		return shown.Length > 60 ? $"{shown.Substring(0, 40)}...({text.Length} characters)" : shown;
	}
}

/// <summary>The cells with a value of each worksheet of a workbook, by sheet name and then by row and 0-based column.</summary>
internal sealed class SheetCells : Dictionary<string, Dictionary<(long Row, int Column), CellValue>>
{
}

/// <summary>The committed corpus (<c>Files/</c>, see its <c>README.md</c>), and reading its workbooks with CsvHelper.Excel.</summary>
internal static class CorpusFiles
{
	/// <summary>The corpus folder in the test output.</summary>
	public static string Folder => Path.Combine(AppContext.BaseDirectory, "Files");

	/// <summary>The workbooks CsvHelper.Excel reads, by path relative to <see cref="Folder"/>, with <c>/</c> between folders.</summary>
	public static IReadOnlyList<string> Workbooks { get; } =
		[.. Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
			.Where(p => Path.GetExtension(p) is ".xlsx" or ".xlsm" or ".xltx")
			.Select(p => p.Substring(Folder.Length + 1).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(p => p != "excel/encrypted.xlsx")
			.OrderBy(p => p, StringComparer.Ordinal)];

	/// <summary>The workbooks, as theory data.</summary>
	public static IEnumerable<object[]> WorkbookData => Workbooks.Select(w => new object[] { w });

	/// <summary>The full path of a corpus file.</summary>
	/// <param name="file">The file, relative to <see cref="Folder"/>.</param>
	/// <returns>The path.</returns>
	public static string PathOf(string file) => Path.Combine(Folder, file);

	/// <summary>
	/// Reads every worksheet of a workbook with CsvHelper.Excel's own layers (the package, the workbook part, the styles,
	/// the shared strings and the sheet scanner), so that each cell's kind can be compared, not only its text.
	/// </summary>
	/// <param name="file">The workbook, relative to <see cref="Folder"/>.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The cells with a value of each worksheet.</returns>
	public static SheetCells Read(string file, out bool date1904)
	{
		using var archive = SyncOrAsync.Result(PackageArchive.OpenAsync(PathOf(file), Packages.Limits, false, CancellationToken.None));
		var workbook = SyncOrAsync.Result(WorkbookPart.LoadAsync(archive, Packages.Limits, false, CancellationToken.None));
		var styles = SyncOrAsync.Result(StyleTable.LoadAsync(archive, workbook.StylesPart, Packages.Limits, false, CancellationToken.None));
		var strings = SyncOrAsync.Result(SharedStrings.LoadAsync(archive, workbook.SharedStringsPart, Packages.Limits, false, CancellationToken.None));
		date1904 = workbook.Date1904;
		var result = new SheetCells();
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet && s.PartName is not null))
		{
			var stream = SyncOrAsync.Result(archive.OpenPartAsync(sheet.PartName!, false, Packages.Limits, false, CancellationToken.None));
			using var scanner = new SheetScanner(new XmlScanner(new XmlWindow(stream, sheet.PartName!, Packages.Limits.MaxPartSize)), sheet.Name, strings, styles, true, stream);
			var captured = new Captured(int.MaxValue);
			Sheets.FillAllAsync(scanner, captured, false).GetAwaiter().GetResult();
			var cells = new Dictionary<(long, int), CellValue>();
			foreach (var record in captured.Records)
			{
				for (var column = 0; column < record.Fields.Count; column++)
				{
					var (text, kind) = record.Fields[column];
					if (kind != SheetCellKind.Empty)
					{
						cells[(record.Row, column)] = new CellValue(text, kind);
					}
				}
			}

			result[sheet.Name] = cells;
		}

		return result;
	}

	/// <summary>Describes the differences between two readings of a workbook, one line per cell, ordered.</summary>
	/// <param name="ours">CsvHelper.Excel's cells.</param>
	/// <param name="theirs">An oracle's cells, each with a value or the reason it can't be compared.</param>
	/// <param name="sheets">The sheets to compare: those both have.</param>
	/// <returns>The differences, such as <c>Data!B3: ours Number:42, theirs Text:42</c>.</returns>
	public static List<string> Differences(SheetCells ours, SheetCells theirs, IEnumerable<string> sheets)
	{
		var differences = new List<string>();
		foreach (var sheet in sheets)
		{
			var mine = ours.TryGetValue(sheet, out var a) ? a : [];
			var other = theirs.TryGetValue(sheet, out var b) ? b : [];
			foreach (var key in mine.Keys.Union(other.Keys).OrderBy(k => k.Row).ThenBy(k => k.Column))
			{
				var hasMine = mine.TryGetValue(key, out var x);
				var hasOther = other.TryGetValue(key, out var y);
				if (hasMine && hasOther && Same(x, y))
				{
					continue;
				}

				var location = new SheetLocation(sheet).Cell(key.Column, key.Row);
				differences.Add($"{location}: ours {(hasMine ? x.ToString() : "valueless")}, theirs {(hasOther ? y.ToString() : "valueless")}");
			}
		}

		return differences;
	}

	/// <summary>
	/// Whether an oracle's value is ours: the same text and kind, or the same number. A date an oracle gives as a date is
	/// the same as our number within a few milliseconds, since a .NET date from a serial is rounded to the millisecond;
	/// Phase 1 reads dates as their serials.
	/// </summary>
	/// <param name="ours">CsvHelper.Excel's value.</param>
	/// <param name="theirs">The oracle's value.</param>
	/// <returns>Whether they are the same.</returns>
	public static bool Same(CellValue ours, CellValue theirs)
	{
		if (ours == theirs)
		{
			return true;
		}

		if (ours.Kind != SheetCellKind.Number || theirs.Kind is not (SheetCellKind.Number or SheetCellKind.DateNumber)
			|| !double.TryParse(ours.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a)
			|| !double.TryParse(theirs.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
		{
			return false;
		}

		return theirs.Kind == SheetCellKind.Number ? a == b : Math.Abs(a - b) < 1e-7;
	}

	/// <summary>Decodes the <c>_xHHHH_</c> escapes of a text with a regular expression, then as UTF-8 (a lone surrogate becomes U+FFFD).</summary>
	/// <param name="text">The text, with its XML decoded.</param>
	/// <returns>The decoded text.</returns>
	public static string DecodeEscapes(string text)
	{
		var decoded = System.Text.RegularExpressions.Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

		return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(decoded));
	}
}
