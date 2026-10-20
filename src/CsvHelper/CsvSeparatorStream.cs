using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// A read-only stream that lets <see cref="CsvReader"/> read files whose separator is longer than one character, such as
/// <c>||</c>, or is not ASCII: it puts one byte, <see cref="Replacement"/>, in place of every separator.
/// </summary>
/// <remarks>
/// <para>
/// Pass it to <see cref="CsvReader.Create(Stream, CsvReaderOptions?, bool)"/>. The reader then splits fields on
/// <see cref="Replacement"/>, and gives the stream its <see cref="CsvReaderOptions.Mode"/>, quote, escape and comment
/// characters, so the separators that are text stay as they are: in quoted fields, after an escape character and in
/// comment lines. Everything else passes through unchanged, so <see cref="CsvReader.RawRecord"/> shows
/// <see cref="Replacement"/> where the separator was.
/// </para>
/// <para>
/// The stream works on bytes, in the buffer of the caller, so the reader keeps its UTF-8 fast path. The input is UTF-8,
/// or a single-byte encoding set in <see cref="CsvReaderOptions.Encoding"/>; UTF-16 and UTF-32 input is not supported.
/// Input that contains <see cref="Replacement"/> outside quoted fields would read wrongly, so reading it throws a
/// <see cref="CsvFormatException"/>: choose another replacement for such data.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var reader = CsvReader.Create(new CsvSeparatorStream(File.OpenRead("orders.csv"), "||"));
/// while (reader.Read()) Console.WriteLine(reader.GetString("Name"));
/// </code>
/// </example>
public sealed partial class CsvSeparatorStream : Stream
{
	/// <summary>The default <see cref="Replacement"/>: U+001F, the ASCII unit separator, which text does not contain.</summary>
	public const char DefaultReplacement = '\u001F';

	// The longest separator, in bytes of the input encoding.
	private const int MaxSeparatorBytes = 16;
	// Reads into smaller buffers go through an internal buffer of SmallBufferSize bytes.
	private const int MinDirectRead = 256;
	private const int SmallBufferSize = 4096;

	private readonly Stream stream;
	private readonly bool leaveOpen;
	private readonly byte replacement;

	// The dialect, set by Configure: the defaults of CsvReaderOptions until a reader attaches.
	private byte[] separator = [];
	private int quote;                  // -1 without quotes (Escape and NoEscape modes)
	private int escape;                 // -1 outside Escape mode
	private int comment;                // -1 without comments
	private bool trim;
	private bool[]? blankBytes;         // with trimming, WhitespaceCharacters by byte value when they are not space and tab
	private bool lineBreaksInQuotes;
	private bool utf8;
	private ulong overlaps;             // bit d set when two separators d bytes apart can overlap, as in ||| for ||
	private ulong dropMultiplier;       // bits 1 to length - 1: a mask of separator starts times this marks their other bytes

	private bool started;
	private bool eof;
	private bool disposed;
	// Input bytes before the data of the current read, for the byte offsets of errors.
	private long consumed;
	// The input after the last byte a read decided: up to a separator's length - 1 bytes that may start one.
	private readonly byte[] pending = new byte[MaxSeparatorBytes];
	private int pendingCount;
	// Output for reads into small buffers.
	private byte[]? small;
	private int smallStart;
	private int smallEnd;

	/// <summary>Creates a stream that reads <paramref name="stream"/> with <paramref name="replacement"/> in place of <paramref name="separator"/>.</summary>
	/// <param name="stream">The input, read from its current position. It does not need to be seekable.</param>
	/// <param name="separator">
	/// The separator of the input, such as <c>||</c>: 1 to 16 bytes in the input encoding, without line breaks, the quote,
	/// escape or comment character, or <paramref name="replacement"/>.
	/// </param>
	/// <param name="replacement">
	/// The ASCII character that takes the place of each separator, and that the reader splits on. The input must not
	/// contain it outside quoted fields.
	/// </param>
	/// <param name="leaveOpen">Whether <paramref name="stream"/> stays open when this stream is disposed.</param>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="separator"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="stream"/> cannot be read, <paramref name="separator"/> is empty, too long or contains a character it
	/// cannot contain, or <paramref name="replacement"/> is not ASCII or is a line break.
	/// </exception>
	public CsvSeparatorStream(Stream stream, string separator, char replacement = DefaultReplacement, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(separator);
		if (!stream.CanRead) throw new ArgumentException("The stream must be readable.", nameof(stream));
		if (separator.Length == 0) throw new ArgumentException("The separator cannot be empty.", nameof(separator));
		CsvReaderOptions.ValidateAscii(replacement, nameof(replacement));
		this.stream = stream;
		this.leaveOpen = leaveOpen;
		this.replacement = (byte)replacement;
		Separator = separator;
		Replacement = replacement;
		Configure(CsvReaderOptions.Default);
	}

	/// <summary>The separator of the input that this stream replaces.</summary>
	public string Separator { get; }

	/// <summary>The character that takes the place of each <see cref="Separator"/>, and that the reader splits on.</summary>
	public char Replacement { get; }

	/// <summary>Whether the stream can be read: <see langword="true"/> until it is disposed.</summary>
	public override bool CanRead => !disposed;

	/// <summary>Always <see langword="false"/>: the stream reads its input once, in order.</summary>
	public override bool CanSeek => false;

	/// <summary>Always <see langword="false"/>: the stream is read-only.</summary>
	public override bool CanWrite => false;

	/// <summary>Not supported: the stream is not seekable.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Length => throw new NotSupportedException();

	/// <summary>Not supported: the stream is not seekable.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <summary>Not supported: the stream is not seekable.</summary>
	/// <param name="offset">Not used.</param>
	/// <param name="origin">Not used.</param>
	/// <returns>Nothing: the method always throws.</returns>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <summary>Not supported: the stream is read-only.</summary>
	/// <param name="value">Not used.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <summary>Not supported: the stream is read-only.</summary>
	/// <param name="buffer">Not used.</param>
	/// <param name="offset">Not used.</param>
	/// <param name="count">Not used.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <summary>Does nothing: the stream is read-only.</summary>
	public override void Flush()
	{
	}

	/// <summary>Reads input, with <see cref="Replacement"/> in place of each separator.</summary>
	/// <param name="buffer">The buffer to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read into.</param>
	/// <param name="count">The most bytes to read.</param>
	/// <returns>The number of bytes read: at least 1, or 0 at the end of the input (or when <paramref name="count"/> is 0).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> and <paramref name="count"/> are not a range of <paramref name="buffer"/>.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <exception cref="CsvFormatException">The input contains <see cref="Replacement"/> outside quoted fields.</exception>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	public override int Read(byte[] buffer, int offset, int count)
	{
		CheckArguments(buffer, offset, count);
#if NET
		return Read(buffer.AsSpan(offset, count));
#else
		ObjectDisposedException.ThrowIf(disposed, this);
		if (count == 0) return 0;
		if (smallEnd > smallStart || count < MinDirectRead) return ReadSmall(buffer.AsSpan(offset, count));
		var length = Begin(buffer.AsSpan(offset, count));
		while (NeedsMore(length)) length = Received(length, stream.Read(buffer, offset + length, count - length));

		return Finish(buffer.AsSpan(offset, count), length);
#endif
	}

#if NET
	/// <summary>Reads input, with <see cref="Replacement"/> in place of each separator.</summary>
	/// <param name="buffer">The buffer to read into.</param>
	/// <returns>The number of bytes read: at least 1, or 0 at the end of the input (or when <paramref name="buffer"/> is empty).</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <exception cref="CsvFormatException">The input contains <see cref="Replacement"/> outside quoted fields.</exception>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	public override int Read(Span<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (buffer.IsEmpty) return 0;
		if (smallEnd > smallStart || buffer.Length < MinDirectRead) return ReadSmall(buffer);
		var length = Begin(buffer);
		while (NeedsMore(length)) length = Received(length, stream.Read(buffer.Slice(length)));

		return Finish(buffer, length);
	}

	/// <summary>Reads input asynchronously, with <see cref="Replacement"/> in place of each separator.</summary>
	/// <param name="buffer">The buffer to read into.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying stream.</param>
	/// <returns>The number of bytes read: at least 1, or 0 at the end of the input (or when <paramref name="buffer"/> is empty).</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <exception cref="CsvFormatException">The input contains <see cref="Replacement"/> outside quoted fields.</exception>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (buffer.IsEmpty) return new ValueTask<int>(0);
		if (smallEnd > smallStart) return new ValueTask<int>(TakeSmall(buffer.Span));

		return buffer.Length < MinDirectRead ? ReadSmallAsync(buffer, cancellationToken) : ReadDirectAsync(buffer, cancellationToken);
	}

	/// <summary>Reads input asynchronously, with <see cref="Replacement"/> in place of each separator.</summary>
	/// <param name="buffer">The buffer to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read into.</param>
	/// <param name="count">The most bytes to read.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying stream.</param>
	/// <returns>The number of bytes read: at least 1, or 0 at the end of the input (or when <paramref name="count"/> is 0).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> and <paramref name="count"/> are not a range of <paramref name="buffer"/>.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <exception cref="CsvFormatException">The input contains <see cref="Replacement"/> outside quoted fields.</exception>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		CheckArguments(buffer, offset, count);

		return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	}

	/// <summary>An asynchronous read into the caller's buffer, which the separators are replaced in.</summary>
	/// <param name="buffer">The buffer of the read.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying stream.</param>
	/// <returns>The number of bytes read.</returns>
	private async ValueTask<int> ReadDirectAsync(Memory<byte> buffer, CancellationToken cancellationToken)
	{
		var length = Begin(buffer.Span);
		while (NeedsMore(length)) length = Received(length, await stream.ReadAsync(buffer.Slice(length), cancellationToken).ConfigureAwait(false));

		return Finish(buffer.Span, length);
	}

	/// <summary>An asynchronous read into a small buffer, through the internal buffer.</summary>
	/// <param name="buffer">The buffer of the read.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying stream.</param>
	/// <returns>The number of bytes read.</returns>
	private async ValueTask<int> ReadSmallAsync(Memory<byte> buffer, CancellationToken cancellationToken)
	{
		var data = small ??= new byte[SmallBufferSize];
		var length = Begin(data);
		while (NeedsMore(length)) length = Received(length, await stream.ReadAsync(data.AsMemory(length), cancellationToken).ConfigureAwait(false));
		smallStart = 0;
		smallEnd = Finish(data, length);

		return TakeSmall(buffer.Span);
	}

	/// <summary>Disposes the stream and, unless it was created to leave it open, the underlying stream.</summary>
	/// <returns>A task that completes when the underlying stream is disposed.</returns>
	public override async ValueTask DisposeAsync()
	{
		if (!disposed && !leaveOpen) await stream.DisposeAsync().ConfigureAwait(false);
		disposed = true;
		await base.DisposeAsync().ConfigureAwait(false);
	}
#else
	/// <summary>Reads input asynchronously, with <see cref="Replacement"/> in place of each separator.</summary>
	/// <param name="buffer">The buffer to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read into.</param>
	/// <param name="count">The most bytes to read.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying stream.</param>
	/// <returns>The number of bytes read: at least 1, or 0 at the end of the input (or when <paramref name="count"/> is 0).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> and <paramref name="count"/> are not a range of <paramref name="buffer"/>.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <exception cref="CsvFormatException">The input contains <see cref="Replacement"/> outside quoted fields.</exception>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		CheckArguments(buffer, offset, count);
		ObjectDisposedException.ThrowIf(disposed, this);
		if (count == 0) return 0;
		if (smallEnd == smallStart && count < MinDirectRead)
		{
			var data = small ??= new byte[SmallBufferSize];
			var filled = Begin(data);
			while (NeedsMore(filled)) filled = Received(filled, await stream.ReadAsync(data, filled, data.Length - filled, cancellationToken).ConfigureAwait(false));
			smallStart = 0;
			smallEnd = Finish(data, filled);
		}

		if (smallEnd > smallStart || count < MinDirectRead) return TakeSmall(buffer.AsSpan(offset, count));
		var length = Begin(buffer.AsSpan(offset, count));
		while (NeedsMore(length)) length = Received(length, await stream.ReadAsync(buffer, offset + length, count - length, cancellationToken).ConfigureAwait(false));

		return Finish(buffer.AsSpan(offset, count), length);
	}
#endif

	/// <summary>Disposes the stream and, unless it was created to leave it open, the underlying stream.</summary>
	/// <param name="disposing">Whether the call comes from <see cref="Stream.Dispose()"/> rather than a finalizer.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing && !disposed && !leaveOpen) stream.Dispose();
		disposed = true;
		base.Dispose(disposing);
	}

	/// <summary>
	/// Called by <see cref="CsvReader.Create(Stream, CsvReaderOptions?, bool)"/>: takes the reader's quoting, escape and
	/// comment rules, and returns its options with <see cref="Replacement"/> as the separator.
	/// </summary>
	/// <param name="options">The reader's options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <returns>The options for the reader.</returns>
	/// <exception cref="ArgumentException">
	/// The options set another separator or FixedWidth mode, or the separator cannot be used with their characters.
	/// </exception>
	/// <exception cref="NotSupportedException">The options set an encoding that is neither UTF-8 nor single-byte.</exception>
	/// <exception cref="InvalidOperationException">The stream has already been read from.</exception>
	internal CsvReaderOptions Attach(CsvReaderOptions? options)
	{
		options ??= CsvReaderOptions.Default;
		if (started) throw new InvalidOperationException("The CsvSeparatorStream has already been read from; create the reader before reading it.");
		if (options.Mode == CsvMode.FixedWidth) throw new ArgumentException("FixedWidth mode has no separator to replace.", nameof(options));
		if (options.Separator is char sep && sep != Replacement)
		{
			throw new ArgumentException(
				$"A CsvSeparatorStream puts U+{(int)Replacement:X4} in place of its separator: leave Separator unset, or set it to that character.",
				nameof(options));
		}

		Configure(options);

		return options.Separator is null ? options with { Separator = Replacement } : options;
	}

	/// <summary>Takes the dialect of <paramref name="options"/>, after checking that the separator can be used with it.</summary>
	/// <param name="options">The reader's options.</param>
	/// <exception cref="ArgumentException">The separator is too long, or contains a character it cannot contain.</exception>
	/// <exception cref="NotSupportedException">The options set an encoding that is neither UTF-8 nor single-byte.</exception>
	private void Configure(CsvReaderOptions options)
	{
		const string SeparatorName = "separator";
		var encoding = options.Encoding;
		var isUtf8 = encoding is null || encoding.CodePage == Encoding.UTF8.CodePage;
		if (!isUtf8 && !IsSingleByteAscii(encoding!))
			throw new NotSupportedException($"A CsvSeparatorStream reads UTF-8 and single-byte encodings, not {encoding!.WebName}.");
		var bytes = (encoding ?? Encoding.UTF8).GetBytes(Separator);
		if (bytes.Length > MaxSeparatorBytes)
			throw new ArgumentException($"The separator can be at most {MaxSeparatorBytes} bytes long.", SeparatorName);
		var quoteChar = options.Mode == CsvMode.Rfc4180 ? options.Quote : -1;
		var escapeChar = options.Mode == CsvMode.Escape ? options.Escape : -1;
		var commentChar = options.CommentPrefix is char c ? c : -1;
		foreach (var b in bytes)
		{
			if (b == '\r' || b == '\n' || b == quoteChar || b == escapeChar || b == commentChar || b == replacement)
			{
				throw new ArgumentException(
					"The separator cannot contain a line break, the quote, escape or comment character, or the replacement.", SeparatorName);
			}
		}

		utf8 = isUtf8;
		quote = quoteChar;
		escape = escapeChar;
		comment = commentChar;
		trim = options.TrimWhitespace ?? false;
		blankBytes = null;
		if (trim && options.WhitespaceCharacters is not (" \t" or "\t "))
		{
			blankBytes = new bool[256];
			foreach (var w in options.WhitespaceCharacters)
			{
				blankBytes[w] = true;
			}
		}

		lineBreaksInQuotes = options.AllowLineBreaksInQuotes;
		separator = bytes;
		dropMultiplier = (1UL << bytes.Length) - 2;
		overlaps = 0;
		for (var d = 1; d < bytes.Length; d++)
		{
			if (bytes.AsSpan(d).SequenceEqual(bytes.AsSpan(0, bytes.Length - d))) overlaps |= 1UL << d;
		}
	}

	/// <summary>Whether <paramref name="encoding"/> has one byte per character and encodes ASCII as ASCII.</summary>
	/// <param name="encoding">The encoding.</param>
	/// <returns><see langword="true"/> for encodings such as Latin-1 and Windows-1252.</returns>
	private static bool IsSingleByteAscii(Encoding encoding)
	{
		if (!encoding.IsSingleByte) return false;
		var ascii = new char[128];
		for (var i = 0; i < ascii.Length; i++) ascii[i] = (char)i;
		var bytes = encoding.GetBytes(ascii);
		if (bytes.Length != ascii.Length) return false;
		for (var i = 0; i < bytes.Length; i++)
		{
			if (bytes[i] != i) return false;
		}

		return true;
	}

	/// <summary>Checks the arguments of the array overloads of <see cref="Read(byte[], int, int)"/> and <c>ReadAsync</c>.</summary>
	/// <param name="buffer">The buffer.</param>
	/// <param name="offset">The position in <paramref name="buffer"/>.</param>
	/// <param name="count">The number of bytes from <paramref name="offset"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> and <paramref name="count"/> are not a range of <paramref name="buffer"/>.</exception>
	private static void CheckArguments(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
		if (count < 0 || count > buffer.Length - offset) throw new ArgumentOutOfRangeException(nameof(count));
	}

	/// <summary>A read into a small buffer: through the internal buffer, which holds the output that does not fit.</summary>
	/// <param name="destination">The buffer of the read.</param>
	/// <returns>The number of bytes read.</returns>
	private int ReadSmall(Span<byte> destination)
	{
		if (smallEnd == smallStart)
		{
			var data = small ??= new byte[SmallBufferSize];
			var length = Begin(data);
			while (NeedsMore(length)) length = Received(length, stream.Read(data, length, data.Length - length));
			smallStart = 0;
			smallEnd = Finish(data, length);
		}

		return TakeSmall(destination);
	}

	/// <summary>Copies output from the internal buffer.</summary>
	/// <param name="destination">The buffer of the read.</param>
	/// <returns>The number of bytes copied: 0 when the internal buffer is empty.</returns>
	private int TakeSmall(Span<byte> destination)
	{
		var n = Math.Min(destination.Length, smallEnd - smallStart);
		small.AsSpan(smallStart, n).CopyTo(destination);
		smallStart += n;

		return n;
	}

	/// <summary>Puts the pending input at the start of <paramref name="buffer"/>.</summary>
	/// <param name="buffer">The buffer of the read.</param>
	/// <returns>The number of bytes put there.</returns>
	private int Begin(Span<byte> buffer)
	{
		var n = pendingCount;
		pending.AsSpan(0, n).CopyTo(buffer);
		pendingCount = 0;

		return n;
	}

	/// <summary>
	/// Whether a read needs more input before it can return anything: a separator's length (and 4 bytes for the byte order
	/// mark at the start), unless the input has ended.
	/// </summary>
	/// <param name="length">The number of bytes of data the read has.</param>
	/// <returns><see langword="true"/> when the read must read more from the underlying stream.</returns>
	private bool NeedsMore(int length) => !eof && length < (started ? separator.Length : Math.Max(separator.Length, 4));

	/// <summary>Counts the bytes of a read from the underlying stream, and notes the end of the input.</summary>
	/// <param name="length">The number of bytes of data before the read.</param>
	/// <param name="read">The result of the read.</param>
	/// <returns>The number of bytes of data after the read.</returns>
	private int Received(int length, int read)
	{
		if (read > 0) return length + read;
		eof = true;

		return length;
	}

	/// <summary>
	/// Replaces the separators in the first <paramref name="length"/> bytes of <paramref name="buffer"/>, and keeps the bytes
	/// that may start a separator for the next read.
	/// </summary>
	/// <param name="buffer">The buffer of the read.</param>
	/// <param name="length">The number of bytes of data in it.</param>
	/// <returns>The number of bytes of output, from the start of <paramref name="buffer"/>.</returns>
	private int Finish(Span<byte> buffer, int length)
	{
		if (!started) Start(buffer.Slice(0, length));
		if (length == 0) return 0;
		// Without the rest of the input, the last length - 1 bytes may start a separator.
		var processEnd = eof ? length : length - (separator.Length - 1);
		var written = Replace(buffer, length, processEnd);
		// A separator that starts before processEnd is taken whole.
		var next = Math.Max(processEnd, skipUntil);
		pendingCount = length - next;
		buffer.Slice(next, pendingCount).CopyTo(pending);
		Rebase(next);

		return written;
	}

	/// <summary>Checks the byte order mark at the start of the input.</summary>
	/// <param name="data">The start of the input.</param>
	/// <exception cref="NotSupportedException">The input starts with a UTF-16 or UTF-32 byte order mark.</exception>
	private void Start(ReadOnlySpan<byte> data)
	{
		started = true;
		// FF FE is also the start of the UTF-32 little-endian mark.
		var utf16 = data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF));
		var utf32 = data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF;
		if (utf16 || utf32)
		{
			throw new NotSupportedException(
				"The input is UTF-16 or UTF-32 (it starts with a byte order mark). A CsvSeparatorStream reads UTF-8 and single-byte encodings.");
		}

		// The reader skips a UTF-8 byte order mark, so the first record starts after it.
		if (utf8 && data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) fieldStart = rowStart = 3;
	}
}
