using System.IO.Compression;
using System.Text;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The zip writer under <see cref="PackageWriter"/>: where its records go, their shape (whole or streamed), at every
/// level, to streams that seek and streams that don't, synchronously and asynchronously; each package checked by its
/// records (<see cref="ZipStructure"/>), read back by <see cref="ZipArchive"/> and, on .NET Framework, by WindowsBase.
/// </summary>
public class ZipWriterTests
{
	/// <summary>Every level, to a stream that seeks and one that doesn't, synchronously and asynchronously.</summary>
	public static TheoryData<CompressionLevel, bool, bool> Modes()
	{
		var data = new TheoryData<CompressionLevel, bool, bool>();
		foreach (var level in ZipPackages.Levels)
		{
			foreach (var seekable in new[] { true, false })
			{
				foreach (var async in new[] { false, true })
				{
					data.Add(level, seekable, async);
				}
			}
		}

		return data;
	}

	/// <summary>Every level.</summary>
	public static TheoryData<CompressionLevel> Levels()
	{
		var data = new TheoryData<CompressionLevel>();
		foreach (var level in ZipPackages.Levels)
		{
			data.Add(level);
		}

		return data;
	}

	/// <summary>Every level, with entries written whole and in pieces of 10,000 bytes.</summary>
	public static TheoryData<CompressionLevel, int> LevelsAndPieces()
	{
		var data = new TheoryData<CompressionLevel, int>();
		foreach (var level in ZipPackages.Levels)
		{
			data.Add(level, int.MaxValue);
			data.Add(level, 10_000);
		}

		return data;
	}

	/// <summary>Every level, to a stream that seeks and one that doesn't.</summary>
	public static TheoryData<CompressionLevel, bool> LevelsAndStreams() => LevelsAndModes();

	/// <summary>Every deflated level, synchronously and asynchronously.</summary>
	/// <returns>The levels and the modes.</returns>
	public static TheoryData<CompressionLevel, bool> DeflatedLevelsAndModes()
	{
		var data = new TheoryData<CompressionLevel, bool>();
		foreach (var level in ZipPackages.Levels.Where(l => l != CompressionLevel.NoCompression))
		{
			data.Add(level, false);
			data.Add(level, true);
		}

		return data;
	}

	/// <summary>Every level, synchronously and asynchronously.</summary>
	public static TheoryData<CompressionLevel, bool> LevelsAndModes()
	{
		var data = new TheoryData<CompressionLevel, bool>();
		foreach (var level in ZipPackages.Levels)
		{
			data.Add(level, false);
			data.Add(level, true);
		}

		return data;
	}

	/// <summary>
	/// A package written into a stream that seeks counts its offsets from the stream's position when the writer was made,
	/// so that they are the stream's own positions; one written into a stream that can't seek counts from 0. Either way
	/// <see cref="ZipArchive"/> reads it back, from position 0 of the stream that seeks, and from the package's first byte
	/// of the one that can't. This is <see cref="ZipArchive"/>'s rule, which the own writer keeps.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(true, false)]
	[InlineData(true, true)]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public async Task CountsOffsetsFromTheStreamsPosition(bool seekable, bool async)
	{
		const int prefix = 100;
		var parts = new[] { ("a.bin", Bytes(1000, 1)), ("b.bin", Bytes(3000, 2)) };
		var memory = new MemoryStream();
		memory.Write(new byte[prefix], 0, prefix);
		var package = new PackageWriter(seekable ? memory : new ForwardOnlyStream(memory), leaveOpen: true, CompressionLevel.Fastest);
		foreach (var (name, bytes) in parts)
		{
			var entry = await package.OpenPartAsync(name, async, CancellationToken.None);
			await WriteAsync(entry, bytes, async);
			await PackageWriter.CloseAsync(entry, async);
		}

		await package.DisposeAsync(async);

		// The stream that seeks is read whole, the prefix included; the other from the package's first byte.
		var written = seekable ? memory.ToArray() : memory.ToArray().AsSpan(prefix).ToArray();
		var zip = ZipStructure.Read(written);
		var first = seekable ? prefix : 0;
		Assert.Equal(first, zip.Entries[0].Offset);
		Assert.Equal(zip.Entries[0].End, zip.Entries[1].Offset);
		Assert.Equal(zip.Entries[1].End, zip.DirectoryOffset);
		using var archive = new ZipArchive(new MemoryStream(written), ZipArchiveMode.Read);
		Assert.Equal(parts.Select(p => p.Item1), archive.Entries.Select(e => e.FullName));
		foreach (var (name, bytes) in parts)
		{
			Assert.Equal(bytes, ReadEntry(archive, name));
		}
	}

	/// <summary>
	/// Entries empty, small, of exactly one buffer of data (whole) and one byte more (streamed), and large, compressible or
	/// not: every package valid by its records, read back by <see cref="ZipArchive"/> and WindowsBase, each entry whole
	/// exactly when it has at most 65,536 bytes of data, deflated (with its level's flags) or stored, and every write of the
	/// stream synchronous or asynchronous as the calls were.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEntriesWholeOrStreamed(CompressionLevel level, bool seekable, bool async)
	{
		var parts = Parts(level);
		var stream = new ZipTestStream(seekable);

		var package = await ZipPackages.WriteAsync(parts, level, stream, async, piece: 10_000);

		ZipStructure.AssertValid(package);
		ZipStructure.AssertPackagingReads(package);
		ZipPackages.AssertReadsBack(package, parts);
		Assert.All(stream.Writes, w => Assert.Equal(async, w.Async));
		Assert.Equal(async ? 0 : 1, stream.Flushes);
		Assert.Equal(async ? 1 : 0, stream.AsyncFlushes);
		var zip = ZipStructure.Read(package);
		var stored = level == CompressionLevel.NoCompression;
		var levelFlags = level switch
		{
			CompressionLevel.Fastest => 0x0006,
			CompressionLevel.Optimal or CompressionLevel.NoCompression => 0x0000,
			_ => 0x0002,
		};
		Assert.All(zip.Entries, e => Assert.Equal(stored ? 0 : 8, e.Central.Method));
		Assert.All(zip.Entries, e => Assert.Equal(levelFlags, e.Central.Flags & 0x0006));
		var entries = zip.Entries.ToDictionary(e => e.Name);
		Assert.Equal(stored ? 0 : 2, entries["parts/empty.bin"].CompressedSize);
		Assert.Equal(0u, entries["parts/empty.bin"].Central.Crc);
		Assert.Equal(65536, entries["parts/whole.bin"].CompressedSize);
		Assert.False(entries["parts/whole.bin"].Streamed);
		Assert.Equal(65537, entries["parts/streamed.bin"].CompressedSize);
		Assert.True(entries["parts/streamed.bin"].Streamed);
		Assert.Equal(["parts/streamed.bin", "parts/large.xml", "parts/random.bin"], zip.Entries.Where(e => e.Streamed).Select(e => e.Name));
	}

	/// <summary>
	/// A package's bytes depend only on its parts, their order and level, and the runtime's deflate output: the same
	/// written synchronously or asynchronously, to a stream that seeks or one that doesn't. The parts are written in the
	/// same pieces each time, since .NET's deflate at <see cref="CompressionLevel.Fastest"/> and
	/// <see cref="CompressionLevel.Optimal"/> gives other bytes for the same input written in other pieces.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="piece">The most bytes of a write to an entry.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(LevelsAndPieces))]
	public async Task WritesTheSameBytesAnyWay(CompressionLevel level, int piece)
	{
		var parts = Parts(level);
		var first = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(), false, piece);

		foreach (var (seekable, async) in new[] { (true, true), (false, false), (false, true) })
		{
			var again = await ZipPackages.WriteAsync(parts, level, new ZipTestStream(seekable), async, piece);

			Assert.True(first.AsSpan().SequenceEqual(again), $"Written {(async ? "asynchronously" : "synchronously")} to a stream that {(seekable ? "seeks" : "doesn't seek")}, the package differs.");
		}
	}

	/// <summary>
	/// A deflated entry's data reaches the stream in whole 65,536-byte writes but for its last, after its local header and
	/// before its descriptor; the directory and the end records follow the last entry. A stored entry's is written
	/// through once streamed (<see cref="ZipWriteThroughTests"/>).
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(DeflatedLevelsAndModes))]
	public async Task WritesDataInWholeBuffers(CompressionLevel level, bool async)
	{
		var stream = new ZipTestStream(false);

		var package = await ZipPackages.WriteAsync([new ZipPart("a/random.bin", ZipPackages.RandomBytes(300_000, 5))], level, stream, async, piece: 7_777);

		var zip = ZipStructure.Read(package);
		var data = zip.Entries[0].CompressedSize;
		var expected = new List<int> { 30 + "a/random.bin".Length + 20 };
		for (var left = data; left > 0; left -= 65536)
		{
			expected.Add((int)Math.Min(left, 65536));
		}

		expected.Add(24);
		var types = zip.Entries[1];
		expected.Add(30 + "[Content_Types].xml".Length);
		expected.Add((int)types.CompressedSize);
		expected.Add((int)zip.DirectorySize);
		expected.Add(22);
		Assert.Equal(expected, stream.Writes.Select(w => w.Size));
	}

	/// <summary>
	/// Opening an entry writes nothing, and so does writing its first 65,536 bytes of data: the local header waits until
	/// the data passes a buffer (streamed) or the entry closes (whole). The stored entry streamed writes the byte that
	/// passed the buffer with it, as it is written through from then on.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesTheHeaderWhenTheShapeIsKnown(bool async)
	{
		var stream = new ZipTestStream();
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.NoCompression);
		var bytes = ZipPackages.RandomBytes(65537, 9);

		var entry = await package.OpenPartAsync("a.bin", async, CancellationToken.None);
		Assert.Empty(stream.Writes);
		await ZipPackages.WriteAsync(entry, bytes, 0, 65536, async);
		Assert.Empty(stream.Writes);
		await ZipPackages.WriteAsync(entry, bytes, 65536, 1, async);
		Assert.Equal([30 + 5 + 20, 65536, 1], stream.Writes.Select(w => w.Size));
		await PackageWriter.CloseAsync(entry, async);
		Assert.Equal([30 + 5 + 20, 65536, 1, 24], stream.Writes.Select(w => w.Size));

		entry = await package.OpenPartAsync("b.bin", async, CancellationToken.None);
		await ZipPackages.WriteAsync(entry, bytes, 0, 65536, async);
		Assert.Equal(4, stream.Writes.Count);
		await PackageWriter.CloseAsync(entry, async);
		Assert.Equal([30 + 5 + 20, 65536, 1, 24, 30 + 5, 65536], stream.Writes.Select(w => w.Size));
		await package.DisposeAsync(async);

		var zip = ZipStructure.Read(stream.ToArray());
		Assert.True(zip.Entries[0].Streamed);
		Assert.False(zip.Entries[1].Streamed);
		ZipStructure.AssertValid(stream.ToArray());
	}

	/// <summary>
	/// An entry's buffers return to the pool as it closes, and the directory's as the package ends: only the directory's
	/// one buffer is held between entries.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Levels))]
	public async Task ReturnsItsBuffers(CompressionLevel level)
	{
		var package = new PackageWriter(new ZipTestStream(false), leaveOpen: false, level);
		foreach (var part in Parts(level))
		{
			var entry = await package.OpenPartAsync(part.Name, true, CancellationToken.None);
			await entry.WriteAsync(part.Bytes, 0, part.Bytes.Length);
			Assert.True(package.Zip.RentedBuffers <= 4, $"{part.Name}: {package.Zip.RentedBuffers} buffers held while it is written.");
			await PackageWriter.CloseAsync(entry, true);
			Assert.Equal(1, package.Zip.RentedBuffers);
		}

		await package.DisposeAsync(true);

		Assert.Equal(0, package.Zip.RentedBuffers);
	}

	/// <summary>
	/// An entry's stream is write-only: it can't read, seek or tell its length, its position is the bytes written and
	/// can't be set, flushing it does nothing (the package's bytes are those of the same writes without flushes), and once
	/// it is closed it can't be written, while closing it again does nothing.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesEntriesThroughAWriteOnlyStream(bool async)
	{
		var bytes = Bytes(200_000, 4);
		var plain = await ZipPackages.WriteAsync([new ZipPart("a.xml", bytes)], CompressionLevel.Fastest, new ZipTestStream(), async, 50_000);
		var stream = new ZipTestStream();
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.Fastest);

		var entry = await package.OpenPartAsync("a.xml", async, CancellationToken.None);
		Assert.False(entry.CanRead);
		Assert.False(entry.CanSeek);
		Assert.True(entry.CanWrite);
		Assert.Throws<NotSupportedException>(() => entry.Read(new byte[1], 0, 1));
		Assert.Throws<NotSupportedException>(() => entry.Seek(0, SeekOrigin.Begin));
		Assert.Throws<NotSupportedException>(() => entry.Length);
		Assert.Throws<NotSupportedException>(() => entry.Position = 0);
		Assert.Throws<NotSupportedException>(() => entry.SetLength(0));
		for (var at = 0; at < bytes.Length; at += 50_000)
		{
			await ZipPackages.WriteAsync(entry, bytes, at, 50_000, async);
			entry.Flush();
			await entry.FlushAsync();
			Assert.Equal(at + 50_000, entry.Position);
		}

		await PackageWriter.CloseAsync(entry, async);
		Assert.False(entry.CanWrite);
		Assert.Throws<ObjectDisposedException>(() => entry.Write(bytes, 0, 1));
		await Assert.ThrowsAsync<ObjectDisposedException>(() => entry.WriteAsync(bytes, 0, 1));
		await PackageWriter.CloseAsync(entry, async);
		entry.Dispose();
		var types = await package.OpenPartAsync("[Content_Types].xml", async, CancellationToken.None);
		await ZipPackages.WriteAsync(types, ZipPackages.ContentTypes, 0, ZipPackages.ContentTypes.Length, async);
		await PackageWriter.CloseAsync(types, async);
		await package.DisposeAsync(async);

		Assert.Equal(plain, stream.ToArray());
	}

	/// <summary>
	/// The entry's stream's other overloads: spans and memory on .NET, a byte at a time, and disposing it with
	/// <c>using</c> and <c>await using</c>, which close it as <see cref="PackageWriter.CloseAsync"/> does; deflated, and
	/// stored, where each overload's write after the first goes straight through, of the caller's own bytes but for the
	/// byte <see cref="Stream.WriteByte"/> copies. The package is valid and reads back. A stored package's bytes are those
	/// of the parts written whole; a deflated one's aren't compared, since .NET's deflate gives other bytes for other
	/// pieces.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(CompressionLevel.Optimal)]
	[InlineData(CompressionLevel.NoCompression)]
	public async Task WritesThroughEveryOverload(CompressionLevel level)
	{
		var bytes = Bytes(200_000, 6);
		var stream = new ZipTestStream { Watched = bytes };
		var package = new PackageWriter(stream, leaveOpen: true, level);

		using (var entry = await package.OpenPartAsync("a.xml", false, CancellationToken.None))
		{
			entry.Write(bytes, 0, 70_000);
			entry.WriteByte(bytes[70_000]);
#if NET
			entry.Write(bytes.AsSpan(70_001, 30_000));
			await entry.WriteAsync(bytes.AsMemory(100_001, 30_000));
#else
			entry.Write(bytes, 70_001, 30_000);
			await entry.WriteAsync(bytes, 100_001, 30_000);
#endif
			entry.Write(bytes, 130_001, bytes.Length - 130_001);
		}

		var second = (ZipEntryStream)await package.OpenPartAsync("b.xml", true, CancellationToken.None);
		await using (second)
		{
			await second.WriteAsync(bytes, 0, 70_000);
			await second.WriteAsync(bytes, 70_000, bytes.Length - 70_000);
		}

		var types = await package.OpenPartAsync("[Content_Types].xml", false, CancellationToken.None);
		types.Write(ZipPackages.ContentTypes, 0, ZipPackages.ContentTypes.Length);
		types.Dispose();
		await package.DisposeAsync(true);

		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		ZipStructure.AssertPackagingReads(written);
		var parts = new List<ZipPart> { new("a.xml", bytes), new("b.xml", bytes) };
		ZipPackages.AssertReadsBack(written, parts);
		if (level == CompressionLevel.NoCompression)
		{
			Assert.Equal(ZipPackages.Write(parts, level), written);
			Assert.Equal([(30_000, false), (30_000, true), (69_999, false), (130_000, true)], stream.Writes.Where(w => w.Watched).Select(w => (w.Size, w.Async)));
		}
	}

	/// <summary>
	/// Opening an entry while another is open throws, as a bug's guard, and so does a name that isn't ASCII, which would
	/// need the UTF-8 flag.
	/// </summary>
	[Fact]
	public void RefusesASecondOpenEntryAndNamesThatArentAscii()
	{
		var package = new PackageWriter(new ZipTestStream(), leaveOpen: false, CompressionLevel.Fastest);
		var entry = SyncOrAsync.Result(package.OpenPartAsync("a.xml", false, CancellationToken.None));

		Assert.Throws<InvalidOperationException>(() => SyncOrAsync.Result(package.OpenPartAsync("b.xml", false, CancellationToken.None)));
		SyncOrAsync.Wait(PackageWriter.CloseAsync(entry, false));
		Assert.Throws<InvalidOperationException>(() => SyncOrAsync.Result(package.OpenPartAsync("xl/" + (char)0xE9 + ".xml", false, CancellationToken.None)));
		SyncOrAsync.Wait(package.DisposeAsync(false));
	}

	/// <summary>
	/// A canceled write throws before it takes any bytes, and leaves the writer whole: the entry goes on with the next
	/// write, and the package is complete without the canceled bytes. The write is too small to reach the stream, so only
	/// the entry's own check can refuse it.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Levels))]
	public async Task TakesNothingFromACanceledWrite(CompressionLevel level)
	{
		var stream = new ZipTestStream();
		var package = new PackageWriter(stream, leaveOpen: true, level);
		var entry = await package.OpenPartAsync("a.bin", true, CancellationToken.None);
		using (var canceled = new CancellationTokenSource())
		{
			canceled.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.WriteAsync([1, 2, 3], 0, 3, canceled.Token));
		}

		await entry.WriteAsync([4, 5], 0, 2, CancellationToken.None);
		await PackageWriter.CloseAsync(entry, true);
		Assert.False(package.Zip.Faulted);
		await package.DisposeAsync(true);

		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		using var archive = new ZipArchive(new MemoryStream(written), ZipArchiveMode.Read);
		Assert.Equal([4, 5], ReadEntry(archive, "a.bin"));
	}

	/// <summary>
	/// Workbooks from <see cref="ExcelWorkbookWriter"/>, with a sheet past a buffer (streamed), shared strings, a table, a
	/// link and a list in the lists sheet (the small parts whole): valid by their records, read by <see cref="ZipArchive"/>
	/// and WindowsBase, with the same parts at every level and in every mode, the same bytes in every mode at a level, and
	/// CsvHelper.Excel reads them back.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesWorkbooksWholeOrStreamed(CompressionLevel level, bool seekable, bool async)
	{
		var stream = new ZipTestStream(seekable);

		var workbook = await WriteWorkbookAsync(stream, level, async);

		ZipStructure.AssertValid(workbook);
		ZipStructure.AssertPackagingReads(workbook);
		Assert.All(stream.Writes, w => Assert.Equal(async, w.Async));
		var zip = ZipStructure.Read(workbook);
		Assert.Contains(zip.Entries, e => e.Streamed);
		Assert.Contains(zip.Entries, e => !e.Streamed);
		Assert.Equal(await WriteWorkbookAsync(new ZipTestStream(), level, false), workbook);
		var reference = Parts(await WriteWorkbookAsync(new ZipTestStream(), CompressionLevel.NoCompression, false));
		var parts = Parts(workbook);
		Assert.Equal(reference.Keys, parts.Keys);
		foreach (var name in reference.Keys)
		{
			Assert.True(reference[name].AsSpan().SequenceEqual(parts[name]), $"{name} differs from the stored workbook's.");
		}

		Assert.Equal([("Orders", WorkbookRows + 1), ("Links", 2), ("Lists", 2)], Written.Read(workbook).Select(s => (s.Sheet, s.Rows.Count)));
	}

	/// <summary>
	/// A workbook written through the workbook writer's asynchronous calls (a sheet added and its records written
	/// asynchronously, one written whole asynchronously, and the asynchronous dispose; shared strings, a table, a link and
	/// the lists sheet included) to a stream whose synchronous writes and flush throw: every write and flush is
	/// asynchronous, on every target, at every level; stored, the sheet is streamed, and written straight through.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(LevelsAndStreams))]
	public async Task WritesWorkbooksOnlyAsynchronously(CompressionLevel level, bool seekable)
	{
		var stream = new ZipTestStream(seekable) { ForbidSync = true };

		var workbook = await WriteWorkbookAsync(stream, level, true);

		Assert.NotEmpty(stream.Writes);
		Assert.All(stream.Writes, w => Assert.True(w.Async));
		Assert.Equal((0, 1), (stream.Flushes, stream.AsyncFlushes));
		ZipStructure.AssertValid(workbook);
		ZipStructure.AssertPackagingReads(workbook);
		Assert.Contains(ZipStructure.Read(workbook).Entries, e => e.Streamed);
		Assert.Equal([("Orders", WorkbookRows + 1), ("Links", 2), ("Lists", 2)], Written.Read(workbook).Select(s => (s.Sheet, s.Rows.Count)));
	}

	/// <summary>
	/// The same workbook written through the workbook writer's synchronous calls to a stream whose asynchronous writes and
	/// flush throw: every write and flush is synchronous, on every target, at every level; stored, the sheet is streamed,
	/// and written straight through.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(LevelsAndStreams))]
	public async Task WritesWorkbooksOnlySynchronously(CompressionLevel level, bool seekable)
	{
		var stream = new ZipTestStream(seekable) { ForbidAsync = true };

		var workbook = await WriteWorkbookAsync(stream, level, false);

		Assert.NotEmpty(stream.Writes);
		Assert.All(stream.Writes, w => Assert.False(w.Async));
		Assert.Equal((1, 0), (stream.Flushes, stream.AsyncFlushes));
		ZipStructure.AssertValid(workbook);
		ZipStructure.AssertPackagingReads(workbook);
		Assert.Contains(ZipStructure.Read(workbook).Entries, e => e.Streamed);
		Assert.Equal([("Orders", WorkbookRows + 1), ("Links", 2), ("Lists", 2)], Written.Read(workbook).Select(s => (s.Sheet, s.Rows.Count)));
	}

	/// <summary>Bytes that deflate a little: a repeating pattern with a seed's variation.</summary>
	/// <param name="length">How many.</param>
	/// <param name="seed">The seed.</param>
	/// <returns>The bytes.</returns>
	internal static byte[] Bytes(int length, int seed)
	{
		var random = new Random(seed);
		var bytes = new byte[length];
		for (var i = 0; i < length; i++)
		{
			bytes[i] = (byte)(i % 7 == 0 ? random.Next(256) : 'a' + (i % 26));
		}

		return bytes;
	}

	/// <summary>Writes bytes to an entry's stream, synchronously or not.</summary>
	/// <param name="entry">The entry's stream.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when they are written.</returns>
	internal static Task WriteAsync(Stream entry, byte[] bytes, bool async) => ZipPackages.WriteAsync(entry, bytes, 0, bytes.Length, async);

	/// <summary>Reads an entry through <see cref="ZipArchive"/>, whole.</summary>
	/// <param name="archive">The archive.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>Its bytes.</returns>
	internal static byte[] ReadEntry(ZipArchive archive, string name)
	{
		using var stream = archive.GetEntry(name)!.Open();
		using var copy = new MemoryStream();
		stream.CopyTo(copy);

		return copy.ToArray();
	}

	/// <summary>The records of <see cref="WriteWorkbookAsync"/>'s first sheet.</summary>
	private const int WorkbookRows = 20_000;

	/// <summary>
	/// The parts of the structure tests: empty, tiny and small; one whose data is exactly a buffer and one a byte more; and
	/// large ones, text and random.
	/// </summary>
	/// <param name="level">The compression, which the boundary parts' inputs depend on.</param>
	/// <returns>The parts.</returns>
	private static List<ZipPart> Parts(CompressionLevel level) =>
	[
		new("parts/empty.bin", []),
		new("parts/tiny.xml", Encoding.ASCII.GetBytes("<a>tiny</a>")),
		new("parts/small.xml", Bytes(20_000, 1)),
		new("parts/whole.bin", ZipPackages.InputOfData(level, 65536)),
		new("parts/streamed.bin", ZipPackages.InputOfData(level, 65537)),
		new("parts/large.xml", Bytes(1_500_000, 2)),
		new("parts/random.bin", ZipPackages.RandomBytes(300_000, 3)),
	];

	/// <summary>A package's parts, inflated, by name.</summary>
	/// <param name="package">The package.</param>
	/// <returns>The parts.</returns>
	private static SortedDictionary<string, byte[]> Parts(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);

		return new SortedDictionary<string, byte[]>(archive.Entries.ToDictionary(e => e.FullName, e => ReadEntry(archive, e.FullName)), StringComparer.Ordinal);
	}

	/// <summary>
	/// Writes a workbook through the workbook writer's calls, synchronously or asynchronously: a sheet of 20,000 records
	/// as a table, with shared strings, then a sheet with a link and a list long enough for the lists sheet.
	/// </summary>
	/// <param name="stream">The stream, left open.</param>
	/// <param name="level">The compression.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The workbook.</returns>
	private static async Task<byte[]> WriteWorkbookAsync(ZipTestStream stream, CompressionLevel level, bool async)
	{
		var options = new ExcelWriterOptions { CompressionLevel = level, Strings = ExcelStrings.Shared };
		var records = Enumerable.Range(0, WorkbookRows).Select(i => new WrittenText { Name = $"name {i}", Note = $"note {i * 7919 % 10007}" }).ToArray();
		var second = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { List = ["one, two", "three"] } } };
		var links = new[] { new WrittenText { Name = "one, two", Note = "https://example.com/a" } };
		var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
		if (async)
		{
			await using (book)
			{
				var writer = book.AddSheet<WrittenText>("Orders", new ExcelSheetOptions { Table = new() });
				await writer.WriteRecordsAsync(records);
				await writer.DisposeAsync();
				await book.WriteSheetAsync("Links", links, second);
			}
		}
		else
		{
			using (book)
			{
				using (var writer = book.AddSheet<WrittenText>("Orders", new ExcelSheetOptions { Table = new() }))
				{
					writer.WriteRecords(records);
				}

				book.WriteSheet("Links", links, second);
			}
		}

		return stream.ToArray();
	}
}
