#if NET
using System.Text;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// What skipping rows allocates (Phase 9's step 2): nothing per row, with raw skips and with token skips. A sheet scanner
/// skipping hidden rows, and the merged cells' pre-read skipping <c>sheetData</c>, allocate within a small constant of
/// each other for 10,000 rows and for 100,000. It runs alone, in <see cref="TimedCollection"/>, as the import's allocation
/// test does, since other tests' garbage collections trim the shared array pool the window rents from.
/// </summary>
/// <param name="output">Shows what the tests measured.</param>
[Collection(TimedCollection.Name)]
public class SkipAllocationTests(ITestOutputHelper output)
{
	/// <summary>The most 100,000 rows may allocate beyond 10,000: far less than a byte for each of the 90,000 more.</summary>
	private const long Slack = 32 * 1024;

	/// <summary>A sheet of 10,000 hidden rows, made once.</summary>
	private static readonly Lazy<byte[]> Small = new(() => Sheet(10_000));

	/// <summary>A sheet of 100,000 hidden rows, made once.</summary>
	private static readonly Lazy<byte[]> Large = new(() => Sheet(100_000));

	[OptimizedFact]
	public void SkipsHiddenRowsWithoutAllocatingPerRow()
	{
		foreach (var raw in new[] { true, false })
		{
			var (few, many) = Measure(sheet => Skipped(sheet, raw));
			var skips = raw ? "raw" : "token by token";
			output.WriteLine($"Skipping hidden rows {skips} allocated {few:N0} bytes for 10,000 rows and {many:N0} for 100,000.");

			Assert.True(many - few < Slack, $"Skipping hidden rows {skips}, 10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		}
	}

	[OptimizedFact]
	public void PreReadsWithoutAllocatingPerRow()
	{
		foreach (var raw in new[] { true, false })
		{
			var (few, many) = Measure(sheet => PreRead(sheet, raw));
			var skips = raw ? "raw" : "token by token";
			output.WriteLine($"The pre-read skipping sheetData {skips} allocated {few:N0} bytes for 10,000 rows and {many:N0} for 100,000.");

			Assert.True(many - few < Slack, $"The pre-read skipping sheetData {skips}, 10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		}
	}

	/// <summary>
	/// Measures a read of both sheets: each once first, then three times, keeping the smallest, as the import's allocation
	/// test does, since the runtime allocates once at a moment tiering sets.
	/// </summary>
	/// <param name="allocated">Reads a sheet, and gives the bytes it allocated.</param>
	/// <returns>The bytes for 10,000 rows and for 100,000.</returns>
	private static (long Few, long Many) Measure(Func<byte[], long> allocated)
	{
		allocated(Small.Value);
		allocated(Large.Value);
		var few = Math.Min(allocated(Small.Value), Math.Min(allocated(Small.Value), allocated(Small.Value)));
		var many = Math.Min(allocated(Large.Value), Math.Min(allocated(Large.Value), allocated(Large.Value)));

		return (few, many);
	}

	/// <summary>Reads a sheet with hidden rows skipped, in one fill, and measures what the fill allocates.</summary>
	/// <param name="bytes">The sheet's part.</param>
	/// <param name="raw">Whether the rows are skipped by a raw search, rather than token by token.</param>
	/// <returns>The bytes the fill allocated on this thread.</returns>
	private static long Skipped(byte[] bytes, bool raw)
	{
		var scanner = new XmlScanner(new XmlWindow(new MemoryStream(bytes, false), Sheets.PartName, 1 << 20)) { RawSkips = raw };
		using var sheet = new SheetScanner(scanner, "Data", SharedStrings.Empty, StyleTable.Empty, true, null, skipHiddenRows: true);
		var captured = new Captured(int.MaxValue);
		var target = new CapturingTarget(captured);

		var before = GC.GetAllocatedBytesForCurrentThread();
		var more = sheet.Fill(target);
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// The shown rows, the first and the last.
		Assert.False(more);
		Assert.Equal(2, captured.Records.Count);
		Assert.Equal("1: number:1", captured.Records[0].ToString());

		return allocated;
	}

	/// <summary>Finds a sheet's merged ranges, reading it through, and measures what the read allocates.</summary>
	/// <param name="bytes">The sheet's part.</param>
	/// <param name="raw">Whether <c>sheetData</c> is skipped by a raw search, rather than token by token.</param>
	/// <returns>The bytes the read allocated on this thread.</returns>
	private static long PreRead(byte[] bytes, bool raw)
	{
		using var scanner = new XmlScanner(new XmlWindow(new MemoryStream(bytes, false), Sheets.PartName, 1 << 20)) { RawSkips = raw };

		var before = GC.GetAllocatedBytesForCurrentThread();
		var cells = SyncOrAsync.Result(MergedCells.ReadAsync(scanner, "Data", 1 << 20, false, CancellationToken.None));
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		Assert.Equal(["A1:B1"], cells!.Ranges.Select(r => r.ToString()));

		return allocated;
	}

	/// <summary>
	/// A worksheet part of a shown row, hidden rows of two cells and a comment each, and a shown row, with <c>A1:B1</c>
	/// merged after them.
	/// </summary>
	/// <param name="hidden">The number of hidden rows.</param>
	/// <returns>The part's bytes.</returns>
	private static byte[] Sheet(int hidden)
	{
		var rows = new StringBuilder("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>");
		for (var n = 2; n <= hidden + 1; n++)
		{
			rows.Append($"<row r=\"{n}\" hidden=\"1\"><c r=\"A{n}\"><v>{n}</v></c><!-- a note --><c r=\"B{n}\" t=\"inlineStr\"><is><t>text {n}</t></is></c></row>");
		}

		var last = hidden + 2;
		rows.Append($"<row r=\"{last}\"><c r=\"A{last}\"><v>{last}</v></c></row>");

		return Encoding.UTF8.GetBytes(MergedScannerTests.Worksheet(rows.ToString(), "A1:B1"));
	}
}
#endif
