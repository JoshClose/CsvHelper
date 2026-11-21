using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>The workbooks <see cref="ExcelScanStages"/> reads.</summary>
public enum StageWorkbook
{
	/// <summary>The package assets with shared strings: <c>ExcelPackageAssetsRead</c> with <c>Strings=Shared</c>.</summary>
	Shared,

	/// <summary>The package assets with inline strings: <c>ExcelPackageAssetsRead</c> with <c>Strings=Inline</c>.</summary>
	Inline,

	/// <summary>The orders: <c>ExcelOrdersRead</c>'s workbook, typed cells and a million distinct shared strings.</summary>
	Orders,

	/// <summary>The package assets as a report: <c>ExcelReportRead</c>'s, which has merged cells after its rows.</summary>
	Report,
}

/// <summary>
/// A sheet's read taken apart into stages, so that the difference between two methods is one stage's cost: opening the
/// package, inflating the sheet's part, the scanner's 64 KB window, the tokenizer, the second walk of each start tag's
/// attributes, the scanner's cell logic, copying fields as the reader's batch does, the batch itself, and making strings.
/// Beside them: a read of a stored copy of the workbook (nothing to inflate), the pre-read that finds merged cells, and
/// the floors of what a raw skip of <c>sheetData</c> and a one-pass cell reader could cost.
/// </summary>
/// <remarks>
/// Compare methods within one run: the same code measured in two processes can differ by more than a stage does. The
/// stages are built from the library's internals, so they show where a change to the scanner moved the time, which the
/// reader's own benchmarks (<c>ExcelPackageAssetsRead</c>, <c>ExcelOrdersRead</c>) can't tell apart.
/// </remarks>
[MemoryDiagnoser]
public class ExcelScanStages
{
	/// <summary>The default limits.</summary>
	private static readonly PartLimits Limits = ExcelReaderOptions.Default.Limits;

	/// <summary>The options under which text cells' styles are looked up.</summary>
	private static readonly ExcelReaderOptions Formatted = new() { Text = ExcelText.Formatted };

	/// <summary>The buffer the inflating stages read into, as large as the scanner's window.</summary>
	private readonly byte[] room = new byte[XmlWindow.DefaultSize];

	/// <summary>The workbook's bytes.</summary>
	private byte[] workbook = [];

	/// <summary>The workbook's bytes with every entry stored, not deflated: set up for <see cref="RowsStored"/> only.</summary>
	private byte[] stored = [];

	/// <summary>The first sheet's part.</summary>
	private string sheetPart = "";

	/// <summary>Gets or sets the workbook read.</summary>
	[Params(StageWorkbook.Shared, StageWorkbook.Inline, StageWorkbook.Orders, StageWorkbook.Report)]
	public StageWorkbook Workbook { get; set; }

	/// <summary>Reads the workbook from the cache and finds its first sheet's part, for every method but <see cref="RowsStored"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		BenchWorkbooks.Ensure();
		workbook = File.ReadAllBytes(SourcePath());

		using var archive = OpenArchive();
		var part = WorkbookPart.LoadAsync(archive, Limits, false, default).GetAwaiter().GetResult();
		sheetPart = part.Sheets[0].PartName!;
	}

	/// <summary>
	/// Reads the stored copy of the workbook from the cache, made there from the workbook the first time, for
	/// <see cref="RowsStored"/> alone: the other methods' processes don't hold it (up to 1.2 GB, for the inline sheet).
	/// </summary>
	/// <remarks>
	/// The copy is cached under the generator's key, which <see cref="Store"/> isn't part of: after a change to
	/// <see cref="Store"/>, delete the cache's <c>stored-*</c> files (or run <c>workbooks-clean</c> after the key changes).
	/// </remarks>
	[GlobalSetup(Target = nameof(RowsStored))]
	public void SetupStored()
	{
		BenchWorkbooks.Ensure();
		var source = SourcePath();
		var path = ExcelDataGenerator.CachedPath($"stored-{Workbook.ToString().ToLowerInvariant()}-", stream => Store(source, stream));
		stored = File.ReadAllBytes(path);
	}

	/// <summary>Opening a reader: the zip's directory, the workbook part, the styles and the shared strings; no rows.</summary>
	/// <returns>The field count, 0.</returns>
	[Benchmark]
	public long Open()
	{
		using var reader = ExcelReader.Create(NewStream());

		return reader.FieldCount;
	}

	/// <summary>
	/// What <see cref="Cells"/> loads before its rows, without a reader: the zip's directory, the workbook part, the styles
	/// and the shared strings, to subtract from <see cref="Cells"/>.
	/// </summary>
	/// <returns>The shared strings' count.</returns>
	[Benchmark]
	public long Load()
	{
		using var archive = OpenArchive();
		var part = WorkbookPart.LoadAsync(archive, Limits, false, default).GetAwaiter().GetResult();
		StyleTable.LoadAsync(archive, part.StylesPart, Limits, false, default).GetAwaiter().GetResult();

		return SharedStrings.LoadAsync(archive, part.SharedStringsPart, Limits, false, default).GetAwaiter().GetResult().Count;
	}

	/// <summary>
	/// Inflating the sheet's part into a buffer of the window's size, through the part's limited stream: zlib alone, the
	/// floor of any scanner.
	/// </summary>
	/// <returns>The part's bytes.</returns>
	[Benchmark]
	public long Inflate()
	{
		using var archive = OpenArchive();
		using var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var total = 0L;
		int read;
		while ((read = stream.Read(room, 0, room.Length)) > 0)
		{
			total += read;
		}

		return total;
	}

	/// <summary><see cref="Inflate"/> through the scanner's window: each refill compacts it and reads into its free room.</summary>
	/// <returns>The bytes the window received.</returns>
	[Benchmark]
	public long Window()
	{
		using var archive = OpenArchive();
		var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		using var window = new XmlWindow(stream, sheetPart, Limits.MaxPartSize);
		var total = 0L;
		while (!window.IsEof)
		{
			window.Compact(window.End);
			window.Read();
			total += window.End;
		}

		return total;
	}

	/// <summary>The tokenizer over the window: every token of the part, its attributes not read.</summary>
	/// <returns>The number of tokens.</returns>
	[Benchmark]
	public long Tokens()
	{
		using var archive = OpenArchive();
		using var scanner = archive.OpenXmlAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var count = 0L;
		while (scanner.Read())
		{
			count++;
		}

		return count;
	}

	/// <summary>
	/// <see cref="Tokens"/>, and each start tag's attributes walked again, as the sheet scanner's readers walk them: the
	/// difference is the second walk.
	/// </summary>
	/// <returns>The number of tokens and attributes.</returns>
	[Benchmark]
	public long TokensAttributes()
	{
		using var archive = OpenArchive();
		using var scanner = archive.OpenXmlAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var count = 0L;
		while (scanner.Read())
		{
			count++;
			if (scanner.Kind == XmlTokenKind.StartTag)
			{
				var attributes = scanner.Attributes;
				while (attributes.MoveNext())
				{
					count++;
				}
			}
		}

		return count;
	}

	/// <summary>
	/// <see cref="Load"/>, then the sheet scanner's fills into a target that counts fields and copies nothing: the tokenizer
	/// and the scanner's cell logic (attributes, reference, value, shared string lookup), without the reader's batch.
	/// </summary>
	/// <returns>The number of fields.</returns>
	[Benchmark]
	public long Cells()
	{
		var counter = new StageCounter();

		return ScanCells(null, counter, new CountingTarget(counter));
	}

	/// <summary>
	/// <see cref="Cells"/> into a target that also copies each added field (shared strings and gaps) into a buffer, the
	/// value and then a separator byte, as the reader's batch copies it: the difference is the copy alone, without the
	/// batch's index.
	/// </summary>
	/// <returns>The number of fields.</returns>
	[Benchmark]
	public long CellsCopy()
	{
		// Twice a fill's size: a fill ends after the record that makes it full, so its last record goes past 64 KB.
		var counter = new StageCounter { Buffer = new byte[2 * StageCounter.FillSize] };

		return ScanCells(null, counter, new CopyingTarget(counter));
	}

	/// <summary><see cref="Cells"/> with a formatter, as under <see cref="ExcelText.Formatted"/>: each valued cell's style looked up.</summary>
	/// <returns>The number of fields.</returns>
	[Benchmark]
	public long CellsFormatted()
	{
		var counter = new StageCounter();

		return ScanCells(Formatted, counter, new CountingTarget(counter));
	}

	/// <summary>The reader's records, as <c>CsvHelper_Rows</c> reads them: the scanner's fills into the reader's batch.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	public long Rows()
	{
		using var reader = ExcelReader.Create(NewStream());
		var count = 0L;
		while (reader.Read())
		{
			count++;
		}

		return count;
	}

	/// <summary><see cref="Rows"/> under <see cref="ExcelText.Formatted"/>: each valued cell's style looked up, with the batch.</summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	public long RowsFormatted()
	{
		using var reader = ExcelReader.Create(NewStream(), Formatted);
		var count = 0L;
		while (reader.Read())
		{
			count++;
		}

		return count;
	}

	/// <summary>Every field as a string, as <c>CsvHelper_Strings</c> reads them: the difference from <see cref="Rows"/> is the strings.</summary>
	/// <returns>The total length of the strings.</returns>
	[Benchmark]
	public long Strings()
	{
		using var reader = ExcelReader.Create(NewStream());
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				total += reader.GetString(i).Length;
			}
		}

		return total;
	}

	/// <summary>
	/// <see cref="Rows"/> over a copy of the workbook whose entries are stored, not deflated: the read without inflating
	/// the sheet or the shared strings, so that <see cref="Rows"/> minus this is inflating's share of a read.
	/// </summary>
	/// <returns>The number of records.</returns>
	[Benchmark]
	public long RowsStored()
	{
		using var reader = ExcelReader.Create(new MemoryStream(stored, writable: false));
		var count = 0L;
		while (reader.Read())
		{
			count++;
		}

		return count;
	}

	/// <summary>
	/// The pre-read of <see cref="ExcelReaderOptions.FillMergedCells"/> alone: the part tokenized to the merged cells after
	/// <c>sheetData</c>.
	/// </summary>
	/// <returns>The number of merged ranges.</returns>
	[Benchmark]
	public long MergedPreRead()
	{
		using var archive = OpenArchive();
		var merged = MergedCells.LoadAsync(archive, sheetPart, "Sheet", Limits, false, default).GetAwaiter().GetResult();

		return merged?.Ranges.Length ?? 0;
	}

	/// <summary>
	/// The floor of a raw skip of <c>sheetData</c>, which <see cref="MergedPreRead"/> could become: <see cref="Inflate"/>,
	/// and each buffer searched for <c>&lt;/sheetData</c>, the needle's length less one byte kept across buffers. Not a
	/// design: no comments, CDATA or depth.
	/// </summary>
	/// <returns>The position after the needle, or -1.</returns>
	[Benchmark]
	public long InflateSearch() => Search(false);

	/// <summary>
	/// <see cref="InflateSearch"/>, and each buffer also searched for <c>!</c> and <c>?</c>, as a raw skip that watches for
	/// comments, CDATA and processing instructions searches.
	/// </summary>
	/// <returns>The position after the needle, plus the hits of <c>!</c> and <c>?</c>.</returns>
	[Benchmark]
	public long InflateSearchMarkup() => Search(true);

	/// <summary>
	/// <see cref="Inflate"/>, and one scalar pass over every byte that counts <c>&lt;</c>, <c>&gt;</c> and <c>"</c> with a
	/// chain of compares: what touching each byte once, as a scalar one-pass cell reader would, costs. Not a parser.
	/// </summary>
	/// <returns>The counts, summed.</returns>
	[Benchmark]
	public long InflateBytePass()
	{
		using var archive = OpenArchive();
		using var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var lt = 0L;
		var gt = 0L;
		var quotes = 0L;
		int read;
		while ((read = stream.Read(room, 0, room.Length)) > 0)
		{
			foreach (var b in room.AsSpan(0, read))
			{
				if (b == (byte)'<')
				{
					lt++;
				}
				else if (b == (byte)'>')
				{
					gt++;
				}
				else if (b == (byte)'"')
				{
					quotes++;
				}
			}
		}

		return lt + gt + quotes;
	}

	/// <summary>
	/// <see cref="Inflate"/>, and every <c>&lt;</c>, <c>&gt;</c> and <c>"</c> found by hopping from one to the next with
	/// <c>IndexOfAny</c>: what a call per structural byte (about ten a cell) costs, without any work between the calls.
	/// </summary>
	/// <returns>The number of structural bytes.</returns>
	[Benchmark]
	public long InflateHops()
	{
		using var archive = OpenArchive();
		using var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var count = 0L;
		int read;
		while ((read = stream.Read(room, 0, room.Length)) > 0)
		{
			var span = room.AsSpan(0, read);
			int at;
			while ((at = span.IndexOfAny((byte)'<', (byte)'>', (byte)'"')) >= 0)
			{
				count++;
				span = span.Slice(at + 1);
			}
		}

		return count;
	}

	/// <summary>Copies a workbook with every entry stored, in the same order.</summary>
	/// <param name="source">The workbook's path.</param>
	/// <param name="output">Where the copy is written.</param>
	private static void Store(string source, Stream output)
	{
		using var input = ZipFile.OpenRead(source);
		using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
		foreach (var entry in input.Entries)
		{
			var copy = zip.CreateEntry(entry.FullName, CompressionLevel.NoCompression);
			using var from = entry.Open();
			using var to = copy.Open();
			from.CopyTo(to);
		}
	}

	/// <summary>Returns the path of the workbook in the cache.</summary>
	/// <returns>The path, the workbook written there if missing.</returns>
	private string SourcePath() => Workbook switch
	{
		StageWorkbook.Shared => ExcelDataGenerator.PackageAssetsPath(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Shared),
		StageWorkbook.Inline => ExcelDataGenerator.PackageAssetsPath(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Inline),
		StageWorkbook.Orders => ExcelDataGenerator.OrdersPath(ExcelDataGenerator.OrderRows),
		_ => ExcelDataGenerator.PackageAssetsReportPath(ExcelDataGenerator.PackageAssetRows),
	};

	/// <summary>Opens the workbook's package.</summary>
	/// <returns>The package.</returns>
	private PackageArchive OpenArchive() => PackageArchive.OpenAsync(NewStream(), false, Limits, false, default).GetAwaiter().GetResult();

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewStream() => new(workbook, writable: false);

	/// <summary>Scans the sheet's cells into a target, with the options' formatter.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
	/// <param name="counter">What the target counts, reset after each fill as a reader's batch is.</param>
	/// <param name="target">The target.</param>
	/// <returns>The number of fields.</returns>
	private long ScanCells<TTarget>(ExcelReaderOptions? options, StageCounter counter, TTarget target)
		where TTarget : struct, IRecordTarget
	{
		using var archive = OpenArchive();
		var part = WorkbookPart.LoadAsync(archive, Limits, false, default).GetAwaiter().GetResult();
		var styles = StyleTable.LoadAsync(archive, part.StylesPart, Limits, false, default).GetAwaiter().GetResult();
		var strings = SharedStrings.LoadAsync(archive, part.SharedStringsPart, Limits, false, default).GetAwaiter().GetResult();
		var formatter = options?.Text == ExcelText.Formatted ? new CellFormatter(styles, DisplayCulture.For(options.Csv.FormatProvider), part.Date1904) : null;
		var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		using var scanner = new SheetScanner(
			new XmlScanner(new XmlWindow(stream, sheetPart, Limits.MaxPartSize)), "Sheet", strings, styles, true, stream, part.Date1904, formatter: formatter);
		while (scanner.Fill(target))
		{
			counter.Bytes = 0;
			counter.Position = 0;
		}

		return counter.Fields;
	}

	/// <summary>Inflates the sheet's part and searches it raw, as <see cref="InflateSearch"/> says.</summary>
	/// <param name="markup">Whether to also search for <c>!</c> and <c>?</c>.</param>
	/// <returns>The position after the needle (or -1), plus the hits of <c>!</c> and <c>?</c>.</returns>
	private long Search(bool markup)
	{
		var needle = "</sheetData"u8;
		using var archive = OpenArchive();
		using var stream = archive.OpenPartAsync(sheetPart, false, Limits, false, default).GetAwaiter().GetResult();
		var kept = 0;
		var discarded = 0L;
		var found = -1L;
		var hits = 0L;
		while (true)
		{
			var read = stream.Read(room, kept, room.Length - kept);
			if (read == 0)
			{
				break;
			}

			var span = room.AsSpan(0, kept + read);
			if (markup)
			{
				// Only the bytes not searched before.
				var fresh = span.Slice(kept);
				int at;
				while ((at = fresh.IndexOfAny((byte)'!', (byte)'?')) >= 0)
				{
					hits++;
					fresh = fresh.Slice(at + 1);
				}
			}

			if (found < 0)
			{
				var index = span.IndexOf(needle);
				if (index >= 0)
				{
					found = discarded + index + needle.Length;
				}
			}

			// The needle's length less one byte is kept, so that a needle across buffers is found.
			var keep = Math.Min(span.Length, needle.Length - 1);
			span.Slice(span.Length - keep).CopyTo(room);
			discarded += span.Length - keep;
			kept = keep;
		}

		return found + hits;
	}
}

/// <summary>What <see cref="CountingTarget"/> and <see cref="CopyingTarget"/> count, and the buffers they write into.</summary>
internal sealed class StageCounter
{
	/// <summary>The bytes of fields after which a target is full: about a reader's batch at the default buffer size.</summary>
	public const int FillSize = 64 * 1024;

	/// <summary>The fields added.</summary>
	public long Fields;

	/// <summary>The records ended.</summary>
	public long Records;

	/// <summary>The bytes of the fill so far, each field with the separator after it, as a batch counts toward its buffer's size.</summary>
	public long Bytes;

	/// <summary>The room a decoded field is written into.</summary>
	public byte[] Room = new byte[FillSize];

	/// <summary>The buffer <see cref="CopyingTarget"/> copies added fields into; empty for <see cref="CountingTarget"/>.</summary>
	public byte[] Buffer = [];

	/// <summary>Where <see cref="CopyingTarget"/> copies the next added field in <see cref="Buffer"/>.</summary>
	public int Position;
}

/// <summary>
/// A record target that counts fields and copies nothing, full after 64 KB of fields as a reader's batch of the default
/// buffer size about is: the scanner's work without the batch's.
/// </summary>
/// <param name="counter">The counts.</param>
internal readonly struct CountingTarget(StageCounter counter) : IRecordTarget
{
	/// <inheritdoc/>
	public bool IsFull => counter.Bytes >= StageCounter.FillSize;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
	{
		counter.Fields++;
		counter.Bytes += utf8.Length + 1;
	}

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
	{
		counter.Fields++;
		counter.Bytes += utf8.Length + 1;
	}

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint)
	{
		if (counter.Room.Length < sizeHint)
		{
			counter.Room = new byte[sizeHint];
		}

		return counter.Room;
	}

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind)
	{
		counter.Fields++;
		counter.Bytes += length + 1;
	}

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind, int style)
	{
		counter.Fields++;
		counter.Bytes += length + 1;
	}

	/// <inheritdoc/>
	public void EndRecord(long row) => counter.Records++;
}

/// <summary>
/// <see cref="CountingTarget"/>, with each added field (a shared string, or a gap's empty value) copied into a buffer, the
/// value and then a separator byte, as <c>RecordBatch.AddField</c> copies it into the reader's buffer, without the batch's
/// index. Decoded fields are written and committed as <see cref="CountingTarget"/> writes them, so that the difference
/// between the two is the copy alone.
/// </summary>
/// <param name="counter">The counts and the buffer, whose position the scan resets after each fill.</param>
internal readonly struct CopyingTarget(StageCounter counter) : IRecordTarget
{
	/// <inheritdoc/>
	public bool IsFull => counter.Bytes >= StageCounter.FillSize;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
	{
		var buffer = counter.Buffer;
		var position = counter.Position;

		// The value and the separator after it. A record longer than the buffer's slack past a fill grows it, as a batch's
		// buffer grows; what it held isn't read again.
		if (buffer.Length - position <= utf8.Length)
		{
			buffer = new byte[Math.Max(buffer.Length * 2, position + utf8.Length + 1)];
			counter.Buffer = buffer;
		}

		utf8.CopyTo(buffer.AsSpan(position));
		buffer[position + utf8.Length] = (byte)',';
		counter.Position = position + utf8.Length + 1;
		counter.Fields++;
		counter.Bytes += utf8.Length + 1;
	}

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style) => AddField(utf8, kind);

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint)
	{
		if (counter.Room.Length < sizeHint)
		{
			counter.Room = new byte[sizeHint];
		}

		return counter.Room;
	}

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind)
	{
		counter.Fields++;
		counter.Bytes += length + 1;
	}

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind, int style)
	{
		counter.Fields++;
		counter.Bytes += length + 1;
	}

	/// <inheritdoc/>
	public void EndRecord(long row) => counter.Records++;
}
