namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// A seekable, read-only stream over bytes that records each range it delivers, so that a test can tell how far into an
/// entry's compressed data the reader went before it stopped.
/// </summary>
/// <param name="bytes">The bytes.</param>
internal sealed class ReadTrackingStream(byte[] bytes) : MemoryStream(bytes, false)
{
	/// <summary>The ranges delivered, as start and end offsets.</summary>
	private readonly List<(long Start, long End)> reads = [];

	/// <summary>
	/// Forgets the ranges delivered so far, such as the blocks <c>ZipArchive</c> reads backwards from the end, looking for
	/// the central directory, when it opens: those reach into the last entries' data.
	/// </summary>
	public void Forget() => reads.Clear();

	/// <summary>How many bytes of a range were delivered, counting from its start to the furthest byte read in it.</summary>
	/// <param name="start">The range's offset.</param>
	/// <param name="length">The range's length.</param>
	/// <returns>The bytes up to the furthest one read, or 0 when none was.</returns>
	public long ReadInto(long start, long length)
	{
		var end = start + length;

		return reads.Where(r => r.End > start && r.Start < end).Select(r => Math.Min(r.End, end) - start).DefaultIfEmpty(0).Max();
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => Record(Position, base.Read(buffer, offset, count));

	/// <inheritdoc/>
	public override int ReadByte()
	{
		var start = Position;
		var value = base.ReadByte();
		Record(start, value < 0 ? 0 : 1);

		return value;
	}

	/// <inheritdoc/>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		Task.FromResult(Read(buffer, offset, count));

#if NET
	/// <inheritdoc/>
	public override int Read(Span<byte> buffer) => Record(Position, base.Read(buffer));

	/// <inheritdoc/>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
		new(Read(buffer.Span));
#endif

	/// <summary>Records a read.</summary>
	/// <param name="start">Where it started.</param>
	/// <param name="count">How many bytes it delivered.</param>
	/// <returns><paramref name="count"/>.</returns>
	private int Record(long start, int count)
	{
		if (count > 0)
		{
			reads.Add((start, start + count));
		}

		return count;
	}
}
