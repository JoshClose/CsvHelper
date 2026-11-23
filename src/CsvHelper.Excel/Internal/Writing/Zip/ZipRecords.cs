using System;
using System.Buffers.Binary;

namespace CsvHelper.Excel;

/// <summary>
/// The zip writer's records, little-endian, as APPNOTE 6.3.10 lays them out: local headers, descriptors, central headers
/// and the end records (<see cref="ZipWriter"/> says which shape each entry takes). Every entry is dated 1980-01-01
/// (time <c>0x0000</c>, date <c>0x0021</c>), made by version 4.5 on host 0 (<c>0x002D</c>), with attributes 0, no comment
/// and disk 0, so that a package's bytes are the same on every system and target for the same deflate output.
/// </summary>
internal static class ZipRecords
{
	/// <summary>The length of a local header before its name and extra field.</summary>
	public const int LocalLength = 30;

	/// <summary>
	/// The length of the local extra field of an entry streamed from its local header (not of a patched entry that falls
	/// back to the streamed form, which has none): a Zip64 extra of two 8-byte zeros.
	/// </summary>
	public const int LocalExtraLength = 20;

	/// <summary>The length of a descriptor: its signature, the CRC and two 8-byte sizes.</summary>
	public const int DescriptorLength = 24;

	/// <summary>The offset in a local header of its version needed, which its flags follow.</summary>
	public const int LocalVersionAt = 4;

	/// <summary>The length of a local header's version needed and flags.</summary>
	public const int LocalVersionLength = 4;

	/// <summary>The offset in a local header of its CRC, which its compressed and uncompressed sizes follow.</summary>
	public const int LocalCrcAt = 14;

	/// <summary>The length of a local header's CRC and sizes.</summary>
	public const int LocalCrcLength = 12;

	/// <summary>The length of a central header before its name and extra field.</summary>
	public const int CentralLength = 46;

	/// <summary>The length of a central extra field, when there is one: a Zip64 extra of three 8-byte fields.</summary>
	public const int CentralExtraLength = 28;

	/// <summary>The most the end records take: the Zip64 end record (56 bytes), its locator (20) and the end record (22).</summary>
	public const int EndLength = 56 + 20 + 22;

	/// <summary>Version 2.0, which a whole entry within 32-bit fields needs.</summary>
	public const ushort Version20 = 20;

	/// <summary>Version 4.5, which Zip64 fields and 8-byte descriptors need.</summary>
	public const ushort Version45 = 45;

	/// <summary>Bit 3 of the flags: the CRC and sizes are in a descriptor after the data.</summary>
	public const ushort Streamed = 0x0008;

	/// <summary>Version made by: 4.5, host 0 (MS-DOS).</summary>
	private const ushort MadeBy = 0x002D;

	/// <summary>The time of every entry: 00:00:00.</summary>
	private const ushort Time = 0x0000;

	/// <summary>The date of every entry: 1980-01-01, the first a zip's date can hold, which Excel's own entries have.</summary>
	private const ushort Date = 0x0021;

	/// <summary>The value of a 32-bit field whose value is in a Zip64 field.</summary>
	private const uint Wide32 = 0xFFFFFFFF;

	/// <summary>The value of a 16-bit field whose value is in a Zip64 field.</summary>
	private const ushort Wide16 = 0xFFFF;

	/// <summary>
	/// Writes an entry's local header: a whole entry's with its CRC and sizes and no extra field; a patched entry's (a
	/// stored entry past one buffer, on a stream that seeks) without bit 3 or an extra field, its CRC and sizes 0 until
	/// they are patched in after its data (<see cref="LocalSizes"/>); a streamed entry's with CRC and sizes 0 (never
	/// <c>0xFFFFFFFF</c>, which Excel refuses) and a Zip64 extra of zeros.
	/// </summary>
	/// <param name="destination">Where it goes: at least <see cref="LocalLength"/>, the name and <see cref="LocalExtraLength"/> bytes.</param>
	/// <param name="entry">The entry, whose shape, version, flags, method, CRC and sizes are set.</param>
	/// <returns>The header's length.</returns>
	public static int Local(Span<byte> destination, ZipEntryStream entry)
	{
		var name = entry.Name;
		var streamed = (entry.Flags & Streamed) != 0;
		var sized = !streamed && !entry.Patched;
		BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x04034B50);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(LocalVersionAt), entry.VersionNeeded);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6), entry.Flags);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8), entry.Method);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10), Time);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(12), Date);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(LocalCrcAt), sized ? entry.Crc : 0);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(18), sized ? (uint)entry.CompressedSize : 0);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(22), sized ? (uint)entry.UncompressedSize : 0);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(26), (ushort)name.Length);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28), streamed ? (ushort)LocalExtraLength : (ushort)0);
		WriteName(destination.Slice(LocalLength), name);
		var length = LocalLength + name.Length;
		if (streamed)
		{
			var extra = destination.Slice(length, LocalExtraLength);
			extra.Clear();
			BinaryPrimitives.WriteUInt16LittleEndian(extra, 0x0001);
			BinaryPrimitives.WriteUInt16LittleEndian(extra.Slice(2), 16);
			length += LocalExtraLength;
		}

		return length;
	}

	/// <summary>
	/// Writes a patched entry's CRC and sizes, closed with both sizes below <see cref="ZipLimits.Max32"/>: the
	/// <see cref="LocalCrcLength"/> bytes of its local header from <see cref="LocalCrcAt"/>.
	/// </summary>
	/// <param name="destination">Where they go: at least <see cref="LocalCrcLength"/> bytes.</param>
	/// <param name="entry">The entry, closed.</param>
	/// <returns>Their length.</returns>
	public static int LocalSizes(Span<byte> destination, ZipEntryStream entry)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(destination, entry.Crc);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4), (uint)entry.CompressedSize);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8), (uint)entry.UncompressedSize);

		return LocalCrcLength;
	}

	/// <summary>
	/// Writes a patched entry's version needed and flags, closed with a size at <see cref="ZipLimits.Max32"/>, which falls
	/// back to the streamed form (version 4.5, bit 3; its CRC and sizes stay 0, with no extra field): the
	/// <see cref="LocalVersionLength"/> bytes of its local header from <see cref="LocalVersionAt"/>.
	/// </summary>
	/// <param name="destination">Where they go: at least <see cref="LocalVersionLength"/> bytes.</param>
	/// <param name="entry">The entry, closed, its version and flags set.</param>
	/// <returns>Their length.</returns>
	public static int LocalVersion(Span<byte> destination, ZipEntryStream entry)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(destination, entry.VersionNeeded);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2), entry.Flags);

		return LocalVersionLength;
	}

	/// <summary>Writes a streamed entry's descriptor: its signature, the CRC, and the compressed and uncompressed sizes in 8 bytes each.</summary>
	/// <param name="destination">Where it goes: at least <see cref="DescriptorLength"/> bytes.</param>
	/// <param name="entry">The entry, closed.</param>
	/// <returns>The descriptor's length.</returns>
	public static int Descriptor(Span<byte> destination, ZipEntryStream entry)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x08074B50);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4), entry.Crc);
		BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), (ulong)entry.CompressedSize);
		BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), (ulong)entry.UncompressedSize);

		return DescriptorLength;
	}

	/// <summary>
	/// Writes an entry's central header: its real sizes and offset when each is below <see cref="ZipLimits.Max32"/>,
	/// with no extra field; when any reaches it, all three are <c>0xFFFFFFFF</c> and a Zip64 extra holds all three, in
	/// APPNOTE's order (uncompressed size, compressed size, offset), as readers that take the extra's fields by
	/// position (LibreOffice) read it.
	/// </summary>
	/// <param name="destination">Where it goes: at least <see cref="CentralLength"/>, the name and <see cref="CentralExtraLength"/> bytes.</param>
	/// <param name="entry">The entry, closed.</param>
	/// <param name="limits">The limits.</param>
	/// <returns>The header's length.</returns>
	public static int Central(Span<byte> destination, ZipEntryStream entry, ZipLimits limits)
	{
		var name = entry.Name;
		var extraLength = CentralExtra(entry, limits);
		var wide = extraLength > 0;
		BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x02014B50);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4), MadeBy);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6), entry.VersionNeeded);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8), entry.Flags);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10), entry.Method);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(12), Time);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(14), Date);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(16), entry.Crc);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20), wide ? Wide32 : (uint)entry.CompressedSize);
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24), wide ? Wide32 : (uint)entry.UncompressedSize);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28), (ushort)name.Length);
		BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(30), (ushort)extraLength);

		// The comment's length, the disk, and the internal and external attributes: all 0.
		destination.Slice(32, 10).Clear();
		BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(42), wide ? Wide32 : (uint)entry.Offset);
		WriteName(destination.Slice(CentralLength), name);
		var length = CentralLength + name.Length;
		if (wide)
		{
			var extra = destination.Slice(length, CentralExtraLength);
			BinaryPrimitives.WriteUInt16LittleEndian(extra, 0x0001);
			BinaryPrimitives.WriteUInt16LittleEndian(extra.Slice(2), CentralExtraLength - 4);
			BinaryPrimitives.WriteUInt64LittleEndian(extra.Slice(4), (ulong)entry.UncompressedSize);
			BinaryPrimitives.WriteUInt64LittleEndian(extra.Slice(12), (ulong)entry.CompressedSize);
			BinaryPrimitives.WriteUInt64LittleEndian(extra.Slice(20), (ulong)entry.Offset);
			length += CentralExtraLength;
		}

		return length;
	}

	/// <summary>
	/// The length of an entry's central extra field: <see cref="CentralExtraLength"/>, a Zip64 extra of all three fields,
	/// when any of its uncompressed size, compressed size or offset reaches <see cref="ZipLimits.Max32"/>, or 0 when none
	/// does.
	/// </summary>
	/// <param name="entry">The entry, its sizes and offset known.</param>
	/// <param name="limits">The limits.</param>
	/// <returns>The length.</returns>
	public static int CentralExtra(ZipEntryStream entry, ZipLimits limits)
	{
		var wide = entry.UncompressedSize >= limits.Max32 || entry.CompressedSize >= limits.Max32 || entry.Offset >= limits.Max32;

		return wide ? CentralExtraLength : 0;
	}

	/// <summary>
	/// Writes the end records: the classic end record, after a Zip64 end record and its locator when its entries reach
	/// <see cref="ZipLimits.Max16"/> or the directory's size or offset reaches <see cref="ZipLimits.Max32"/>, those fields,
	/// and only those, all ones.
	/// </summary>
	/// <param name="destination">Where they go: at least <see cref="EndLength"/> bytes.</param>
	/// <param name="entries">The entries.</param>
	/// <param name="directorySize">The central directory's size.</param>
	/// <param name="directoryOffset">The central directory's offset.</param>
	/// <param name="position">The offset of the first end record: right after the directory.</param>
	/// <param name="limits">The limits.</param>
	/// <returns>The records' length.</returns>
	public static int End(Span<byte> destination, long entries, long directorySize, long directoryOffset, long position, ZipLimits limits)
	{
		var wideEntries = entries >= limits.Max16;
		var wideSize = directorySize >= limits.Max32;
		var wideOffset = directoryOffset >= limits.Max32;
		var length = 0;
		if (wideEntries || wideSize || wideOffset)
		{
			// The Zip64 end record, its size counted after its first 12 bytes.
			var record = destination.Slice(0, 56);
			BinaryPrimitives.WriteUInt32LittleEndian(record, 0x06064B50);
			BinaryPrimitives.WriteUInt64LittleEndian(record.Slice(4), 44);
			BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(12), MadeBy);
			BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(14), Version45);
			BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(16), 0);
			BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(20), 0);
			BinaryPrimitives.WriteUInt64LittleEndian(record.Slice(24), (ulong)entries);
			BinaryPrimitives.WriteUInt64LittleEndian(record.Slice(32), (ulong)entries);
			BinaryPrimitives.WriteUInt64LittleEndian(record.Slice(40), (ulong)directorySize);
			BinaryPrimitives.WriteUInt64LittleEndian(record.Slice(48), (ulong)directoryOffset);

			// Its locator: disk 0, its offset, 1 disk.
			var locator = destination.Slice(56, 20);
			BinaryPrimitives.WriteUInt32LittleEndian(locator, 0x07064B50);
			BinaryPrimitives.WriteUInt32LittleEndian(locator.Slice(4), 0);
			BinaryPrimitives.WriteUInt64LittleEndian(locator.Slice(8), (ulong)position);
			BinaryPrimitives.WriteUInt32LittleEndian(locator.Slice(16), 1);
			length = 76;
		}

		var end = destination.Slice(length, 22);
		BinaryPrimitives.WriteUInt32LittleEndian(end, 0x06054B50);
		BinaryPrimitives.WriteUInt16LittleEndian(end.Slice(4), 0);
		BinaryPrimitives.WriteUInt16LittleEndian(end.Slice(6), 0);
		BinaryPrimitives.WriteUInt16LittleEndian(end.Slice(8), wideEntries ? Wide16 : (ushort)entries);
		BinaryPrimitives.WriteUInt16LittleEndian(end.Slice(10), wideEntries ? Wide16 : (ushort)entries);
		BinaryPrimitives.WriteUInt32LittleEndian(end.Slice(12), wideSize ? Wide32 : (uint)directorySize);
		BinaryPrimitives.WriteUInt32LittleEndian(end.Slice(16), wideOffset ? Wide32 : (uint)directoryOffset);
		BinaryPrimitives.WriteUInt16LittleEndian(end.Slice(20), 0);

		return length + 22;
	}

	/// <summary>Writes a name's ASCII bytes.</summary>
	/// <param name="destination">Where they go.</param>
	/// <param name="name">The name, which is ASCII.</param>
	private static void WriteName(Span<byte> destination, string name)
	{
		for (var i = 0; i < name.Length; i++)
		{
			destination[i] = (byte)name[i];
		}
	}
}
