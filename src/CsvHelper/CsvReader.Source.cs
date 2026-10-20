using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

public sealed partial class CsvReader
{
	private enum SourceKind { Memory, Stream, TextReader, String }

	private SourceKind sourceKind;
	private Stream? stream;
	private TextReader? textReader;
	private bool disposeStream;
	private bool disposeTextReader;
	private string? text;
	private int textPos;
	private char[]? chars;
	private int charsPending;
	private bool hintChecked;

	/// <summary>Creates a reader over a stream. The stream is read sequentially; it does not need to be seekable.</summary>
	/// <param name="stream">
	/// The input, read from its current position. A UTF-16 or UTF-32 byte order mark sets its encoding; otherwise it
	/// is <see cref="CsvReaderOptions.Encoding"/>, or UTF-8 when that is not set.
	/// </param>
	/// <param name="options">
	/// The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>. For a
	/// <see cref="CsvSeparatorStream"/>, the separator is its <see cref="CsvSeparatorStream.Replacement"/>, and the stream
	/// takes the quoting, escape and comment rules of these options.
	/// </param>
	/// <param name="leaveOpen">Whether <paramref name="stream"/> stays open when the reader is disposed.</param>
	/// <returns>A reader positioned before the header. Nothing is read from <paramref name="stream"/> until it is needed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes; or, for
	/// a <see cref="CsvSeparatorStream"/>, sets another separator or FixedWidth mode, or a character its separator contains.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	/// <exception cref="NotSupportedException">
	/// <paramref name="stream"/> is a <see cref="CsvSeparatorStream"/> and <see cref="CsvReaderOptions.Encoding"/> is
	/// neither UTF-8 nor a single-byte encoding.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="stream"/> is a <see cref="CsvSeparatorStream"/> that has already been read from.
	/// </exception>
	public static CsvReader Create(Stream stream, CsvReaderOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (stream is CsvSeparatorStream separatorStream) options = separatorStream.Attach(options);
		var reader = new CsvReader(options);
		reader.sourceKind = SourceKind.Stream;
		reader.stream = stream;
		reader.disposeStream = !leaveOpen;
		var size = reader.options.BufferSize;
		if (stream.CanSeek)
		{
			var remaining = stream.Length - stream.Position;
			if (remaining + 1024 < size) size = (int)Math.Max(remaining + 1024, 1024);
		}

		reader.InitOwnedBuffer(size);

		return reader;
	}

	/// <summary>Creates a reader over UTF-16 text from a <see cref="TextReader"/>.</summary>
	/// <param name="textReader">The input. <see cref="CsvReaderOptions.Encoding"/> does not apply to it.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether <paramref name="textReader"/> stays open when the reader is disposed.</param>
	/// <returns>A reader positioned before the header. Nothing is read from <paramref name="textReader"/> until it is needed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="textReader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	public static CsvReader Create(TextReader textReader, CsvReaderOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(textReader);
		var reader = new CsvReader(options);
		reader.UseTextReader(textReader, dispose: !leaveOpen);

		return reader;
	}

	/// <summary>Creates a reader over UTF-8 bytes in memory. Array backed memory is read in place without copying.</summary>
	/// <param name="utf8">
	/// The input. It must not change while the reader is in use. A UTF-16 or UTF-32 byte order mark, or
	/// <see cref="CsvReaderOptions.Encoding"/>, makes the reader transcode it instead.
	/// </param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <returns>A reader positioned before the header.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	public static CsvReader Create(ReadOnlyMemory<byte> utf8, CsvReaderOptions? options = null)
	{
		var reader = new CsvReader(options);
		if (!MemoryMarshal.TryGetArray(utf8, out var segment))
			segment = new ArraySegment<byte>(utf8.ToArray());
		reader.sourceKind = SourceKind.Memory;
		reader.buffer = segment.Array!;
		reader.bufferRented = false;
		reader.dataStart = segment.Offset;
		reader.dataEnd = segment.Offset + segment.Count;
		reader.scanPos = segment.Offset;
		reader.ends[0] = segment.Offset;
		// Byte offsets count from the start of the input, not of the array.
		reader.discarded = -segment.Offset;
		reader.eof = true;

		return reader;
	}

	/// <summary>Creates a reader over UTF-8 bytes in memory. The array is read in place without copying.</summary>
	/// <param name="utf8">
	/// The input. It must not change while the reader is in use. A UTF-16 or UTF-32 byte order mark, or
	/// <see cref="CsvReaderOptions.Encoding"/>, makes the reader transcode it instead.
	/// </param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <returns>A reader positioned before the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="utf8"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	public static CsvReader Create(byte[] utf8, CsvReaderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(utf8);

		return Create(new ReadOnlyMemory<byte>(utf8), options);
	}

	/// <summary>Creates a reader over CSV text held in a string.</summary>
	/// <param name="text">The input. <see cref="CsvReaderOptions.Encoding"/> does not apply to it.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <returns>A reader positioned before the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	public static CsvReader FromText(string text, CsvReaderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(text);
		var reader = new CsvReader(options);
		reader.UseString(text);

		return reader;
	}

	/// <summary>
	/// Opens a file for reading, sharing it with other readers. The reader closes the file when it is disposed.
	/// See <see cref="Create(Stream, CsvReaderOptions?, bool)"/>.
	/// </summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <param name="fileOptions">How the file is opened. The default tells the operating system it is read sequentially.</param>
	/// <returns>A reader positioned before the header.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or <paramref name="options"/> sets a character that is not ASCII, or uses
	/// the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	/// <exception cref="FileNotFoundException">The file does not exist.</exception>
	/// <exception cref="DirectoryNotFoundException">The directory of the file does not exist.</exception>
	/// <exception cref="UnauthorizedAccessException">The file cannot be read with the caller's permissions.</exception>
	/// <exception cref="IOException">The file cannot be opened, for example because another process is writing it.</exception>
	public static CsvReader FromFile(string path, CsvReaderOptions? options = null, FileOptions fileOptions = FileOptions.SequentialScan)
	{
		var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, fileOptions);
		try
		{
			return Create(stream, options, leaveOpen: false);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	private void InitOwnedBuffer(int size)
	{
		buffer = ArrayPool<byte>.Shared.Rent(size);
		bufferRented = true;
		dataStart = dataEnd = scanPos = 0;
		ends[0] = 0;
		eof = false;
	}

	private void UseTextReader(TextReader textReader, bool dispose)
	{
		sourceKind = SourceKind.TextReader;
		this.textReader = textReader;
		disposeTextReader = dispose;
		var size = options.BufferSize;
		chars = ArrayPool<char>.Shared.Rent(Math.Max(size / 4, 1024));
		charsPending = 0;
		InitOwnedBuffer(size);
	}

	private void UseString(string text)
	{
		sourceKind = SourceKind.String;
		this.text = text;
		textPos = 0;
		var estimate = (long)text.Length * 3 + 1024;
		InitOwnedBuffer((int)Math.Min(options.BufferSize, Math.Max(estimate, 4096)));
	}

	private void Refill()
	{
		PrepareForRead();
		ReadMore();
	}

	/// <summary>Reads from the source into the free space at the end of the buffer.</summary>
	private void ReadMore()
	{
		var offset = dataEnd;
		var count = buffer.Length - dataEnd;
		var n = sourceKind switch
		{
			SourceKind.Stream => stream!.Read(buffer, offset, count),
			SourceKind.String => ReadFromString(offset, count),
			SourceKind.TextReader => ReadFromTextReader(offset, count),
			_ => 0,
		};
		CompleteRead(n);
	}

	private async ValueTask RefillAsync(CancellationToken cancellationToken)
	{
		PrepareForRead();
		var offset = dataEnd;
		var count = buffer.Length - dataEnd;
		var n = sourceKind switch
		{
			SourceKind.Stream => await stream!.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false),
			SourceKind.String => ReadFromString(offset, count),
			SourceKind.TextReader => await ReadFromTextReaderAsync(offset, count, cancellationToken).ConfigureAwait(false),
			_ => 0,
		};
		CompleteRead(n);
	}

	private void CompleteRead(int n)
	{
		if (n <= 0) eof = true;
		else dataEnd += n;
	}

	/// <summary>
	/// Makes room for more input: drops the bytes of records already returned (moving the pending record to
	/// the start of the buffer and shifting every stored position) and grows the buffer when a record is
	/// larger than the free space.
	/// </summary>
	private void PrepareForRead()
	{
		if (sourceKind == SourceKind.Memory) throw new InvalidOperationException("In-memory input cannot be refilled.");
		// ends[0] is the start of the first record not yet returned; everything before it is consumed.
		var keepFrom = ends[0] & int.MaxValue;
		if (keepFrom > 0)
		{
			var keep = dataEnd - keepFrom;
			Buffer.BlockCopy(buffer, keepFrom, buffer, 0, keep);
			dataEnd = keep;
			scanPos -= keepFrom;
			dataStart = Math.Max(0, dataStart - keepFrom);
			for (var i = 0; i < endsCount; i++) ends[i] -= keepFrom;
			for (var i = badIndex; i < badCount; i++) badPos[i] -= keepFrom;
			if (nextBadPos != int.MaxValue) nextBadPos -= keepFrom;
			discarded += keepFrom;
		}

		// What is left is the record still being read, and less than a block of input not scanned yet, which can hold
		// the record's end. The scanned bytes have no record terminator: they are all part of the record.
		if (maxRecordSize > 0 && scanPos > maxRecordSize)
		{
			ThrowRecordTooLarge();
		}

		var minFree = sourceKind == SourceKind.Stream ? 1024 : 8192;
		var free = buffer.Length - dataEnd;
		// Grow when there is almost no room, or when nothing could be dropped and the pending
		// record already fills most of the buffer (avoids many tiny reads for huge records).
		if (free < minFree || (keepFrom <= 0 && free < buffer.Length / 4))
		{
			var newSize = Math.Max((long)buffer.Length * 2, (long)dataEnd + minFree * 2);
			if (newSize > Array.MaxLength) newSize = Array.MaxLength;
			if (newSize - dataEnd < 64) throw new CsvFormatException("A single CSV record is too large to buffer.");
			var bigger = ArrayPool<byte>.Shared.Rent((int)newSize);
			Buffer.BlockCopy(buffer, 0, bigger, 0, dataEnd);
			if (bufferRented) ArrayPool<byte>.Shared.Return(buffer);
			buffer = bigger;
			bufferRented = true;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ThrowRecordTooLarge() =>
		throw new CsvFormatException(
			$"Record {recordNumber + 1} is longer than MaxRecordSize ({maxRecordSize} bytes).", recordNumber + 1, discarded)
		{
			LineNumber = trackLines ? nextLine : 0,
		};

	private int ReadFromString(int offset, int count)
	{
		var remaining = text!.Length - textPos;
		if (remaining == 0) return 0;
		// Transcode as much as fits: FromUtf16 stops cleanly when the destination is full.
		var n = Math.Min(remaining, count);
		var final = n == remaining;
		var written = Transcode(text.AsSpan(textPos, n), buffer.AsSpan(offset, count), final, out var consumed);
		textPos += consumed;

		return written;
	}

	/// <summary>UTF-16 to UTF-8, with a fast narrowing path for the (usual) all-ASCII case.</summary>
	private static int Transcode(ReadOnlySpan<char> source, Span<byte> destination, bool final, out int consumed)
	{
		var ascii = Math.Min(source.Length, destination.Length);
		if (Ascii.FromUtf16(source.Slice(0, ascii), destination, out var written) == OperationStatus.Done)
		{
			consumed = ascii;

			return written;
		}

		// Non-ASCII from 'written' on: the ASCII prefix is already in place, transcode the rest properly.
		Utf8.FromUtf16(source.Slice(written), destination.Slice(written), out var restConsumed, out var restWritten,
			replaceInvalidSequences: true, isFinalBlock: final);
		consumed = written + restConsumed;

		return written + restWritten;
	}

	private int ReadFromTextReader(int offset, int count)
	{
		while (true)
		{
			// Chars left over from the previous call (the destination filled up) are transcoded first.
			var room = Math.Min(chars!.Length, count) - charsPending;
			var read = room > 0 ? textReader!.Read(chars, charsPending, room) : 0;
			var final = room > 0 && read == 0;
			var written = TranscodeChars(charsPending + read, final, offset, count);
			if (written > 0 || final) return written;
		}
	}

	private async ValueTask<int> ReadFromTextReaderAsync(int offset, int count, CancellationToken cancellationToken)
	{
		while (true)
		{
			var room = Math.Min(chars!.Length, count) - charsPending;
			var read = room > 0
				? await textReader!.ReadAsync(chars.AsMemory(charsPending, room), cancellationToken).ConfigureAwait(false)
				: 0;
			var final = room > 0 && read == 0;
			var written = TranscodeChars(charsPending + read, final, offset, count);
			if (written > 0 || final) return written;
		}
	}

	private int TranscodeChars(int charCount, bool final, int offset, int count)
	{
		if (charCount == 0) return 0;
		var written = Transcode(chars.AsSpan(0, charCount), buffer.AsSpan(offset, count), final, out var consumed);
		var left = charCount - consumed;
		if (left > 0) chars.AsSpan(consumed, left).CopyTo(chars);
		charsPending = left;

		return written;
	}

	/// <summary>
	/// Runs once before scanning: handles byte order marks and other encodings, and detects the separator.
	/// Returns false when more input is needed first.
	/// </summary>
	private bool TryInitialize()
	{
		if (!encodingChecked)
		{
			if (!eof && dataEnd - dataStart < 4) return false;
			if (!CheckEncoding()) return false;
		}

		if (options.DetectSeparatorHint && !hintChecked && !fixedWidth)
		{
			// Excel's "sep=;" first line names the separator.
			ReadOnlySpan<byte> head = buffer.AsSpan(dataStart, dataEnd - dataStart);
			if (!eof && head.Length < 7 && head.IndexOfAny((byte)'\r', (byte)'\n') < 0) return false;
			hintChecked = true;
			if (head.Length >= 5 && Ascii.EqualsIgnoreCase(head.Slice(0, 4), "sep="u8) &&
				(head.Length == 5 || head[5] == (byte)'\r' || head[5] == (byte)'\n') &&
				head[4] < 0x80 && head[4] != (byte)'\r' && head[4] != (byte)'\n' && head[4] != quoteInt && head[4] != escapeInt)
			{
				var hinted = head[4];
				if (!separatorKnown || hinted == sepByte)
				{
					sepByte = hinted;
					separatorKnown = true;
					var skip = head.Length == 5 ? 5 : head.Length > 6 && head[5] == (byte)'\r' && head[6] == (byte)'\n' ? 7 : 6;
					dataStart += skip;
					scanPos = dataStart;
					ends[0] = dataStart;
					// The hint is a line of the input, though not a record.
					nextLine++;
				}
			}
		}

		if (!separatorKnown)
		{
			ReadOnlySpan<byte> data = buffer.AsSpan(dataStart, dataEnd - dataStart);
			var bufferFull = dataEnd >= buffer.Length - 1024;
			if (!eof && !bufferFull && !CsvSeparatorDetector.HasEnoughData(data)) return false;
			sepByte = CsvSeparatorDetector.Detect(data, quoteInt, commentInt, escapeInt);
			separatorKnown = true;
		}

		initialized = true;

		return true;
	}

	/// <summary>Returns false if the source was replaced by a transcoding source and must be read again.</summary>
	private bool CheckEncoding()
	{
		encodingChecked = true;
		ReadOnlySpan<byte> data = buffer.AsSpan(dataStart, dataEnd - dataStart);
		var encoding = options.Encoding;
		var isUtf8 = encoding is null || encoding.CodePage == Encoding.UTF8.CodePage;
		if (sourceKind is SourceKind.Stream or SourceKind.Memory)
		{
			var bomEncoding = DetectUnicodeBom(data, out var bomLength);
			if (bomEncoding is not null)
			{
				SwitchToTranscoding(bomEncoding, data.Slice(bomLength));

				return false;
			}

			if (!isUtf8)
			{
				SwitchToTranscoding(encoding!, data);

				return false;
			}
		}

		if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
		{
			dataStart += 3;
			scanPos = dataStart;
			ends[0] = dataStart;
		}

		return true;
	}

	/// <summary>Recognizes UTF-16 and UTF-32 byte order marks (UTF-8 is handled without transcoding).</summary>
	private static Encoding? DetectUnicodeBom(ReadOnlySpan<byte> data, out int length)
	{
		length = 4;
		if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE && data[2] == 0 && data[3] == 0)
			return new UTF32Encoding(bigEndian: false, byteOrderMark: false);
		if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF)
			return new UTF32Encoding(bigEndian: true, byteOrderMark: false);
		length = 2;
		if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
		if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
		length = 0;

		return null;
	}

	private void SwitchToTranscoding(Encoding encoding, ReadOnlySpan<byte> alreadyRead)
	{
		var prefix = alreadyRead.ToArray();
		Stream inner;
		bool ownsInner;
		if (sourceKind == SourceKind.Memory)
		{
			inner = Stream.Null;
			ownsInner = false;
			buffer = [];
		}
		else
		{
			inner = stream!;
			ownsInner = disposeStream;
			if (bufferRented) ArrayPool<byte>.Shared.Return(buffer);
			buffer = [];
			bufferRented = false;
		}

		stream = null;
		disposeStream = false;
		// The new buffer holds the UTF-8 form of the input from its start (an in-memory slice set this negative).
		discarded = 0;
		var replay = new ReplayStream(prefix, inner, ownsInner);
		var textReader = new StreamReader(replay, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 16 * 1024, leaveOpen: false);
		UseTextReader(textReader, dispose: true);
	}

	private void DisposeSource()
	{
		if (disposeStream) stream?.Dispose();
		if (disposeTextReader) textReader?.Dispose();
		stream = null;
		textReader = null;
		text = null;
	}

	private async ValueTask DisposeSourceAsync()
	{
		if (disposeStream && stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
		if (disposeTextReader) textReader?.Dispose();
		stream = null;
		textReader = null;
		text = null;
	}
}
