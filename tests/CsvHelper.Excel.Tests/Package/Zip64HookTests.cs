using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Zip64 without 4 GiB: the writer's limits lowered (<see cref="ZipLimits"/>) so that small packages carry each Zip64
/// field, extra and end record in turn and together, which is valid, since a Zip64 field may hold a value that would have
/// fit; each package written to a stream that seeks and to one that doesn't, checked by its records, read back by
/// <see cref="ZipArchive"/> and, on .NET Framework, by WindowsBase. When any of an entry's sizes and offset overflows, its
/// central header has all three all ones and in its Zip64 extra. A stored entry past one buffer whose sizes overflow, on
/// a stream that seeks, has its patched local header patched to the streamed form instead. And a real package of 65,536
/// entries, whose end record overflows for real.
/// </summary>
public class Zip64HookTests
{
	/// <summary>Which of an entry's fields overflow, none, alone and together, synchronously and asynchronously.</summary>
	/// <returns>The overflows and the modes.</returns>
	public static TheoryData<string, bool> Overflows()
	{
		var data = new TheoryData<string, bool>();
		foreach (var overflow in new[] { "none", "uncompressed size", "compressed size", "offset", "both sizes", "uncompressed size and offset", "compressed size and offset", "all three" })
		{
			data.Add(overflow, false);
			data.Add(overflow, true);
		}

		return data;
	}

	/// <summary>
	/// A stored entry past one buffer against the limit, below it, at it and past it, written to a stream that seeks and
	/// one that doesn't, synchronously and asynchronously.
	/// </summary>
	/// <returns>The sizes, the stream kinds and the modes.</returns>
	public static TheoryData<string, bool, bool> Fallbacks()
	{
		var data = new TheoryData<string, bool, bool>();
		foreach (var size in new[] { "below", "at", "past" })
		{
			foreach (var seekable in new[] { true, false })
			{
				data.Add(size, seekable, false);
				data.Add(size, seekable, true);
			}
		}

		return data;
	}

	/// <summary>
	/// An entry, <c>t.bin</c>, whose fields overflow as named, alone or together: its central header has all three fields
	/// all ones and a Zip64 extra (<c>0x0001</c>, size 24) of all three, uncompressed size, compressed size and offset in
	/// that order, holding its real sizes and offset; without an overflow, its real fields and no extra. Its version needed
	/// is 4.5 in both headers, and its local header is as before: whole without an extra field, streamed with its Zip64
	/// extra of zeros.
	/// </summary>
	/// <param name="overflow">The fields that overflow.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Overflows))]
	public async Task WritesAllThreeFieldsWhenAnyOverflows(string overflow, bool async)
	{
		var (parts, level, limits) = await OverflowCaseAsync(overflow);
		var expected = overflow switch
		{
			"none" => (false, false, false),
			"uncompressed size" => (true, false, false),
			"compressed size" => (false, true, false),
			"offset" => (false, false, true),
			"both sizes" => (true, true, false),
			"uncompressed size and offset" => (true, false, true),
			"compressed size and offset" => (false, true, true),
			_ => (true, true, true),
		};

		var (zip, unseekable) = await CheckAsync(parts, level, limits, async);

		var target = parts.Single(p => p.Name == "t.bin");
		foreach (var layout in new[] { zip, unseekable })
		{
			var index = layout.Entries.ToList().FindIndex(e => e.Name == "t.bin");
			var entry = layout.Entries[index];
			Assert.Equal(expected, (entry.UncompressedSize >= limits.Max32, entry.CompressedSize >= limits.Max32, entry.Offset >= limits.Max32));
			Assert.Equal(index == 0 ? 0 : layout.Entries[index - 1].End, entry.Offset);
			Assert.Equal(target.Bytes.Length, entry.UncompressedSize);
			var central = entry.Central;
			if (overflow == "none")
			{
				Assert.Equal(((uint)target.Bytes.Length, (uint)entry.CompressedSize, (uint)entry.Offset), (central.UncompressedField, central.CompressedField, central.OffsetField));
				Assert.Empty(central.Extra);
				Assert.Equal((20, 20), (central.VersionNeeded, entry.Local.VersionNeeded));
				continue;
			}

			Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu), (central.UncompressedField, central.CompressedField, central.OffsetField));
			var extra = central.Extra;
			Assert.Equal((28, (ushort)0x0001, (ushort)24), (extra.Length, BinaryPrimitives.ReadUInt16LittleEndian(extra), BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(2))));
			Assert.Equal(((ulong)target.Bytes.Length, (ulong)entry.CompressedSize, (ulong)entry.Offset), (BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(4)), BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(12)), BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(20))));
			Assert.Equal((45, 45), (central.VersionNeeded, entry.Local.VersionNeeded));
			Assert.Equal(entry.Streamed ? 20 : 0, entry.Local.Extra.Length);
		}

		var t = zip.Entries.Single(e => e.Name == "t.bin");
		using (var archive = new ZipArchive(new MemoryStream(await ZipPackages.WriteAsync(parts, level, new ZipTestStream(), async, 3_000, limits)), ZipArchiveMode.Read))
		{
			Assert.Equal(t.CompressedSize, archive.GetEntry("t.bin")!.CompressedLength);
		}

		switch (overflow)
		{
			case "uncompressed size":
				Assert.True(t.Streamed);
				Assert.Null(zip.End64);
				break;
			case "compressed size":
				Assert.True(t.Streamed);
				Assert.Equal(0xFFFFFFFFu, zip.End.DirectoryOffset);
				break;
			case "both sizes":
				// A stored entry of one buffer or less is written as it closes, streamed with a local Zip64 extra, on
				// either kind of stream: only an entry past one buffer is patched.
				Assert.True(t.Streamed && !t.Patched);
				break;
			case "offset":
				Assert.False(t.Streamed);
				Assert.Equal(20, zip.Entries[0].Central.VersionNeeded);
				break;
		}
	}

	/// <summary>
	/// A stored entry past one buffer whose sizes reach the limit, on a stream that seeks: its local header, written
	/// without bit 3 as its data passed one buffer, is patched to the streamed form as it closes (bit 3, version 4.5, CRC
	/// and sizes 0, still no extra field: a 4-byte write over its version and flags), and a signed descriptor with 8-byte
	/// sizes follows its data; its central header has bit 3, version 4.5, and all three fields in its Zip64 extra. At
	/// exactly the limit too; below it, the entry is patched with its CRC and sizes, without a descriptor. On a stream that
	/// can't seek, it is streamed with a local Zip64 extra of zeros, whatever its size.
	/// </summary>
	/// <param name="size">The entry's size against the limit: below, at or past it.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Fallbacks))]
	public async Task PatchesTheStreamedFormPastTheLimit(string size, bool seekable, bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", Text(1_000)), new("t.bin", ZipPackages.RandomBytes(100_000, 11)), new("c.xml", Text(500)) };
		var limits = new ZipLimits(size switch { "below" => 100_001, "at" => 100_000, _ => 80_000 }, 0xFFFF);
		var stream = new ZipTestStream(seekable) { ForbidSync = async, ForbidAsync = !async };

		var package = await ZipPackages.WriteAsync(parts, CompressionLevel.NoCompression, stream, async, 3_000, limits);

		ZipStructure.AssertValid(package, limits.Max32, limits.Max16);
		ZipPackages.AssertReadsBack(package, parts);
		ZipStructure.AssertPackagingReads(package);
		var zip = ZipStructure.Read(package);
		var t = zip.Entries[1];
		var header = (int)t.Offset;
		var fits = size == "below";
		Assert.Equal((!fits || !seekable, seekable), (t.Streamed, t.Patched));
		if (seekable && fits)
		{
			// Patched: no bit 3, version 2.0, its CRC and sizes, no descriptor; 12 bytes written at the CRC's offset.
			Assert.Equal((20, (ushort)0, 0), (t.Local.VersionNeeded, t.Local.Flags, t.Local.Extra.Length));
			Assert.Equal((System.IO.Hashing.Crc32.HashToUInt32(parts[1].Bytes), 100_000u, 100_000u), (t.Local.Crc, t.Local.CompressedSize, t.Local.UncompressedSize));
			Assert.Null(t.Descriptor);
			Assert.Equal((100_000u, 100_000u), (t.Central.UncompressedField, t.Central.CompressedField));
			AssertPatch(stream, header + 14, 12, async);
		}
		else if (seekable)
		{
			// The fallback: the streamed form without a local extra field; 4 bytes written over the version and flags.
			Assert.Equal((45, (ushort)0x0008, 0), (t.Local.VersionNeeded, t.Local.Flags, t.Local.Extra.Length));
			Assert.Equal((0u, 0u, 0u), (t.Local.Crc, t.Local.CompressedSize, t.Local.UncompressedSize));
			Assert.Equal((true, true, 100_000ul, 100_000ul, 24), (t.Descriptor!.Signed, t.Descriptor.Wide, t.Descriptor.CompressedSize, t.Descriptor.UncompressedSize, t.Descriptor.Length));
			Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(parts[1].Bytes), t.Descriptor.Crc);
			Assert.Equal((45, (ushort)0x0008), (t.Central.VersionNeeded, t.Central.Flags));
			Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 28), (t.Central.UncompressedField, t.Central.CompressedField, t.Central.OffsetField, t.Central.Extra.Length));
			AssertPatch(stream, header + 4, 4, async);
			var patch = stream.Starts.FindIndex(s => s == header + 4);
			Assert.Equal((24, t.Local.DataStart + 100_000), (stream.Writes[patch + 1].Size, stream.Starts[patch + 1]));
		}
		else
		{
			Assert.Equal((45, (ushort)0x0008, 20), (t.Local.VersionNeeded, t.Local.Flags, t.Local.Extra.Length));
			Assert.Equal(24, t.Descriptor!.Length);
			Assert.Empty(stream.Seeks);
		}

		// The entry after it starts where its descriptor, or its data, ends, past the limit: all three in its extra.
		var c = zip.Entries[2];
		Assert.Equal((t.End, 28), (c.Offset, c.Central.Extra.Length));
	}

	/// <summary>
	/// Stored entries written straight through, past the limit: the first's sizes, the second's offset (its sizes below
	/// the limit), then a whole entry's offset and the directory's, every entry with a Zip64 extra holding all three
	/// fields. On a stream that can't seek, both are streamed; on one that seeks, the first, its sizes past the limit, is
	/// patched to the streamed form, and the second is patched with its sizes, at version 4.5 for its offset. The writes
	/// straight through count toward the sizes and offsets as buffers do.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesStoredEntriesWrittenThroughPastTheLimit(bool async)
	{
		var limits = new ZipLimits(150_000, 0xFFFF);
		var parts = new List<ZipPart> { new("a.bin", ZipPackages.RandomBytes(200_003, 11)), new("b.bin", ZipPackages.RandomBytes(100_003, 12)), new("c.xml", Text(1_000)) };

		var layouts = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

		foreach (var (zip, seekable) in new[] { (layouts.Seekable, true), (layouts.Unseekable, false) })
		{
			var (a, b, c) = (zip.Entries[0], zip.Entries[1], zip.Entries[2]);
			Assert.Equal((true, seekable, seekable ? 0 : 20), (a.Streamed, a.Patched, a.Local.Extra.Length));
			Assert.Equal((!seekable, seekable, 45), (b.Streamed, b.Patched, b.Local.VersionNeeded));
			Assert.All(zip.Entries, e => Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 28), (e.Central.UncompressedField, e.Central.CompressedField, e.Central.OffsetField, e.Central.Extra.Length)));
			Assert.Equal((200_003L, 0L), (a.UncompressedSize, a.Offset));
			Assert.Equal((100_003L, a.End), (b.UncompressedSize, b.Offset));
			Assert.False(c.Streamed);
			Assert.Equal((45, 45), (c.Central.VersionNeeded, c.Local.VersionNeeded));
			Assert.Equal(0xFFFFFFFFu, zip.End.DirectoryOffset);
		}
	}

	/// <summary>Entries that reach the 16-bit limit alone: the end record's entries all ones, after a Zip64 end record.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesEntriesPastTheLimit(bool async)
	{
		var limits = new ZipLimits(0xFFFFFFFF, 3);
		var parts = Enumerable.Range(0, 3).Select(i => new ZipPart($"p{i}.xml", Text(100 * i))).ToList();

		var (zip, _) = await CheckAsync(parts, CompressionLevel.Fastest, limits, async);

		Assert.Equal((0xFFFF, 0xFFFF), (zip.End.EntriesOnDisk, zip.End.Entries));
		Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectorySize);
		Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectoryOffset);
		Assert.Equal(4ul, zip.End64!.Entries);
	}

	/// <summary>
	/// A directory whose size alone reaches the limit: empty entries with long names, whose central headers outweigh
	/// their local ones, and the limit between the directory's offset and its size.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesADirectorySizePastTheLimit(bool async)
	{
		var parts = Enumerable.Range(0, 40).Select(i => new ZipPart($"parts/{new string('n', 60)}{i:D2}.bin", [])).ToList();
		var plain = ZipStructure.Read(ZipPackages.Write(parts, CompressionLevel.NoCompression));
		Assert.True(plain.DirectorySize > plain.DirectoryOffset);
		var limits = new ZipLimits((plain.DirectorySize + plain.DirectoryOffset) / 2, 0xFFFF);

		var (zip, _) = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

		Assert.Equal(0xFFFFFFFFu, zip.End.DirectorySize);
		Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectoryOffset);
		Assert.NotEqual(0xFFFF, zip.End.Entries);
		Assert.All(zip.Entries, e => Assert.Empty(e.Central.Extra));
	}

	/// <summary>
	/// A directory whose offset alone reaches the limit: the last entry starts below it and ends past it, and the
	/// directory is smaller.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesADirectoryOffsetPastTheLimit(bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", Text(3_000)), new("b.bin", ZipPackages.RandomBytes(2_000, 5)) };
		var plain = ZipStructure.Read(ZipPackages.Write(parts, CompressionLevel.NoCompression));
		var last = plain.Entries[plain.Entries.Count - 1];
		var limits = new ZipLimits(last.Offset + 1, 0xFFFF);
		Assert.True(plain.DirectoryOffset >= limits.Max32 && plain.DirectorySize < limits.Max32);
		Assert.All(plain.Entries, e => Assert.True(e.CompressedSize < limits.Max32));

		var (zip, _) = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

		Assert.Equal(0xFFFFFFFFu, zip.End.DirectoryOffset);
		Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectorySize);
		Assert.NotEqual(0xFFFF, zip.End.Entries);
		Assert.All(zip.Entries, e => Assert.Empty(e.Central.Extra));
	}

	/// <summary>
	/// Every field past the limits together: every entry's sizes and (past the first) offset overflow, so every entry has
	/// a Zip64 extra of all three fields, and every field of the end record is all ones. Every entry is streamed: stored
	/// entries past one buffer on a stream that seeks fall back to the streamed form, their sizes past the limit.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(CompressionLevel.Fastest, false)]
	[InlineData(CompressionLevel.Fastest, true)]
	[InlineData(CompressionLevel.NoCompression, false)]
	[InlineData(CompressionLevel.NoCompression, true)]
	public async Task WritesEveryFieldPastTheLimits(CompressionLevel level, bool async)
	{
		var limits = new ZipLimits(100, 1);
		var parts = new List<ZipPart> { new("a.bin", ZipPackages.RandomBytes(200, 6)), new("b.xml", Text(100_000)), new("c.bin", ZipPackages.RandomBytes(70_000, 7)) };

		var (seekable, unseekable) = await CheckAsync(parts, level, limits, async);

		foreach (var zip in new[] { seekable, unseekable })
		{
			Assert.Equal((0xFFFF, 0xFFFF, 0xFFFFFFFFu, 0xFFFFFFFFu), (zip.End.EntriesOnDisk, zip.End.Entries, zip.End.DirectorySize, zip.End.DirectoryOffset));
			Assert.All(zip.Entries, e => Assert.True(e.Streamed));
			Assert.All(zip.Entries, e => Assert.Equal(28, e.Central.Extra.Length));
		}

		string[] patched = level == CompressionLevel.NoCompression ? ["b.xml", "c.bin"] : [];
		Assert.Equal(patched, seekable.Entries.Where(e => e.Patched).Select(e => e.Name));
	}

	/// <summary>
	/// Each field at exactly its limit, not past it, goes to Zip64 too, since a classic field of all ones says that the
	/// value is in a Zip64 field. The limit is the value the package has there, and nothing written before that field
	/// reaches it, so the field stays where it was. An entry's field at its limit puts all three of its fields in its
	/// Zip64 extra.
	/// </summary>
	/// <param name="field">The field at its limit.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData("uncompressed size", false)]
	[InlineData("uncompressed size", true)]
	[InlineData("compressed size", false)]
	[InlineData("compressed size", true)]
	[InlineData("offset", false)]
	[InlineData("offset", true)]
	[InlineData("entries", false)]
	[InlineData("entries", true)]
	[InlineData("directory size", false)]
	[InlineData("directory size", true)]
	[InlineData("directory offset", false)]
	[InlineData("directory offset", true)]
	public async Task PutsAFieldAtExactlyItsLimitInZip64(string field, bool async)
	{
		var level = field is "uncompressed size" or "compressed size" ? CompressionLevel.Fastest : CompressionLevel.NoCompression;
		var parts = field switch
		{
			"uncompressed size" => [new ZipPart("a.xml", Text(3_000))],
			"compressed size" => [new ZipPart("a.bin", ZipPackages.RandomBytes(2_000, 8))],
			"directory size" => Enumerable.Range(0, 40).Select(i => new ZipPart($"parts/{new string('n', 60)}{i:D2}.bin", [])).ToList(),
			_ => new List<ZipPart> { new("a.bin", ZipPackages.RandomBytes(100, 9)), new("b.bin", ZipPackages.RandomBytes(50, 10)) },
		};
		var plain = ZipStructure.Read(ZipPackages.Write(parts, level));
		var limits = field switch
		{
			"uncompressed size" => new ZipLimits(plain.Entries[0].UncompressedSize, 0xFFFF),
			"compressed size" => new ZipLimits(plain.Entries[0].CompressedSize, 0xFFFF),
			"offset" => new ZipLimits(plain.Entries[1].Offset, 0xFFFF),
			"entries" => new ZipLimits(0xFFFFFFFF, plain.Entries.Count),
			"directory size" => new ZipLimits(plain.DirectorySize, 0xFFFF),
			_ => new ZipLimits(plain.DirectoryOffset, 0xFFFF),
		};

		var (zip, _) = await CheckAsync(parts, level, limits, async);

		switch (field)
		{
			case "uncompressed size":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[0].UncompressedSize, zip.Entries[0].Central.UncompressedField));
				Assert.True(zip.Entries[0].CompressedSize < limits.Max32);
				Assert.Equal((0xFFFFFFFFu, 28), (zip.Entries[0].Central.CompressedField, zip.Entries[0].Central.Extra.Length));
				break;
			case "compressed size":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[0].CompressedSize, zip.Entries[0].Central.CompressedField));
				Assert.True(zip.Entries[0].UncompressedSize < limits.Max32);
				Assert.Equal((0xFFFFFFFFu, 28), (zip.Entries[0].Central.UncompressedField, zip.Entries[0].Central.Extra.Length));
				break;
			case "offset":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[1].Offset, zip.Entries[1].Central.OffsetField));
				Assert.Equal(28, zip.Entries[1].Central.Extra.Length);
				Assert.Empty(zip.Entries[0].Central.Extra);
				break;
			case "entries":
				Assert.Equal((0xFFFF, 0xFFFF), (zip.End.EntriesOnDisk, zip.End.Entries));
				Assert.Equal((ulong)limits.Max16, zip.End64!.Entries);
				break;
			case "directory size":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.DirectorySize, zip.End.DirectorySize));
				Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectoryOffset);
				break;
			default:
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.DirectoryOffset, zip.End.DirectoryOffset));
				Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectorySize);
				break;
		}
	}

	/// <summary>
	/// A real package of 65,536 entries, tiny parts and the content types: the end record's entries are all ones, and the
	/// Zip64 end record holds them.
	/// </summary>
	[Fact]
	public void WritesTheZip64EndRecordForSixtyFiveThousandFiveHundredThirtySixEntries()
	{
		var parts = Enumerable.Range(0, 65535).Select(i => new ZipPart($"p/{i:D5}.bin", i % 2 == 0 ? [] : [(byte)i])).ToList();

		var package = ZipPackages.Write(parts, CompressionLevel.Fastest);

		ZipStructure.AssertValid(package);
		var zip = ZipStructure.Read(package);
		Assert.Equal((0xFFFF, 0xFFFF), (zip.End.EntriesOnDisk, zip.End.Entries));
		Assert.Equal(65536ul, zip.End64!.Entries);
		using (var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
		{
			Assert.Equal(65536, archive.Entries.Count);
			Assert.Equal([unchecked((byte)12345)], ZipWriterTests.ReadEntry(archive, "p/12345.bin"));
			Assert.Empty(ZipWriterTests.ReadEntry(archive, "p/65534.bin"));
		}

		ZipStructure.AssertPackagingReads(package);
	}

	/// <summary>
	/// Writes a package with lowered limits to a stream that seeks and to one that doesn't, checks that both are valid by
	/// their records under those limits and read back by <see cref="ZipArchive"/> and WindowsBase, and that they have the
	/// same bytes unless a stored entry passes one buffer (patched on the stream that seeks, streamed on the other).
	/// </summary>
	/// <param name="parts">The parts.</param>
	/// <param name="level">The compression.</param>
	/// <param name="limits">The limits.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The packages' records: written to the stream that seeks, and to the one that doesn't.</returns>
	private static async Task<(ZipLayout Seekable, ZipLayout Unseekable)> CheckAsync(List<ZipPart> parts, CompressionLevel level, ZipLimits limits, bool async)
	{
		var package = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(), async, 3_000, limits);
		var unseekable = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(false), async, 3_000, limits);

		var patched = level == CompressionLevel.NoCompression && parts.Any(p => p.Bytes.Length > ZipStructure.WholeData);
		Assert.Equal(!patched, package.AsSpan().SequenceEqual(unseekable));
		foreach (var written in new[] { package, unseekable })
		{
			ZipStructure.AssertValid(written, limits.Max32, limits.Max16);
			ZipPackages.AssertReadsBack(written, parts);
			ZipStructure.AssertPackagingReads(written);
		}

		return (ZipStructure.Read(package), ZipStructure.Read(unseekable));
	}

	/// <summary>
	/// A package whose entry <c>t.bin</c> has the fields named overflow, alone or together, and no other: its parts, level
	/// and limits. Limits that depend on the package are taken from it written with the default ones, where the entries
	/// before <c>t.bin</c> have the same shape.
	/// </summary>
	/// <param name="overflow">The fields that overflow.</param>
	/// <returns>The parts, the level and the limits.</returns>
	private static async Task<(List<ZipPart> Parts, CompressionLevel Level, ZipLimits Limits)> OverflowCaseAsync(string overflow)
	{
		switch (overflow)
		{
			case "none":
				return ([new ZipPart("t.bin", Text(3_000))], CompressionLevel.Fastest, ZipLimits.Default);
			case "uncompressed size":
				// Text that deflates far below the limit.
				return ([new ZipPart("t.bin", Text(20_000))], CompressionLevel.Optimal, new ZipLimits(10_000, 0xFFFF));
			case "compressed size":
			{
				// Random bytes, which deflate to a few bytes more.
				var bytes = ZipPackages.RandomBytes(20_000, 2);
				var compressed = ZipStructure.Read(ZipPackages.Write([new ZipPart("t.bin", bytes)], CompressionLevel.Fastest)).Entries[0].CompressedSize;
				Assert.True(compressed > bytes.Length);

				return ([new ZipPart("t.bin", bytes)], CompressionLevel.Fastest, new ZipLimits(bytes.Length + 1, 0xFFFF));
			}

			case "offset":
				return ([new ZipPart("a.bin", ZipPackages.RandomBytes(990, 4)), new ZipPart("t.bin", ZipPackages.RandomBytes(100, 5))], CompressionLevel.NoCompression, new ZipLimits(1_000, 0xFFFF));
			case "both sizes":
				return ([new ZipPart("t.bin", ZipPackages.RandomBytes(6_000, 3))], CompressionLevel.NoCompression, new ZipLimits(5_000, 0xFFFF));
			case "uncompressed size and offset":
			{
				// Random bytes before it, whole, and text, which deflates below its offset: the limit at its offset.
				List<ZipPart> parts = [new ZipPart("a.bin", ZipPackages.RandomBytes(2_000, 6)), new ZipPart("t.bin", Text(3_000))];
				var plain = ZipStructure.Read(await ZipPackages.WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(), false, 3_000));

				return (parts, CompressionLevel.Fastest, new ZipLimits(plain.Entries[1].Offset, 0xFFFF));
			}

			case "compressed size and offset":
			{
				// Random bytes, after more of them: the limit at its compressed size, above its uncompressed one.
				List<ZipPart> parts = [new ZipPart("a.bin", ZipPackages.RandomBytes(3_000, 7)), new ZipPart("t.bin", ZipPackages.RandomBytes(2_000, 8))];
				var plain = ZipStructure.Read(await ZipPackages.WriteAsync(parts, CompressionLevel.Fastest, new ZipTestStream(), false, 3_000));

				return (parts, CompressionLevel.Fastest, new ZipLimits(plain.Entries[1].CompressedSize, 0xFFFF));
			}

			default:
				return ([new ZipPart("a.bin", ZipPackages.RandomBytes(3_000, 9)), new ZipPart("t.bin", ZipPackages.RandomBytes(2_000, 10))], CompressionLevel.NoCompression, new ZipLimits(2_000, 0xFFFF));
		}
	}

	/// <summary>
	/// Checks a patch of a stored entry's local header: one seek to the bytes patched, one write of them there in the
	/// call's mode, and one seek back to where the next write starts, the package's end.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="at">Where the patch starts.</param>
	/// <param name="size">Its size.</param>
	/// <param name="async">Whether it was written asynchronously.</param>
	private static void AssertPatch(ZipTestStream stream, long at, int size, bool async)
	{
		var patch = stream.Starts.FindIndex(s => s == at);
		Assert.True(patch > 0, $"No write started at {at}.");
		Assert.Equal((size, async), (stream.Writes[patch].Size, stream.Writes[patch].Async));
		Assert.Equal(2, stream.Seeks.Count);
		Assert.Equal(at, stream.Seeks[0]);
		Assert.Equal(stream.Starts[patch - 1] + stream.Writes[patch - 1].Size, stream.Seeks[1]);
		Assert.Equal(stream.Seeks[1], stream.Starts[patch + 1]);
	}

	/// <summary>Text that deflates well.</summary>
	/// <param name="length">Its length.</param>
	/// <returns>Its bytes.</returns>
	private static byte[] Text(int length)
	{
		var text = new StringBuilder(length + 40);
		for (var i = 0; text.Length < length; i++)
		{
			text.Append("<row r=\"").Append(i).Append("\"><c><v>").Append(i % 97).Append("</v></c></row>");
		}

		return Encoding.ASCII.GetBytes(text.ToString(0, length));
	}
}
