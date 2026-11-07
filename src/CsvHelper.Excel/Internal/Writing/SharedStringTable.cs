using System;

namespace CsvHelper.Excel;

/// <summary>
/// The shared strings of a workbook written with <see cref="ExcelStrings.Shared"/>: each distinct string once, in the
/// order first written, with the index its cells hold (<c>t="s"</c>). A string is kept as the XML of its <c>si</c>
/// element's content, escaped (<see cref="XmlOutput.AppendText"/>), so equal texts have equal bytes. The bytes are in one
/// array (the arena) and are found by an open-addressing hash table, so a string costs its bytes and three integers.
/// </summary>
internal sealed class SharedStringTable
{
	/// <summary>The most strings a table holds, so that its slots, twice as many, still fit an array.</summary>
	private const int MaxCount = 1 << 29;

	private byte[] arena = new byte[4096];
	private int arenaLength;
	// The start of each string in the arena, and its hash; a string ends where the next starts (or at arenaLength).
	private int[] starts = new int[64];
	private int[] hashes = new int[64];
	// Each slot holds a string's index + 1, or 0 when it is empty. Its length is a power of two, at least twice Count.
	private int[] slots = new int[128];
	private int count;

	/// <summary>The number of distinct strings.</summary>
	public int Count => count;

	/// <summary>The number of cells that refer to a string, for the part's <c>count</c>.</summary>
	public long References { get; set; }

	/// <summary>The bytes the table holds: its arena, and its integers.</summary>
	public long MemorySize => arena.Length + 4L * (starts.Length + hashes.Length + slots.Length);

	/// <summary>A string's escaped XML.</summary>
	/// <param name="index">The string's index, below <see cref="Count"/>.</param>
	/// <returns>Its bytes.</returns>
	public ReadOnlySpan<byte> this[int index] => arena.AsSpan(starts[index], (index + 1 < count ? starts[index + 1] : arenaLength) - starts[index]);

	/// <summary>The index of a string, which is added when the table doesn't have it.</summary>
	/// <param name="xml">The string's escaped XML: what goes between <c>&lt;si&gt;</c> and <c>&lt;/si&gt;</c>.</param>
	/// <returns>Its index.</returns>
	/// <exception cref="InvalidOperationException">The table holds as many strings, or as many bytes, as it can.</exception>
	public int IndexOf(ReadOnlySpan<byte> xml)
	{
		var hash = Hash(xml);
		var mask = slots.Length - 1;
		var slot = hash & mask;
		while (slots[slot] != 0)
		{
			var index = slots[slot] - 1;
			if (hashes[index] == hash && this[index].SequenceEqual(xml))
			{
				return index;
			}

			slot = (slot + 1) & mask;
		}

		return Add(xml, hash, slot);
	}

	/// <summary>A hash of bytes: .NET's randomized one where it has it, FNV-1a elsewhere.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash, which is not negative.</returns>
	private static int Hash(ReadOnlySpan<byte> bytes)
	{
#if NET
		var hash = new HashCode();
		hash.AddBytes(bytes);

		return hash.ToHashCode() & int.MaxValue;
#else
		var hash = 2166136261u;
		foreach (var b in bytes)
		{
			hash = (hash ^ b) * 16777619u;
		}

		return (int)(hash & int.MaxValue);
#endif
	}

	/// <summary>Adds a string the table doesn't have.</summary>
	/// <param name="xml">The string's escaped XML.</param>
	/// <param name="hash">Its hash.</param>
	/// <param name="slot">The empty slot its search ended at.</param>
	/// <returns>Its index.</returns>
	/// <exception cref="InvalidOperationException">The table holds as many strings, or as many bytes, as it can.</exception>
	private int Add(ReadOnlySpan<byte> xml, int hash, int slot)
	{
		if (count == MaxCount || (long)arenaLength + xml.Length > Array.MaxLength)
		{
			throw new InvalidOperationException("The workbook's shared strings are full: write it with ExcelWriterOptions.Strings set to Inline.");
		}

		if (arena.Length - arenaLength < xml.Length)
		{
			Array.Resize(ref arena, (int)Math.Min(Math.Max(2L * arena.Length, (long)arenaLength + xml.Length), Array.MaxLength));
		}

		if (count == starts.Length)
		{
			Array.Resize(ref starts, starts.Length * 2);
			Array.Resize(ref hashes, hashes.Length * 2);
		}

		var index = count++;
		starts[index] = arenaLength;
		hashes[index] = hash;
		xml.CopyTo(arena.AsSpan(arenaLength));
		arenaLength += xml.Length;
		slots[slot] = index + 1;
		if (count * 2 > slots.Length)
		{
			Rehash();
		}

		return index;
	}

	/// <summary>Doubles the slots, placing each string again.</summary>
	private void Rehash()
	{
		slots = new int[slots.Length * 2];
		var mask = slots.Length - 1;
		for (var index = 0; index < count; index++)
		{
			var slot = hashes[index] & mask;
			while (slots[slot] != 0)
			{
				slot = (slot + 1) & mask;
			}

			slots[slot] = index + 1;
		}
	}
}
