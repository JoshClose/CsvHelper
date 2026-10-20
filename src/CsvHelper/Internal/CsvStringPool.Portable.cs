#if !NET
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CsvHelper;

/// <summary>
/// The strings of one column, for targets without SIMD: the same table as the .NET version (see CsvStringPool.cs),
/// with the head and tail of the value held in four <see cref="ulong"/>s instead of two vectors. Keep the two in step.
/// </summary>
internal sealed class ColumnStringPool
{
	private const int InitialSlots = 64;
	private const int MaxCount = 4096;             // values; the table has up to twice as many slots
	private const int MinMissesToSwitchOff = 1024;
	// A column with this many values where nearly every record brought a new one (ids, timestamps) switches off here,
	// before its table grows to 2048 slots: 98 KB, which would go to the large object heap.
	private const int EarlyCheckCount = 512;

	/// <summary>
	/// A slot of the table. The first and the last 16 bytes of the value (first byte lowest), zero padded when it is
	/// shorter, are with the length an exact key for up to 32 bytes; longer values also compare their middle bytes,
	/// stored at <see cref="Offset"/>.
	/// </summary>
	private struct Entry
	{
		/// <summary>
		/// Bytes 0-7 and 8-15 (<c>Head0</c>, <c>Head1</c>) and the last 16 bytes (<c>Tail0</c>, <c>Tail1</c>) of the
		/// value, each little-endian; the tail equals the head when the value is shorter than 16 bytes.
		/// </summary>
		public ulong Head0, Head1, Tail0, Tail1;

		/// <summary>The string, written last when the entry is added; null marks an empty slot.</summary>
		public string? Value;

		/// <summary>The length of the value in bytes.</summary>
		public int Length;

		/// <summary>For a value longer than 32 bytes, the position of its bytes in the byte store.</summary>
		public int Offset;
	}

	/// <summary>The key of a value: the same bytes the .NET version loads into its head and tail vectors.</summary>
	private readonly struct Key
	{
		/// <summary>The head and tail of the value, laid out as in <see cref="Entry"/>.</summary>
		public readonly ulong Head0, Head1, Tail0, Tail1;

		/// <summary>The 16 bytes from <paramref name="p"/> must be readable even when the value is shorter.</summary>
		/// <param name="p">The first byte of the value.</param>
		/// <param name="length">The length of the value in bytes, at least 1.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Key(ref byte p, int length)
		{
			var h0 = Portable.LoadLittleEndian(ref p);
			var h1 = Portable.LoadLittleEndian(ref Unsafe.Add(ref p, 8));
			if (length < 16)
			{
				// Keep the first 'length' bytes; the tail is the same masked 16 bytes, as in the .NET version.
				h0 &= Mask(length);
				h1 &= Mask(length - 8);
				Head0 = Tail0 = h0;
				Head1 = Tail1 = h1;
			}
			else
			{
				Head0 = h0;
				Head1 = h1;
				Tail0 = Portable.LoadLittleEndian(ref Unsafe.Add(ref p, length - 16));
				Tail1 = Portable.LoadLittleEndian(ref Unsafe.Add(ref p, length - 8));
			}
		}

		/// <summary>The low <paramref name="bytes"/> bytes (none when negative, all from 8).</summary>
		private static ulong Mask(int bytes) => bytes <= 0 ? 0 : bytes >= 8 ? ulong.MaxValue : (1UL << (bytes * 8)) - 1;

		/// <summary>Whether the entry has the same head and tail; the caller compares the length and the middle bytes.</summary>
		/// <param name="e">The entry to compare with.</param>
		/// <returns>Whether the head and tail bytes are equal.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Matches(in Entry e) =>
			((e.Head0 ^ Head0) | (e.Head1 ^ Head1) | (e.Tail0 ^ Tail0) | (e.Tail1 ^ Tail1)) == 0;

		/// <summary>The hash of the value, from which the first slot to probe is taken.</summary>
		/// <param name="length">The length of the value in bytes.</param>
		/// <returns>The hash of the head, the tail and the length.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong Hash(int length) => ColumnStringPool.Hash(Head0, Head1, Tail0, Tail1, length);
	}

	private volatile Entry[] entries = new Entry[InitialSlots];
	private volatile int maxLength = CsvStringPool.MaxPooledLength;   // 0 once pooling is switched off
	private volatile byte[] bytes = [];            // values longer than 32 bytes
	// Guarded by the lock:
	private int count;
	private int bytesUsed;
	private long fullSince;
	private long firstRecord = -1;
	private int fullMisses;

	/// <summary>Gets the string for the <paramref name="length"/> bytes at <paramref name="p"/>, adding it to the table when it is new.</summary>
	/// <param name="p">The value; the 16 bytes from here must be readable even when it is shorter.</param>
	/// <param name="length">Its length in bytes, at least 1.</param>
	/// <param name="record">
	/// The number of the current record. Once the table is full, pooling is switched off when most records still bring
	/// a new value.
	/// </param>
	/// <param name="last">The caller's hint: the slot of the value it got last from this column.</param>
	/// <returns>The pooled string, or a new one when the value is not pooled.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string Get(ref byte p, int length, long record, ref int last)
	{
		if ((uint)length > (uint)maxLength) return Unpooled(ref p, length);
		var key = new Key(ref p, length);
		var table = entries;
		var hint = last;
		// The hint can come from a bigger table that has been replaced since (switched off).
		if ((uint)hint < (uint)table.Length)
		{
			var value = Match(ref table[hint], ref p, length, key);
			if (value is not null) return value;
		}

		var slotMask = table.Length - 1;
		var i = Slot(key.Hash(length), table.Length);
		while (true)
		{
			ref var e = ref table[i];
			// Read the value first: once it is set, the rest of the entry is complete.
			var value = Volatile.Read(ref e.Value);
			if (value is null) return Add(ref p, length, key, record, ref last);
			if (e.Length == length && key.Matches(e) && (length <= 32 || MiddleEquals(ref p, e.Offset, length)))
			{
				last = i;

				return value;
			}

			i = (i + 1) & slotMask;
		}
	}

	/// <summary>The entry's value when it holds these bytes, otherwise null.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string? Match(ref Entry e, ref byte p, int length, in Key key)
	{
		var value = Volatile.Read(ref e.Value);

		return e.Length == length && key.Matches(e) && (length <= 32 || MiddleEquals(ref p, e.Offset, length)) ? value : null;
	}

	private static ulong Hash(ulong head0, ulong head1, ulong tail0, ulong tail1, int length) =>
		(head0 * 0x9E3779B97F4A7C15UL) ^ (head1 * 0xC2B2AE3D27D4EB4FUL) ^
		(tail0 * 0x165667B19E3779F9UL) ^ (tail1 * 0x27D4EB2F165667C5UL) ^ (ulong)length;

	/// <summary>
	/// The first slot to probe: high bits of the hash, as in the .NET version, but without a (software) leading zero
	/// count. The table never has more than 2^13 slots, so the 13 bits from bit 51 cover every size.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Slot(ulong hash, int slots) => (int)(hash >> 51) & (slots - 1);

	/// <summary>Whether bytes 16 .. length-16 (the head and tail already matched) equal the stored value's.</summary>
	private bool MiddleEquals(ref byte p, int offset, int length)
	{
		// The entry was read after its value, so this store already holds its bytes.
		ref var q = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), offset);
		var end = length - 16;
		var i = 16;
		for (; i + 8 < end; i += 8)
		{
			if (Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, i)) != Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref q, i))) return false;
		}

		// The last step overlaps the previous one (or the head) instead of running past the middle.
		return Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, end - 8)) == Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref q, end - 8));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string Unpooled(ref byte p, int length) => Utf8String.Create(ref p, length);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private string Add(ref byte p, int length, in Key key, long record, ref int last)
	{
		lock (this)
		{
			if ((uint)length > (uint)maxLength) return Unpooled(ref p, length);
			if (firstRecord < 0) firstRecord = record;
			// Probe again: another thread may have added the value, or grown the table, in the meantime.
			var table = entries;
			var slotMask = table.Length - 1;
			var i = Slot(key.Hash(length), table.Length);
			while (true)
			{
				ref var e = ref table[i];
				if (e.Value is null) break;
				if (e.Length == length && key.Matches(e) && (length <= 32 || MiddleEquals(ref p, e.Offset, length)))
				{
					last = i;

					return e.Value;
				}

				i = (i + 1) & slotMask;
			}

			var value = Utf8String.Create(ref p, length);
			if (count == MaxCount)
			{
				// Full: keep the values seen so far, and give up when most records still bring a new value.
				if (++fullMisses >= MinMissesToSwitchOff && fullMisses * 2L > record - fullSince) SwitchOff();

				return value;
			}

			ref var slot = ref table[i];
			slot.Head0 = key.Head0;
			slot.Head1 = key.Head1;
			slot.Tail0 = key.Tail0;
			slot.Tail1 = key.Tail1;
			slot.Length = length;
			if (length > 32) slot.Offset = Store(ref p, length);
			Volatile.Write(ref slot.Value, value);
			last = i;
			if (++count == EarlyCheckCount && count * 20L > (record - firstRecord + 1) * 19)
			{
				SwitchOff();

				return value;
			}

			if (count * 2 > table.Length) Grow();
			if (count == MaxCount) fullSince = record;

			return value;
		}
	}

	private int Store(ref byte value, int length)
	{
		var store = bytes;
		if (store.Length - bytesUsed < length)
		{
			// Readers may still use the old array: copy into a new one rather than resizing in place.
			var bigger = new byte[Math.Max(store.Length * 2, Math.Max(1024, bytesUsed + length))];
			store.AsSpan(0, bytesUsed).CopyTo(bigger);
			bytes = store = bigger;
		}

		Unsafe.CopyBlockUnaligned(ref store[bytesUsed], ref value, (uint)length);
		var offset = bytesUsed;
		bytesUsed += length;

		return offset;
	}

	private void Grow()
	{
		var old = entries;
		var table = new Entry[old.Length * 2];
		var slotMask = table.Length - 1;
		foreach (var e in old)
		{
			if (e.Value is null) continue;
			var i = Slot(Hash(e.Head0, e.Head1, e.Tail0, e.Tail1, e.Length), table.Length);
			while (table[i].Value is not null) i = (i + 1) & slotMask;
			table[i] = e;
		}

		entries = table;
	}

	private void SwitchOff()
	{
		maxLength = 0;
		// Lookups still running may use the old table and its byte store; they stay valid. The byte store
		// is kept, as such a lookup reads it through the field.
		entries = new Entry[1];
	}
}
#endif
