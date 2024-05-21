using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CsvHelper;

public class StringCache
{
	private readonly object locker = new object();
	private readonly int maxFieldSize;
	private int size;
	private int[] buckets;
	private Entry[] entries;
	private int count;

	public StringCache(int initialSize = 128, int maxFieldSize = 128)
	{
		this.maxFieldSize = maxFieldSize;
		size = initialSize;
		buckets = new int[size];
		entries = new Entry[size];
	}

	public string GetString(ReadOnlySpan<char> chars)
	{
		if (chars.Length == 0)
		{
			return string.Empty;
		}

		if (chars.Length > maxFieldSize)
		{
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
			return new string(chars);
#else
			return chars.ToString();
#endif
		}

		var hashCode = GetHashCode(chars);

		ref var bucket = ref GetBucket(hashCode);
		int i = bucket - 1;
		while ((uint)i < (uint)entries.Length)
		{
			ref var entry = ref entries[i];

			if (entry.HashCode == hashCode && entry.Value.AsSpan().SequenceEqual(chars))
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
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		reference.Value = new string(chars);
#else
		reference.Value = chars.ToString();
#endif
		bucket = count + 1;
		count++;

		return reference.Value;
	}

	public string GetStringThreadSafe(ReadOnlySpan<char> chars)
	{
		if (chars.Length == 0)
		{
			return string.Empty;
		}

		if (chars.Length > maxFieldSize)
		{
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
			return new string(chars);
#else
			return chars.ToString();
#endif
		}

		var hashCode = GetHashCode(chars);

		lock (locker)
		{
			ref var bucket = ref GetBucket(hashCode);
			int i = bucket - 1;
			while ((uint)i < (uint)entries.Length)
			{
				ref var entry = ref entries[i];

				if (entry.HashCode == hashCode && entry.Value.AsSpan().SequenceEqual(chars))
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
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
			reference.Value = new string(chars);
#else
			reference.Value = chars.ToString();
#endif
			bucket = count + 1;
			count++;

			return reference.Value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint GetHashCode(ReadOnlySpan<char> chars)
	{
		ref var byteRef = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(chars));
		nint totalByteLength = chars.Length * sizeof(char);
		nint byteIndex = 0;
		nint nativeSize = Unsafe.SizeOf<nuint>();

		nuint hash = 17;
		while (byteIndex <= (totalByteLength - nativeSize))
		{
			hash = hash * 31 + Unsafe.As<byte, nuint>(ref Unsafe.Add(ref byteRef, byteIndex));
			byteIndex += nativeSize;
		}

		return (uint)hash;
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
		entries = tempEntries;

		buckets = new int[size];

		for (int i = 0; i < count; i++)
		{
			ref var entry = ref entries[i];

			if (entry.Next >= -1)
			{
				ref var bucket = ref GetBucket(entry.HashCode);
				entry.Next = bucket - 1;
				bucket = i + 1;
			}
		}
	}

	[DebuggerDisplay("HashCode = {HashCode}, Next = {Next}, Value = {Value}")]
	private record struct Entry
	(
		uint HashCode,
		int Next,
		string Value
	);
}
