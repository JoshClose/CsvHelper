using System.IO.Compression;
using System.Text;
#if NET
using System.Runtime.Intrinsics.X86;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;
#endif

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The zip writer's CRC-32: each path that runs on this machine (slice-by-8 everywhere; PCLMULQDQ's fold and ARM64's
/// instructions where supported; the dispatch) against <c>System.IO.Hashing</c>'s over random data, at unaligned starts
/// and in random pieces; the check value; the GZip trailer's CRC of deflated entries; and the GZip header check.
/// </summary>
public class Crc32Tests
{
	/// <summary>The lengths checked: 0 to 17, around 64 and 128, then up to 1 MiB.</summary>
	private static readonly int[] Lengths =
	[
		.. Enumerable.Range(0, 18), 63, 64, 65, 79, 80, 127, 128, 129, 255, 256, 1000, 4095, 4096, 65535, 65536, 65537, 100_003, 1 << 20,
	];

	/// <summary>The paths: a name and the method, those this machine supports.</summary>
	public static TheoryData<string> Paths()
	{
		var data = new TheoryData<string> { "Append", "SliceBy8" };
#if NET
		if (Pclmulqdq.IsSupported)
		{
			data.Add("Fold");
		}

		if (ArmCrc32.Arm64.IsSupported)
		{
			data.Add("Arm64");
		}
#endif

		return data;
	}

	/// <summary>Each path gives System.IO.Hashing's CRC for every length, at each of eight unaligned starts.</summary>
	/// <param name="path">The path.</param>
	[Theory]
	[MemberData(nameof(Paths))]
	public void MatchesTheReferenceAtEveryLengthAndStart(string path)
	{
		var bytes = ZipPackages.RandomBytes((1 << 20) + 16, 11);
		foreach (var length in Lengths)
		{
			for (var start = 0; start < 8; start++)
			{
				var data = bytes.AsSpan(start, length);

				Assert.Equal(Reference(data), Run(path, 0, data));
			}
		}
	}

	/// <summary>Each path appends: random pieces of the data, each appended to the CRC of those before, give the whole's.</summary>
	/// <param name="path">The path.</param>
	[Theory]
	[MemberData(nameof(Paths))]
	public void AppendsInRandomPieces(string path)
	{
		var bytes = ZipPackages.RandomBytes(300_000, 12);
		var random = new Random(13);
		for (var round = 0; round < 20; round++)
		{
			var crc = 0u;
			for (var at = 0; at < bytes.Length;)
			{
				var piece = Math.Min(bytes.Length - at, random.Next(round % 2 == 0 ? 200 : 70_000));
				crc = Run(path, crc, bytes.AsSpan(at, piece));
				at += piece;
			}

			Assert.Equal(Reference(bytes), crc);
		}
	}

	/// <summary>The check value: <c>"123456789"</c>'s CRC is <c>0xCBF43926</c>, and nothing's is 0.</summary>
	[Fact]
	public void GivesTheCheckValue()
	{
		var check = Encoding.ASCII.GetBytes("123456789");

		Assert.Equal(0xCBF43926u, Crc32.Append(0, check));
		Assert.Equal(0xCBF43926u, Crc32.SliceBy8(0, check));
		Assert.Equal(0u, Crc32.Append(0, []));
		Assert.Equal(0u, Crc32.SliceBy8(0, []));
#if NET
		if (ArmCrc32.Arm64.IsSupported)
		{
			Assert.Equal(0xCBF43926u, Crc32.Arm64(0, check));
		}
#endif
	}

	/// <summary>
	/// Every entry's CRC, at every level, is System.IO.Hashing's of its bytes: the GZip trailer's for deflated entries on
	/// .NET 8 and later, the own CRC's for stored entries and before .NET 8.
	/// </summary>
	/// <param name="level">The compression.</param>
	[Theory]
	[MemberData(nameof(ZipWriterTests.Levels), MemberType = typeof(ZipWriterTests))]
	public void GivesEveryEntryItsCrc(CompressionLevel level)
	{
		var parts = new List<ZipPart>
		{
			new("empty.bin", []),
			new("one.bin", [42]),
			new("text.xml", ZipWriterTests.Bytes(200_000, 14)),
			new("random.bin", ZipPackages.RandomBytes(150_001, 15)),
		};

		var zip = ZipStructure.Read(ZipPackages.Write(parts, level));

		foreach (var part in parts)
		{
			Assert.Equal(Reference(part.Bytes), zip.Entries.Single(e => e.Name == part.Name).Central.Crc);
		}
	}

#if NET
	/// <summary>
	/// The GZip header is taken only as <c>1F 8B 08 00</c> (deflate, no flags: 10 bytes), whatever its MTIME, XFL and OS,
	/// however its bytes are split; any other start is refused.
	/// </summary>
	[Fact]
	public void TakesOnlyAGZipHeaderOfTenBytes()
	{
		byte[] good = [0x1F, 0x8B, 0x08, 0x00, 0x12, 0x34, 0x56, 0x78, 0x04, 0x0A];
		foreach (var split in new[] { 0, 1, 3, 4, 9, 10 })
		{
			var data = new ZipBuffers(new ZipWriter(new MemoryStream(), false, CompressionLevel.Fastest, ZipLimits.Default));
			var sink = new DeflaterSink(data);
			sink.Write(good, 0, split);
			sink.Write(good, split, good.Length - split);
			sink.Write([1, 2, 3], 0, 3);

			Assert.Equal(3, data.Length);
		}

		foreach (var bad in new byte[][] { [0x1F, 0x8C, 0x08, 0x00], [0x1E, 0x8B, 0x08, 0x00], [0x1F, 0x8B, 0x07, 0x00], [0x1F, 0x8B, 0x08, 0x08], [0x1F, 0x8B, 0x08, 0x01] })
		{
			var sink = new DeflaterSink(new ZipBuffers(new ZipWriter(new MemoryStream(), false, CompressionLevel.Fastest, ZipLimits.Default)));

			Assert.Throws<InvalidOperationException>(() => sink.Write([.. bad, 0, 0, 0, 0, 0, 0], 0, 10));
			Assert.Throws<InvalidOperationException>(() => DeflaterSink.CheckHeader(bad, 0));
		}

		DeflaterSink.CheckHeader([0x1F, 0x8B, 0x08, 0x00, 0xFF, 0xFF], 0);
		DeflaterSink.CheckHeader([0x00], 3);
	}
#endif

	/// <summary>Runs a path.</summary>
	/// <param name="path">The path's name.</param>
	/// <param name="crc">The CRC so far.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC with the bytes.</returns>
	private static uint Run(string path, uint crc, ReadOnlySpan<byte> data)
	{
		return path switch
		{
			"Append" => Crc32.Append(crc, data),
			"SliceBy8" => Crc32.SliceBy8(crc, data),
#if NET
			"Fold" => Crc32.Fold(crc, data),
			"Arm64" => Crc32.Arm64(crc, data),
#endif
			_ => throw new ArgumentOutOfRangeException(nameof(path), path, "No such path."),
		};
	}

	/// <summary>System.IO.Hashing's CRC-32.</summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC.</returns>
	private static uint Reference(ReadOnlySpan<byte> data) => System.IO.Hashing.Crc32.HashToUInt32(data);
}
