using System;
using System.Collections.Generic;
using System.Threading;

namespace CsvHelper.Excel;

/// <summary>
/// A segment of a chunked entry's data that a deflate stage holds (<see cref="ThreadedDeflateStage"/>), counted by
/// the pieces that point into it: a segment across a cut belongs to every chunk it gives bytes to, and the last to let it go
/// returns it to its writer (<see cref="ZipWriter.Return(OwnedSegment)"/>), on whatever thread that is. The writing thread
/// holds a reference of its own while it appends from the segment, so that a chunk deflated meanwhile can't return it early.
/// </summary>
internal sealed class HeldSegment
{
	/// <summary>The pieces that point into the segment, and the writing thread's hold while it appends from it.</summary>
	private int references = 1;

	/// <summary>Holds a segment: the writing thread's reference, which <see cref="Release"/> lets go once it has appended from it.</summary>
	/// <param name="segment">The segment, counted by the writer (<see cref="ZipWriter.Hold"/>).</param>
	public HeldSegment(OwnedSegment segment) => Segment = segment;

	/// <summary>The segment.</summary>
	public OwnedSegment Segment { get; }

	/// <summary>Counts a piece that points into the segment.</summary>
	public void AddReference() => Interlocked.Increment(ref references);

	/// <summary>Lets a reference go: the last returns the segment to the writer, once.</summary>
	/// <param name="writer">The writer whose segment it is.</param>
	public void Release(ZipWriter writer)
	{
		if (Interlocked.Decrement(ref references) == 0)
		{
			writer.Return(Segment);
		}
	}
}

/// <summary>Bytes of a held segment that a deflate job takes: from an offset in the segment's array, in order with its other pieces.</summary>
internal readonly struct JobPiece
{
	/// <summary>Creates a piece.</summary>
	/// <param name="segment">The segment.</param>
	/// <param name="offset">The first byte, in the segment's array.</param>
	/// <param name="length">How many bytes: 1 or more, within the segment.</param>
	public JobPiece(HeldSegment segment, int offset, int length)
	{
		Segment = segment;
		Offset = offset;
		Length = length;
	}

	/// <summary>The segment the bytes are in.</summary>
	public HeldSegment Segment { get; }

	/// <summary>The first byte, in the segment's array.</summary>
	public int Offset { get; }

	/// <summary>How many bytes.</summary>
	public int Length { get; }

	/// <summary>The bytes.</summary>
	public ReadOnlySpan<byte> Span => Segment.Segment.Array!.AsSpan(Offset, Length);
}

/// <summary>
/// A chunk of a chunked entry's data on its way through a deflate stage (<see cref="ThreadedDeflateStage"/>): the
/// pieces of held segments it takes, in order, until a stage thread gathers and releases them; then its deflated chunk,
/// once the thread publishes it. The writing thread fills it and hands it over; one stage thread at a time owns its pieces
/// after that; its <see cref="Chunk"/> and <see cref="Done"/> are set and read under the stage's lock of finished chunks.
/// Reused, once its chunk is taken.
/// </summary>
internal sealed class DeflateJob
{
	/// <summary>The pieces, in the data's order.</summary>
	private readonly List<JobPiece> pieces = [];

	/// <summary>The chunk's place in its entry, from 0.</summary>
	public long Sequence { get; set; }

	/// <summary>The bytes of the pieces.</summary>
	public int Length { get; private set; }

	/// <summary>Whether the chunk is the entry's last, which ends the deflate stream (<see cref="DeflateStage.Complete"/>).</summary>
	public bool Last { get; set; }

	/// <summary>The deflated chunk, once <see cref="Done"/>.</summary>
	public DeflatedChunk? Chunk { get; set; }

	/// <summary>Whether the chunk is deflated, and <see cref="Chunk"/> set.</summary>
	public bool Done { get; set; }

	/// <summary>The pieces held: 0 once they are released.</summary>
	public int Count => pieces.Count;

	/// <summary>A piece.</summary>
	/// <param name="index">Its place, from 0.</param>
	/// <returns>The piece.</returns>
	public JobPiece this[int index] => pieces[index];

	/// <summary>
	/// Adds bytes of a segment after the pieces before: they extend the last piece when they follow it in the same segment,
	/// and are a new piece, which counts a reference to the segment, otherwise.
	/// </summary>
	/// <param name="segment">The segment.</param>
	/// <param name="offset">The first byte, in the segment's array.</param>
	/// <param name="length">How many bytes: 1 or more.</param>
	public void Add(HeldSegment segment, int offset, int length)
	{
		var last = pieces.Count - 1;
		if (last >= 0 && pieces[last].Segment == segment && pieces[last].Offset + pieces[last].Length == offset)
		{
			pieces[last] = new JobPiece(segment, pieces[last].Offset, pieces[last].Length + length);
		}
		else
		{
			segment.AddReference();
			pieces.Add(new JobPiece(segment, offset, length));
		}

		Length += length;
	}

	/// <summary>Releases the pieces' references to their segments, once: calling it again does nothing.</summary>
	/// <param name="writer">The writer whose segments they are.</param>
	public void Release(ZipWriter writer)
	{
		foreach (var piece in pieces)
		{
			piece.Segment.Release(writer);
		}

		pieces.Clear();
	}

	/// <summary>Empties the job for reuse; its pieces are released already.</summary>
	public void Reset()
	{
		pieces.Clear();
		Sequence = 0;
		Length = 0;
		Last = false;
		Chunk = null;
		Done = false;
	}
}
