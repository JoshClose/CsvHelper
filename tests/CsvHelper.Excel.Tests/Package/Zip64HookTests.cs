using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Zip64 without 4 GiB: the writer's limits lowered (<see cref="ZipLimits"/>) so that small packages carry each Zip64
/// field, extra and end record in turn and together, which is valid, since a Zip64 field may hold a value that would have
/// fit; each package checked by its records, read back by <see cref="ZipArchive"/> and, on .NET Framework, by WindowsBase.
/// And a real package of 65,536 entries, whose end record overflows for real.
/// </summary>
public class Zip64HookTests
{
	/// <summary>
	/// An entry whose uncompressed size alone reaches the limit: text that deflates below it. Its central header holds the
	/// size in a Zip64 extra of one field, and the entry is streamed, since a whole entry's sizes fit 32 bits.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesAnUncompressedSizePastTheLimit(bool async)
	{
		var parts = new List<ZipPart> { new("a.xml", Text(20_000)) };
		var limits = new ZipLimits(10_000, 0xFFFF);

		var zip = await CheckAsync(parts, CompressionLevel.Optimal, limits, async);

		var entry = zip.Entries[0];
		Assert.True(entry.Streamed);
		Assert.Equal((0xFFFFFFFFu, true), (entry.Central.UncompressedField, entry.Central.CompressedField != 0xFFFFFFFF));
		Assert.Equal(12, entry.Central.Extra.Length);
		Assert.Null(zip.End64);
	}

	/// <summary>
	/// An entry whose compressed size alone reaches the limit: random bytes, which deflate to a few bytes more. Its central
	/// header holds that size alone in its Zip64 extra; the directory after it starts past the limit too.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesACompressedSizePastTheLimit(bool async)
	{
		var bytes = ZipPackages.RandomBytes(20_000, 2);
		var compressed = ZipStructure.Read(ZipPackages.Write([new ZipPart("a.bin", bytes)], CompressionLevel.Fastest)).Entries[0].CompressedSize;
		Assert.True(compressed > bytes.Length);
		var limits = new ZipLimits(bytes.Length + 1, 0xFFFF);

		var zip = await CheckAsync([new ZipPart("a.bin", bytes)], CompressionLevel.Fastest, limits, async);

		var entry = zip.Entries[0];
		Assert.True(entry.Streamed);
		Assert.Equal((false, 0xFFFFFFFFu, false), (entry.Central.UncompressedField == 0xFFFFFFFF, entry.Central.CompressedField, entry.Central.OffsetField == 0xFFFFFFFF));
		Assert.Equal(12, entry.Central.Extra.Length);
		Assert.Equal(0xFFFFFFFFu, zip.End.DirectoryOffset);
	}

	/// <summary>
	/// A stored entry past the limit: both sizes in the Zip64 extra, uncompressed first. Stored entries take the same
	/// streamed shape.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesBothSizesPastTheLimit(bool async)
	{
		var limits = new ZipLimits(5_000, 0xFFFF);

		var zip = await CheckAsync([new ZipPart("a.bin", ZipPackages.RandomBytes(6_000, 3))], CompressionLevel.NoCompression, limits, async);

		var entry = zip.Entries[0];
		Assert.True(entry.Streamed);
		Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu, 0u), (entry.Central.UncompressedField, entry.Central.CompressedField, entry.Central.OffsetField));
		Assert.Equal(20, entry.Central.Extra.Length);
	}

	/// <summary>
	/// A whole entry whose offset alone reaches the limit: its central header holds the offset in a Zip64 extra of one
	/// field, at version 4.5, and so does its local header's version, which agrees.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesAnOffsetPastTheLimit(bool async)
	{
		var limits = new ZipLimits(1_000, 0xFFFF);
		var parts = new List<ZipPart> { new("a.bin", ZipPackages.RandomBytes(990, 4)), new("b.xml", Text(100)) };

		var zip = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

		var first = zip.Entries[0];
		Assert.False(first.Streamed);
		Assert.Equal(20, first.Central.VersionNeeded);
		var second = zip.Entries[1];
		Assert.False(second.Streamed);
		Assert.Equal((45, 45), (second.Central.VersionNeeded, second.Local.VersionNeeded));
		Assert.Equal((false, false, 0xFFFFFFFFu), (second.Central.UncompressedField == 0xFFFFFFFF, second.Central.CompressedField == 0xFFFFFFFF, second.Central.OffsetField));
		Assert.Equal(12, second.Central.Extra.Length);
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

		var zip = await CheckAsync(parts, CompressionLevel.Fastest, limits, async);

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

		var zip = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

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

		var zip = await CheckAsync(parts, CompressionLevel.NoCompression, limits, async);

		Assert.Equal(0xFFFFFFFFu, zip.End.DirectoryOffset);
		Assert.NotEqual(0xFFFFFFFFu, zip.End.DirectorySize);
		Assert.NotEqual(0xFFFF, zip.End.Entries);
		Assert.All(zip.Entries, e => Assert.Empty(e.Central.Extra));
	}

	/// <summary>
	/// Every field past the limits together: every entry's sizes and (past the first) offset in Zip64 extras of up to
	/// three fields, and every field of the end record all ones.
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

		var zip = await CheckAsync(parts, level, limits, async);

		Assert.Equal((0xFFFF, 0xFFFF, 0xFFFFFFFFu, 0xFFFFFFFFu), (zip.End.EntriesOnDisk, zip.End.Entries, zip.End.DirectorySize, zip.End.DirectoryOffset));
		Assert.All(zip.Entries, e => Assert.True(e.Streamed));
		Assert.Equal(20, zip.Entries[0].Central.Extra.Length);
		Assert.All(zip.Entries.Skip(1), e => Assert.Equal(28, e.Central.Extra.Length));
	}

	/// <summary>
	/// Each field at exactly its limit, not past it, goes to Zip64 too, since a classic field of all ones says that the
	/// value is in a Zip64 field. The limit is the value the package has there, and nothing written before that field
	/// reaches it, so the field stays where it was.
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

		var zip = await CheckAsync(parts, level, limits, async);

		switch (field)
		{
			case "uncompressed size":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[0].UncompressedSize, zip.Entries[0].Central.UncompressedField));
				Assert.NotEqual(0xFFFFFFFFu, zip.Entries[0].Central.CompressedField);
				break;
			case "compressed size":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[0].CompressedSize, zip.Entries[0].Central.CompressedField));
				Assert.NotEqual(0xFFFFFFFFu, zip.Entries[0].Central.UncompressedField);
				break;
			case "offset":
				Assert.Equal((limits.Max32, 0xFFFFFFFFu), (zip.Entries[1].Offset, zip.Entries[1].Central.OffsetField));
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
	/// Writes a package with lowered limits to a stream that seeks and to one that doesn't, checks that both have the same
	/// bytes, valid by their records under those limits, read back by <see cref="ZipArchive"/> and WindowsBase.
	/// </summary>
	/// <param name="parts">The parts.</param>
	/// <param name="level">The compression.</param>
	/// <param name="limits">The limits.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The package's records.</returns>
	private static async Task<ZipLayout> CheckAsync(List<ZipPart> parts, CompressionLevel level, ZipLimits limits, bool async)
	{
		var package = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(), async, 3_000, limits);
		var unseekable = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(false), async, 3_000, limits);

		Assert.Equal(package, unseekable);
		ZipStructure.AssertValid(package, limits.Max32, limits.Max16);
		ZipPackages.AssertReadsBack(package, parts);
		ZipStructure.AssertPackagingReads(package);

		return ZipStructure.Read(package);
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
