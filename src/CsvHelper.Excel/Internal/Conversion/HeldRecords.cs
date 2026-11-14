using System;
using System.Buffers;

namespace CsvHelper.Excel;

/// <summary>
/// The records of a CSV held while their fields decide the columns' types (<see cref="CsvToSheet"/>): a copy of their
/// fields' bytes in one arena, with each field's end and each record's first field, in pooled arrays that grow as needed
/// and are returned when the records have been written.
/// </summary>
internal sealed class HeldRecords : IDisposable
{
	// The fields' bytes, one after the other, and how many are used.
	private byte[] bytes = ArrayPool<byte>.Shared.Rent(4096);
	private int length;
	// Each field's end in the bytes, and how many fields are held.
	private int[] ends = ArrayPool<int>.Shared.Rent(256);
	private int fields;
	// Each record's first field.
	private int[] firsts = ArrayPool<int>.Shared.Rent(64);
	private bool disposed;

	/// <summary>The number of records held.</summary>
	public int Count { get; private set; }

	/// <summary>The most fields a record held has.</summary>
	public int Width { get; private set; }

	/// <summary>Holds a copy of the reader's record.</summary>
	/// <param name="csv">The reader, on a record.</param>
	public void Add(CsvReader csv)
	{
		var count = csv.FieldCount;
		if (Count == firsts.Length)
		{
			firsts = Grow(firsts, Count + 1);
		}

		if (fields + count > ends.Length)
		{
			ends = Grow(ends, fields + count);
		}

		firsts[Count++] = fields;
		Width = Math.Max(Width, count);
		for (var i = 0; i < count; i++)
		{
			var field = csv.GetSpan(i);
			if (field.Length > bytes.Length - length)
			{
				bytes = Grow(bytes, checked(length + field.Length));
			}

			field.CopyTo(bytes.AsSpan(length));
			length += field.Length;
			ends[fields++] = length;
		}
	}

	/// <summary>The number of fields of a record held.</summary>
	/// <param name="record">The record, from 0.</param>
	/// <returns>Its fields.</returns>
	public int FieldCount(int record) => (record + 1 < Count ? firsts[record + 1] : fields) - firsts[record];

	/// <summary>A field of a record held.</summary>
	/// <param name="record">The record, from 0.</param>
	/// <param name="index">The field, from 0, below the record's <see cref="FieldCount"/>.</param>
	/// <returns>The field's bytes.</returns>
	public ReadOnlySpan<byte> Field(int record, int index)
	{
		var field = firsts[record] + index;
		var start = field == 0 ? 0 : ends[field - 1];

		return bytes.AsSpan(start, ends[field] - start);
	}

	/// <summary>Returns the arrays to the pool. Calling it again does nothing.</summary>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		ArrayPool<byte>.Shared.Return(bytes);
		ArrayPool<int>.Shared.Return(ends);
		ArrayPool<int>.Shared.Return(firsts);
		bytes = [];
		ends = [];
		firsts = [];
	}

	/// <summary>Rents a larger array, at least twice as large, copies an array into it and returns the array.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="array">The array, rented.</param>
	/// <param name="needed">The elements needed.</param>
	/// <returns>The larger array.</returns>
	private static T[] Grow<T>(T[] array, int needed)
	{
		var grown = ArrayPool<T>.Shared.Rent(Math.Max(needed, (int)Math.Min(array.Length * 2L, Array.MaxLength)));
		array.CopyTo(grown, 0);
		ArrayPool<T>.Shared.Return(array);

		return grown;
	}
}
