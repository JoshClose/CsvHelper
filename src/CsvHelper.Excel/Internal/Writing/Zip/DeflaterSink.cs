using System;
using System.IO;
#if NET
using System.Buffers.Binary;
#endif

namespace CsvHelper.Excel;

/// <summary>
/// The stream a deflated entry's deflater writes into: its output goes into the entry's buffers, in memory, never into
/// the package's stream. On .NET 8 and later the deflater is a <see cref="System.IO.Compression.GZipStream"/>, whose
/// 10-byte header the sink checks and drops, and whose 8-byte trailer, the CRC and the size, the entry takes off the end
/// (<c>TakeTrailer</c>), so that the data is the raw deflate stream. Elsewhere it is a
/// <see cref="System.IO.Compression.DeflateStream"/>, whose output is the data.
/// </summary>
/// <param name="data">The entry's buffers.</param>
internal sealed class DeflaterSink(ZipBuffers data) : Stream
{
#if NET
	/// <summary>The length of the GZip header: <c>1F 8B 08 00</c>, then MTIME, XFL and OS.</summary>
	private const int HeaderLength = 10;

	/// <summary>The header bytes seen so far.</summary>
	private int headerSeen;
#endif

	/// <summary>Whether what is written is dropped: the entry is released, and its deflater disposed into nothing.</summary>
	private bool discarding;

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => true;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

#if NET
	/// <summary>
	/// Checks the start of a GZip header: <c>1F 8B</c>, deflate (<c>08</c>), and no flags (<c>00</c>), so that it is 10
	/// bytes, whatever its MTIME, XFL and OS.
	/// </summary>
	/// <param name="bytes">Header bytes, from the header's position <paramref name="start"/>.</param>
	/// <param name="start">Their position in the header.</param>
	/// <exception cref="InvalidOperationException">The header doesn't start <c>1F 8B 08 00</c>.</exception>
	public static void CheckHeader(ReadOnlySpan<byte> bytes, int start)
	{
		for (var i = 0; i < bytes.Length && start + i < 4; i++)
		{
			var expected = (start + i) switch
			{
				0 => 0x1F,
				1 => 0x8B,
				2 => 0x08,
				_ => 0x00,
			};
			if (bytes[i] != expected)
			{
				throw new InvalidOperationException(
					$"The deflater's GZip header doesn't start 1F 8B 08 00 (byte {start + i} is 0x{bytes[i]:X2}), so the zip writer can't take its deflate stream and CRC.");
			}
		}
	}

	/// <summary>
	/// Takes the GZip trailer off the end of the data, once the deflater is disposed: the CRC-32 and the size mod 2^32.
	/// </summary>
	/// <returns>The CRC and the size.</returns>
	/// <exception cref="InvalidOperationException">The deflater wrote no header, or no trailer.</exception>
	public (uint Crc, uint Size) TakeTrailer()
	{
		if (headerSeen < HeaderLength || data.Length < 8)
		{
			throw new InvalidOperationException("The deflater's GZip stream ended without its header or trailer.");
		}

		Span<byte> trailer = stackalloc byte[8];
		data.RemoveLast(trailer);

		return (BinaryPrimitives.ReadUInt32LittleEndian(trailer), BinaryPrimitives.ReadUInt32LittleEndian(trailer.Slice(4)));
	}
#endif

	/// <summary>Drops whatever is written from now on.</summary>
	public void Discard() => discarding = true;

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => Take(new ReadOnlySpan<byte>(buffer, offset, count));

#if NET || NETSTANDARD2_1_OR_GREATER
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer) => Take(buffer);
#endif

	/// <summary>Appends the deflater's output to the entry's buffers, the GZip header checked and dropped first.</summary>
	/// <param name="bytes">The output.</param>
	/// <exception cref="InvalidOperationException">The GZip header doesn't start <c>1F 8B 08 00</c>.</exception>
	private void Take(ReadOnlySpan<byte> bytes)
	{
		if (discarding)
		{
			return;
		}

#if NET
		if (headerSeen < HeaderLength)
		{
			var n = Math.Min(HeaderLength - headerSeen, bytes.Length);
			CheckHeader(bytes.Slice(0, n), headerSeen);
			headerSeen += n;
			bytes = bytes.Slice(n);
		}
#endif

		data.Append(bytes);
	}
}
