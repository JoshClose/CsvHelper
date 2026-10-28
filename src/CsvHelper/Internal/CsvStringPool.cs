using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif
using System.Threading;

namespace CsvHelper;

/// <summary>
/// De-duplicates strings decoded from UTF-8, with one hash table per column. A column whose values
/// rarely repeat (ids, timestamps) switches itself off, after 512 values when nearly every record brought a new one,
/// otherwise once its table is full and most records still bring one, so it costs little more than plain decoding.
/// </summary>
/// <remarks>
/// The tables can be shared by the readers of a parallel enumeration (<see cref="CreateShared"/>): lookups
/// that find a value take no lock, adding one does. Each pool instance is used by one thread at a time.
/// </remarks>
internal sealed class CsvStringPool
{
	/// <summary>Longer values are not pooled: they rarely repeat and cost more to compare.</summary>
	internal const int MaxPooledLength = 128;

	private readonly ColumnTables tables;
	// Per column, the slot of the value returned last. Neighbouring records often repeat a value, so it is
	// checked before hashing. Kept per pool instance, so threads sharing the tables do not write to them.
	private int[] hints = new int[32];

	/// <summary>Creates a pool with empty tables of its own.</summary>
	public CsvStringPool() : this(new ColumnTables()) { }

	private CsvStringPool(ColumnTables tables) => this.tables = tables;

	/// <summary>A pool for another thread that shares this pool's values.</summary>
	/// <returns>A pool with the same tables and hints of its own.</returns>
	public CsvStringPool CreateShared() => new(tables);

	/// <summary>
	/// Gets the string for the <paramref name="length"/> (at least 1) bytes at <paramref name="p"/>. The 16
	/// bytes from <paramref name="p"/> must be readable even when the value is shorter.
	/// </summary>
	/// <param name="column">The 0-based column of the value; values of a negative column are not pooled.</param>
	/// <param name="p">The first byte of the UTF-8 value.</param>
	/// <param name="length">The length of the value in bytes, at least 1.</param>
	/// <param name="record">
	/// The number of the current record, with which a column decides to switch pooling off when most records bring a
	/// new value.
	/// </param>
	/// <returns>The pooled string, or a new one when the value is not pooled.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string Get(int column, ref byte p, int length, long record)
	{
		var pools = tables.Columns;
		var last = hints;
		if ((uint)column < (uint)pools.Length && (uint)column < (uint)last.Length)
		{
#if NET
			var pool = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(pools), (nint)(uint)column);
			if (pool is not null)
				return pool.Get(ref p, length, record, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(last), (nint)(uint)column));
#else
			// Plain indexing: without runtime support, a reference to the data of an array of references costs a type check.
			var pool = pools[column];
			if (pool is not null) return pool.Get(ref p, length, record, ref last[column]);
#endif
		}

		return GetSlow(column, ref p, length, record);
	}

	/// <summary>Gets the string for a UTF-8 value of at least 1 byte; nothing beyond it needs to be readable.</summary>
	/// <param name="column">The 0-based column of the value; values of a negative column are not pooled.</param>
	/// <param name="utf8">The UTF-8 value, at least 1 byte long.</param>
	/// <param name="record">The number of the current record (see <see cref="Get(int, ref byte, int, long)"/>).</param>
	/// <returns>The pooled string, or a new one when the value is not pooled (also when it is longer than <see cref="MaxPooledLength"/>).</returns>
	[SkipLocalsInit]
	public string Get(int column, ReadOnlySpan<byte> utf8, long record)
	{
		if (column < 0 || utf8.Length > MaxPooledLength) return Utf8String.Create(utf8);
		Span<byte> padded = stackalloc byte[MaxPooledLength + 16];
		utf8.CopyTo(padded);

		return Get(column, ref MemoryMarshal.GetReference(padded), utf8.Length, record);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private string GetSlow(int column, ref byte p, int length, long record)
	{
#if NET
		if (column < 0) return Utf8String.Create(MemoryMarshal.CreateReadOnlySpan(ref p, length));
#else
		if (column < 0) return Utf8String.Create(ref p, length);
#endif
		if (column >= hints.Length) Array.Resize(ref hints, Math.Max(hints.Length * 2, column + 1));

		return tables.GetOrCreate(column).Get(ref p, length, record, ref hints[column]);
	}

	/// <summary>The column tables, shared by every pool created from the same root.</summary>
	private sealed class ColumnTables
	{
		private volatile ColumnStringPool?[] columns = new ColumnStringPool?[32];

		/// <summary>
		/// The table of each column, null for a column without one yet. Read without a lock: growing replaces the
		/// array.
		/// </summary>
		public ColumnStringPool?[] Columns => columns;

		/// <summary>Gets the table of a column, creating it (and growing <see cref="Columns"/>) under a lock if needed.</summary>
		/// <param name="column">The 0-based column, not negative.</param>
		/// <returns>The column's table.</returns>
		public ColumnStringPool GetOrCreate(int column)
		{
			lock (this)
			{
				var pools = columns;
				if (column >= pools.Length)
				{
					var bigger = new ColumnStringPool?[Math.Max(pools.Length * 2, column + 1)];
					pools.CopyTo(bigger, 0);
					pools = bigger;
				}

				var pool = pools[column] ??= new ColumnStringPool();
				columns = pools;

				return pool;
			}
		}
	}
}

#if NET
/// <summary>
/// The strings of one column: an open addressing table keyed by the bytes of the value. Lookups take no
/// lock (an entry is published by writing its <see cref="Entry.Value"/> last and never changes afterwards;
/// growing replaces the table); adding a value locks.
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
	/// A slot of the table. The first and the last 16 bytes of the value, zero padded when it is shorter, are with the
	/// length an exact key for up to 32 bytes; longer values also compare their middle bytes, stored at
	/// <see cref="Offset"/>.
	/// </summary>
	private struct Entry
	{
		/// <summary>The first 16 bytes of the value, zero padded when it is shorter.</summary>
		public Vector128<byte> Head;

		/// <summary>The last 16 bytes of the value; the same as <see cref="Head"/> when it is shorter than 16 bytes.</summary>
		public Vector128<byte> Tail;

		/// <summary>The string, written last when the entry is added; null marks an empty slot.</summary>
		public string? Value;

		/// <summary>The length of the value in bytes.</summary>
		public int Length;

		/// <summary>For a value longer than 32 bytes, the position of its bytes in the byte store.</summary>
		public int Offset;
	}

	// Loaded at offset 16 - length: the first 'length' bytes are set.
	private static ReadOnlySpan<byte> HeadMasks =>
	[
		0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	];

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
		var mask = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(HeadMasks), (nuint)(16 - Math.Min(length, 16)));
		var head = Vector128.LoadUnsafe(ref p) & mask;
		var tail = Vector128.LoadUnsafe(ref p, (nuint)(Math.Max(length, 16) - 16)) & mask;
		var table = entries;
		ref var first = ref MemoryMarshal.GetArrayDataReference(table);
		var hint = last;
		// The hint can come from a bigger table that has been replaced since (switched off).
		if ((uint)hint < (uint)table.Length)
		{
			var value = Match(ref Unsafe.Add(ref first, hint), ref p, length, head, tail);
			if (value is not null) return value;
		}

		var slotMask = table.Length - 1;
		var i = Slot(Hash(head, tail, length), table.Length);
		while (true)
		{
			ref var e = ref Unsafe.Add(ref first, i);
			// Read the value first: once it is set, the rest of the entry is complete.
			var value = Volatile.Read(ref e.Value);
			if (value is null) return Add(ref p, length, head, tail, record, ref last);
			if (e.Length == length && ((e.Head ^ head) | (e.Tail ^ tail)) == Vector128<byte>.Zero &&
				(length <= 32 || MiddleEquals(ref p, e.Offset, length)))
			{
				last = i;

				return value;
			}

			i = (i + 1) & slotMask;
		}
	}

	/// <summary>The entry's value when it holds these bytes, otherwise null.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string? Match(ref Entry e, ref byte p, int length, Vector128<byte> head, Vector128<byte> tail)
	{
		var value = Volatile.Read(ref e.Value);

		return e.Length == length && ((e.Head ^ head) | (e.Tail ^ tail)) == Vector128<byte>.Zero &&
			   (length <= 32 || MiddleEquals(ref p, e.Offset, length)) ? value : null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Hash(Vector128<byte> head, Vector128<byte> tail, int length)
	{
		var h = head.AsUInt64();
		var t = tail.AsUInt64();

		return (h.ToScalar() * 0x9E3779B97F4A7C15UL) ^ (h.GetElement(1) * 0xC2B2AE3D27D4EB4FUL) ^
			   (t.ToScalar() * 0x165667B19E3779F9UL) ^ (t.GetElement(1) * 0x27D4EB2F165667C5UL) ^ (ulong)length;
	}

	/// <summary>
	/// The first slot to probe in a table of <paramref name="slots"/> (a power of two): the top log2(slots) bits of
	/// the hash. Masked as well, so the index stays inside the table for any size a concurrent lookup may see
	/// (with one slot the shift count is 64, which C# takes as 0).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Slot(ulong hash, int slots) => (int)(hash >> (32 + BitOperations.LeadingZeroCount((uint)slots - 1))) & (slots - 1);

	/// <summary>Whether bytes 16 .. length-16 (the head and tail already matched) equal the stored value's.</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool MiddleEquals(ref byte p, int offset, int length)
	{
		// The entry was read after its value, so this store already holds its bytes.
		ref var q = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), (nint)(uint)offset);
		var end = (nuint)(length - 16);
		nuint i = 16;
		for (; i + 16 < end; i += 16)
		{
			if (Vector128.LoadUnsafe(ref p, i) != Vector128.LoadUnsafe(ref q, i)) return false;
		}

		// The last step overlaps the previous one (or the head) instead of running past the middle.
		return Vector128.LoadUnsafe(ref p, end - 16) == Vector128.LoadUnsafe(ref q, end - 16);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string Unpooled(ref byte p, int length) => Utf8String.Create(MemoryMarshal.CreateReadOnlySpan(ref p, length));

	[MethodImpl(MethodImplOptions.NoInlining)]
	private string Add(ref byte p, int length, Vector128<byte> head, Vector128<byte> tail, long record, ref int last)
	{
		lock (this)
		{
			if ((uint)length > (uint)maxLength) return Unpooled(ref p, length);
			if (firstRecord < 0) firstRecord = record;
			// Probe again: another thread may have added the value, or grown the table, in the meantime.
			var table = entries;
			var slotMask = table.Length - 1;
			var i = Slot(Hash(head, tail, length), table.Length);
			while (true)
			{
				ref var e = ref table[i];
				if (e.Value is null) break;
				if (e.Length == length && ((e.Head ^ head) | (e.Tail ^ tail)) == Vector128<byte>.Zero &&
					(length <= 32 || MiddleEquals(ref p, e.Offset, length)))
				{
					last = i;

					return e.Value;
				}

				i = (i + 1) & slotMask;
			}

			var value = Utf8String.Create(MemoryMarshal.CreateReadOnlySpan(ref p, length));
			if (count == MaxCount)
			{
				// Full: keep the values seen so far, and give up when most records still bring a new value.
				if (++fullMisses >= MinMissesToSwitchOff && fullMisses * 2L > record - fullSince) SwitchOff();

				return value;
			}

			ref var slot = ref table[i];
			slot.Head = head;
			slot.Tail = tail;
			slot.Length = length;
			if (length > 32) slot.Offset = Store(MemoryMarshal.CreateReadOnlySpan(ref p, length));
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

	private int Store(ReadOnlySpan<byte> value)
	{
		var store = bytes;
		if (store.Length - bytesUsed < value.Length)
		{
			// Readers may still use the old array: copy into a new one rather than resizing in place.
			var bigger = new byte[Math.Max(store.Length * 2, Math.Max(1024, bytesUsed + value.Length))];
			store.AsSpan(0, bytesUsed).CopyTo(bigger);
			bytes = store = bigger;
		}

		value.CopyTo(store.AsSpan(bytesUsed));
		var offset = bytesUsed;
		bytesUsed += value.Length;

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
			var i = Slot(Hash(e.Head, e.Tail, e.Length), table.Length);
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
