using System.IO.Compression;
using System.Security.Cryptography;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Stored entries written through: a stored entry, once its data passes one buffer (its local header written then, with
/// bit 3 on a stream that can't seek, or to be patched after its data on one that seeks), writes what its buffers hold,
/// and from then on each write straight to the package's stream, in one write, without copying it. The packages are byte
/// for byte those pinned by their SHA-256, whatever the pieces they are written in, synchronously and asynchronously:
/// <c>6b21adca</c>'s on a stream that can't seek, and on one that seeks those of the patched local headers; every package
/// checked by its records (<see cref="ZipStructure"/>), read back by <see cref="ZipArchive"/> and, on .NET Framework, by
/// WindowsBase.
/// </summary>
public class ZipWriteThroughTests
{
	/// <summary>
	/// The cases: their entries' sizes, one entry and several, across the whole-or-streamed edge (65,536 bytes) and the
	/// next buffer's, about 1 MB and several MB; and their limits, <see langword="null"/> for the default ones.
	/// </summary>
	private static readonly Dictionary<string, (int[] Sizes, ZipLimits? Limits)> Cases = new()
	{
		["0"] = ([0], null),
		["1"] = ([1], null),
		["65535"] = ([65_535], null),
		["65536"] = ([65_536], null),
		["65537"] = ([65_537], null),
		["131071"] = ([131_071], null),
		["131072"] = ([131_072], null),
		["131073"] = ([131_073], null),
		["about 1 MB"] = ([1_048_583], null),
		["several MB"] = ([3_000_017], null),
		["several"] = ([0, 1, 65_535, 65_536, 65_537, 131_071, 131_072, 131_073, 1_048_583], null),
		["streamed in a row"] = ([200_003, 65_537, 1_048_583, 3], null),
		["Zip64 limits"] = ([200_003, 100_003, 1_000], new ZipLimits(150_000, 2)),
	};

	/// <summary>The pieces each case is written in: the most bytes of each write, or <c>random</c> for seeded random sizes.</summary>
	private static readonly string[] PieceSizes = ["1", "1000", "65535", "65536", "65537", "1048576", "random"];

	/// <summary>
	/// The SHA-256 of each case's package written to a stream that can't seek (synchronously, each part in one write): as
	/// <c>6b21adca</c>'s writer wrote it, before stored entries were written through, but for <c>Zip64 limits</c>, whose
	/// central Zip64 extras hold all three fields since X1's follow-up (<c>6b21adca</c>'s was
	/// <c>64661AB0BA2976A9C9F947F0FB7E1942B32F9147CF777448A16604CFDC9D4858</c>).
	/// </summary>
	private static readonly Dictionary<string, string> Pinned = new()
	{
		["0"] = "35B3B094D1ABB4DD5059A019A786483F65018CCD14421D4F0FCA2ECBDE27CC42",
		["1"] = "211393F96C2344ACEC144E9481621A561F7D9DFF7DE286DB40927C0FA69A746A",
		["65535"] = "E48678A30E75B1036F96ED8509F6DBCBBE9071E25C04299734E9248EBF1F04B1",
		["65536"] = "D84B90E9CD123A7B01FC90BAC20B8FC694FE5EF2F023A4F7AA58B4C5940101F7",
		["65537"] = "7977EBF8D8D0D8377AECC9F14CFF7F26B5D223BE7995FF19B79101440A28AC17",
		["131071"] = "E9C6D650AC35DBC4A3202F2C07E11ACCB00B9BC17947E217382E075AF467CE89",
		["131072"] = "F07C048CAB2B0EC8ABB13E12EAE7FE53DE67609FDAFF251AAF568A6D7330E509",
		["131073"] = "866865B60CE8F788293185BA56CC2817681F0287C391C37FD60CED8DFC289F16",
		["about 1 MB"] = "1D19ED57327D443E239E61CF6CFE4C2C99788B5573EBC500782F393C0D6610D8",
		["several MB"] = "1CB9397171C31A43B69562154E5C8E5E9683A2AB500267B57452FD4EFC4653BB",
		["several"] = "32BFD7E70A495D0CF7C69B94FB353E7357BE271FFE7CE7FD8673FDD4CC9EE4A1",
		["streamed in a row"] = "77FD87CBF41281AF5FAEF95F6A9244F9391DEF45B5C9C920354050E4FBD02E3E",
		["Zip64 limits"] = "7661C0D6A936776DCA6C3C23F981E5F0BC23DA5AB77011AB7591058F977F4E7C",
	};

	/// <summary>
	/// The SHA-256 of each case's package written to a stream that seeks (synchronously, each part in one write), since
	/// X1's follow-up: a stored entry past one buffer has its CRC and sizes patched into its local header, or past
	/// <see cref="ZipLimits.Max32"/> the streamed form patched in. Those of at most one buffer per entry are
	/// <c>6b21adca</c>'s, unchanged.
	/// </summary>
	private static readonly Dictionary<string, string> PinnedSeekable = new()
	{
		["0"] = "35B3B094D1ABB4DD5059A019A786483F65018CCD14421D4F0FCA2ECBDE27CC42",
		["1"] = "211393F96C2344ACEC144E9481621A561F7D9DFF7DE286DB40927C0FA69A746A",
		["65535"] = "E48678A30E75B1036F96ED8509F6DBCBBE9071E25C04299734E9248EBF1F04B1",
		["65536"] = "D84B90E9CD123A7B01FC90BAC20B8FC694FE5EF2F023A4F7AA58B4C5940101F7",
		["65537"] = "B9FABA40159392D5AC37B3D6A12B10E2F8B3C9C1F2CD8C02C50A69BCD15BF663",
		["131071"] = "565D360E2B15268870E1E58A2221195B5936D1C44FD7B9940B0AF3F1AEAF0122",
		["131072"] = "A04CF1394734F5542A4AC356FA13324B7F9EF03C12C724FD83F0233F61A29CCC",
		["131073"] = "0CD4C90E045BD44BE3CACEE9CB1E76D0F5E106CE634AAADD00B10CBF1FBB48D9",
		["about 1 MB"] = "F3B7D8E369F64CC9F1643DE5F4C6A98BAF9545B23AC69450D3B0CB403976E074",
		["several MB"] = "D7A514D2D329A5793784895A0746E381CF8E70893B05C42ED6907B67D95E3F12",
		["several"] = "512E402B0EC753CF0D563F64AD952F98A406364533EF420991A79200A7A5B835",
		["streamed in a row"] = "40D6FDE5F93A97D3CD3F1A6BCC3A2D0D3E057EADCB7D6F938517A9818395EBE5",
		["Zip64 limits"] = "F61714C77E5047B41A44C51FFCD15DBD5DE28B24EA53BF9E8909079AC882E52A",
	};

	/// <summary>Every case, in every piece size, to a stream that seeks and one that doesn't, synchronously and asynchronously.</summary>
	/// <returns>The cases' names, the pieces, the stream kinds and the modes.</returns>
	public static TheoryData<string, string, bool, bool> SameBytesCases()
	{
		var data = new TheoryData<string, string, bool, bool>();
		foreach (var name in Cases.Keys)
		{
			foreach (var pieces in PieceSizes)
			{
				foreach (var seekable in new[] { true, false })
				{
					data.Add(name, pieces, seekable, false);
					data.Add(name, pieces, seekable, true);
				}
			}
		}

		return data;
	}

	/// <summary>
	/// A stored package's bytes are the pinned ones for its stream's kind, whatever the pieces its entries are written in,
	/// synchronously or asynchronously: <c>6b21adca</c>'s on a stream that can't seek (but for the full Zip64 extras),
	/// and the patched local headers' on one that seeks. Every write and flush of the stream is in the call's mode (the
	/// other mode throws), the package is valid by its records under its limits, every entry past one buffer is patched
	/// exactly when the stream seeks, and it reads back.
	/// </summary>
	/// <param name="name">The case.</param>
	/// <param name="pieces">The pieces it is written in.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(SameBytesCases))]
	public async Task WritesTheSameBytesInAnyPieces(string name, string pieces, bool seekable, bool async)
	{
		var (parts, limits) = Parts(name);
		var stream = new ZipTestStream(seekable) { ForbidSync = async, ForbidAsync = !async, Yields = false };

		var task = WriteAsync(parts, stream, async, Pieces(pieces), limits);
		Assert.True(async || task.IsCompleted, "A synchronous write didn't complete synchronously.");
		var package = await task;

		Assert.Equal((seekable ? PinnedSeekable : Pinned)[name], Sha256(package));
		Assert.Equal((async ? 0 : 1, async ? 1 : 0), (stream.Flushes, stream.AsyncFlushes));
		ZipStructure.AssertValid(package, limits.Max32, limits.Max16);
		Assert.All(ZipStructure.Read(package).Entries.Where(e => e.CompressedSize > ZipStructure.WholeData), e => Assert.Equal(seekable, e.Patched));
		ZipStructure.AssertPackagingReads(package);
		ZipPackages.AssertReadsBack(package, parts);
	}

	/// <summary>
	/// The stream's writes of stored entries. Until an entry's data passes one buffer, it is held, as before: an entry of
	/// at most 65,536 bytes is whole, its header written as it closes. The write that passes one buffer writes the local
	/// header (with a Zip64 extra, streamed, on a stream that can't seek; without, patched, on one that seeks), the full
	/// buffer and the rest the entry held; from then on each write reaches the stream as one write (1 MB too, not cut into
	/// buffers), of the caller's own bytes, not a copy; and closing the entry writes its descriptor, or on a stream that
	/// seeks its 12 bytes of CRC and sizes over its header's. The package's bytes are those of the same parts written whole
	/// to a stream of the same kind.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task WritesStraightThroughPastOneBuffer(bool seekable, bool async)
	{
		var source = Data(2_500_000, 11);
		var stream = new ZipTestStream(seekable) { Watched = source };
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.NoCompression);
		var parts = new List<ZipPart>();
		var at = 0;

		async Task WriteEntryAsync(string name, int[] writes)
		{
			var start = at;
			var entry = await Packages.Run(async, a => package.OpenPartAsync(name, a, CancellationToken.None));
			foreach (var count in writes)
			{
				await ZipPackages.WriteAsync(entry, source, at, count, async);
				at += count;
			}

			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
			parts.Add(new ZipPart(name, source.AsSpan(start, at - start).ToArray()));
		}

		// Nine writes of 7,777 bytes pass one buffer at the ninth, which writes the header, the full buffer and the 4,457
		// bytes held; then 7,777 bytes, 1 MB and 1 byte go through.
		await WriteEntryAsync("a.bin", [7_777, 7_777, 7_777, 7_777, 7_777, 7_777, 7_777, 7_777, 7_777, 7_777, 1_048_576, 1]);

		// A first write of 1 MB and 5 bytes holds two full buffers, both written after the header; the rest of it goes
		// through.
		await WriteEntryAsync("b.bin", [1_048_581, 3]);

		// A full buffer held, then 1 byte: the header, the buffer and the byte; then a buffer's worth goes through.
		await WriteEntryAsync("c.bin", [65_536, 1, 65_536]);

		// Exactly one buffer: whole.
		await WriteEntryAsync("d.bin", [65_535, 1]);

		var types = await Packages.Run(async, a => package.OpenPartAsync("[Content_Types].xml", a, CancellationToken.None));
		await ZipPackages.WriteAsync(types, ZipPackages.ContentTypes, 0, ZipPackages.ContentTypes.Length, async);
		await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(types, a));
		await ZipPackages.RunAsync(async, package.DisposeAsync);

		(int, bool, bool) Header(string name) => (30 + name.Length + (seekable ? 0 : 20), async, false);
		(int, bool, bool) Held(int size) => (size, async, false);
		(int, bool, bool) Through(int size) => (size, async, true);
		var end = (seekable ? 12 : 24, async, false);
		Assert.Equal(
		[
			Header("a.bin"), Held(65_536), Held(4_457), Through(7_777), Through(1_048_576), Through(1), end,
			Header("b.bin"), Held(65_536), Held(65_536), Through(917_509), Through(3), end,
			Header("c.bin"), Held(65_536), Held(1), Through(65_536), end,
			(30 + 5, async, false), Held(65_536),
			(30 + 19, async, false), Held(ZipPackages.ContentTypes.Length),
		],
		stream.Writes.Take(stream.Writes.Count - 2));
		var written = stream.ToArray();
		Assert.Equal(await ZipPackages.WriteAsync(parts, CompressionLevel.NoCompression, new ZipTestStream(seekable), false), written);
		ZipStructure.AssertValid(written);
		ZipStructure.AssertPackagingReads(written);
		var zip = ZipStructure.Read(written);
		string[] large = ["a.bin", "b.bin", "c.bin"];
		Assert.Equal(seekable ? [] : large, zip.Entries.Where(e => e.Streamed).Select(e => e.Name));
		Assert.Equal(seekable ? large : [], zip.Entries.Where(e => e.Patched).Select(e => e.Name));
	}

	/// <summary>
	/// A canceled write to a stored entry that writes through throws before it takes any bytes (its CRC and sizes
	/// included), and leaves the writer whole: nothing reaches the stream, the entry's position stays, and the package is
	/// complete without the canceled bytes.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task TakesNothingFromACanceledWriteThrough()
	{
		var bytes = Data(70_000, 12);
		var stream = new ZipTestStream();
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.NoCompression);
		var entry = await package.OpenPartAsync("a.bin", true, CancellationToken.None);
		await entry.WriteAsync(bytes, 0, 70_000);
		var writes = stream.Writes.Count;
		using (var canceled = new CancellationTokenSource())
		{
			canceled.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entry.WriteAsync(bytes, 0, 3, canceled.Token));
		}

		Assert.Equal((writes, 70_000L), (stream.Writes.Count, entry.Position));
		await entry.WriteAsync(bytes, 0, 2, CancellationToken.None);
		await PackageWriter.CloseAsync(entry, true);
		Assert.False(package.Zip.Faulted);
		var types = await package.OpenPartAsync("[Content_Types].xml", true, CancellationToken.None);
		await types.WriteAsync(ZipPackages.ContentTypes, 0, ZipPackages.ContentTypes.Length);
		await PackageWriter.CloseAsync(types, true);
		await package.DisposeAsync(true);

		var written = stream.ToArray();
		ZipStructure.AssertValid(written);
		ZipStructure.AssertPackagingReads(written);
		ZipPackages.AssertReadsBack(written, [new ZipPart("a.bin", [.. bytes, bytes[0], bytes[1]])]);
	}

	/// <summary>A case's parts, each of seeded bytes, and its limits.</summary>
	/// <param name="name">The case.</param>
	/// <returns>The parts, and the limits.</returns>
	private static (List<ZipPart> Parts, ZipLimits Limits) Parts(string name)
	{
		var (sizes, limits) = Cases[name];
		var parts = sizes.Select((size, i) => new ZipPart(sizes.Length == 1 ? "data.bin" : $"parts/{i}.bin", Data(size, i))).ToList();

		return (parts, limits ?? ZipLimits.Default);
	}

	/// <summary>
	/// The sizes of a case's writes: each the size given, or for <c>random</c> seeded random sizes, half of them up to
	/// 2,000 bytes, three in ten about a buffer (60,000 to 70,000), and the rest up to 300,000.
	/// </summary>
	/// <param name="pieces">The pieces: a size, or <c>random</c>.</param>
	/// <returns>The next write's most bytes, given the bytes left.</returns>
	private static Func<int, int> Pieces(string pieces)
	{
		if (pieces != "random")
		{
			var size = int.Parse(pieces);

			return _ => size;
		}

		var random = new Random(7);

		return _ =>
		{
			var kind = random.Next(10);

			return kind < 5 ? random.Next(1, 2_001) : kind < 8 ? random.Next(60_000, 70_001) : random.Next(1, 300_001);
		};
	}

	/// <summary>
	/// Writes a stored package: each part opened, written in pieces, and closed, then <c>[Content_Types].xml</c> in the
	/// same pieces, and the package disposed, synchronously (each call's task completed when it returns) or asynchronously.
	/// Every pooled buffer has returned at the end.
	/// </summary>
	/// <param name="parts">The parts.</param>
	/// <param name="stream">The stream written to, left open.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="piece">The next write's most bytes, given the bytes left.</param>
	/// <param name="limits">The limits.</param>
	/// <returns>The bytes the stream holds.</returns>
	private static async Task<byte[]> WriteAsync(IReadOnlyList<ZipPart> parts, ZipTestStream stream, bool async, Func<int, int> piece, ZipLimits limits)
	{
		var package = new PackageWriter(stream, leaveOpen: true, CompressionLevel.NoCompression, limits);
		foreach (var part in parts.Append(new ZipPart("[Content_Types].xml", ZipPackages.ContentTypes)))
		{
			var entry = await Packages.Run(async, a => package.OpenPartAsync(part.Name, a, CancellationToken.None));
			for (var at = 0; at < part.Bytes.Length;)
			{
				var count = Math.Min(piece(part.Bytes.Length - at), part.Bytes.Length - at);
				await ZipPackages.WriteAsync(entry, part.Bytes, at, count, async);
				at += count;
			}

			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		}

		await ZipPackages.RunAsync(async, package.DisposeAsync);
		Assert.Equal(0, package.Zip.RentedBuffers);

		return stream.ToArray();
	}

	/// <summary>Seeded bytes, the same on every runtime: xorshift64's.</summary>
	/// <param name="length">How many.</param>
	/// <param name="seed">The seed.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Data(int length, int seed)
	{
		var bytes = new byte[length];
		var state = 0x9E3779B97F4A7C15UL * (ulong)(seed + 1);
		for (var i = 0; i < length; i++)
		{
			state ^= state << 13;
			state ^= state >> 7;
			state ^= state << 17;
			bytes[i] = (byte)(state >> 24);
		}

		return bytes;
	}

	/// <summary>The SHA-256 of bytes, in hex.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash, upper case.</returns>
	private static string Sha256(byte[] bytes)
	{
		using var sha = SHA256.Create();

		return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
	}
}
