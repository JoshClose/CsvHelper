using System;

namespace CsvHelper.Excel;

/// <summary>
/// Bytes held in order in pooled buffers of <see cref="ZipWriter.BufferSize"/> bytes, rented from and returned to their
/// <see cref="ZipWriter"/>: an entry's data until it is written, or the central directory until the package ends. Every
/// buffer but the last is full.
/// </summary>
/// <param name="owner">The writer whose pool the buffers come from.</param>
internal sealed class ZipBuffers(ZipWriter owner)
{
	/// <summary>The buffers, the first <see cref="count"/> in use.</summary>
	private byte[][] buffers = new byte[4][];

	/// <summary>The buffers in use.</summary>
	private int count;

	/// <summary>The bytes in the last buffer in use.</summary>
	private int last;

	/// <summary>The buffers in use.</summary>
	public int Count => count;

	/// <summary>The bytes held.</summary>
	public long Length => count == 0 ? 0 : ((long)(count - 1) * ZipWriter.BufferSize) + last;

	/// <summary>A buffer in use.</summary>
	/// <param name="index">Its place, from 0.</param>
	/// <returns>The buffer: full, but for the last.</returns>
	public byte[] this[int index] => buffers[index];

	/// <summary>The bytes in a buffer in use: <see cref="ZipWriter.BufferSize"/>, but for the last.</summary>
	/// <param name="index">The buffer's place, from 0.</param>
	/// <returns>The bytes.</returns>
	public int LengthOf(int index) => index == count - 1 ? last : ZipWriter.BufferSize;

	/// <summary>Appends bytes, renting buffers as they fill.</summary>
	/// <param name="bytes">The bytes.</param>
	public void Append(ReadOnlySpan<byte> bytes)
	{
		while (!bytes.IsEmpty)
		{
			if (count == 0 || last == ZipWriter.BufferSize)
			{
				if (count == buffers.Length)
				{
					Array.Resize(ref buffers, count * 2);
				}

				buffers[count++] = owner.Rent();
				last = 0;
			}

			var n = Math.Min(ZipWriter.BufferSize - last, bytes.Length);
			bytes.Slice(0, n).CopyTo(buffers[count - 1].AsSpan(last));
			last += n;
			bytes = bytes.Slice(n);
		}
	}

	/// <summary>Removes the last bytes, copying them out, and returns the buffers that empties.</summary>
	/// <param name="destination">Where the bytes go: as many as it holds, at most <see cref="Length"/>.</param>
	public void RemoveLast(Span<byte> destination)
	{
		var end = destination.Length;
		while (end > 0)
		{
			var n = Math.Min(end, last);
			buffers[count - 1].AsSpan(last - n, n).CopyTo(destination.Slice(end - n));
			end -= n;
			last -= n;
			if (last == 0)
			{
				owner.Return(buffers[--count]);
				buffers[count] = null!;
				last = count == 0 ? 0 : ZipWriter.BufferSize;
			}
		}
	}

	/// <summary>Removes the first buffers, which are full, returning them to the pool.</summary>
	/// <param name="removed">How many: fewer than <see cref="Count"/>.</param>
	public void RemoveFirst(int removed)
	{
		for (var i = 0; i < removed; i++)
		{
			owner.Return(buffers[i]);
		}

		Array.Copy(buffers, removed, buffers, 0, count - removed);
		Array.Clear(buffers, count - removed, removed);
		count -= removed;
	}

	/// <summary>Removes every byte, returning the buffers to the pool. Calling it again does nothing.</summary>
	public void Clear()
	{
		for (var i = 0; i < count; i++)
		{
			owner.Return(buffers[i]);
			buffers[i] = null!;
		}

		count = 0;
		last = 0;
	}
}
