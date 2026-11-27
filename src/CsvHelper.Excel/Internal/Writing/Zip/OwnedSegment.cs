using System;
using System.Buffers;

namespace CsvHelper.Excel;

/// <summary>
/// Bytes at the start of an array from <see cref="ArrayPool{T}.Shared"/>, handed from one owner to the next without
/// being copied: a part's XML from the buffer it was written into (<see cref="XmlOutput.Detach"/>) to its zip entry
/// (<see cref="ZipEntryStream.AppendAsync"/>), which returns the array once its bytes are deflated or written. One owner
/// holds a segment at a time, and the last one returns it, once (<see cref="Return"/>): a copy of the struct is the
/// same segment, not another.
/// </summary>
internal readonly struct OwnedSegment
{
	/// <summary>Creates a segment over a pooled array.</summary>
	/// <param name="array">The array, from <see cref="ArrayPool{T}.Shared"/>.</param>
	/// <param name="length">The bytes, from the array's start: 1 or more, at most its length.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is 0 or less, or past the array's end.</exception>
	public OwnedSegment(byte[] array, int length)
	{
		ArgumentNullException.ThrowIfNull(array);
		if (length <= 0 || length > array.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(length), length, "A segment has 1 byte or more, within its array.");
		}

		Array = array;
		Length = length;
	}

	/// <summary>The pooled array, or <see langword="null"/> for the empty segment (<c>default</c>), which has none.</summary>
	public byte[]? Array { get; }

	/// <summary>The bytes, from the array's start: 0 for the empty segment.</summary>
	public int Length { get; }

	/// <summary>Whether the segment is the empty one, which has no array and needs no return.</summary>
	public bool IsEmpty => Array is null;

	/// <summary>The bytes.</summary>
	public ReadOnlySpan<byte> Span => Array is null ? default : Array.AsSpan(0, Length);

	/// <summary>The bytes, as memory.</summary>
	public ReadOnlyMemory<byte> Memory => Array is null ? default : Array.AsMemory(0, Length);

	/// <summary>Returns the array to the pool: the segment's last owner does, once. The empty segment does nothing.</summary>
	public void Return()
	{
		if (Array is not null)
		{
			ArrayPool<byte>.Shared.Return(Array);
		}
	}
}
