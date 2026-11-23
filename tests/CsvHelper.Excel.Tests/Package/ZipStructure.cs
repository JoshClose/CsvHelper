using System.Buffers.Binary;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>A zip's local file header (APPNOTE 4.3.7), its fields as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="VersionNeeded">The version needed to extract.</param>
/// <param name="Flags">The general purpose flags.</param>
/// <param name="Method">The compression method.</param>
/// <param name="Time">The last modification time.</param>
/// <param name="Date">The last modification date.</param>
/// <param name="Crc">The CRC-32 field.</param>
/// <param name="CompressedSize">The compressed size field.</param>
/// <param name="UncompressedSize">The uncompressed size field.</param>
/// <param name="Name">The name's bytes.</param>
/// <param name="Extra">The extra field's bytes.</param>
internal sealed record ZipLocalHeader(long Position, ushort VersionNeeded, ushort Flags, ushort Method, ushort Time, ushort Date, uint Crc, uint CompressedSize, uint UncompressedSize, byte[] Name, byte[] Extra)
{
	/// <summary>Where the entry's data starts: after the header, its name and its extra field.</summary>
	public long DataStart => Position + 30 + Name.Length + Extra.Length;
}

/// <summary>A zip's central directory header (APPNOTE 4.3.12), its fields as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="MadeBy">The version made by.</param>
/// <param name="VersionNeeded">The version needed to extract.</param>
/// <param name="Flags">The general purpose flags.</param>
/// <param name="Method">The compression method.</param>
/// <param name="Time">The last modification time.</param>
/// <param name="Date">The last modification date.</param>
/// <param name="Crc">The CRC-32.</param>
/// <param name="CompressedField">The compressed size field: the size, or <c>0xFFFFFFFF</c> for the Zip64 extra's.</param>
/// <param name="UncompressedField">The uncompressed size field: the size, or <c>0xFFFFFFFF</c> for the Zip64 extra's.</param>
/// <param name="Disk">The disk number start.</param>
/// <param name="InternalAttributes">The internal file attributes.</param>
/// <param name="ExternalAttributes">The external file attributes.</param>
/// <param name="OffsetField">The local header's offset field: the offset, or <c>0xFFFFFFFF</c> for the Zip64 extra's.</param>
/// <param name="Name">The name's bytes.</param>
/// <param name="Extra">The extra field's bytes.</param>
/// <param name="Comment">The comment's bytes.</param>
internal sealed record ZipCentralHeader(long Position, ushort MadeBy, ushort VersionNeeded, ushort Flags, ushort Method, ushort Time, ushort Date, uint Crc, uint CompressedField, uint UncompressedField,
	ushort Disk, ushort InternalAttributes, uint ExternalAttributes, uint OffsetField, byte[] Name, byte[] Extra, byte[] Comment)
{
	/// <summary>The header's length: its fixed fields, name, extra field and comment.</summary>
	public long Length => 46 + Name.Length + Extra.Length + Comment.Length;
}

/// <summary>A zip entry's data descriptor (APPNOTE 4.3.9), as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="Signed">Whether it starts with the signature <c>50 4B 07 08</c>.</param>
/// <param name="Crc">The CRC-32.</param>
/// <param name="CompressedSize">The compressed size.</param>
/// <param name="UncompressedSize">The uncompressed size.</param>
/// <param name="Wide">Whether its sizes are 8 bytes each, as they are read when the local header has a Zip64 extra.</param>
internal sealed record ZipDescriptor(long Position, bool Signed, uint Crc, ulong CompressedSize, ulong UncompressedSize, bool Wide)
{
	/// <summary>The descriptor's length.</summary>
	public int Length => (Signed ? 4 : 0) + 4 + (Wide ? 16 : 8);
}

/// <summary>A zip entry: its central header, its local header and its descriptor, with its sizes and offset as meant.</summary>
/// <param name="Name">The name, decoded as ASCII (any other byte as <c>?</c>).</param>
/// <param name="Central">The central header.</param>
/// <param name="Local">The local header.</param>
/// <param name="Descriptor">The descriptor, or <see langword="null"/> for an entry without bit 3.</param>
/// <param name="CompressedSize">The compressed size: the central field's, or its Zip64 extra's.</param>
/// <param name="UncompressedSize">The uncompressed size: the central field's, or its Zip64 extra's.</param>
/// <param name="Offset">The local header's offset: the central field's, or its Zip64 extra's.</param>
internal sealed record ZipEntryRecords(string Name, ZipCentralHeader Central, ZipLocalHeader Local, ZipDescriptor? Descriptor, long CompressedSize, long UncompressedSize, long Offset)
{
	/// <summary>Whether the entry is streamed: bit 3, sizes in a descriptor after its data.</summary>
	public bool Streamed => (Central.Flags & 0x0008) != 0;

	/// <summary>
	/// Whether the entry's local header was patched after its data: a stored entry past one buffer, written to a stream
	/// that seeks, without a local extra field. Its local header holds its CRC and sizes, without bit 3; or, when a size
	/// reaches Max32, it was patched to the streamed form (bit 3, version 4.5, CRC and sizes 0), and a descriptor follows.
	/// </summary>
	public bool Patched => Central.Method == 0 && CompressedSize > ZipStructure.WholeData && Local.Extra.Length == 0;

	/// <summary>Where the entry ends: after its data and descriptor.</summary>
	public long End => Local.DataStart + CompressedSize + (Descriptor?.Length ?? 0);
}

/// <summary>A zip's end of central directory record (APPNOTE 4.3.16), as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="Disk">The number of this disk.</param>
/// <param name="DirectoryDisk">The disk the directory starts on.</param>
/// <param name="EntriesOnDisk">The entries on this disk.</param>
/// <param name="Entries">The entries.</param>
/// <param name="DirectorySize">The directory's size.</param>
/// <param name="DirectoryOffset">The directory's offset.</param>
/// <param name="CommentLength">The comment's length.</param>
internal sealed record ZipEndRecord(long Position, ushort Disk, ushort DirectoryDisk, ushort EntriesOnDisk, ushort Entries, uint DirectorySize, uint DirectoryOffset, ushort CommentLength);

/// <summary>A zip's Zip64 end of central directory record (APPNOTE 4.3.14), as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="RecordSize">The size of the record after this field.</param>
/// <param name="MadeBy">The version made by.</param>
/// <param name="VersionNeeded">The version needed to extract.</param>
/// <param name="Disk">The number of this disk.</param>
/// <param name="DirectoryDisk">The disk the directory starts on.</param>
/// <param name="EntriesOnDisk">The entries on this disk.</param>
/// <param name="Entries">The entries.</param>
/// <param name="DirectorySize">The directory's size.</param>
/// <param name="DirectoryOffset">The directory's offset.</param>
internal sealed record Zip64EndRecord(long Position, ulong RecordSize, ushort MadeBy, ushort VersionNeeded, uint Disk, uint DirectoryDisk, ulong EntriesOnDisk, ulong Entries, ulong DirectorySize, ulong DirectoryOffset);

/// <summary>A zip's Zip64 end of central directory locator (APPNOTE 4.3.15), as written.</summary>
/// <param name="Position">Where it starts.</param>
/// <param name="Disk">The disk the Zip64 end record is on.</param>
/// <param name="EndOffset">The Zip64 end record's offset.</param>
/// <param name="Disks">The total number of disks.</param>
internal sealed record Zip64Locator(long Position, uint Disk, ulong EndOffset, uint Disks);

/// <summary>A zip's records, read from its end: the end records, then every entry's.</summary>
/// <param name="Length">The package's length.</param>
/// <param name="End">The end of central directory record.</param>
/// <param name="Locator">The Zip64 locator, or <see langword="null"/> without one.</param>
/// <param name="End64">The Zip64 end record, or <see langword="null"/> without one.</param>
/// <param name="DirectoryOffset">The directory's offset: the end record's, or the Zip64 end record's where the end record's is all ones.</param>
/// <param name="DirectorySize">The directory's size, likewise.</param>
/// <param name="Entries">The entries, in the directory's order.</param>
internal sealed record ZipLayout(long Length, ZipEndRecord End, Zip64Locator? Locator, Zip64EndRecord? End64, long DirectoryOffset, long DirectorySize, IReadOnlyList<ZipEntryRecords> Entries);

/// <summary>
/// The zip structure of written packages, read by hand from their records and checked against the rules the own zip
/// writer keeps (APPNOTE 6.3.10, and what Excel, WindowsBase, EPPlus, POI from a stream and LibreOffice refuse), and
/// opened with System.IO.Packaging's <c>Package</c> on .NET Framework, the WindowsBase that the Open XML SDK and ClosedXML
/// read with there.
/// </summary>
internal static class ZipStructure
{
	/// <summary>The most data a whole entry has: one buffer.</summary>
	public const int WholeData = 65536;

	/// <summary>The default 32-bit limit: a size or offset at or past it takes a Zip64 field.</summary>
	public const long Max32 = 0xFFFFFFFF;

	/// <summary>The default 16-bit limit: entries at or past it take the Zip64 end record.</summary>
	public const long Max16 = 0xFFFF;

	/// <summary>A local header's signature, <c>50 4B 03 04</c>.</summary>
	private const uint LocalSignature = 0x04034B50;

	/// <summary>A central header's signature, <c>50 4B 01 02</c>.</summary>
	private const uint CentralSignature = 0x02014B50;

	/// <summary>A descriptor's signature, <c>50 4B 07 08</c>.</summary>
	private const uint DescriptorSignature = 0x08074B50;

	/// <summary>The end record's signature, <c>50 4B 05 06</c>.</summary>
	private const uint EndSignature = 0x06054B50;

	/// <summary>The Zip64 end record's signature, <c>50 4B 06 06</c>.</summary>
	private const uint End64Signature = 0x06064B50;

	/// <summary>The Zip64 locator's signature, <c>50 4B 06 07</c>.</summary>
	private const uint LocatorSignature = 0x07064B50;

	/// <summary>Checks a package's structure against every rule, with the default limits.</summary>
	/// <param name="package">The package's bytes.</param>
	public static void AssertValid(byte[] package) => AssertValid(package, Max32, Max16);

	/// <summary>Checks a package's structure against every rule, with the default limits.</summary>
	/// <param name="package">A stream that seeks, the package from its position 0.</param>
	public static void AssertValid(Stream package) => AssertValid(package, Max32, Max16);

	/// <summary>Checks a package's structure against every rule, with the limits it was written with.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <param name="max32">The 32-bit limit the package was written with.</param>
	/// <param name="max16">The 16-bit limit the package was written with.</param>
	public static void AssertValid(byte[] package, long max32, long max16)
	{
		using var stream = new MemoryStream(package, false);
		AssertValid(stream, max32, max16);
	}

	/// <summary>
	/// Checks a package's structure against every rule, with the limits it was written with: the end records read from
	/// the end, then every central header, local header and descriptor, without reading the data (its sizes are the
	/// central directory's). A broken rule fails with a message naming the entry, the field and the rule. A package's
	/// stored entries past one buffer are all patched (written to a stream that seeks) or all streamed with a local Zip64
	/// extra (to one that can't), since a package is written to one stream.
	/// </summary>
	/// <param name="package">A stream that seeks, the package from its position 0.</param>
	/// <param name="max32">The 32-bit limit the package was written with.</param>
	/// <param name="max16">The 16-bit limit the package was written with.</param>
	public static void AssertValid(Stream package, long max32, long max16)
	{
		var zip = Read(package);
		CheckEnd(zip, max32, max16);
		var expected = 0L;
		foreach (var entry in zip.Entries)
		{
			CheckCentral(entry, max32);
			CheckLocal(entry, max32);
			Check(entry.Offset == expected, entry.Name, "local header offset", "each entry starts where the one before ends, counted from the package's first byte");
			expected = entry.End;
		}

		Check(zip.DirectoryOffset == expected, "central directory", "offset", "the directory starts where the last entry ends");
		var kinds = zip.Entries.Where(e => e.Central.Method == 0 && e.CompressedSize > WholeData).Select(e => e.Patched).Distinct().Count();
		Check(kinds <= 1, "package", "stored entries past one buffer", "all patched (no local extra field), written to a stream that seeks, or all streamed with a local Zip64 extra, to one that can't");
	}

	/// <summary>
	/// On .NET Framework, opens a package with WindowsBase's <c>System.IO.Packaging.Package</c> and reads every part
	/// through to its end; elsewhere, does nothing.
	/// </summary>
	/// <param name="package">The package's bytes.</param>
	public static void AssertPackagingReads(byte[] package)
	{
#if NETFRAMEWORK
		using var stream = new MemoryStream(package, false);
		using var opened = System.IO.Packaging.Package.Open(stream, FileMode.Open, FileAccess.Read);
		ReadParts(opened);
#endif
	}

	/// <summary>
	/// On .NET Framework, opens a package file with WindowsBase's <c>System.IO.Packaging.Package</c> and reads every part
	/// through to its end; elsewhere, does nothing.
	/// </summary>
	/// <param name="path">The package's path.</param>
	public static void AssertPackagingReads(string path)
	{
#if NETFRAMEWORK
		using var opened = System.IO.Packaging.Package.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		ReadParts(opened);
#endif
	}

	/// <summary>Reads a package's records.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <returns>Its records.</returns>
	public static ZipLayout Read(byte[] package)
	{
		using var stream = new MemoryStream(package, false);

		return Read(stream);
	}

	/// <summary>
	/// Reads a package's records: its end record (from the end, past a comment if it has one), its Zip64 locator and end
	/// record if it has them, then each central header, the local header it points at and, for an entry with bit 3, the
	/// descriptor after its data, whose sizes are read as 8 bytes each when the local header has a Zip64 extra or needs
	/// version 4.5, as WindowsBase reads them. A record that isn't where its signature should be fails.
	/// </summary>
	/// <param name="package">A stream that seeks, the package from its position 0.</param>
	/// <returns>Its records.</returns>
	public static ZipLayout Read(Stream package)
	{
		var length = package.Length;
		var end = ReadEnd(package, length);
		Zip64Locator? locator = null;
		Zip64EndRecord? end64 = null;
		if (end.Position >= 20 && BinaryPrimitives.ReadUInt32LittleEndian(ReadAt(package, end.Position - 20, 4, "Zip64 locator")) == LocatorSignature)
		{
			var bytes = ReadAt(package, end.Position - 20, 20, "Zip64 locator");
			locator = new Zip64Locator(end.Position - 20, U32(bytes, 4), U64(bytes, 8), U32(bytes, 16));
			var at = checked((long)locator.EndOffset);
			var record = ReadAt(package, at, 56, "Zip64 end record");
			Check(U32(record, 0) == End64Signature, "Zip64 end record", "signature", "the locator points at a Zip64 end record");
			end64 = new Zip64EndRecord(at, U64(record, 4), U16(record, 12), U16(record, 14), U32(record, 16), U32(record, 20), U64(record, 24), U64(record, 32), U64(record, 40), U64(record, 48));
		}

		var count = end.Entries == 0xFFFF && end64 is not null ? checked((long)end64.Entries) : end.Entries;
		var directorySize = end.DirectorySize == 0xFFFFFFFF && end64 is not null ? checked((long)end64.DirectorySize) : end.DirectorySize;
		var directoryOffset = end.DirectoryOffset == 0xFFFFFFFF && end64 is not null ? checked((long)end64.DirectoryOffset) : end.DirectoryOffset;
		var entries = new List<ZipEntryRecords>((int)Math.Min(count, 1 << 20));
		var position = directoryOffset;
		for (var i = 0L; i < count; i++)
		{
			var central = ReadCentral(package, position, i);
			entries.Add(ReadEntry(package, central));
			position += central.Length;
		}

		Check(position == directoryOffset + directorySize, "central directory", "size", "the directory's headers fill exactly its size");

		return new ZipLayout(length, end, locator, end64, directoryOffset, directorySize, entries);
	}

	/// <summary>Checks the end records: the classic one, and the Zip64 end record and locator exactly when it overflows.</summary>
	/// <param name="zip">The records.</param>
	/// <param name="max32">The 32-bit limit.</param>
	/// <param name="max16">The 16-bit limit.</param>
	private static void CheckEnd(ZipLayout zip, long max32, long max16)
	{
		var end = zip.End;
		var count = zip.Entries.Count;
		Check(end.Position == zip.Length - 22 && end.CommentLength == 0, "end record", "position and comment length", "the end record is the package's last 22 bytes, without a comment");
		Check(end.Disk == 0 && end.DirectoryDisk == 0, "end record", "disk numbers", "disk numbers are 0");
		Check(end.EntriesOnDisk == end.Entries, "end record", "entries on this disk", "the entries on this disk are the entries");
		Check((end.Entries == 0xFFFF) == (count >= max16), "end record", "entries", $"the entries are 0xFFFF exactly when they reach Max16 ({max16}); there are {count}, written {end.Entries}");
		Check(end.Entries == 0xFFFF || end.Entries == count, "end record", "entries", $"the entries are the directory's {count}, written {end.Entries}");
		Check((end.DirectorySize == 0xFFFFFFFF) == (zip.DirectorySize >= max32), "end record", "directory size", $"the size is 0xFFFFFFFF exactly when it reaches Max32 ({max32}); it is {zip.DirectorySize}");
		Check((end.DirectoryOffset == 0xFFFFFFFF) == (zip.DirectoryOffset >= max32), "end record", "directory offset", $"the offset is 0xFFFFFFFF exactly when it reaches Max32 ({max32}); it is {zip.DirectoryOffset}");
		var saturated = end.Entries == 0xFFFF || end.DirectorySize == 0xFFFFFFFF || end.DirectoryOffset == 0xFFFFFFFF;
		Check(saturated == (zip.End64 is not null), "end record", "Zip64 end record", "a Zip64 end record and locator come exactly when a field of the end record is all ones");
		Check((zip.Locator is null) == (zip.End64 is null), "end record", "Zip64 locator", "a Zip64 end record comes with its locator");
		if (zip.End64 is { } end64 && zip.Locator is { } locator)
		{
			Check(end64.RecordSize == 44, "Zip64 end record", "size of record", "the record's size is 44");
			Check(end64.MadeBy == 0x002D && end64.VersionNeeded == 45, "Zip64 end record", "versions", "made by 0x002D and version needed 45");
			Check(end64.Disk == 0 && end64.DirectoryDisk == 0, "Zip64 end record", "disk numbers", "disk numbers are 0");
			Check(end64.EntriesOnDisk == end64.Entries && end64.Entries == (ulong)count, "Zip64 end record", "entries", $"the entries on this disk and in all are the directory's {count}");
			Check(end64.DirectorySize == (ulong)zip.DirectorySize && end64.DirectoryOffset == (ulong)zip.DirectoryOffset, "Zip64 end record", "directory size and offset", "the directory's real size and offset");
			Check(end.Entries == 0xFFFF || end.Entries == (long)end64.Entries, "end record", "entries", "a field that isn't all ones equals the Zip64 end record's");
			Check(end.DirectorySize == 0xFFFFFFFF || end.DirectorySize == (long)end64.DirectorySize, "end record", "directory size", "a field that isn't all ones equals the Zip64 end record's");
			Check(end.DirectoryOffset == 0xFFFFFFFF || end.DirectoryOffset == (long)end64.DirectoryOffset, "end record", "directory offset", "a field that isn't all ones equals the Zip64 end record's");
			Check(locator.Disk == 0 && locator.Disks == 1, "Zip64 locator", "disks", "disk 0 of 1 disk");
			Check(locator.Position == end.Position - 20 && (long)locator.EndOffset == end64.Position && end64.Position + 56 == locator.Position, "Zip64 locator", "position and offset", "the Zip64 end record, its locator and the end record come in turn, the locator pointing at the Zip64 end record");
		}

		var recordsStart = zip.End64?.Position ?? end.Position;
		Check(zip.DirectoryOffset + zip.DirectorySize == recordsStart, "central directory", "size", "the directory ends where the end records start");
	}

	/// <summary>Checks an entry's central header.</summary>
	/// <param name="entry">The entry.</param>
	/// <param name="max32">The 32-bit limit.</param>
	private static void CheckCentral(ZipEntryRecords entry, long max32)
	{
		var central = entry.Central;
		var name = entry.Name;
		Check(central.MadeBy == 0x002D, name, "version made by", "made by 0x002D (4.5, host 0)");
		Check(central.Time == 0 && central.Date == 0x0021, name, "time and date", "time 0x0000 and date 0x0021 (1980-01-01)");
		Check(central.Disk == 0 && central.InternalAttributes == 0 && central.ExternalAttributes == 0, name, "disk and attributes", "disk 0 and attributes 0");
		Check(central.Comment.Length == 0, name, "comment", "no comment");
		Check(central.Name.All(b => b < 0x80), name, "name", "names are ASCII");
		Check(central.Method is 0 or 8, name, "method", "stored (0) or deflated (8)");
		var level = central.Flags & ~0x0008;
		Check(central.Method == 8 ? level is 0x0000 or 0x0002 or 0x0006 : level == 0, name, "flags", "bits 1-2 only with deflate (0x0000, 0x0002 or 0x0006), bit 3 for a streamed entry, no other bit (no UTF-8 flag)");
		Check(central.Method != 0 || entry.CompressedSize == entry.UncompressedSize, name, "sizes", "a stored entry's sizes are equal");

		// When any of the sizes and offset reaches Max32, all three are all ones and the Zip64 extra holds all three, in
		// APPNOTE's order, since LibreOffice reads the extra's fields by position; no extra field when none does.
		var extra = central.Extra;
		var wide = entry.UncompressedSize >= max32 || entry.CompressedSize >= max32 || entry.Offset >= max32;
		if (wide)
		{
			Check(central.UncompressedField == 0xFFFFFFFF && central.CompressedField == 0xFFFFFFFF && central.OffsetField == 0xFFFFFFFF, name, "central sizes and offset",
				$"all three are 0xFFFFFFFF when any reaches Max32 ({max32}); written 0x{central.UncompressedField:X8}, 0x{central.CompressedField:X8} and 0x{central.OffsetField:X8}");
			Check(extra.Length == 28 && U16(extra, 0) == 0x0001 && U16(extra, 2) == 24, name, "central extra field", "one Zip64 extra (0x0001, size 24) of all three fields when any reaches Max32");
			Check((long)U64(extra, 4) == entry.UncompressedSize && (long)U64(extra, 12) == entry.CompressedSize && (long)U64(extra, 20) == entry.Offset, name, "central extra field", "the Zip64 extra holds the uncompressed size, compressed size and offset, in that order");
		}
		else
		{
			Check(central.UncompressedField == entry.UncompressedSize && central.CompressedField == entry.CompressedSize && central.OffsetField == entry.Offset, name, "central sizes and offset", "the real sizes and offset when none reaches Max32");
			Check(extra.Length == 0, name, "central extra field", "no extra field when no field reaches Max32");
		}

		Check(central.VersionNeeded == (entry.Streamed || extra.Length > 0 ? 45 : 20), name, "version needed", "45 for a streamed entry or one with a Zip64 extra, else 20");

		// Whole: at most one buffer of data, both sizes below Max32. A stored entry past one buffer, its sizes below Max32,
		// is patched on a stream that seeks (no bit 3) and streamed on one that can't; any other entry is streamed.
		var small = entry.CompressedSize <= WholeData;
		var fits = entry.CompressedSize < max32 && entry.UncompressedSize < max32;
		if (entry.Streamed)
		{
			Check(!small || !fits, name, "flags (bit 3)", $"an entry of at most {WholeData} bytes of data with both sizes below Max32 is whole, without bit 3; it is streamed with {entry.CompressedSize} bytes of {entry.UncompressedSize}");
		}
		else
		{
			Check(fits && (small || central.Method == 0), name, "flags (bit 3)", $"an entry without bit 3 has both sizes below Max32, and at most {WholeData} bytes of data unless stored (patched, on a stream that seeks); it has {entry.CompressedSize} bytes of {entry.UncompressedSize}");
		}
	}

	/// <summary>Checks an entry's local header and descriptor against its central header.</summary>
	/// <param name="entry">The entry.</param>
	/// <param name="max32">The 32-bit limit.</param>
	private static void CheckLocal(ZipEntryRecords entry, long max32)
	{
		var central = entry.Central;
		var local = entry.Local;
		var name = entry.Name;
		Check(local.VersionNeeded == central.VersionNeeded, name, "local version needed", "the local and central headers' versions needed agree");
		Check(local.Flags == central.Flags && local.Method == central.Method, name, "local flags and method", "the local header's flags and method are the central header's");
		Check(local.Time == central.Time && local.Date == central.Date, name, "local time and date", "the local header's time and date are the central header's");
		Check(local.Name.AsSpan().SequenceEqual(central.Name), name, "local name", "the local header's name is the central header's");
		if (!entry.Streamed)
		{
			Check(local.Crc == central.Crc && local.CompressedSize == entry.CompressedSize && local.UncompressedSize == entry.UncompressedSize, name, "local CRC and sizes", "a whole or patched entry's local header holds its CRC and sizes");
			Check(local.Extra.Length == 0, name, "local extra field", "a whole or patched entry's local header has no extra field");

			return;
		}

		Check(local.Crc == 0 && local.CompressedSize == 0 && local.UncompressedSize == 0, name, "local CRC and sizes", "a streamed entry's local CRC and sizes are 0, not 0xFFFFFFFF");
		if (local.Extra.Length == 0)
		{
			Check(entry.Patched && (entry.CompressedSize >= max32 || entry.UncompressedSize >= max32), name, "local extra field",
				"only a patched entry (stored, past one buffer, on a stream that seeks) whose sizes reach Max32 is streamed without a local extra field, its header patched to the streamed form");
		}
		else
		{
			Check(local.Extra.Length == 20 && U16(local.Extra, 0) == 0x0001 && U16(local.Extra, 2) == 16 && U64(local.Extra, 4) == 0 && U64(local.Extra, 12) == 0, name, "local extra field", "a streamed entry's local header has a Zip64 extra of zeros (0x0001, size 16)");
		}

		var descriptor = entry.Descriptor!;
		Check(descriptor.Signed, name, "descriptor signature", "every descriptor is signed 50 4B 07 08");
		Check(descriptor.Wide, name, "descriptor sizes", "a descriptor's sizes are 8 bytes each");
		Check(descriptor.Crc == central.Crc && (long)descriptor.CompressedSize == entry.CompressedSize && (long)descriptor.UncompressedSize == entry.UncompressedSize, name, "descriptor CRC and sizes", "the descriptor holds the central header's CRC and sizes");
	}

	/// <summary>Fails with a message naming the entry, the field and the rule, unless a rule holds.</summary>
	/// <param name="holds">Whether the rule holds.</param>
	/// <param name="entry">The entry or record checked.</param>
	/// <param name="field">The field checked.</param>
	/// <param name="rule">The rule.</param>
	private static void Check(bool holds, string entry, string field, string rule)
	{
		if (!holds)
		{
			Fail(entry, field, rule);
		}
	}

	/// <summary>Fails with a message naming the entry, the field and the rule.</summary>
	/// <param name="entry">The entry or record checked.</param>
	/// <param name="field">The field checked.</param>
	/// <param name="rule">The rule, with what was found.</param>
	private static void Fail(string entry, string field, string rule) => Assert.Fail($"{entry}: {field}: {rule}.");

	/// <summary>Reads the end record: the package's last 22 bytes, or before a comment.</summary>
	/// <param name="package">The package.</param>
	/// <param name="length">Its length.</param>
	/// <returns>The record.</returns>
	private static ZipEndRecord ReadEnd(Stream package, long length)
	{
		Check(length >= 22, "end record", "length", "a zip ends with a 22-byte end record");
		var tail = ReadAt(package, Math.Max(0, length - 22 - 0xFFFF), (int)Math.Min(length, 22 + 0xFFFF), "end record");
		for (var i = tail.Length - 22; i >= 0; i--)
		{
			if (U32(tail, i) == EndSignature)
			{
				var position = length - tail.Length + i;

				return new ZipEndRecord(position, U16(tail, i + 4), U16(tail, i + 6), U16(tail, i + 8), U16(tail, i + 10), U32(tail, i + 12), U32(tail, i + 16), U16(tail, i + 20));
			}
		}

		Assert.Fail("end record: signature: a zip ends with an end record (50 4B 05 06).");

		return null!;
	}

	/// <summary>Reads a central header.</summary>
	/// <param name="package">The package.</param>
	/// <param name="position">Where it starts.</param>
	/// <param name="index">Its place in the directory, for messages.</param>
	/// <returns>The header.</returns>
	private static ZipCentralHeader ReadCentral(Stream package, long position, long index)
	{
		var bytes = ReadAt(package, position, 46, $"central header {index}");
		Check(U32(bytes, 0) == CentralSignature, $"central header {index}", "signature", "the directory's headers start 50 4B 01 02");
		var nameLength = U16(bytes, 28);
		var extraLength = U16(bytes, 30);
		var commentLength = U16(bytes, 32);
		var rest = ReadAt(package, position + 46, nameLength + extraLength + commentLength, $"central header {index}");

		return new ZipCentralHeader(position, U16(bytes, 4), U16(bytes, 6), U16(bytes, 8), U16(bytes, 10), U16(bytes, 12), U16(bytes, 14), U32(bytes, 16), U32(bytes, 20), U32(bytes, 24),
			U16(bytes, 34), U16(bytes, 36), U32(bytes, 38), U32(bytes, 42), rest.AsSpan(0, nameLength).ToArray(), rest.AsSpan(nameLength, extraLength).ToArray(), rest.AsSpan(nameLength + extraLength).ToArray());
	}

	/// <summary>Reads an entry's local header and descriptor, its sizes and offset taken from the central header.</summary>
	/// <param name="package">The package.</param>
	/// <param name="central">The entry's central header.</param>
	/// <returns>The entry.</returns>
	private static ZipEntryRecords ReadEntry(Stream package, ZipCentralHeader central)
	{
		var name = new string(central.Name.Select(b => b < 0x80 ? (char)b : '?').ToArray());

		// The Zip64 extra's fields, in order, for the central fields that are all ones.
		var extra = Zip64Fields(central.Extra);
		var next = 0;
		long Wide(uint field)
		{
			if (field != 0xFFFFFFFF)
			{
				return field;
			}

			Check(next < extra.Count, name, "central extra field", "a field that is all ones has its value in the Zip64 extra");

			return extra[next++];
		}

		var uncompressed = Wide(central.UncompressedField);
		var compressed = Wide(central.CompressedField);
		var offset = Wide(central.OffsetField);
		var bytes = ReadAt(package, offset, 30, name);
		Check(U32(bytes, 0) == LocalSignature, name, "local header signature", "the central header's offset points at a local header (50 4B 03 04)");
		var nameLength = U16(bytes, 26);
		var extraLength = U16(bytes, 28);
		var rest = ReadAt(package, offset + 30, nameLength + extraLength, name);
		var local = new ZipLocalHeader(offset, U16(bytes, 4), U16(bytes, 6), U16(bytes, 8), U16(bytes, 10), U16(bytes, 12), U32(bytes, 14), U32(bytes, 18), U32(bytes, 22),
			rest.AsSpan(0, nameLength).ToArray(), rest.AsSpan(nameLength).ToArray());
		ZipDescriptor? descriptor = null;
		if ((central.Flags & 0x0008) != 0)
		{
			var at = local.DataStart + compressed;
			var zip64 = Zip64Fields(local.Extra, true).Count > 0 || local.VersionNeeded >= 45;
			var signed = U32(ReadAt(package, at, 4, name), 0) == DescriptorSignature;
			var fields = ReadAt(package, at + (signed ? 4 : 0), zip64 ? 20 : 12, name);
			descriptor = zip64
				? new ZipDescriptor(at, signed, U32(fields, 0), U64(fields, 4), U64(fields, 12), true)
				: new ZipDescriptor(at, signed, U32(fields, 0), U32(fields, 4), U32(fields, 8), false);
		}

		return new ZipEntryRecords(name, central, local, descriptor, compressed, uncompressed, offset);
	}

	/// <summary>The 8-byte fields of an extra field's Zip64 block, if it has one.</summary>
	/// <param name="extra">The extra field.</param>
	/// <param name="any">Whether to give one field for a block whatever its size, to tell that the block is there.</param>
	/// <returns>The fields.</returns>
	private static List<long> Zip64Fields(byte[] extra, bool any = false)
	{
		var fields = new List<long>();
		for (var i = 0; i + 4 <= extra.Length;)
		{
			var id = U16(extra, i);
			var size = U16(extra, i + 2);
			if (id == 0x0001)
			{
				if (any)
				{
					fields.Add(0);
				}
				else
				{
					for (var j = 0; j + 8 <= size && i + 4 + j + 8 <= extra.Length; j += 8)
					{
						fields.Add((long)U64(extra, i + 4 + j));
					}
				}

				break;
			}

			i += 4 + size;
		}

		return fields;
	}

	/// <summary>Reads bytes at a position, failing if the package ends first.</summary>
	/// <param name="package">The package.</param>
	/// <param name="position">Where the bytes start.</param>
	/// <param name="count">How many.</param>
	/// <param name="record">The record read, for the message.</param>
	/// <returns>The bytes.</returns>
	private static byte[] ReadAt(Stream package, long position, int count, string record)
	{
		Check(position >= 0 && position + count <= package.Length, record, "position", $"its {count} bytes at {position} lie inside the package's {package.Length}");
		package.Position = position;
		var bytes = new byte[count];
		var read = 0;
		while (read < count)
		{
			var n = package.Read(bytes, read, count - read);
			Check(n > 0, record, "position", "the package doesn't end inside a record");
			read += n;
		}

		return bytes;
	}

#if NETFRAMEWORK
	/// <summary>Reads every part of a package through to its end.</summary>
	/// <param name="package">The package.</param>
	private static void ReadParts(System.IO.Packaging.Package package)
	{
		var buffer = new byte[81920];
		foreach (var part in package.GetParts())
		{
			using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
			while (stream.Read(buffer, 0, buffer.Length) > 0)
			{
			}
		}
	}
#endif

	/// <summary>Reads a little-endian 16-bit field.</summary>
	/// <param name="bytes">The record's bytes.</param>
	/// <param name="offset">The field's offset in them.</param>
	/// <returns>The field.</returns>
	private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));

	/// <summary>Reads a little-endian 32-bit field.</summary>
	/// <param name="bytes">The record's bytes.</param>
	/// <param name="offset">The field's offset in them.</param>
	/// <returns>The field.</returns>
	private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

	/// <summary>Reads a little-endian 64-bit field.</summary>
	/// <param name="bytes">The record's bytes.</param>
	/// <param name="offset">The field's offset in them.</param>
	/// <returns>The field.</returns>
	private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
}
