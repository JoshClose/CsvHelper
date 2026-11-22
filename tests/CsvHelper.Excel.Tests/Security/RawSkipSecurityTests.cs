using CsvHelper.Excel.Tests.Sheet;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The raw skips against large content (Phase 9's step 2): a megabyte comment, and a megabyte attribute value, in a hidden
/// row, read under a window that can't grow past 64 KB, as <c>ExcelReaderOptions.MaxPartSize</c> at 64 KB makes it. The
/// merged cells' pre-read skips <c>sheetData</c>, and the sheet's reader the row, keeping only the last bytes of what they
/// pass over, so neither grows the window; a token skip holds each token whole, and stops at the window's limit. Each with
/// the sheet scanner's fast cells on and off (Phase 9's step 3), which read the shown rows' cells either way.
/// </summary>
public class RawSkipSecurityTests
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The window's size limit.</summary>
	private const int WindowLimit = 64 * 1024;

	/// <summary>
	/// The modes the megabyte sheets are read in: whole reads from a 64 KB window, which can't grow, synchronously; and from
	/// a 16-byte one, which can grow to the limit, asynchronously.
	/// </summary>
	private static readonly ScanMode[] Modes = [ScanMode.All[0], ScanMode.All[3]];

	/// <summary>The large content of the hidden row: a comment, or an attribute value.</summary>
	public static TheoryData<string> Contents => new() { "comment", "value" };

	[Theory]
	[MemberData(nameof(Contents))]
	public async Task SkipsAMegabyteWithoutGrowingTheWindow(string content)
	{
		var xml = Sheet(content);
		var readings = new (string Name, CellReading Reading, string[] Records)[]
		{
			("merged cells filled and hidden rows skipped", CellReading.Merged(rows: true), new[] { "1: number:1 | number:1", "3: number:3 | ()" }),
			("hidden rows skipped", CellReading.Hidden(rows: true, columns: false), new[] { "1: number:1", "3: number:3" }),
			("an area below the row", CellReading.Area("A3:B3"), new[] { "3: number:3" }),
		};

		foreach (var scan in Modes.SelectMany(m => new[] { m, m.WithFastCells(false) }))
		{
			foreach (var (name, reading, records) in readings)
			{
				// Raw, the pre-read and the row's skip pass over it.
				var mode = new SheetMode(scan, int.MaxValue);
				Assert.Equal(records, await Sheets.ReadAsync(xml, mode, maxSize: WindowLimit, reading: reading));

				// Token by token, the pre-read (with merged cells filled) or the row's skip must hold it whole.
				var e = await Record.ExceptionAsync(() => Sheets.ReadAsync(xml, mode.WithRawSkips(false), maxSize: WindowLimit, reading: reading));
				Assert.True(e is CsvFormatException or InvalidDataException, $"{scan}, {name}: {e?.GetType().Name} {e?.Message}");
				Assert.EndsWith($"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({WindowLimit} bytes).", e!.Message);
			}
		}
	}

	/// <summary>A worksheet part whose hidden row 2 has a megabyte of content, between shown rows 1 and 3, with <c>A1:B1</c> merged.</summary>
	/// <param name="content">A comment, after the row's cell, or an attribute value of the cell.</param>
	/// <returns>The part's XML.</returns>
	private static string Sheet(string content)
	{
		var filler = new string('x', MiB);
		var row = content == "comment"
			? $"<c r=\"A2\"><v>2</v></c><!--{filler}-->"
			: $"<c r=\"A2\" t=\"str\" note=\"{filler}\"><v>2</v></c>";

		return MergedScannerTests.Worksheet($"<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\" hidden=\"1\">{row}</row><row r=\"3\"><c r=\"A3\"><v>3</v></c></row>", "A1:B1");
	}
}
