// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// https://blog.markvincze.com/back-to-basics-dictionary-part-2-net-implementation/

namespace CsvHelper;

/// <summary>
/// Caches fields.
/// Based on C#'s <see cref="Dictionary{TKey, TValue}"/>.
/// </summary>
internal class FieldCache
{
	private readonly int maxFieldSize;
	private int size;
	private int[] buckets;
	private Entry[] entries;
	private int count;

	public FieldCache(int initialSize = 128, int maxFieldSize = 128)
	{
		this.maxFieldSize = maxFieldSize;
		size = initialSize;
		buckets = new int[size];
		entries = new Entry[size];
	}

	public string GetField(ReadOnlySpan<char> buffer)
	{
		if (buffer.IsEmpty)
		{
			return string.Empty;
		}

		if (buffer.Length > maxFieldSize)
		{
			return buffer.ToString();
		}

		var hashCode = GetHashCode(buffer);
		ref var bucket = ref GetBucket(hashCode);
		int i = bucket - 1;
		while ((uint)i < (uint)entries.Length)
		{
			ref var entry = ref entries[i];

			if (entry.HashCode == hashCode && entry.Value.AsSpan().SequenceEqual(buffer))
			{
				return entry.Value;
			}

			i = entry.Next;
		}

		if (count == entries.Length)
		{
			Resize();
			bucket = ref GetBucket(hashCode);
		}

		ref var reference = ref entries[count];
		reference.HashCode = hashCode;
		reference.Next = bucket - 1;
		reference.Value = buffer.ToString();
		bucket = count + 1;
		count++;

		return reference.Value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint GetHashCode(ReadOnlySpan<char> buffer)
	{
		unchecked
		{
#if NET6_0_OR_GREATER
			HashCode hash = new();
			hash.AddBytes(MemoryMarshal.AsBytes(buffer));
			return (uint)hash.ToHashCode();
#else
			uint hash = 17;
			foreach (char c in buffer)
			{
				hash = hash * 31 + c;
			}

			return hash;
#endif
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ref int GetBucket(uint hashCode)
	{
		return ref buckets[hashCode & buckets.Length - 1];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Resize()
	{
		size *= 2;

		var tempEntries = new Entry[size];

		Array.Copy(entries, tempEntries, count);

		buckets = new int[size];

		for (int i = 0; i < count; i++)
		{
			ref var tempEntry = ref tempEntries[i];

			if (tempEntry.Next >= -1)
			{
				ref var bucket = ref GetBucket(tempEntry.HashCode);
				tempEntry.Next = bucket - 1;
				bucket = i + 1;
			}
		}

		entries = tempEntries;
	}

	[DebuggerDisplay("HashCode = {HashCode}, Next = {Next}, Value = {Value}")]
	private struct Entry
	{
		public uint HashCode;

		public int Next;

		public string Value;
	}
}
