namespace CsvHelper.Excel;

/// <summary>
/// A chunk of a chunked entry's data, deflated (<see cref="DeflateStage"/>): its raw deflate bytes, which follow the
/// chunks before it in the entry, and the CRC and length of its input, which the entry combines in order
/// (<see cref="Crc32.Combine"/>).
/// </summary>
internal sealed class DeflatedChunk
{
	/// <summary>Creates an empty chunk, whose output buffers come from a writer's pool.</summary>
	/// <param name="writer">The writer.</param>
	public DeflatedChunk(ZipWriter writer) => Output = new ZipBuffers(writer);

	/// <summary>
	/// The deflate bytes: a sync-flushed, non-final deflate stream for a chunk that isn't the last, and a final one for the
	/// last; every buffer full but the last.
	/// </summary>
	public ZipBuffers Output { get; }

	/// <summary>The CRC-32 of the chunk's input.</summary>
	public uint Crc { get; set; }

	/// <summary>The bytes of input the chunk took: 0 or more.</summary>
	public long InputLength { get; set; }

	/// <summary>
	/// The chunk's place among its stage's chunks, from 0, in the order they are taken: the entry refuses one out of that
	/// order. A switched entry's serial bytes before its stage (<see cref="ZipEntryStream.UseChunks"/>) aren't one of them.
	/// </summary>
	public long Sequence { get; set; }

	/// <summary>Empties the chunk for reuse: its output buffers go back to the pool.</summary>
	public void Clear()
	{
		Output.Clear();
		Crc = 0;
		InputLength = 0;
		Sequence = 0;
	}
}
