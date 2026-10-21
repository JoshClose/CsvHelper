using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// A forward-only CSV reader. Input is parsed as UTF-8 bytes with SIMD instructions; fields are exposed as
/// spans into the read buffer and are only materialized as strings or numbers when asked for.
/// </summary>
/// <remarks>
/// <para>
/// Spans returned for a record are valid until the next call to <see cref="Read"/> or <see cref="ReadAsync"/>.
/// </para>
/// <para>
/// A reader is not thread-safe: use it from one thread at a time, and finish each async call before starting the next.
/// Nothing checks this; two threads reading one reader get wrong records, not an exception. To map records on several
/// threads, use <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>. The options, the record mappers and the
/// <see cref="CsvDynamicRecord"/>s a reader returns do not change, so they can be shared between threads.
/// </para>
/// </remarks>
public sealed partial class CsvReader : IDisposable, IAsyncDisposable
{
	private const int BlockSize = 64;
	// The flatten loop may write up to 16 entries past the real count.
	private const int EndsSlack = 64;
	private const int MaxBlocksPerScan = 256;
	private const int IndexCapacity = MaxBlocksPerScan * BlockSize;

	private static readonly int defaultVectorWidth =
#if NET
		Vector512.IsHardwareAccelerated ? 512 :
		Vector256.IsHardwareAccelerated ? 256 : 128;
#else
		64;
#endif

	private readonly CsvReaderOptions options;
	private readonly IFormatProvider provider;
	private readonly CsvStringPool? pool;

	// Dialect
	private byte sepByte;
	private bool separatorKnown;
	private readonly byte quoteByte;
	private readonly int quoteInt;          // -1 when quoting is disabled
	private readonly byte commentByte;
	private readonly int commentInt;        // -1 when comments are disabled
	private readonly byte escapeByte;
	private readonly int escapeInt;         // -1 outside Escape mode
	private readonly bool quotes;           // Rfc4180 mode
	private readonly bool escapes;          // Escape mode
	private readonly bool fixedWidth;       // FixedWidth mode
	private readonly bool quickUnquote;     // DecodeField may take its quoted-field fast path (Rfc4180 without trimming)
	private readonly int[] fixedStarts = []; // FixedWidth: the character position of each column start, then the end of the last
	private readonly int fixedTotal;        // FixedWidth: the width of a whole line
	private readonly int indexReserve;      // the free index entries a scan of one block (or one fixed-width line) may need
	private readonly bool strict;
	private readonly bool comments;
	private readonly bool lineBreaksInQuotes;
	private readonly bool trim;
	private readonly bool trimInsideQuotes;
	private readonly bool[] whitespace;     // the bytes that trimming removes, by value (WhitespaceCharacters; space and tab by default)
	private readonly bool customWhitespace; // WhitespaceCharacters is not space and tab
	private readonly int maxRecordSize;     // 0 for no limit
	private readonly bool trackLines;
	private readonly bool hideValues;       // IncludeValuesInExceptions is off
	private readonly Func<string, string>? normalizeHeader;
	private readonly CsvShouldSkipRecord? shouldSkip;
	private readonly Func<CsvRecordError, bool>? onRecordError;
	private readonly byte[][]? nullValues;  // the UTF-8 of NullValues
	private readonly CsvBooleanText? booleans; // TrueValues and FalseValues
	private readonly bool skipEmpty;
	private readonly CsvFieldCountHandling fieldCountMode;
	private readonly CsvMalformedDataHandling malformedMode;
	private readonly Action<CsvMalformedRecord>? onMalformed;
	private readonly bool throwOnMissing;
	internal int vectorWidth;       // 512, 256, 128 (SIMD, .NET only), 64 (64-bit integer masks) or 0 (scalar only, for testing)

	// Buffer. Positions are absolute indexes into buffer.
	private byte[] buffer;
	private bool bufferRented;
	private int dataStart;
	private int dataEnd;
	private int scanPos;
	private bool eof;
	private bool finished;
	private bool initialized;
	private bool encodingChecked;
	private long discarded;

	// Scanner state carried from one 64 byte block to the next.
	private ScanCarry carry = ScanCarry.Initial;
	private bool forceScalar;               // set while inside a comment line
	// Set when a scalar block ends after a closing quote and whitespace, with trimming and malformed detection on: the
	// SIMD carry cannot express that state, so the next block, where text would be malformed, is scanned exactly too.
	private bool afterClosingQuote;
	// Positions of malformed quoting found by the scanner (only when detection is on), in input order.
	private int[] badPos = [];
	private CsvMalformedReason[] badReason = [];
	private int badCount;
	private int badIndex;
	private int nextBadPos = int.MaxValue;

	// The index (see CsvReader.Scan.cs). ends[rowBase] is the start of the next record to return.
	private int[] ends;
	private int endsCount;
	private int[] rowEnds;
	private int rowCount;
	private int rowIndex;
	private int rowBase;
	private readonly bool rowChecks;        // comments, malformed detection or field count checks need per-record work

	// Current record
	private int fieldBase;
	private int fieldCount;
	private long rowNumber;
	private long recordNumber;
	private long lineNumber;                // TrackLineNumbers: the line where the current record starts
	private long nextLine = 1;              // and the line where the next one starts

	// Header
	private bool headerDone;
	private string[] headers = [];
	private string[] headerKeys = [];       // the header names as they are matched (NormalizeHeaderName)
	private Dictionary<string, int>? headerIndex;
	private Dictionary<string, int>? headerIndexLoose;
	private int expectedFieldCount = -1;

	// Scratch space for unescaped values of the current record
	private byte[] scratch = [];
	private int scratchPos;

	private bool disposed;

	private enum Step { Rows, NeedData, End }

	/// <param name="options">The options.</param>
	/// <param name="rentIndex">False for parallel workers, which use the index of the batches given to them.</param>
	/// <param name="sharedPool">A string pool whose values this reader shares (parallel workers).</param>
	// Not inlined: it runs once per reader, and inlined into the method that creates the reader it took registers from
	// that method's read loop (a 13% slower loop over GetSingle, depending on the size of the constructor).
	[MethodImpl(MethodImplOptions.NoInlining)]
	private CsvReader(CsvReaderOptions? options, bool rentIndex = true, CsvStringPool? sharedPool = null)
	{
		options ??= CsvReaderOptions.Default;
		options.Validate();
		this.options = options;
		provider = options.FormatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
		quotes = options.Mode == CsvMode.Rfc4180;
		escapes = options.Mode == CsvMode.Escape;
		fixedWidth = options.Mode == CsvMode.FixedWidth;
		// The scanner's quote mask matches the escape character in Escape mode; elsewhere quoteByte is only used with quotes.
		quoteByte = (byte)(escapes ? options.Escape : options.Quote);
		quoteInt = quotes ? quoteByte : -1;
		escapeByte = (byte)options.Escape;
		escapeInt = escapes ? escapeByte : -1;
		comments = options.CommentPrefix.HasValue;
		commentByte = (byte)options.CommentPrefix.GetValueOrDefault();
		commentInt = comments ? commentByte : -1;
		malformedMode = options.MalformedData;
		onMalformed = options.OnMalformedRecord;
		// Detect malformed quoting only when someone needs to know about it.
		strict = malformedMode != CsvMalformedDataHandling.Recover || onMalformed is not null;
		lineBreaksInQuotes = options.AllowLineBreaksInQuotes;
		trim = options.TrimWhitespace ?? fixedWidth;
		trimInsideQuotes = quotes && options.TrimInsideQuotes;
		quickUnquote = quotes && !trim && !trimInsideQuotes;
		whitespace = new bool[256];
		foreach (var w in options.WhitespaceCharacters)
		{
			whitespace[w] = true;
		}

		customWhitespace = options.WhitespaceCharacters is not (" \t" or "\t ");

		maxRecordSize = options.MaxRecordSize ?? 0;
		trackLines = options.TrackLineNumbers;
		hideValues = !options.IncludeValuesInExceptions;
		normalizeHeader = options.NormalizeHeaderName;
		shouldSkip = options.ShouldSkipRecord;
		onRecordError = options.OnRecordError;
		if (options.NullValues is { } nulls)
		{
			nullValues = new byte[nulls.Count][];
			for (var i = 0; i < nullValues.Length; i++)
			{
				nullValues[i] = Encoding.UTF8.GetBytes(nulls[i]);
			}
		}

		if (options.TrueValues is not null || options.FalseValues is not null)
		{
			booleans = new CsvBooleanText(options.TrueValues, options.FalseValues);
		}

		skipEmpty = options.SkipEmptyLines;
		fieldCountMode = options.FieldCountMismatch;
		throwOnMissing = options.MissingField == CsvMissingFieldHandling.Throw;
		// Trimming needs the slow path too: a line of only whitespace counts as empty. A fixed-width line has as many
		// fields as columns, so an empty one is found there too. Line numbers and the skip predicate look at every record.
		rowChecks = comments || strict || trim || fieldCountMode != CsvFieldCountHandling.Allow || fixedWidth ||
					trackLines || shouldSkip is not null;
		pool = options.PoolStrings ? sharedPool?.CreateShared() ?? new CsvStringPool() : null;
		vectorWidth = defaultVectorWidth;
		indexReserve = BlockSize + 1;
		if (fixedWidth)
		{
			fixedTotal = FixedWidth.Validate(options.FieldWidths, nameof(options.FieldWidths));
			fixedStarts = FixedWidth.Starts(options.FieldWidths!);
			// A line writes a column entry for every column but the first, its terminator and, after CR, an LF entry.
			indexReserve = Math.Max(BlockSize, fixedStarts.Length) + 1;
			separatorKnown = true;
		}
		else if (options.Separator is char sep)
		{
			sepByte = (byte)sep;
			separatorKnown = true;
		}

		buffer = [];
		ends = rentIndex ? ArrayPool<int>.Shared.Rent(IndexCapacity + EndsSlack) : [];
		rowEnds = rentIndex ? ArrayPool<int>.Shared.Rent(IndexCapacity) : [];
		endsCount = 1;
	}

	/// <summary>The options this reader was created with.</summary>
	public CsvReaderOptions Options => options;

	/// <summary>
	/// The separator in use. When it is detected from the input, getting it before the first <see cref="Read"/>
	/// reads the header (or the start of the input) synchronously. <c>'\0'</c> in <see cref="CsvMode.FixedWidth"/>
	/// mode, which has no separator.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The separator had to be detected and the reader has been disposed.</exception>
	/// <exception cref="CsvFormatException">The separator had to be detected and the header is malformed (see <see cref="Read"/>).</exception>
	public char Separator
	{
		get
		{
			if (!separatorKnown) EnsureHeader();

			return (char)sepByte;
		}
	}

	/// <summary>The number of fields in the current record.</summary>
	public int FieldCount => fieldCount;

	/// <summary>The 1-based number of the current data record (the header is not counted).</summary>
	public long RowNumber => rowNumber;

	/// <summary>
	/// The offset, in bytes of UTF-8 input, where the current record starts, counted from the start of the input (a byte
	/// order mark included), for progress reports and error messages. UTF-16 input (strings, text readers and streams
	/// with another encoding) is counted in the bytes of its UTF-8 form.
	/// </summary>
	public long ByteOffset => discarded + (ends.Length == 0 ? 0 : ends[fieldBase] & int.MaxValue);

	/// <summary>
	/// The 1-based line of the input where the current record starts, counting every line: those inside quoted fields,
	/// empty lines, comments and the header too. Requires <see cref="CsvReaderOptions.TrackLineNumbers"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException"><see cref="CsvReaderOptions.TrackLineNumbers"/> is off.</exception>
	public long LineNumber => trackLines ? lineNumber : throw new InvalidOperationException("Set TrackLineNumbers in the reader options to count lines.");

	/// <summary>
	/// The header names. Empty when <see cref="CsvReaderOptions.HasHeader"/> is <see langword="false"/>.
	/// Accessing this before the first <see cref="Read"/> reads the header synchronously; async callers
	/// can use <see cref="ReadHeaderAsync"/> first.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The header had to be read and the reader has been disposed.</exception>
	/// <exception cref="CsvFormatException">The header had to be read and is malformed (see <see cref="Read"/>).</exception>
	public IReadOnlyList<string> Headers
	{
		get
		{
			if (!headerDone) EnsureHeader();

			return headers;
		}
	}

	/// <summary>
	/// Advances to the next record, reading the header first if it has not been read. Empty lines, comments and the
	/// records that the options say to skip are passed over.
	/// </summary>
	/// <returns><see langword="true"/> when the reader is positioned on a record; <see langword="false"/> at the end of the input.</returns>
	/// <exception cref="CsvFormatException">
	/// A record is malformed and <see cref="CsvReaderOptions.MalformedData"/> or
	/// <see cref="CsvReaderOptions.FieldCountMismatch"/> is set to throw, or a record is too large to buffer.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	/// This reader is the one a <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	public bool Read()
	{
		// Nothing is parsed before the header has been read, so no header check is needed here.
		if (TryNextRecord())
		{
			rowNumber++;

			return true;
		}

		return ReadSlow();
	}

	/// <summary>
	/// Asynchronously advances to the next record, reading the header first if it has not been read. Completes
	/// synchronously while records that have already been read from the source remain. See <see cref="Read"/>.
	/// </summary>
	/// <param name="cancellationToken">Cancels reading from the source.</param>
	/// <returns>
	/// <see langword="true"/> when the reader is positioned on a record; <see langword="false"/> at the end of the input.
	/// </returns>
	/// <exception cref="CsvFormatException">
	/// A record is malformed and <see cref="CsvReaderOptions.MalformedData"/> or
	/// <see cref="CsvReaderOptions.FieldCountMismatch"/> is set to throw, or a record is too large to buffer.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	/// This reader is the one a <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
	{
		if (TryNextRecord())
		{
			rowNumber++;

			return new ValueTask<bool>(true);
		}

		return ReadAsyncSlow(cancellationToken);
	}

	/// <summary>
	/// Reads the header row (if any) without advancing to the first data record. Without a header, it still reads the
	/// start of the input, so that <see cref="Separator"/> is known. Once the header has been read, calling it while the
	/// reader is on a record makes that record the new header, for files with several data sets one after the other:
	/// <see cref="Headers"/>, the by-name lookups and the record mappers created afterwards use it, and the record does
	/// not count as a data record. Otherwise it does nothing.
	/// </summary>
	/// <exception cref="CsvFormatException">The header is malformed and the options say to throw (see <see cref="Read"/>).</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	/// This reader is the one a <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	public void ReadHeader()
	{
		if (!headerDone)
		{
			EnsureHeader();

			return;
		}

		ReplaceHeader();
	}

	/// <summary>
	/// Asynchronously reads the header row (if any) without advancing to the first data record, or makes the current
	/// record the new header. See <see cref="ReadHeader"/>.
	/// </summary>
	/// <param name="cancellationToken">Cancels reading from the source.</param>
	/// <returns>A task that completes when the header has been read.</returns>
	/// <exception cref="CsvFormatException">The header is malformed and the options say to throw (see <see cref="Read"/>).</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	/// This reader is the one a <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> selector got.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public async ValueTask ReadHeaderAsync(CancellationToken cancellationToken = default)
	{
		if (!headerDone)
		{
			await EnsureHeaderAsync(cancellationToken).ConfigureAwait(false);

			return;
		}

		ReplaceHeader();
	}

	/// <summary>Makes the current record, if there is one, the header (a new data set in the same file).</summary>
	private void ReplaceHeader()
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		if (fieldCount == 0)
		{
			return;
		}

		CaptureHeader();
		// It was counted as a data record when it was read.
		rowNumber--;
		if (fieldCountMode != CsvFieldCountHandling.Allow)
		{
			expectedFieldCount = headers.Length;
		}

		recordMapper = null;
	}

	private bool ReadSlow()
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		if (!headerDone && !EnsureHeader()) return false;
		while (true)
		{
			if (TryNextRecord())
			{
				rowNumber++;

				return true;
			}

			if (!Fill())
			{
				fieldCount = 0;

				return false;
			}
		}
	}

	private async ValueTask<bool> ReadAsyncSlow(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfParallelWorker();
		if (!headerDone && !await EnsureHeaderAsync(cancellationToken).ConfigureAwait(false)) return false;
		while (true)
		{
			if (TryNextRecord())
			{
				rowNumber++;

				return true;
			}

			if (!await FillAsync(cancellationToken).ConfigureAwait(false))
			{
				fieldCount = 0;

				return false;
			}
		}
	}

	private bool EnsureHeader()
	{
		ThrowIfDisposed();
		if (options.HasHeader)
		{
			while (!TryNextRecord())
			{
				if (!Fill()) return FinishHeader(false);
			}

			CaptureHeader();
		}
		else
		{
			// No header: still run encoding and separator detection so Separator is meaningful.
			while (!initialized && !TryInitialize()) Refill();
		}

		return FinishHeader(true);
	}

	private async ValueTask<bool> EnsureHeaderAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		if (options.HasHeader)
		{
			while (!TryNextRecord())
			{
				if (!await FillAsync(cancellationToken).ConfigureAwait(false)) return FinishHeader(false);
			}

			CaptureHeader();
		}
		else
		{
			while (!initialized && !TryInitialize()) await RefillAsync(cancellationToken).ConfigureAwait(false);
		}

		return FinishHeader(true);
	}

	private bool FinishHeader(bool hasMore)
	{
		headerDone = true;
		headerIndex ??= new Dictionary<string, int>(StringComparer.Ordinal);
		headerIndexLoose ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (!hasMore) fieldCount = 0;

		return hasMore;
	}

	private void CaptureHeader()
	{
		var count = fieldCount;
		var names = new string[count];
		var keys = normalizeHeader is null ? names : new string[count];
		var exact = new Dictionary<string, int>(count, StringComparer.Ordinal);
		var loose = new Dictionary<string, int>(count, StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < count; i++)
		{
			var name = MakeString(GetSpan(i), -1);
			names[i] = name;
			var key = HeaderKey(name);
			keys[i] = key;
			exact.TryAdd(key, i);
			loose.TryAdd(key.Trim(), i);
		}

		headers = names;
		headerKeys = keys;
		headerIndex = exact;
		headerIndexLoose = loose;
		fieldCount = 0;
	}

	/// <summary>A header name as it is matched: changed by <see cref="CsvReaderOptions.NormalizeHeaderName"/>, if set.</summary>
	private string HeaderKey(string name) => normalizeHeader is null ? name : normalizeHeader(name) ?? "";

	/// <summary>
	/// Moves to the next parsed record, skipping empty lines and comments. Returns false when the
	/// parsed index is exhausted and more input must be scanned.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryNextRecord()
	{
		while (rowIndex < rowCount)
		{
			var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(rowEnds), (nint)(uint)rowIndex);
			rowIndex++;
			var last = entry & int.MaxValue;
			var b = rowBase;
			rowBase = last;
			// For CRLF the record's last field ends at the CR entry just before the LF entry.
			var count = last - b - (int)((uint)entry >> 31);
			if (rowChecks)
			{
				if (SkipRecord(b, last, count)) continue;
			}
			else if (count == 1 && skipEmpty)
			{
				// An empty line: the only field starts where it ends.
				ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(ends), (nint)(uint)b);
				if ((Unsafe.Add(ref e, 1) & int.MaxValue) - (e & int.MaxValue) == 1) continue;
			}

			fieldBase = b;
			fieldCount = count;
			recordNumber++;
			scratchPos = 0;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Per-record checks for records that may be empty lines or comments, contain malformed quoting, or
	/// have an unexpected field count, and the line count and skip predicate. Returns true when the record must be skipped.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool SkipRecord(int b, int last, int count)
	{
		var start = ends[b] & int.MaxValue;
		var recordEnd = (ends[b + count] & int.MaxValue) - 1;
		if (trackLines)
		{
			// Every record, skipped or not, is at least one line, and its quoted (or escaped) line breaks are more.
			lineNumber = nextLine;
			nextLine += 1 + LineBreaks(new ReadOnlySpan<byte>(buffer, start, Math.Max(recordEnd - start, 0)));
		}

		if (nextBadPos != int.MaxValue && (ends[last] & int.MaxValue) > nextBadPos)
		{
			// The record contains malformed quoting; consume every problem found inside it.
			var limit = ends[last] & int.MaxValue;
			var pos = badPos[badIndex];
			var reason = badReason[badIndex];
			while (badIndex < badCount && badPos[badIndex] < limit) badIndex++;
			nextBadPos = badIndex < badCount ? badPos[badIndex] : int.MaxValue;
			if (badIndex == badCount) badIndex = badCount = 0;
			if (malformedMode == CsvMalformedDataHandling.Throw) ThrowMalformed(reason, pos);
			var skip = malformedMode == CsvMalformedDataHandling.Skip;
			onMalformed?.Invoke(new CsvMalformedRecord(recordNumber + 1, reason, skip, discarded + pos, Raw(start, recordEnd), LineOrZero));
			if (skip) return true;
		}

		// A fixed-width record has a field per column, empty or not: look at the whole line.
		var end = fixedWidth ? recordEnd : (ends[b + 1] & int.MaxValue) - 1;
		if ((count == 1 || fixedWidth) && skipEmpty && IsBlank(start, end)) return true;
		if (commentInt >= 0 && start < end && buffer[start] == commentInt) return true;
		if (fieldCountMode != CsvFieldCountHandling.Allow)
		{
			if (expectedFieldCount < 0)
			{
				expectedFieldCount = count;
			}
			else if (count != expectedFieldCount)
			{
				if (fieldCountMode == CsvFieldCountHandling.Throw)
				{
					throw new CsvFormatException(
						$"Record {recordNumber + 1}{AtLine} has {count} fields but {expectedFieldCount} were expected.",
						recordNumber + 1, discarded + start) { LineNumber = LineOrZero };
				}

				onMalformed?.Invoke(new CsvMalformedRecord(recordNumber + 1, CsvMalformedReason.FieldCountMismatch, true,
					discarded + start, Raw(start, recordEnd), LineOrZero));

				return true;
			}
		}

		return shouldSkip is not null && headerDone && SkipByPredicate(b, count);
	}

	/// <summary>Asks <see cref="CsvReaderOptions.ShouldSkipRecord"/> about the record, with the reader positioned on it.</summary>
	private bool SkipByPredicate(int b, int count)
	{
		fieldBase = b;
		fieldCount = count;
		scratchPos = 0;
		// The number the record gets when it is not skipped.
		rowNumber++;
		try
		{
			return shouldSkip!(new CsvRow(this));
		}
		finally
		{
			rowNumber--;
		}
	}

	// The line of the record being checked, for errors: 0 without TrackLineNumbers.
	private long LineOrZero => trackLines ? lineNumber : 0;

	// " (line n)" in error messages, with TrackLineNumbers.
	private string AtLine => trackLines ? $" (line {lineNumber})" : "";

	/// <summary>The number of line breaks in <paramref name="s"/>: CR, LF and CR LF each count once.</summary>
	private static int LineBreaks(ReadOnlySpan<byte> s)
	{
		var count = 0;
		while (true)
		{
			var k = s.IndexOfAny((byte)'\r', (byte)'\n');
			if (k < 0)
			{
				return count;
			}

			count++;
			if (s[k] == (byte)'\r' && k + 1 < s.Length && s[k + 1] == (byte)'\n')
			{
				k++;
			}

			s = s.Slice(k + 1);
		}
	}

	private string Raw(int start, int end) => Encoding.UTF8.GetString(buffer, start, end - start);

	private bool IsBlank(int start, int end)
	{
		if (start == end) return true;
		if (!trim) return false;
		for (var i = start; i < end; i++)
		{
			if (!whitespace[buffer[i]]) return false;
		}

		return true;
	}

	/// <summary>Discards consumed records from the index so that the next scan starts with a compact index.</summary>
	private void Recycle()
	{
		var keep = rowBase;
		if (keep > 0)
		{
			var n = endsCount - keep;
			ends.AsSpan(keep, n).CopyTo(ends);
			endsCount = n;
			rowBase = 0;
			fieldBase = 0;
		}

		rowCount = 0;
		rowIndex = 0;
	}

	private bool Fill()
	{
		Recycle();
		while (true)
		{
			switch (ParseStep())
			{
				case Step.Rows: return true;
				case Step.End: return false;
				default: Refill(); break;
			}
		}
	}

	private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
	{
		Recycle();
		while (true)
		{
			switch (ParseStep())
			{
				case Step.Rows: return true;
				case Step.End: return false;
				default: await RefillAsync(cancellationToken).ConfigureAwait(false); break;
			}
		}
	}

	private Step ParseStep()
	{
		if (!initialized && !TryInitialize()) return Step.NeedData;
		while (true)
		{
			if (dataEnd - scanPos >= BlockSize)
			{
				ScanAvailable();
				if (rowCount > 0) return Step.Rows;
				continue;
			}

			if (!eof) return Step.NeedData;
			if (!finished)
			{
				ScanFinalBlock();
				if (rowCount > 0) return Step.Rows;
			}

			return Step.End;
		}
	}

	/// <summary>Scans the partial block at the end of the input and terminates the last record.</summary>
	private void ScanFinalBlock()
	{
		EnsureIndexCapacity();
		if (fixedWidth)
		{
			// The scan stops when the index is full; nothing is consumed here, so grow it and go on.
			while (scanPos < dataEnd)
			{
				scanPos = ScanFixed(scanPos, dataEnd);
				EnsureIndexCapacity();
			}
		}
		else if (scanPos < dataEnd)
		{
			ScalarBlock(scanPos, dataEnd - scanPos);
			scanPos = dataEnd;
		}

		FinishInput();
		finished = true;
	}

	private void EnsureIndexCapacity()
	{
		if (ends.Length - EndsSlack - endsCount < indexReserve)
		{
			var bigger = ArrayPool<int>.Shared.Rent(Math.Max(ends.Length * 2, endsCount + indexReserve + EndsSlack));
			ends.AsSpan(0, endsCount).CopyTo(bigger);
			ArrayPool<int>.Shared.Return(ends);
			ends = bigger;
		}

		if (rowEnds.Length - rowCount < BlockSize + 1)
		{
			var bigger = ArrayPool<int>.Shared.Rent(rowEnds.Length * 2);
			rowEnds.AsSpan(0, rowCount).CopyTo(bigger);
			ArrayPool<int>.Shared.Return(rowEnds);
			rowEnds = bigger;
		}
	}

	private void ScanAvailable() => ScanAvailable(dataEnd);

	/// <summary>Scans whole blocks up to <paramref name="end"/>, as many as the index has room for.</summary>
	private void ScanAvailable(int end)
	{
		EnsureIndexCapacity();
		var pos = scanPos;
		if (fixedWidth)
		{
			// Whole lines, up to as many bytes as a block scan takes (a partial line waits for more input).
			scanPos = ScanFixed(pos, (int)Math.Min(end, (long)pos + IndexCapacity));

			return;
		}

		var blocks = (end - pos) >> 6;
		var room = Math.Min((ends.Length - EndsSlack - endsCount) >> 6, (rowEnds.Length - rowCount) >> 6);
		if (blocks > room) blocks = room;
		if (blocks > MaxBlocksPerScan) blocks = MaxBlocksPerScan;
		var limit = pos + (blocks << 6);
		while (pos < limit)
		{
			if (!forceScalar && !afterClosingQuote)
			{
				pos = ScanSimd(pos, limit);
				if (pos >= limit) break;
			}

			ScalarBlock(pos, BlockSize);
			pos += BlockSize;
		}

		scanPos = pos;
	}

	/// <summary>Terminates the last record when the input does not end with a line break.</summary>
	private void FinishInput()
	{
		if (strict && carry.InQuote != 0) RecordMalformed(dataEnd, CsvMalformedReason.UnterminatedQuote);
		if (strict && carry.PrevEscape != 0) RecordMalformed(dataEnd - 1, CsvMalformedReason.TrailingEscape);
		if (carry.PrevCr != 0)
		{
			// The input ends with a CR: it ended the last record.
			rowEnds[rowCount++] = endsCount - 1;
			carry.PrevCr = 0;
		}

		var lastTermIdx = rowCount > 0 ? rowEnds[rowCount - 1] & int.MaxValue : rowBase;
		var start = ends[lastTermIdx] & int.MaxValue;
		if (start < dataEnd)
		{
			// The last record has no line break: terminate it at the end of the input.
			if (fixedWidth) WriteFixedColumns(start, dataEnd);
			var flags = (carry.FieldQuoted != 0 || trim || fixedWidth) ? int.MinValue : 0;
			ends[endsCount] = (dataEnd + 1) | flags;
			rowEnds[rowCount++] = endsCount;
			endsCount++;
		}
	}

	private void RecordMalformed(int pos, CsvMalformedReason reason)
	{
		if (badCount == badPos.Length)
		{
			Array.Resize(ref badPos, Math.Max(16, badCount * 2));
			Array.Resize(ref badReason, badPos.Length);
		}

		badPos[badCount] = pos;
		badReason[badCount] = reason;
		if (badCount++ == badIndex) nextBadPos = pos;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ThrowMalformed(CsvMalformedReason reason, int pos)
	{
		var what = reason switch
		{
			CsvMalformedReason.StrayQuote => "a quote inside an unquoted field",
			CsvMalformedReason.TextAfterClosingQuote => "text after the closing quote of a quoted field",
			CsvMalformedReason.UnterminatedQuote => "a quoted field that is never closed",
			CsvMalformedReason.LineBreakInQuotes => "a line break inside a quoted field",
			CsvMalformedReason.TrailingEscape => "an escape character at the end of the input",
			_ => "malformed data",
		};
		var record = recordNumber + 1;
		throw new CsvFormatException($"Malformed CSV in record {record}{AtLine}: {what} (byte offset {discarded + pos}).",
			record, discarded + pos) { LineNumber = LineOrZero };
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

	/// <summary>Returns an enumerator so records can be read with <c>foreach</c>.</summary>
	/// <returns>An enumerator that calls <see cref="Read"/> to move to the next record.</returns>
	public Enumerator GetEnumerator() => new(this);

	/// <summary>Returns an async enumerator so records can be read with <c>await foreach</c>.</summary>
	/// <param name="cancellationToken">Cancels reading from the source.</param>
	/// <returns>An enumerator that calls <see cref="ReadAsync"/> to move to the next record.</returns>
	public AsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default) => new(this, cancellationToken);

	/// <summary>Enumerates the records of a <see cref="CsvReader"/>.</summary>
	public readonly struct Enumerator
	{
		private readonly CsvReader reader;
		internal Enumerator(CsvReader reader) => this.reader = reader;

		/// <summary>The current record. Valid until the next call to <see cref="MoveNext"/>.</summary>
		public CsvRow Current => new(reader);

		/// <summary>Advances to the next record. See <see cref="CsvReader.Read"/>.</summary>
		/// <returns><see langword="true"/> when the reader is positioned on a record; <see langword="false"/> at the end of the input.</returns>
		public bool MoveNext() => reader.Read();
	}

	/// <summary>Asynchronously enumerates the records of a <see cref="CsvReader"/>.</summary>
	public readonly struct AsyncEnumerator
	{
		private readonly CsvReader reader;
		private readonly CancellationToken cancellationToken;
		internal AsyncEnumerator(CsvReader reader, CancellationToken cancellationToken)
		{
			this.reader = reader;
			this.cancellationToken = cancellationToken;
		}

		/// <summary>The current record. Valid until the next call to <see cref="MoveNextAsync"/>.</summary>
		public CsvRow Current => new(reader);

		/// <summary>Asynchronously advances to the next record. See <see cref="CsvReader.ReadAsync"/>.</summary>
		/// <returns>
		/// <see langword="true"/> when the reader is positioned on a record; <see langword="false"/> at the end of the input.
		/// </returns>
		public ValueTask<bool> MoveNextAsync() => reader.ReadAsync(cancellationToken);

		/// <summary>Does nothing: the reader stays open, and is disposed by its owner.</summary>
		/// <returns>A completed task.</returns>
		public ValueTask DisposeAsync() => default;
	}

	/// <summary>
	/// Returns the buffers to their pools and disposes the stream or text reader being read, unless the reader was
	/// created with <c>leaveOpen</c>. Calling it again does nothing.
	/// </summary>
	public void Dispose()
	{
		if (disposed) return;
		disposed = true;
		ReturnBuffers();
		DisposeSource();
	}

	/// <summary>
	/// Returns the buffers to their pools and asynchronously disposes the stream or text reader being read, unless the
	/// reader was created with <c>leaveOpen</c>. Calling it again does nothing.
	/// </summary>
	/// <returns>A task that completes when the source has been disposed.</returns>
	public async ValueTask DisposeAsync()
	{
		if (disposed) return;
		disposed = true;
		ReturnBuffers();
		await DisposeSourceAsync().ConfigureAwait(false);
	}

	private void ReturnBuffers()
	{
		if (bufferRented) ArrayPool<byte>.Shared.Return(buffer);
		bufferRented = false;
		buffer = [];
		ArrayPool<int>.Shared.Return(ends);
		ArrayPool<int>.Shared.Return(rowEnds);
		ends = [];
		rowEnds = [];
		if (chars is not null) ArrayPool<char>.Shared.Return(chars);
		chars = null;
		rowCount = rowIndex = fieldCount = 0;
		endsCount = 0;
	}
}
