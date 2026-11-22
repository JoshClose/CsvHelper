#if NET
using System.Text;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// What skipping and reading rows allocates (Phase 9's steps 2 and 3): nothing per row, with raw skips and with token skips,
/// and with the sheet scanner's fast cells and with its fallback. A sheet scanner skipping hidden rows, the merged cells'
/// pre-read skipping <c>sheetData</c>, and a sheet scanner reading shown rows of every kind of cell, allocate within a small
/// constant of each other for 10,000 rows and for 100,000. It runs alone, in <see cref="TimedCollection"/>, as the import's
/// allocation test does, since other tests' garbage collections trim the shared array pool the window rents from.
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

	/// <summary>A sheet of 10,000 shown rows, made once.</summary>
	private static readonly Lazy<byte[]> FewShown = new(() => ShownSheet(10_000));

	/// <summary>A sheet of 100,000 shown rows, made once.</summary>
	private static readonly Lazy<byte[]> ManyShown = new(() => ShownSheet(100_000));

	/// <summary>The shared strings of the shown rows.</summary>
	private static readonly SharedStrings Strings = Sheets.Strings("shared", "other");

	[OptimizedFact]
	public void SkipsHiddenRowsWithoutAllocatingPerRow()
	{
		// Raw skips with fast cells (whose rows are the first and the last), and token skips with the fallback.
		foreach (var fast in new[] { true, false })
		{
			var (few, many) = Measure(Small, Large, sheet => Skipped(sheet, fast));
			var skips = fast ? "raw, with fast cells" : "token by token, with the fallback";
			output.WriteLine($"Skipping hidden rows {skips} allocated {few:N0} bytes for 10,000 rows and {many:N0} for 100,000.");

			Assert.True(many - few < Slack, $"Skipping hidden rows {skips}, 10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		}
	}

	[OptimizedFact]
	public void PreReadsWithoutAllocatingPerRow()
	{
		foreach (var raw in new[] { true, false })
		{
			var (few, many) = Measure(Small, Large, sheet => PreRead(sheet, raw));
			var skips = raw ? "raw" : "token by token";
			output.WriteLine($"The pre-read skipping sheetData {skips} allocated {few:N0} bytes for 10,000 rows and {many:N0} for 100,000.");

			Assert.True(many - few < Slack, $"The pre-read skipping sheetData {skips}, 10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		}
	}

	[OptimizedFact]
	public void ReadsCellsWithoutAllocatingPerRow()
	{
		// Every row's cells, canonical (which the fast path takes) and not (which it leaves to the fallback), into a target
		// that keeps nothing, with fast cells on and off.
		foreach (var fast in new[] { true, false })
		{
			var (few, many) = Measure(FewShown, ManyShown, sheet => Shown(sheet, fast));
			var cells = fast ? "with fast cells" : "with the fallback";
			output.WriteLine($"Reading shown rows {cells} allocated {few:N0} bytes for 10,000 rows and {many:N0} for 100,000.");

			Assert.True(many - few < Slack, $"Reading shown rows {cells}, 10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		}
	}

	/// <summary>
	/// Measures a read of two sheets: each once first, then three times, keeping the smallest, as the import's allocation
	/// test does, since the runtime allocates once at a moment tiering sets.
	/// </summary>
	/// <param name="small">The sheet of 10,000 rows.</param>
	/// <param name="large">The sheet of 100,000 rows.</param>
	/// <param name="allocated">Reads a sheet, and gives the bytes it allocated.</param>
	/// <returns>The bytes for 10,000 rows and for 100,000.</returns>
	private static (long Few, long Many) Measure(Lazy<byte[]> small, Lazy<byte[]> large, Func<byte[], long> allocated)
	{
		allocated(small.Value);
		allocated(large.Value);
		var few = Math.Min(allocated(small.Value), Math.Min(allocated(small.Value), allocated(small.Value)));
		var many = Math.Min(allocated(large.Value), Math.Min(allocated(large.Value), allocated(large.Value)));

		return (few, many);
	}

	/// <summary>Reads a sheet with hidden rows skipped, in one fill, and measures what the fill allocates.</summary>
	/// <param name="bytes">The sheet's part.</param>
	/// <param name="fast">
	/// Whether the rows are skipped by a raw search and the shown rows' cells read through the fast path, rather than both
	/// token by token.
	/// </param>
	/// <returns>The bytes the fill allocated on this thread.</returns>
	private static long Skipped(byte[] bytes, bool fast)
	{
		var scanner = new XmlScanner(new XmlWindow(new MemoryStream(bytes, false), Sheets.PartName, 1 << 20)) { RawSkips = fast };
		using var sheet = new SheetScanner(scanner, "Data", SharedStrings.Empty, StyleTable.Empty, true, null, skipHiddenRows: true, fastCells: fast);
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
	/// Reads every row of a sheet of shown rows into a target that keeps nothing, in fills of about 64 KB of fields as a
	/// reader's are, and measures what the fills allocate.
	/// </summary>
	/// <param name="bytes">The sheet's part.</param>
	/// <param name="fast">Whether the scanner reads the cells it can through its fast path, rather than every cell token by token.</param>
	/// <returns>The bytes the fills allocated on this thread.</returns>
	private static long Shown(byte[] bytes, bool fast)
	{
		var scanner = new XmlScanner(new XmlWindow(new MemoryStream(bytes, false), Sheets.PartName, 1 << 20));
		using var sheet = new SheetScanner(scanner, "Data", Strings, StyleTable.Empty, true, null, fastCells: fast);
		var counts = new FieldCounts();
		var target = new CountingTarget(counts);

		var before = GC.GetAllocatedBytesForCurrentThread();
		while (sheet.Fill(target))
		{
			counts.Bytes = 0;
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// Eight fields a row: the seven cells, and the gap at G.
		Assert.InRange(counts.Records, 10_000, 100_000);
		Assert.Equal(counts.Records * 8, counts.Fields);

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

	/// <summary>
	/// A worksheet part of shown rows: each a shared string, a number, a boolean, an inline string, a styled number with a
	/// reference in it, a formula's cached value (which the fast path declines), and, after a gap, a text with a reference;
	/// every tenth row without <c>r</c>, its cells without theirs.
	/// </summary>
	/// <param name="count">The number of rows.</param>
	/// <returns>The part's bytes.</returns>
	private static byte[] ShownSheet(int count)
	{
		var rows = new StringBuilder();
		for (var n = 1; n <= count; n++)
		{
			if (n % 10 == 0)
			{
				rows.Append($"<row><c t=\"s\"><v>1</v></c><c><v>{n}</v></c><c t=\"b\"><v>0</v></c><c t=\"inlineStr\"><is><t>text {n}</t></is></c>");
				rows.Append($"<c s=\"1\"><v>&#49;.5</v></c><c><f>B{n}*2</f><v>{2 * n}</v></c><c r=\"H{n}\" t=\"str\"><v>a&amp;b</v></c></row>");
			}
			else
			{
				rows.Append($"<row r=\"{n}\"><c r=\"A{n}\" t=\"s\"><v>0</v></c><c r=\"B{n}\"><v>{n}</v></c><c r=\"C{n}\" t=\"b\"><v>1</v></c>");
				rows.Append($"<c r=\"D{n}\" t=\"inlineStr\"><is><t>text {n}</t></is></c><c r=\"E{n}\" s=\"1\"><v>&#49;.5</v></c><c r=\"F{n}\"><f>B{n}*2</f><v>{2 * n}</v></c>");
				rows.Append($"<c r=\"H{n}\" t=\"str\"><v>a&amp;b</v></c></row>");
			}
		}

		return Encoding.UTF8.GetBytes(Sheets.Worksheet(rows.ToString()));
	}

	/// <summary>What <see cref="CountingTarget"/> counts, and the room it lends for decoded fields.</summary>
	private sealed class FieldCounts
	{
		/// <summary>The fields added.</summary>
		public long Fields;

		/// <summary>The records ended.</summary>
		public long Records;

		/// <summary>The bytes of the fill so far, each field with a separator after it, as a reader's batch counts them.</summary>
		public long Bytes;

		/// <summary>The room a decoded field is written into, grown only for a field longer than it.</summary>
		public byte[] Room = new byte[4096];
	}

	/// <summary>A record target that counts fields and keeps nothing, full after 64 KB of fields as a reader's batch about is.</summary>
	/// <param name="counts">The counts.</param>
	private readonly struct CountingTarget(FieldCounts counts) : IRecordTarget
	{
		/// <inheritdoc/>
		public bool IsFull => counts.Bytes >= 64 * 1024;

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
		{
			counts.Fields++;
			counts.Bytes += utf8.Length + 1;
		}

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style) => AddField(utf8, kind);

		/// <inheritdoc/>
		public Span<byte> GetFieldSpan(int sizeHint)
		{
			if (counts.Room.Length < sizeHint)
			{
				counts.Room = new byte[sizeHint];
			}

			return counts.Room;
		}

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind)
		{
			counts.Fields++;
			counts.Bytes += length + 1;
		}

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind, int style) => CommitField(length, kind);

		/// <inheritdoc/>
		public void EndRecord(long row) => counts.Records++;
	}
}
#endif
