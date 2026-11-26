using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The bytes of one XML part that <see cref="XmlScanner"/> reads tokens from: a window that slides over the part's stream.
/// Each refill first drops the bytes before the token in progress (<see cref="Compact"/>), and then reads more after the
/// rest with one read of the stream (<see cref="Read"/>, <see cref="ReadAsync"/>). A token that fills the window doubles
/// it, up to the part's size limit.
/// </summary>
/// <remarks>
/// <para>
/// The window reads UTF-8. A UTF-8 byte order mark is dropped, and a part that starts with a UTF-16 byte order mark (either
/// byte order) is read through <see cref="Utf16Stream"/>, as UTF-8. The encoding of the XML declaration is otherwise
/// ignored. To tell, the first reads take only the bytes a byte order mark can have, so nothing past a UTF-16 one is read
/// before the stream is wrapped.
/// </para>
/// <para>
/// Byte offsets (<see cref="Offset"/>, and in errors) count from the start of the part, its UTF-8 byte order mark included.
/// In a UTF-16 part they count the bytes of its UTF-8 text.
/// </para>
/// <para>
/// A window for segments (<see cref="ForSegments"/>) has no stream: a segment's scan puts it over the segment's bytes, at
/// their offset in the part (<see cref="Segment"/>), and it never reads, compacts or grows, so the bytes, which the segment
/// pipeline owns, are never written. Its bytes are never the part's last (<see cref="IsEof"/> is false), so a token they cut
/// short asks for more, as at any refill.
/// </para>
/// </remarks>
internal sealed class XmlWindow : IDisposable, IAsyncDisposable
{
	/// <summary>The size a window starts at: 64 KB.</summary>
	public const int DefaultSize = 64 * 1024;

	/// <summary>The smallest size a window starts at, for tests of tokens split between reads: enough for a byte order mark.</summary>
	public const int MinimumSize = 4;

	private readonly string partName;
	private readonly int maxSize;
	private Stream stream;
	private byte[] buffer;
	// The bytes of the buffer in use: a rented array can be longer.
	private int capacity;
	private int end;
	// The bytes of the part before the start of the buffer.
	private long discarded;
	private bool sniffed;
	private bool eof;
	private bool disposed;
	// Whether the window is over segments' bytes (ForSegments): no stream, and a buffer it doesn't own. Declared last, so
	// that the fields a scan reads keep their places.
	private readonly bool segments;

	/// <summary>Creates a window over the stream of a part.</summary>
	/// <param name="stream">The stream of the part, which the window owns: disposing the window disposes it.</param>
	/// <param name="partName">The name of the part, such as <c>xl/worksheets/sheet1.xml</c>, for errors.</param>
	/// <param name="maxSize">
	/// The most bytes the window grows to (<c>ExcelReaderOptions.MaxPartSize</c>), which bounds the size of a single token.
	/// Limits past <c>Array.MaxLength</c> are that.
	/// </param>
	/// <param name="initialSize">The size the window starts at; smaller than <see cref="DefaultSize"/> only for tests.</param>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="partName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxSize"/> is 0 or less, or <paramref name="initialSize"/> is less than <see cref="MinimumSize"/>.
	/// </exception>
	public XmlWindow(Stream stream, string partName, long maxSize, int initialSize = DefaultSize)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(partName);
		if (maxSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "The size limit must be more than 0.");
		}

		if (initialSize < MinimumSize)
		{
			throw new ArgumentOutOfRangeException(nameof(initialSize), initialSize, $"The window must start at {MinimumSize} bytes or more.");
		}

		this.stream = stream;
		this.partName = partName;
		this.maxSize = (int)Math.Min(maxSize, Array.MaxLength);
		capacity = Math.Max(Math.Min(initialSize, this.maxSize), MinimumSize);
		buffer = ArrayPool<byte>.Shared.Rent(capacity);
	}

	/// <summary>Creates a window for segments, without a stream or bytes until <see cref="Segment"/>.</summary>
	/// <param name="partName">The name of the part, for errors.</param>
	/// <param name="maxSize">The part's size limit, as the part's own window has it.</param>
	private XmlWindow(string partName, int maxSize)
	{
		stream = Stream.Null;
		this.partName = partName;
		this.maxSize = maxSize;
		buffer = [];
		sniffed = true;
		segments = true;
	}

	/// <summary>The name of the part, for errors.</summary>
	public string PartName => partName;

	/// <summary>The bytes of the window: those before <see cref="End"/> are the part's. A refill can replace the array.</summary>
	public byte[] Buffer => buffer;

	/// <summary>The end of the part's bytes in <see cref="Buffer"/>.</summary>
	public int End => end;

	/// <summary>The number of bytes the window can hold before it has to grow.</summary>
	public int Capacity => capacity;

	/// <summary>The most bytes the window grows to: the part's size limit, or <c>Array.MaxLength</c> when that is less.</summary>
	public int MaxSize => maxSize;

	/// <summary>Whether the stream has ended: the bytes up to <see cref="End"/> are the last of the part.</summary>
	public bool IsEof => eof;

	/// <summary>Whether the window reads its part through <see cref="Utf16Stream"/>: the part's bytes are UTF-16, and the window's their UTF-8 text.</summary>
	public bool Transcodes => stream is Utf16Stream;

	/// <summary>Whether the window is over segments' bytes (<see cref="ForSegments"/>), which it never reads more of.</summary>
	public bool HoldsSegments => segments;

	/// <summary>The offset in the part of a position in <see cref="Buffer"/>.</summary>
	/// <param name="position">The position in <see cref="Buffer"/>.</param>
	/// <returns>The number of bytes of the part before <paramref name="position"/>.</returns>
	public long Offset(int position) => discarded + position;

	/// <summary>
	/// Creates a window for the segments of a part (<see cref="ISegmentScanner"/>): no stream, and no bytes until
	/// <see cref="Segment"/> puts it over a segment's. Disposing it disposes nothing, and returns no buffer.
	/// </summary>
	/// <param name="partName">The name of the part, for errors.</param>
	/// <param name="maxSize">The part's size limit, as the part's own window has it (<see cref="MaxSize"/>).</param>
	/// <returns>The window.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="partName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> is 0 or less.</exception>
	public static XmlWindow ForSegments(string partName, int maxSize)
	{
		ArgumentNullException.ThrowIfNull(partName);
		if (maxSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "The size limit must be more than 0.");
		}

		return new XmlWindow(partName, maxSize);
	}

	/// <summary>
	/// Puts a window for segments over a segment's bytes: <see cref="Buffer"/> is <paramref name="bytes"/>, whose first
	/// <paramref name="length"/> are the window's, and position 0 is <paramref name="offset"/> in the part. The bytes are
	/// read, never written; the window holds them until the next call.
	/// </summary>
	/// <param name="bytes">The segment's bytes, from its start.</param>
	/// <param name="length">The number of the segment's bytes.</param>
	/// <param name="offset">The offset in the part of the segment's first byte.</param>
	/// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is outside <paramref name="bytes"/>, or <paramref name="offset"/> is negative.</exception>
	/// <exception cref="InvalidOperationException">The window is a part's, over its stream.</exception>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	public void Segment(byte[] bytes, int length, long offset)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		ObjectDisposedException.ThrowIf(disposed, this);
		if (!segments)
		{
			throw new InvalidOperationException("Only a window for segments is put over a segment's bytes.");
		}

		if ((uint)length > (uint)bytes.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(length), length, "The length is outside the bytes.");
		}

		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(offset), offset, "The offset can't be negative.");
		}

		buffer = bytes;
		capacity = length;
		end = length;
		discarded = offset;
	}

	/// <summary>
	/// Restarts the window over another stream of the same part, which reads on from <paramref name="offset"/>: the bytes
	/// the window holds are dropped (<see cref="End"/> is 0, and the next byte read is at <paramref name="offset"/>), its
	/// buffer and size are kept, the byte order mark is not looked for again, and the window owns the new stream. The old
	/// stream is not disposed: whoever gave the new one owns it. For a segmented read that falls back to the part's window.
	/// </summary>
	/// <param name="stream">The part's bytes from <paramref name="offset"/> on.</param>
	/// <param name="offset">The offset in the part of the stream's first byte.</param>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
	/// <exception cref="InvalidOperationException">The window is for segments, which has no stream.</exception>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	public void Restart(Stream stream, long offset)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ObjectDisposedException.ThrowIf(disposed, this);
		if (segments)
		{
			throw new InvalidOperationException("A window for segments has no stream to restart.");
		}

		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(offset), offset, "The offset can't be negative.");
		}

		this.stream = stream;
		end = 0;
		discarded = offset;
		sniffed = true;
		eof = false;
	}

	/// <summary>
	/// Makes room to read more: drops the bytes before <paramref name="keepFrom"/> and moves the rest to the start, so a
	/// position <c>p</c> becomes <c>p - keepFrom</c>. When that leaves no room (the token from <paramref name="keepFrom"/>
	/// fills the window), the window doubles.
	/// </summary>
	/// <param name="keepFrom">The position of the first byte still needed: the start of the token in progress.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="keepFrom"/> is not between 0 and <see cref="End"/>.</exception>
	/// <exception cref="InvalidOperationException">The stream has ended.</exception>
	/// <exception cref="InvalidDataException">The token fills a window of the size limit.</exception>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	public void Compact(int keepFrom)
	{
		CheckReadable();
		if ((uint)keepFrom > (uint)end)
		{
			throw new ArgumentOutOfRangeException(nameof(keepFrom), keepFrom, "The position is outside the window.");
		}

		if (keepFrom > 0)
		{
			System.Buffer.BlockCopy(buffer, keepFrom, buffer, 0, end - keepFrom);
			end -= keepFrom;
			discarded += keepFrom;
		}

		if (end == capacity)
		{
			Grow();
		}
	}

	/// <summary>Reads more of the part after <see cref="End"/>, with one read of the stream; at its end, sets <see cref="IsEof"/>.</summary>
	/// <exception cref="InvalidOperationException">The stream has ended, or the window has no room (call <see cref="Compact"/> first).</exception>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	public void Read()
	{
		var count = ReadCount();
		Received(stream.Read(buffer, end, count));
	}

	/// <summary>Asynchronously reads more of the part after <see cref="End"/>. See <see cref="Read"/>.</summary>
	/// <param name="cancellationToken">Cancels the read; the window is then as it was.</param>
	/// <returns>A task that completes when the bytes are read.</returns>
	/// <exception cref="InvalidOperationException">The stream has ended, or the window has no room (call <see cref="Compact"/> first).</exception>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	public async ValueTask ReadAsync(CancellationToken cancellationToken)
	{
		var count = ReadCount();
		Received(await stream.ReadAsync(buffer.AsMemory(end, count), cancellationToken).ConfigureAwait(false));
	}

	/// <summary>Creates the exception for XML that can't be read, with the part's name and the byte offset.</summary>
	/// <param name="position">The position in <see cref="Buffer"/> of what can't be read.</param>
	/// <param name="detail">What is wrong, as a clause, such as "it ends inside a tag".</param>
	/// <returns>The exception, to throw.</returns>
	public InvalidDataException Error(int position, string detail) => XmlError.Create(partName, Offset(position), detail);

	/// <summary>Returns the window's buffer and disposes the stream.</summary>
	public void Dispose()
	{
		if (Release())
		{
			stream.Dispose();
		}
	}

	/// <summary>Returns the window's buffer and asynchronously disposes the stream.</summary>
	/// <returns>A task that completes when the stream is disposed.</returns>
	public ValueTask DisposeAsync() => Release() ? stream.DisposeAsync() : default;

	/// <summary>Marks the window disposed and returns its buffer to the pool.</summary>
	/// <returns>Whether the window was not disposed already.</returns>
	private bool Release()
	{
		if (disposed)
		{
			return false;
		}

		disposed = true;
		var rented = buffer;
		buffer = [];
		end = capacity = 0;
		if (!segments)
		{
			ArrayPool<byte>.Shared.Return(rented);
		}

		return true;
	}

	/// <summary>Checks that the window can read, and says how many bytes the next read may take.</summary>
	/// <returns>The bytes to read: the room after <see cref="End"/>, or, before the byte order mark is checked, only its bytes.</returns>
	private int ReadCount()
	{
		CheckReadable();
		if (!sniffed)
		{
			// Two bytes tell UTF-16 (FF FE, FE FF), and EF BB needs a third for UTF-8 (EF BB BF).
			return (end < 2 ? 2 : 3) - end;
		}

		if (end == capacity)
		{
			throw new InvalidOperationException("The window is full: compact it before reading.");
		}

		return capacity - end;
	}

	/// <summary>Takes the bytes of a read, and, until it is done, checks for a byte order mark.</summary>
	/// <param name="count">The number of bytes read: 0 at the end of the stream.</param>
	private void Received(int count)
	{
		if (count == 0)
		{
			eof = true;
		}
		else
		{
			end += count;
		}

		if (!sniffed)
		{
			Sniff();
		}
	}

	/// <summary>Checks the first bytes for a byte order mark, once there are enough of them to tell.</summary>
	private void Sniff()
	{
		// A byte order mark only counts at the start of the part. No token fits in its bytes, so nothing is dropped before
		// the check is done; this is for safety.
		if (discarded > 0)
		{
			sniffed = true;

			return;
		}

		if (end < 2)
		{
			sniffed = eof;

			return;
		}

		var first = buffer[0];
		var second = buffer[1];
		if ((first == 0xFF && second == 0xFE) || (first == 0xFE && second == 0xFF))
		{
			// Exactly the byte order mark has been read: the rest of the stream is the UTF-16 text.
			stream = new Utf16Stream(stream, first == 0xFE);
			end = 0;
			sniffed = true;

			return;
		}

		if (first == 0xEF && second == 0xBB)
		{
			if (end < 3 && !eof)
			{
				return;
			}

			if (end == 3 && buffer[2] == 0xBF)
			{
				end = 0;
				discarded = 3;
			}
		}

		sniffed = true;
	}

	/// <summary>Doubles the window, up to the size limit, keeping its bytes.</summary>
	/// <exception cref="InvalidDataException">The window is at the size limit.</exception>
	private void Grow()
	{
		if (capacity >= maxSize)
		{
			throw Error(0, $"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({maxSize} bytes)");
		}

		var size = (int)Math.Min((long)capacity * 2, maxSize);
		if (size > buffer.Length)
		{
			var bigger = ArrayPool<byte>.Shared.Rent(size);
			System.Buffer.BlockCopy(buffer, 0, bigger, 0, end);
			ArrayPool<byte>.Shared.Return(buffer);
			buffer = bigger;
		}

		capacity = size;
	}

	/// <summary>Throws when the window is disposed, the stream has ended, or the window is for segments, which never reads.</summary>
	/// <exception cref="ObjectDisposedException">The window is disposed.</exception>
	/// <exception cref="InvalidOperationException">The stream has ended, or the window is for segments.</exception>
	private void CheckReadable()
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (eof)
		{
			throw new InvalidOperationException("The part has no more bytes to read.");
		}

		if (segments)
		{
			throw new InvalidOperationException("A window for segments reads nothing more: its bytes are the segment's.");
		}
	}
}
