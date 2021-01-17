// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

// https://blog.markvincze.com/back-to-basics-dictionary-part-2-net-implementation/

namespace CsvHelper
{
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

		public string GetField(char[] buffer, int start, int length)
		{
			if (length == 0)
			{
				return string.Empty;
			}

			if (length > maxFieldSize)
			{
				return new string(buffer, start, length);
			}

			var hashCode = GetHashCode(buffer, start, length);
			ref var bucket = ref GetBucket(hashCode);
			int i = bucket - 1;
			while ((uint)i < (uint)entries.Length)
			{
				ref var entry = ref entries[i];

				if (entry.HashCode == hashCode && entry.Value.AsSpan().SequenceEqual(new Span<char>(buffer, start, length)))
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
			reference.Value = new string(buffer, start, length);
			bucket = count + 1;
			count++;

			return reference.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint GetHashCode(char[] buffer, int start, int length)
		{
			unchecked
			{
				uint hash = 17;
				for (var i = start; i < start + length; i++)
				{
					hash = hash * 31 + buffer[i];
				}

				return hash;
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
}
