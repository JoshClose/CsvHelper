using System.IO.Compression;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Stored entries past one buffer on a stream that seeks: each local header, written without bit 3 or an extra field as
/// the entry's data passes one buffer, gets its real CRC and sizes as the entry closes, from one seek to them, one write
/// of 12 bytes in the call's mode and one seek back, so that no descriptor follows (POI from a stream refuses a stored
/// entry with one). Every other entry is as before: whole entries, deflated entries past one buffer on any stream, and
/// stored ones on a stream that can't seek. Faults are <see cref="ZipFailureTests"/>'s, sizes past the limit
/// <see cref="Zip64HookTests"/>'.
/// </summary>
public class ZipPatchTests
{
	/// <summary>
	/// Large entries that aren't patched: deflated on streams that seek and streams that don't, and stored on a stream
	/// that can't seek; synchronously and asynchronously.
	/// </summary>
	/// <returns>The levels, the stream kinds and the modes.</returns>
	public static TheoryData<CompressionLevel, bool, bool> Unpatched()
	{
		var data = new TheoryData<CompressionLevel, bool, bool>();
		foreach (var (level, seekable) in new[] { (CompressionLevel.NoCompression, false), (CompressionLevel.Fastest, true), (CompressionLevel.Fastest, false), (CompressionLevel.Optimal, true) })
		{
			data.Add(level, seekable, false);
			data.Add(level, seekable, true);
		}

		return data;
	}

	/// <summary>
	/// A stored entry past one buffer, on a stream that seeks, has its real CRC and sizes in its local header, no bit 3, no
	/// extra field and no descriptor (the next entry starts where its data ends), and its local and central headers agree:
	/// version 2.0, flags, CRC and sizes. Its patch is one write of 12 bytes at the header's CRC, between a seek there and
	/// a seek back to where the next write starts; a synchronous call writes only with <c>Write</c>, an asynchronous one
	/// only with <c>WriteAsync</c>, the patch too (the other mode throws).
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PatchesAStoredEntrysCrcAndSizesIntoItsLocalHeader(bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(5_000, 1)), new("b.bin", ZipPackages.RandomBytes(300_000, 3)), new("c.bin", ZipPackages.RandomBytes(65_537, 4)) };
		var stream = new ZipTestStream { ForbidSync = async, ForbidAsync = !async };

		var package = await ZipPackages.WriteAsync(parts, CompressionLevel.NoCompression, stream, async, 30_000);

		ZipStructure.AssertValid(package);
		ZipStructure.AssertPackagingReads(package);
		ZipPackages.AssertReadsBack(package, parts);
		Assert.All(stream.Writes, w => Assert.Equal(async, w.Async));
		var zip = ZipStructure.Read(package);
		var seeks = new List<long>();
		for (var i = 1; i <= 2; i++)
		{
			var part = parts[i];
			var entry = zip.Entries[i];
			var (local, central) = (entry.Local, entry.Central);
			var crc = System.IO.Hashing.Crc32.HashToUInt32(part.Bytes);
			Assert.Equal((part.Name, true, false), (entry.Name, entry.Patched, entry.Streamed));
			Assert.Equal((20, (ushort)0, (ushort)0, 0), (local.VersionNeeded, local.Flags, local.Method, local.Extra.Length));
			Assert.Equal((crc, (uint)part.Bytes.Length, (uint)part.Bytes.Length), (local.Crc, local.CompressedSize, local.UncompressedSize));
			Assert.Equal((local.VersionNeeded, local.Flags, local.Crc, local.CompressedSize, local.UncompressedSize), (central.VersionNeeded, central.Flags, central.Crc, central.CompressedField, central.UncompressedField));
			Assert.Empty(central.Extra);
			Assert.Null(entry.Descriptor);
			Assert.Equal(local.DataStart + part.Bytes.Length, zip.Entries[i + 1].Offset);

			var patch = stream.Starts.IndexOf(entry.Offset + 14);
			Assert.Equal((12, async), (stream.Writes[patch].Size, stream.Writes[patch].Async));
			Assert.Equal(local.DataStart + part.Bytes.Length, stream.Starts[patch + 1]);
			seeks.Add(entry.Offset + 14);
			seeks.Add(local.DataStart + part.Bytes.Length);
		}

		Assert.Equal(seeks, stream.Seeks);
	}

	/// <summary>
	/// A package written into a stream that seeks, from position 100: its offsets count from there (the start rule), and
	/// each patch lands on its own header, not 100 bytes before it. The bytes before the package are untouched, every
	/// entry's local header and data are those of the same package written from position 0, and every offset is 100 more,
	/// the entries after a patched one too. <see cref="ZipArchive"/> reads the stream back whole.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PatchesAtTheHeaderFromTheStreamsPosition(bool async)
	{
		const int prefix = 100;
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(1_000, 1)), new("b.bin", ZipPackages.RandomBytes(150_000, 2)), new("c.bin", ZipPackages.RandomBytes(100_000, 3)), new("d.xml", ZipWriterTests.Bytes(2_000, 4)) };
		var plain = await ZipPackages.WriteAsync(parts, CompressionLevel.NoCompression, new ZipTestStream(), async, 30_000);
		var stream = new ZipTestStream();
		stream.Write(Enumerable.Repeat((byte)0xAB, prefix).ToArray(), 0, prefix);

		var written = await ZipPackages.WriteAsync(parts, CompressionLevel.NoCompression, stream, async, 30_000);

		Assert.All(written.Take(prefix), octet => Assert.Equal(0xAB, octet));
		var from0 = ZipStructure.Read(plain);
		var zip = ZipStructure.Read(written);
		Assert.Equal(from0.Entries.Select(e => e.Offset + prefix), zip.Entries.Select(e => e.Offset));
		Assert.Equal(from0.DirectoryOffset + prefix, zip.DirectoryOffset);
		Assert.True(written.AsSpan(prefix, (int)from0.DirectoryOffset).SequenceEqual(plain.AsSpan(0, (int)from0.DirectoryOffset)), "The entries written from position 100 differ from those written from 0.");
		var (b, c) = (zip.Entries[1], zip.Entries[2]);
		Assert.True(b.Patched && c.Patched);
		Assert.Equal(prefix + 30 + 5 + 1_000, b.Offset);
		Assert.Equal([b.Offset + 14, b.End, c.Offset + 14, c.End], stream.Seeks);
		Assert.Equal((b.Central.Crc, 150_000u), (b.Local.Crc, b.Local.CompressedSize));
		Assert.Equal((c.Central.Crc, 100_000u), (c.Local.Crc, c.Local.CompressedSize));
		using var archive = new ZipArchive(new MemoryStream(written), ZipArchiveMode.Read);
		foreach (var part in parts)
		{
			Assert.Equal(part.Bytes, ZipWriterTests.ReadEntry(archive, part.Name));
		}
	}

	/// <summary>
	/// Entries past one buffer that aren't patched: deflated ones on any stream, and stored ones on a stream that can't
	/// seek, are streamed as before (bit 3, version 4.5, a local Zip64 extra of zeros and a descriptor), without a seek;
	/// whole entries beside them have their CRC and sizes in their local headers, as before.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Unpatched))]
	public async Task StreamsLargeEntriesThatArentPatched(CompressionLevel level, bool seekable, bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", ZipWriterTests.Bytes(5_000, 1)), new("b.bin", ZipPackages.RandomBytes(300_000, 3)) };
		var stream = new ZipTestStream(seekable);

		var package = await ZipPackages.WriteAsync(parts, level, stream, async, 30_000);

		ZipStructure.AssertValid(package);
		ZipStructure.AssertPackagingReads(package);
		ZipPackages.AssertReadsBack(package, parts);
		Assert.Empty(stream.Seeks);
		var zip = ZipStructure.Read(package);
		var (a, b) = (zip.Entries[0], zip.Entries[1]);
		Assert.Equal((false, false, 0), (a.Streamed, a.Patched, a.Local.Extra.Length));
		Assert.Equal(a.Central.Crc, a.Local.Crc);
		Assert.Equal((true, false, 45, 20), (b.Streamed, b.Patched, b.Local.VersionNeeded, b.Local.Extra.Length));
		Assert.Equal((0u, 0u, 0u, 24), (b.Local.Crc, b.Local.CompressedSize, b.Local.UncompressedSize, b.Descriptor!.Length));
	}
}
