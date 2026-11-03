using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Reads a UTF-16 stream as UTF-8, for an XML part that starts with a UTF-16 byte order mark: Office Open XML allows
/// UTF-16 parts, and the XML layer reads only UTF-8. A code unit or surrogate pair split between reads of the inner
/// stream is kept until it is whole. A surrogate that is not part of a pair becomes U+FFFD.
/// </summary>
internal sealed class Utf16Stream : Stream
{
	private readonly Stream inner;
	private readonly Decoder decoder;
	private readonly Encoder encoder = new UTF8Encoding(false).GetEncoder();
	private readonly byte[] input = new byte[4096];
	private readonly char[] chars;
	private readonly byte[] output;
	private int outputStart;
	private int outputEnd;
	private bool innerEnded;

	/// <summary>Creates the stream over the UTF-16 text after the byte order mark.</summary>
	/// <param name="inner">The UTF-16 stream, which this stream owns: disposing this stream disposes it.</param>
	/// <param name="bigEndian">Whether the text is big endian (the byte order mark was <c>FE FF</c>), rather than little endian (<c>FF FE</c>).</param>
	public Utf16Stream(Stream inner, bool bigEndian)
	{
		this.inner = inner;
		decoder = (bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode).GetDecoder();
		// One char per two bytes, plus the half kept from the last read; three UTF-8 bytes per char, plus a kept surrogate.
		chars = new char[input.Length / 2 + 2];
		output = new byte[chars.Length * 3 + 4];
	}

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		while (outputStart == outputEnd && !innerEnded)
		{
			Transcode(inner.Read(input, 0, input.Length));
		}

		return Take(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc/>
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		while (outputStart == outputEnd && !innerEnded)
		{
			Transcode(await inner.ReadAsync(input, 0, input.Length, cancellationToken).ConfigureAwait(false));
		}

		return Take(buffer.AsSpan(offset, count));
	}

#if NET || NETSTANDARD2_1
	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		while (outputStart == outputEnd && !innerEnded)
		{
			Transcode(await inner.ReadAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false));
		}

		return Take(buffer.Span);
	}
#endif

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			inner.Dispose();
		}

		base.Dispose(disposing);
	}

	/// <summary>Turns the bytes of one read of the inner stream into UTF-8 output.</summary>
	/// <param name="count">The number of bytes read into <see cref="input"/>: 0 at the end of the inner stream, which flushes what is kept.</param>
	private void Transcode(int count)
	{
		var flush = count == 0;
		innerEnded = flush;
		var charCount = decoder.GetChars(input, 0, count, chars, 0, flush);
		outputStart = 0;
		outputEnd = encoder.GetBytes(chars, 0, charCount, output, 0, flush);
	}

	/// <summary>Copies output into <paramref name="destination"/>.</summary>
	/// <param name="destination">Where the UTF-8 goes.</param>
	/// <returns>The number of bytes copied: 0 only at the end.</returns>
	private int Take(Span<byte> destination)
	{
		var count = Math.Min(destination.Length, outputEnd - outputStart);
		output.AsSpan(outputStart, count).CopyTo(destination);
		outputStart += count;

		return count;
	}
}
