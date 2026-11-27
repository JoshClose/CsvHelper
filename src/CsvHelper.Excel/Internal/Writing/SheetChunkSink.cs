using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The sink of a parallel write's worker (<see cref="CsvCellSink.CreateWorker"/>): it writes the rows of each batch of
/// records below a sheet's header into a chunk of its own (<see cref="SheetChunk"/>), as the sheet's sink
/// (<see cref="SheetSink"/>) would write them there, which the sheet's sink appends in order or refuses, so that the batch
/// is written again serially.
/// </summary>
/// <remarks>
/// <para>
/// Its cell and row code is a copy of <see cref="SheetSink"/>'s for the records below the header, so that the serial
/// write's code stays as it is, and tests compare the two byte for byte (<c>ChunkDriftTests</c>). It reads the sheet only
/// through the view the owner froze (<see cref="SheetView"/>), the latest as each chunk begins, with caches of its own
/// over it: its copies of the columns' formats and styles, its columns' starts and placeholders, and its columns' types.
/// Below the header no cell is measured, the row's font is the regular one, the header's cells don't say their style, and
/// a gap is always a placeholder; and the worker writes no shared string, since a sheet of
/// <see cref="ExcelStrings.Shared"/> is written serially.
/// </para>
/// <para>
/// A chunk's rows have no <c>r</c>, as the sheet's: its first record with a cell starts with its <c>&lt;row&gt;</c>, and
/// each record without a cell between two with one is a <c>&lt;row/&gt;</c>; the records before its first with a cell and
/// after its last write nothing, and the owner writes the empty rows before them, which depend on the rows before the
/// chunk.
/// </para>
/// <para>
/// What would make the chunk differ from the serial write is flagged (<see cref="SheetChunk.Redo"/>), and the field throws
/// <see cref="InvalidOperationException"/>, which stops the batch: a cell format the view's snapshot lacks, which the
/// serial write adds in its order (<see cref="SheetChunkRedo.Style"/>); a link before the workbook has the link style
/// (<see cref="SheetChunkRedo.LinkStyle"/>); and the sheet's own errors (<see cref="SheetChunkRedo.Error"/>), whose
/// exception, with its cell's place, the serial write throws. The flag stays when the record is discarded. A worker writes
/// nothing shared, so one is used by one thread at a time, and any number of them at once.
/// </para>
/// </remarks>
internal sealed class SheetChunkSink : CsvCellSink
{
	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxSignificantDigits = 15;

	/// <summary>The most bytes of a cell's start: <c>&lt;c s="</c>, a cell format's index of up to 10 digits, and <c>"</c>.</summary>
	private const int MaxCellStart = 17;

	/// <summary>Why the worker's sink is never flushed or completed, for <see cref="NotSupportedException"/>.</summary>
	private const string NotFlushed = "A parallel write's worker sink is never flushed or completed: the sheet's sink appends its chunks.";

	/// <summary>Why a chunk whose cell format the view lacks is written again, for the exception's message.</summary>
	private const string StyleMissing = "The cell needs a cell format that the workbook doesn't have yet: the parallel write writes the batch again serially, which adds it.";

	/// <summary>A gap's placeholder in a column without a default style.</summary>
	private static readonly byte[] EmptyCell = "<c/>"u8.ToArray();

	/// <summary>The views of the parallel write.</summary>
	private readonly SheetViewSlot views;
	private readonly XmlOutput output = new(SheetSink.FlushSize + 4096);
	// The view the caches below are built over, or null before the first chunk; and what the worker reads of it.
	private SheetView? view;
	private ExcelWriterOptions options = ExcelWriterOptions.Default;
	private NumberCulture? culture;
	private StyleSnapshot? styles;
	private int linkStyle;
	private bool linksFull;
	// The worker's copies of each column's member's format and of each column's style below the header; null when no
	// column has one. And each column's default style, the view's (read only).
	private ColumnFormat?[]? columnFormats;
	private ChunkColumnStyle?[]? columnStyles;
	private int[]? columnDefaults;
	// The header's columns, whose cells are unlocked; whether a cell's style is more than its own number format; and whether
	// a cell without a style of its own takes its column's start.
	private int unlockedColumns;
	private bool styling;
	private bool stylesColumns;
	// The worker's copies of each column's placeholder and start, made when a gap or a cell in it first needs one.
	private byte[]?[]? placeholders;
	private byte[]?[]? columnStarts;
	// Each column's last type, and the kind of cell it makes.
	private Type?[] columnTypes = [];
	private CellKind[] columnKinds = [];
	// The record number core gave the chunk's first item; the current record's index in the chunk, and the column of its
	// next field.
	private long firstRecord;
	private int record;
	private int column;
	// Whether the current record's row element has been written; and the column after its last cell, 0 before its first.
	private bool rowOpen;
	private int recordNextColumn;
	// Where the current record starts in the buffer: what is before it is whole records.
	private int recordStart;
	// The current record's last cell's column, and whether one of its text cells starts like a number.
	private int recordLastColumn;
	private bool recordNumberLikeText;
	// The chunk's records with a cell, first and last (-1 for none), its last column, and whether a text cell of it starts
	// like a number.
	private int firstCellRecord = -1;
	private int lastCellRecord = -1;
	private int lastColumn = -1;
	private bool numberLikeText;
	// The chunk's links, null for none, and how many of them are the records' ended.
	private List<ChunkLink>? links;
	private int keptLinks;
	// Why the chunk must be written again serially, the first reason found.
	private SheetChunkRedo redo;
	// 1 once released.
	private int released;
	// Whether a record was discarded after a field of it reached the sink, since the chunk began.
	private bool discardedFields;

	/// <summary>Creates a worker's sink: called by the owner's <see cref="CsvCellSink.CreateWorker"/>, on a worker thread.</summary>
	/// <param name="views">The parallel write's views, whose latest the worker reads as each chunk begins.</param>
	public SheetChunkSink(SheetViewSlot views) => this.views = views;

	/// <summary>
	/// Begins a chunk: reads the latest view, rebuilds the worker's caches over it when its <see cref="SheetView.Version"/>
	/// is another than theirs, and starts an empty chunk.
	/// </summary>
	/// <param name="firstRecord">The record number core gave the batch's first item (<see cref="SheetChunk.FirstRecord"/>).</param>
	/// <exception cref="InvalidOperationException">No view has been published.</exception>
	public override void BeginChunk(long firstRecord)
	{
		var latest = views.View;
		if (view is null || latest.Version != view.Version)
		{
			Build(latest);
		}

		this.firstRecord = firstRecord;
		output.Truncate(0);
		record = 0;
		column = 0;
		rowOpen = false;
		recordNextColumn = 0;
		recordStart = 0;
		recordNumberLikeText = false;
		firstCellRecord = -1;
		lastCellRecord = -1;
		lastColumn = -1;
		numberLikeText = false;
		links = null;
		keptLinks = 0;
		redo = SheetChunkRedo.None;
	}

	/// <summary>
	/// Hands over the chunk: its whole records' XML, which the chunk owns from then on, and what the owner adds up. A record
	/// left open is discarded first.
	/// </summary>
	/// <param name="length">
	/// The chunk's length, by which core sizes its batches: the view's <see cref="SheetView.ChunkLength"/> when it is above 0
	/// (a test's), else the XML's bytes.
	/// </param>
	/// <returns>The chunk, a <see cref="SheetChunk"/>.</returns>
	public override object TakeChunk(out int length)
	{
		if (column > 0 || output.Length > recordStart)
		{
			// Core discards a record that failed before it takes the chunk.
			DiscardRecord();
		}

		var chunk = new SheetChunk
		{
			Xml = output.Detach(output.Length),
			Records = record,
			FirstCellRecord = firstCellRecord,
			LastCellRecord = lastCellRecord,
			LastColumn = lastColumn,
			NumberLikeText = numberLikeText,
			// Null for none, also when the records that had links were discarded.
			Links = links is { Count: > 0 } ? links : null,
			Redo = redo,
			FirstRecord = firstRecord,
			ViewVersion = view!.Version,
			DiscardedFields = discardedFields,
		};
		discardedFields = false;
		links = null;
		keptLinks = 0;
		recordStart = 0;
		length = view.ChunkLength > 0 ? view.ChunkLength : chunk.Xml.Length;

		return chunk;
	}

	/// <summary>Writes a field's cell into the current record, as <see cref="SheetSink.WriteField"/> writes it below the header.</summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="type">The type it was formatted from.</param>
	/// <exception cref="InvalidOperationException">
	/// The cell differs from the serial write's, and the chunk is flagged: it needs a cell format or the link style that
	/// the workbook doesn't have yet, or it fails as the sheet's rules say (past Excel's last column, its text too long for
	/// a cell, or a value that the options say to throw for).
	/// </exception>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type)
	{
		// The column counts whether or not the field throws, as the writer's field count does.
		var at = column++;
		if (at >= CellReference.MaxColumns)
		{
			throw Redo(SheetChunkRedo.Error, $"A record has a field in column {(at + 1).ToString("N0", CultureInfo.InvariantCulture)}, past XFD, Excel's last column.");
		}

		if (utf8.IsEmpty)
		{
			return;
		}

		switch (KindOf(at, type))
		{
			case CellKind.Bool:
				WriteBool(utf8, at);

				break;
			case CellKind.Integer or CellKind.Decimal:
				WriteNumber(utf8, at, exact: true);

				break;
			case CellKind.Float:
				WriteNumber(utf8, at, exact: false);

				break;
			case CellKind.DateTime:
				WriteDateTime(utf8, at);

				break;
			case CellKind.DateTimeOffset:
				WriteDateTimeOffset(utf8, at);

				break;
			case CellKind.DateOnly:
				WriteDate(utf8, at);

				break;
			case CellKind.TimeOnly:
				WriteTime(utf8, at);

				break;
			case CellKind.TimeSpan:
				WriteDuration(utf8, at);

				break;
			case CellKind.Formula:
				WriteFormula(utf8, at);

				break;
			case CellKind.Uri:
				WriteUri(utf8, at);

				break;
			default:
				WriteText(utf8, at);

				break;
		}
	}

	/// <summary>
	/// Ends the current record: closes its row, if it has one, and counts it. The worker never asks to be flushed.
	/// </summary>
	/// <returns><see langword="false"/>.</returns>
	public override bool EndRecord()
	{
		if (column == 0)
		{
			// As the sheet's sink: a record without a field isn't one.
			return false;
		}

		if (rowOpen)
		{
			output.Append("</row>"u8);
			rowOpen = false;
			if (firstCellRecord < 0)
			{
				firstCellRecord = record;
			}

			lastCellRecord = record;
			lastColumn = Math.Max(lastColumn, recordLastColumn);
		}

		numberLikeText |= recordNumberLikeText;
		recordNumberLikeText = false;
		keptLinks = links?.Count ?? 0;
		record++;
		column = 0;
		recordNextColumn = 0;
		recordStart = output.Length;

		return false;
	}

	/// <summary>
	/// Drops the current record's cells, the empty rows written before them, and its links, as the sheet's sink does. The
	/// chunk's flag stays: a field that threw for it is still in the batch.
	/// </summary>
	public override void DiscardRecord()
	{
		// A field of it reached the sink: the serial write's row limit would have acted at the first one (SheetChunk.DiscardedFields).
		discardedFields |= column > 0;
		output.Truncate(recordStart);
		rowOpen = false;
		column = 0;
		recordNextColumn = 0;
		recordNumberLikeText = false;
		if (links is not null && links.Count > keptLinks)
		{
			links.RemoveRange(keptLinks, links.Count - keptLinks);
		}
	}

	/// <summary>Never called on a worker's sink.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void Flush() => throw new NotSupportedException(NotFlushed);

	/// <summary>Never called on a worker's sink.</summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>Nothing: it throws.</returns>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override ValueTask FlushAsync(CancellationToken cancellationToken) => throw new NotSupportedException(NotFlushed);

	/// <summary>Never called on a worker's sink.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void Complete() => throw new NotSupportedException(NotFlushed);

	/// <summary>Never called on a worker's sink.</summary>
	/// <returns>Nothing: it throws.</returns>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override ValueTask CompleteAsync() => throw new NotSupportedException(NotFlushed);

	/// <summary>
	/// Returns the worker's buffers to the pool: called by the owner's <see cref="CsvCellSink.ReleaseWorker"/>, once, on any
	/// thread, maybe after the parallel write has returned. Touches only the worker's own state; calling it again does
	/// nothing.
	/// </summary>
	public void Release()
	{
		if (Interlocked.Exchange(ref released, 1) == 0)
		{
			output.Dispose();
		}
	}

	/// <summary>Makes a cell's text valid UTF-8, as .NET decodes it, and counts its UTF-16 code units, as the sheet's sink does.</summary>
	/// <param name="text">The text, replaced when it isn't valid.</param>
	/// <returns>The number of code units.</returns>
	private static int ValidUtf16Length(ref ReadOnlySpan<byte> text)
	{
		if (!XmlOutput.IsValidUtf8(text))
		{
			text = XmlOutput.ReplaceInvalidUtf8(text);
		}

		return XmlOutput.Utf16Length(text);
	}

	/// <summary>Copies of the columns' formats, for the worker's own caches.</summary>
	/// <param name="formats">The view's.</param>
	/// <returns>The copies.</returns>
	private static ColumnFormat?[] Copies(ColumnFormat?[] formats)
	{
		var copies = new ColumnFormat?[formats.Length];
		for (var at = 0; at < formats.Length; at++)
		{
			copies[at] = formats[at]?.ForWorker();
		}

		return copies;
	}

	/// <summary>Copies of the columns' styles, whose lookups go through a snapshot.</summary>
	/// <param name="columns">The view's.</param>
	/// <param name="styles">The view's snapshot.</param>
	/// <returns>The copies.</returns>
	private static ChunkColumnStyle?[] Copies(ColumnStyle?[] columns, StyleSnapshot styles)
	{
		var copies = new ChunkColumnStyle?[columns.Length];
		for (var at = 0; at < columns.Length; at++)
		{
			copies[at] = columns[at] is { } style ? new ChunkColumnStyle(style, styles) : null;
		}

		return copies;
	}

	/// <summary>Builds the worker's caches over a view: copies of what it fills as it writes, and what it reads.</summary>
	/// <param name="latest">The view.</param>
	private void Build(SheetView latest)
	{
		view = latest;
		options = latest.Options;
		culture = latest.Culture;
		styles = latest.Styles;
		linkStyle = latest.LinkStyle;
		linksFull = latest.LinksFull;
		columnFormats = latest.ColumnFormats is { } formats ? Copies(formats) : null;
		columnStyles = latest.ColumnStyles is { } columns ? Copies(columns, latest.Styles) : null;
		columnDefaults = latest.ColumnDefaults;
		unlockedColumns = latest.UnlockedColumns;
		styling = latest.Styling;
		stylesColumns = latest.StylesColumns;
		placeholders = (byte[]?[]?)latest.Placeholders?.Clone();
		columnStarts = (byte[]?[]?)latest.ColumnStarts?.Clone();
		columnTypes = (Type?[])latest.ColumnTypes.Clone();
		columnKinds = (CellKind[])latest.ColumnKinds.Clone();
	}

	/// <summary>Flags the chunk to be written again serially, keeping the first reason, and makes the exception that stops the batch.</summary>
	/// <param name="reason">Why.</param>
	/// <param name="message">What the cell needs or did.</param>
	/// <returns>The exception to throw.</returns>
	private InvalidOperationException Redo(SheetChunkRedo reason, string message)
	{
		if (redo == SheetChunkRedo.None)
		{
			redo = reason;
		}

		return new InvalidOperationException(message);
	}

	/// <summary>The format and culture of a column's member, the worker's copy.</summary>
	/// <param name="at">The column.</param>
	/// <returns>Them, or <see langword="null"/> when the column's member has neither, or isn't known.</returns>
	private ColumnFormat? FormatOf(int at) => columnFormats is { } formats && at < formats.Length ? formats[at] : null;

	/// <summary>The kind of cell a field of a type makes in a column, from the column's last type when it is the same.</summary>
	/// <param name="at">The column.</param>
	/// <param name="type">The field's type.</param>
	/// <returns>The kind.</returns>
	private CellKind KindOf(int at, Type type)
	{
		if (at >= columnTypes.Length)
		{
			var size = Math.Min(Math.Max(at + 1, columnTypes.Length * 2), CellReference.MaxColumns);
			Array.Resize(ref columnTypes, size);
			Array.Resize(ref columnKinds, size);
		}

		if (!ReferenceEquals(columnTypes[at], type))
		{
			columnKinds[at] = CellKinds.Of(type);
			columnTypes[at] = type;
		}

		return columnKinds[at];
	}

	/// <summary>Writes a boolean cell from <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteBool(ReadOnlySpan<byte> text, int at)
	{
		var value = text.SequenceEqual("True"u8) ? (byte)'1' : text.SequenceEqual("False"u8) ? (byte)'0' : (byte)0;
		if (value == 0)
		{
			WriteText(text, at);

			return;
		}

		WriteCell(at, 0, " t=\"b\"><v>"u8, value == (byte)'1' ? "1"u8 : "0"u8);
	}

	/// <summary>
	/// Writes a number cell, when the text is a number in .NET's default form, in the invariant culture or the writer's (or
	/// the column's member's): its invariant text goes into <c>v</c>. Other text is a text cell.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="exact">Whether the number is an integer or a <see cref="decimal"/>, whose digits past Excel's 15 follow <see cref="ExcelWriterOptions.LongNumbers"/>.</param>
	/// <exception cref="InvalidOperationException">The number has more than 15 significant digits, and the options say to throw: flagged.</exception>
	private void WriteNumber(ReadOnlySpan<byte> text, int at, bool exact)
	{
		Span<byte> invariant = stackalloc byte[DotNetText.MaxNumberLength];
		var numbers = FormatOf(at) is { HasCulture: true } column ? column.Numbers : culture;
		var length = DotNetText.ToInvariantNumber(text, numbers, invariant, out var significant);
		if (length < 0)
		{
			WriteText(text, at);

			return;
		}

		if (exact && significant > MaxSignificantDigits && options.LongNumbers != ExcelLongNumbers.Number)
		{
			if (options.LongNumbers == ExcelLongNumbers.Throw)
			{
				throw Redo(SheetChunkRedo.Error, $"A cell holds a number of {significant} significant digits, and Excel keeps {MaxSignificantDigits} (ExcelWriterOptions.LongNumbers is Throw).");
			}

			WriteText(text, at);

			return;
		}

		WriteCell(at, 0, "><v>"u8, invariant.Slice(0, length));
	}

	/// <summary>Writes a date cell from a <see cref="DateTime"/>'s <c>"O"</c> text, or from its member's format's text.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTime(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.DateTime);

			return;
		}

		if (!DotNetText.TryDateTime(text, out var ticks, out _))
		{
			WriteText(text, at);

			return;
		}

		WriteDateTime(text, at, ticks);
	}

	/// <summary>Writes a cell from a <see cref="DateTimeOffset"/>'s text, as <see cref="ExcelWriterOptions.DateTimeOffsets"/> says.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDateTimeOffset(ReadOnlySpan<byte> text, int at)
	{
		if (options.DateTimeOffsets != ExcelDateTimeOffsets.Text && FormatOf(at) is { HasDateFormat: true } column)
		{
			if (options.DateTimeOffsets == ExcelDateTimeOffsets.ClockTime)
			{
				WriteFormatted(text, at, column, CellKind.DateTimeOffset);
			}
			else
			{
				WriteText(text, at);
			}

			return;
		}

		if (options.DateTimeOffsets == ExcelDateTimeOffsets.Text || !DotNetText.TryDateTime(text, out var ticks, out var offset))
		{
			WriteText(text, at);

			return;
		}

		WriteDateTime(text, at, options.DateTimeOffsets == ExcelDateTimeOffsets.Utc ? ticks - offset : ticks);
	}

	/// <summary>Writes a date cell of a date and time, in the format of what it holds.</summary>
	/// <param name="text">The field's text, for a text cell.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="ticks">The date and time's ticks.</param>
	private void WriteDateTime(ReadOnlySpan<byte> text, int at, long ticks)
	{
		var milliseconds = Serial.DateMilliseconds(ticks);
		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at);

			return;
		}

		WriteSerial(at, milliseconds, milliseconds % Serial.MillisecondsPerDay == 0 ? FixedFormat.Date : milliseconds % 1000 == 0 ? FixedFormat.DateTime : FixedFormat.DateTimeMilliseconds);
	}

	/// <summary>Writes a date cell from a <c>DateOnly</c>'s <c>"O"</c> text, in <c>yyyy-mm-dd</c>, or from its member's format's text.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDate(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.DateOnly);

			return;
		}

		if (!DotNetText.TryDate(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.DateMilliseconds(ticks);
		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at);

			return;
		}

		WriteSerial(at, milliseconds, FixedFormat.Date);
	}

	/// <summary>Writes a time cell from a <c>TimeOnly</c>'s <c>"O"</c> text, or from its member's format's text.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteTime(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDateFormat: true } column)
		{
			WriteFormatted(text, at, column, CellKind.TimeOnly);

			return;
		}

		if (!DotNetText.TryTime(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.TimeMilliseconds(ticks);
		WriteSerial(at, milliseconds, milliseconds % 1000 == 0 ? FixedFormat.Time : FixedFormat.TimeMilliseconds);
	}

	/// <summary>
	/// Writes a duration cell from a <see cref="TimeSpan"/>'s <c>"c"</c> text; one in its member's format other than
	/// <c>c</c> is a text cell, and one Excel can't show follows <see cref="ExcelWriterOptions.OutOfRangeDates"/>.
	/// </summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteDuration(ReadOnlySpan<byte> text, int at)
	{
		if (FormatOf(at) is { HasDurationFormat: true } || !DotNetText.TryDuration(text, out var ticks))
		{
			WriteText(text, at);

			return;
		}

		var milliseconds = Serial.DurationMilliseconds(ticks);
		if (milliseconds < 0 || milliseconds >= Serial.PastLast)
		{
			WriteOutOfRange(text, at);

			return;
		}

		WriteSerial(at, milliseconds, milliseconds % 1000 == 0 ? FixedFormat.Duration : FixedFormat.DurationMilliseconds);
	}

	/// <summary>Writes a date, time or duration that Excel can't show, as <see cref="ExcelWriterOptions.OutOfRangeDates"/> says.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <exception cref="InvalidOperationException">The options say to throw: flagged.</exception>
	private void WriteOutOfRange(ReadOnlySpan<byte> text, int at)
	{
		if (options.OutOfRangeDates == ExcelOutOfRangeDates.Throw)
		{
			throw Redo(SheetChunkRedo.Error, "A cell holds a date, time or duration that Excel can't show (ExcelWriterOptions.OutOfRangeDates is Throw).");
		}

		WriteText(text, at);
	}

	/// <summary>Writes a date or time cell from the text of its member's format, when it reads back; otherwise a text cell.</summary>
	/// <param name="text">The field's text.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="column">The column's format, the worker's copy.</param>
	/// <param name="kind">The kind of value.</param>
	/// <exception cref="InvalidOperationException">The translation's cell format isn't the workbook's yet, or the date is before 1900 and the options say to throw: flagged.</exception>
	private void WriteFormatted(ReadOnlySpan<byte> text, int at, ColumnFormat column, CellKind kind)
	{
		if (column.Translation(kind) is not { } format || !format.TryRead(text, out var milliseconds))
		{
			WriteText(text, at);

			return;
		}

		if (milliseconds < 0)
		{
			WriteOutOfRange(text, at);

			return;
		}

		var style = 0;
		if (!FormatsColumn(at) && !column.TryStyle(styles!, out style))
		{
			throw Redo(SheetChunkRedo.Style, StyleMissing);
		}

		WriteSerial(at, milliseconds, style);
	}

	/// <summary>Writes a number cell of a serial, in one of the writer's formats.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="format">The cell's number format.</param>
	/// <exception cref="InvalidOperationException">The format's cell format isn't the workbook's yet: flagged.</exception>
	private void WriteSerial(int at, long milliseconds, FixedFormat format)
	{
		var style = 0;
		if (!FormatsColumn(at) && !styles!.TryIndexOf(format, out style))
		{
			throw Redo(SheetChunkRedo.Style, StyleMissing);
		}

		WriteSerial(at, milliseconds, style);
	}

	/// <summary>Whether a cell's column has a format of its own, which takes the place of the cell's.</summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>Whether it has.</returns>
	private bool FormatsColumn(int at) =>
		styling && columnStyles is { } columns && at < columns.Length && columns[at] is { HasFormat: true };

	/// <summary>Writes a number cell of a serial, in a cell format.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="milliseconds">The serial, in milliseconds.</param>
	/// <param name="style">The cell format's index.</param>
	private void WriteSerial(int at, long milliseconds, int style)
	{
		Span<byte> days = stackalloc byte[FloatFormatter.MaxDoubleLength];
		FloatFormatter.TryFormat(Serial.Days(milliseconds), days, out var length);
		WriteCell(at, style, "><v>"u8, days.Slice(0, length));
	}

	/// <summary>Writes a formula cell from an <see cref="ExcelFormula"/>'s text: <c>f</c> alone, escaped as text is.</summary>
	/// <param name="text">The field's text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	private void WriteFormula(ReadOnlySpan<byte> text, int at)
	{
		var formula = text[0] == (byte)'=' ? text.Slice(1) : text;
		StartCell(at, 0, XmlOutput.NeedsPreserve(formula) ? "><f xml:space=\"preserve\">"u8 : "><f>"u8);
		output.AppendText(formula);
		output.Append("</f></c>"u8);
	}

	/// <summary>
	/// Writes a URI's cell: a text cell of its text, with a link to it in the <c>Hyperlink</c> cell style when
	/// <see cref="ExcelWriterOptions.Links"/> takes it and the sheet wasn't full of links when the view was frozen; the
	/// link goes into the chunk's (<see cref="SheetChunk.Links"/>), which the owner checks against the cap.
	/// </summary>
	/// <param name="text">The URI's text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	/// <exception cref="InvalidOperationException">The workbook has no link style yet, or the cell is flagged as a text cell is: flagged.</exception>
	private void WriteUri(ReadOnlySpan<byte> text, int at)
	{
		if (options.Links == ExcelLinks.None || linksFull || SheetLinks.Target(Encoding.UTF8.GetString(text.ToArray()), options.Links) is not { } target)
		{
			WriteText(text, at);

			return;
		}

		if (linkStyle == 0)
		{
			throw Redo(SheetChunkRedo.LinkStyle, "The cell is a link, and the workbook has no link style yet: the parallel write writes the batch again serially, which adds it.");
		}

		WriteText(text, at, linkStyle);
		(links ??= []).Add(new ChunkLink(record, at, target));
	}

	/// <summary>Writes a text cell, its string inline.</summary>
	/// <param name="text">The text, which isn't empty.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own: 0 (General), or a link's.</param>
	/// <exception cref="InvalidOperationException">The text has more than <see cref="XmlOutput.MaxCellLength"/> UTF-16 code units, or its cell format isn't the workbook's yet: flagged.</exception>
	private void WriteText(ReadOnlySpan<byte> text, int at, int style = 0)
	{
#if NET8_0_OR_GREATER
		// ASCII text is valid, and has as many UTF-16 code units as bytes: one pass, where other text takes two.
		var units = Ascii.IsValid(text) ? text.Length : ValidUtf16Length(ref text);
#else
		var units = ValidUtf16Length(ref text);
#endif
		if (units > XmlOutput.MaxCellLength)
		{
			throw Redo(SheetChunkRedo.Error, $"A cell has {units.ToString("N0", CultureInfo.InvariantCulture)} characters, and Excel opens no cell of more than {XmlOutput.MaxCellLength.ToString("N0", CultureInfo.InvariantCulture)}.");
		}

		// Text that starts like a number (00123, 1.5, -1, .5) gets Excel's warning triangle unless the sheet ignores it.
		recordNumberLikeText |= text[0] is >= (byte)'0' and <= (byte)'9' or (byte)'+' or (byte)'-' or (byte)'.';
		StartCell(at, style, XmlOutput.NeedsPreserve(text) ? " t=\"inlineStr\"><is><t xml:space=\"preserve\">"u8 : " t=\"inlineStr\"><is><t>"u8);
		output.AppendText(text);
		output.Append("</t></is></c>"u8);
	}

	/// <summary>Writes the start of a cell, up to its attributes' end, with its style, and its kind's markup after them.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own.</param>
	/// <param name="tail">The markup after the attributes.</param>
	private void StartCell(int at, int style, ReadOnlySpan<byte> tail)
	{
		OpenCell(at);
		AppendCellStart(at, style, tail);
	}

	/// <summary>Writes a cell whose value is a number or a boolean, whole, under one capacity check.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own.</param>
	/// <param name="tail">The markup between the attributes and the value.</param>
	/// <param name="value">The value's text, which needs no escaping.</param>
	private void WriteCell(int at, int style, ReadOnlySpan<byte> tail, ReadOnlySpan<byte> value)
	{
		OpenCell(at);
		var end = "</v></c>"u8;
		var room = output.GetSpan(MaxCellStart + tail.Length + value.Length + end.Length);
		var written = PutCellStart(room, at, style);
		tail.CopyTo(room.Slice(written));
		written += tail.Length;
		value.CopyTo(room.Slice(written));
		written += value.Length;
		end.CopyTo(room.Slice(written));
		output.Advance(written + end.Length);
	}

	/// <summary>Opens a cell in the current record: its row first, when it is the record's first cell, and the gaps' placeholders before it.</summary>
	/// <param name="at">The cell's column.</param>
	private void OpenCell(int at)
	{
		if (!rowOpen)
		{
			OpenRow();
		}

		if (at != recordNextColumn)
		{
			AppendGaps(at);
		}

		recordLastColumn = at;
		recordNextColumn = at + 1;
	}

	/// <summary>Writes a cell's start, with its style, and markup after it, under one capacity check.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own.</param>
	/// <param name="tail">The markup after the attributes.</param>
	private void AppendCellStart(int at, int style, ReadOnlySpan<byte> tail)
	{
		var room = output.GetSpan(MaxCellStart + tail.Length);
		var written = PutCellStart(room, at, style);
		tail.CopyTo(room.Slice(written));
		output.Advance(written + tail.Length);
	}

	/// <summary>
	/// Puts a cell's element up to its attributes' end into the buffer's room: <c>&lt;c</c>, and its style when it has one;
	/// its column's start for a cell without a style of its own, in a sheet whose cells take styles.
	/// </summary>
	/// <param name="room">The room, of <see cref="MaxCellStart"/> bytes at least.</param>
	/// <param name="at">The cell's column.</param>
	/// <param name="style">The cell format the cell has of its own.</param>
	/// <returns>The bytes put.</returns>
	/// <exception cref="InvalidOperationException">The cell's style isn't the workbook's yet: flagged.</exception>
	private int PutCellStart(Span<byte> room, int at, int style)
	{
		if (stylesColumns && style == 0)
		{
			var start = ColumnStart(at);
			start.CopyTo(room);

			return start.Length;
		}

		"<c"u8.CopyTo(room);
		if (styling)
		{
			style = StyleOf(at, style);
		}

		if (style == 0)
		{
			return 2;
		}

		" s=\""u8.CopyTo(room.Slice(2));
		Utf8Formatter.TryFormat(style, room.Slice(6), out var digits);
		room[6 + digits] = (byte)'"';

		return 7 + digits;
	}

	/// <summary>
	/// The start of a cell that has no style of its own, in a sheet whose cells take styles: the view's, or made by the
	/// worker when its column's first such cell is written and its style is the workbook's.
	/// </summary>
	/// <param name="at">The cell's column.</param>
	/// <returns>The start's bytes, at most <see cref="MaxCellStart"/>.</returns>
	/// <exception cref="InvalidOperationException">The start's style isn't the workbook's yet: flagged.</exception>
	private byte[] ColumnStart(int at)
	{
		var starts = columnStarts;
		if (starts is null || at >= starts.Length)
		{
			var grown = new byte[Math.Min(Math.Max(at + 1, (starts?.Length ?? 8) * 2), CellReference.MaxColumns)][];
			starts?.CopyTo(grown, 0);
			columnStarts = starts = grown;
		}

		if (starts[at] is { } start)
		{
			return start;
		}

		var style = StyleOf(at, 0);
		start = style == 0 ? "<c"u8.ToArray() : Encoding.ASCII.GetBytes("<c s=\"" + style.ToString(CultureInfo.InvariantCulture) + "\"");
		starts[at] = start;

		return start;
	}

	/// <summary>
	/// Starts the current record's row: an empty row first for each record without a cell between the chunk's last record
	/// with one and this one. Those before the chunk's first record with a cell are the owner's to write.
	/// </summary>
	private void OpenRow()
	{
		if (lastCellRecord >= 0)
		{
			for (var empty = record - lastCellRecord - 1; empty > 0; empty--)
			{
				output.Append("<row/>"u8);
			}
		}

		output.Append("<row>"u8);
		rowOpen = true;
	}

	/// <summary>Writes the current record's gaps before a cell: a placeholder for each column from the one after its last cell to the cell's.</summary>
	/// <param name="at">The cell's column, past the record's next one.</param>
	/// <exception cref="InvalidOperationException">A placeholder's style isn't the workbook's yet: flagged.</exception>
	private void AppendGaps(int at)
	{
		for (var gap = recordNextColumn; gap < at; gap++)
		{
			output.Append(Placeholder(gap));
		}
	}

	/// <summary>
	/// A gap's placeholder: an empty cell in its column's default style, unlocked in a protected sheet's header's columns, or
	/// <c>&lt;c/&gt;</c> for a column without one; the view's, or made by the worker when its style is the workbook's.
	/// </summary>
	/// <param name="at">The gap's column.</param>
	/// <returns>The placeholder's bytes.</returns>
	/// <exception cref="InvalidOperationException">The placeholder's unlocked style isn't the workbook's yet: flagged.</exception>
	private byte[] Placeholder(int at)
	{
		var cells = placeholders;
		if (cells is null || at >= cells.Length)
		{
			var grown = new byte[Math.Min(Math.Max(at + 1, (cells?.Length ?? 8) * 2), CellReference.MaxColumns)][];
			cells?.CopyTo(grown, 0);
			placeholders = cells = grown;
		}

		if (cells[at] is { } placeholder)
		{
			return placeholder;
		}

		var style = columnDefaults is { } defaults && at < defaults.Length ? defaults[at] : 0;
		if (at < unlockedColumns && !styles!.TryUnlocked(style, out style))
		{
			throw Redo(SheetChunkRedo.Style, StyleMissing);
		}

		placeholder = style == 0 ? EmptyCell : Encoding.ASCII.GetBytes("<c s=\"" + style.ToString(CultureInfo.InvariantCulture) + "\"/>");
		cells[at] = placeholder;

		return placeholder;
	}

	/// <summary>A cell's style below the header: its own in its column's style, unlocked in a protected sheet's header's columns.</summary>
	/// <param name="at">The cell's column.</param>
	/// <param name="own">The cell format the cell has of its own.</param>
	/// <returns>The cell format.</returns>
	/// <exception cref="InvalidOperationException">The cell format isn't the workbook's yet: flagged.</exception>
	private int StyleOf(int at, int own)
	{
		var style = own;
		if (columnStyles is { } columns && at < columns.Length && columns[at] is { } column && !column.TryOf(own, out style))
		{
			throw Redo(SheetChunkRedo.Style, StyleMissing);
		}

		if (at < unlockedColumns && !styles!.TryUnlocked(style, out style))
		{
			throw Redo(SheetChunkRedo.Style, StyleMissing);
		}

		return style;
	}
}
