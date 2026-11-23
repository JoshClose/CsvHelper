using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Xunit.Sdk;
#if NET
using System.Runtime.Intrinsics.X86;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;
#endif

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The zip writer's CRC-32: each path (slice-by-8 everywhere; PCLMULQDQ's 128-bit fold and ARM64's instructions on .NET 8
/// and later; VPCLMULQDQ's 256- and 512-bit folds on .NET 10; the dispatch) against <c>System.IO.Hashing</c>'s over random
/// data, at every length to 1,100, at random lengths to 4 MiB and around each path's minimum and steps, at unaligned starts,
/// from 0 and carried, and in random pieces, a path this build or machine hasn't skipped with the reason; the folds'
/// constants against their generator; the check value; the GZip trailer's CRC of deflated entries; and the GZip header
/// check.
/// </summary>
public class Crc32Tests
{
	/// <summary>Zip's polynomial, reflected: the generator's own, not taken from the code.</summary>
	private const uint ReflectedPolynomial = 0xEDB88320;

	/// <summary>The random bytes the paths run over: 4 MiB, and 64 bytes more for the starts.</summary>
	private static readonly byte[] Bytes = ZipPackages.RandomBytes((4 << 20) + 64, 11);

	/// <summary>
	/// The lengths around the paths' minimums and steps: 64 (the 128-bit fold's minimum and step), 128 and 256 (the 256- and
	/// 512-bit folds'), 512 (the 512-bit fold's second step), 4,096 and 1 MiB, each with 0, 16, 64, 128, 192 and 240 bytes
	/// more (the steps of the folds that finish a wider one), and a byte either side.
	/// </summary>
	private static readonly int[] Edges =
	[
		.. new[] { 64, 128, 256, 512, 4096, 1 << 20 }
			.SelectMany(n => new[] { 0, 16, 64, 128, 192, 240 }, (n, more) => n + more)
			.SelectMany(n => new[] { -1, 0, 1 }, (n, by) => n + by),
	];

	/// <summary>The starts the edges are checked at: aligned and not, to 16, 32 and 64 bytes.</summary>
	private static readonly int[] Starts = [0, 1, 7, 8, 16, 31, 32, 63];

	/// <summary>Each path gives System.IO.Hashing's CRC at every length from 0 to 1,100, at each of 64 starts.</summary>
	/// <param name="path">The path.</param>
	[Theory]
	[PathData("Append")]
	[PathData("SliceBy8")]
	[PathData("Fold")]
	[PathData("Fold256")]
	[PathData("Fold512")]
	[PathData("Arm64")]
	public void MatchesTheReferenceAtEveryLength(string path)
	{
		for (var length = 0; length <= 1100; length++)
		{
			for (var start = 0; start < 64; start++)
			{
				Check(path, start, length);
			}
		}
	}

	/// <summary>
	/// Each path gives System.IO.Hashing's CRC at random lengths to 4 MiB, half of any size and half spread over the powers
	/// of two, at random starts.
	/// </summary>
	/// <param name="path">The path.</param>
	[Theory]
	[PathData("Append")]
	[PathData("SliceBy8")]
	[PathData("Fold")]
	[PathData("Fold256")]
	[PathData("Fold512")]
	[PathData("Arm64")]
	public void MatchesTheReferenceAtRandomLengths(string path)
	{
		var random = new Random(16);
		for (var round = 0; round < 64; round++)
		{
			var length = round % 2 == 0 ? random.Next((4 << 20) + 1) : random.Next(1 << random.Next(23));

			Check(path, random.Next(64), length);
		}
	}

	/// <summary>Each path gives System.IO.Hashing's CRC around each path's minimum and steps (<see cref="Edges"/>).</summary>
	/// <param name="path">The path.</param>
	[Theory]
	[PathData("Append")]
	[PathData("SliceBy8")]
	[PathData("Fold")]
	[PathData("Fold256")]
	[PathData("Fold512")]
	[PathData("Arm64")]
	public void MatchesTheReferenceAroundTheMinimumsAndSteps(string path)
	{
		foreach (var length in Edges)
		{
			foreach (var start in Starts)
			{
				Check(path, start, length);
			}
		}
	}

	/// <summary>
	/// Each path appends: random pieces of the data, to 200 bytes, 2,000 or 70,000, each appended to the CRC of those before,
	/// give the whole's.
	/// </summary>
	/// <param name="path">The path.</param>
	[Theory]
	[PathData("Append")]
	[PathData("SliceBy8")]
	[PathData("Fold")]
	[PathData("Fold256")]
	[PathData("Fold512")]
	[PathData("Arm64")]
	public void AppendsInRandomPieces(string path)
	{
		var bytes = ZipPackages.RandomBytes(300_000, 12);
		var random = new Random(13);
		for (var round = 0; round < 21; round++)
		{
			var most = (round % 3) switch
			{
				0 => 200,
				1 => 2_000,
				_ => 70_000,
			};

			var crc = 0u;
			for (var at = 0; at < bytes.Length;)
			{
				var piece = Math.Min(bytes.Length - at, random.Next(most));
				crc = Run(path, crc, bytes.AsSpan(at, piece));
				at += piece;
			}

			Assert.Equal(Reference(bytes), crc);
		}
	}

#if NET
	/// <summary>
	/// The folds' constants are their generator's, none typed in by hand: each <c>X</c><i>n</i> is x^n mod P, and Barrett's
	/// polynomial and quotient are P and x^64 / P, in the folds' bit order. Every <c>ulong</c> constant <see cref="Crc32"/>
	/// holds is checked, and they are the ones this build's folds use.
	/// </summary>
	[Fact]
	public void HoldsTheGeneratedConstants()
	{
		var constants = typeof(Crc32).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(field => field.IsLiteral && field.FieldType == typeof(ulong))
			.ToDictionary(field => field.Name, field => (ulong)field.GetRawConstantValue()!);

		foreach (var (name, value) in constants)
		{
			var expected = name switch
			{
				"BarrettPolynomial" => Reflect(Normal(), 33),
				"BarrettQuotient" => Quotient(),
				_ when name[0] == 'X' => Power(int.Parse(name.Substring(1), CultureInfo.InvariantCulture)),
				_ => throw new InvalidOperationException($"{name} has no generator."),
			};

			// The name rides along, so that a failure names the constant.
			Assert.Equal((name, expected), (name, value));
		}

		string[] names =
		[
#if NET10_0_OR_GREATER
			"X2080", "X2016", "X1056", "X992",
#endif
			"X544", "X480", "X160", "X96", "X64", "BarrettPolynomial", "BarrettQuotient",
		];

		Assert.Equal(names.Order(), constants.Keys.Order());
	}
#endif

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

	/// <summary>Why a path can't run in this build on this machine.</summary>
	/// <param name="path">The path's name.</param>
	/// <returns>The reason, or null where the path runs.</returns>
	internal static string? WhyNot(string path)
	{
		return path switch
		{
			"Append" or "SliceBy8" => null,
#if NET
			"Fold" => Pclmulqdq.IsSupported ? null : "This machine has no PCLMULQDQ (Pclmulqdq).",
			"Arm64" => ArmCrc32.Arm64.IsSupported ? null : "This machine has no ARM64 CRC-32 instructions (Crc32.Arm64).",
#else
			"Fold" or "Arm64" => "Only the .NET 8 and later builds have it.",
#endif
#if NET10_0_OR_GREATER
			"Fold256" => Pclmulqdq.V256.IsSupported ? null : "This machine has no VPCLMULQDQ on 256-bit vectors (Pclmulqdq.V256).",
			"Fold512" => Pclmulqdq.V512.IsSupported ? null : "This machine has no VPCLMULQDQ on 512-bit vectors (Pclmulqdq.V512).",
#else
			"Fold256" or "Fold512" => "Only the .NET 10 build has it.",
#endif
			_ => throw new ArgumentOutOfRangeException(nameof(path), path, "No such path."),
		};
	}

	/// <summary>
	/// Checks a path over the bytes from a start against System.IO.Hashing's CRC: from 0, and carried from the CRC of the
	/// bytes before the start.
	/// </summary>
	/// <param name="path">The path.</param>
	/// <param name="start">The first byte's offset in <see cref="Bytes"/>.</param>
	/// <param name="length">How many bytes.</param>
	private static void Check(string path, int start, int length)
	{
		var data = Bytes.AsSpan(start, length);
		var before = Reference(Bytes.AsSpan(0, start));

		// The length and start ride along, so that a failure names them.
		Assert.Equal((length, start, Reference(data)), (length, start, Run(path, 0, data)));
		Assert.Equal((length, start, Reference(Bytes.AsSpan(0, start + length))), (length, start, Run(path, before, data)));
	}

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
#if NET10_0_OR_GREATER
			"Fold256" => Crc32.Fold256(crc, data),
			"Fold512" => Crc32.Fold512(crc, data),
#endif
			_ => throw new ArgumentOutOfRangeException(nameof(path), path, "No such path."),
		};
	}

	/// <summary>System.IO.Hashing's CRC-32.</summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC.</returns>
	private static uint Reference(ReadOnlySpan<byte> data) => System.IO.Hashing.Crc32.HashToUInt32(data);

	/// <summary>
	/// The generator: x^n mod P in the folds' bit order. The remainder is kept reflected (x^0 the top bit of 32, so that
	/// multiplying by x shifts it right, and an x^32 shifted out is replaced by the rest of P), then shifted left one bit
	/// into 33, which the carry-less product of reflected numbers, 127 bits in 128, needs.
	/// </summary>
	/// <param name="n">The power.</param>
	/// <returns>The constant.</returns>
	private static ulong Power(int n)
	{
		var remainder = 0x80000000u;
		for (var i = 0; i < n; i++)
		{
			remainder = (remainder & 1) != 0 ? (remainder >> 1) ^ ReflectedPolynomial : remainder >> 1;
		}

		return (ulong)remainder << 1;
	}

	/// <summary>
	/// The generator of Barrett's quotient: x^64 / P (33 bits), by long division in the normal bit order, then reflected.
	/// </summary>
	/// <returns>The quotient, reflected.</returns>
	private static ulong Quotient()
	{
		var polynomial = Normal();
		var window = 1UL << 32;
		var quotient = 0UL;
		for (var bit = 32; bit >= 0; bit--)
		{
			if ((window >> 32) != 0)
			{
				window ^= polynomial;
				quotient |= 1UL << bit;
			}

			window <<= 1;
		}

		return Reflect(quotient, 33);
	}

	/// <summary>P in the normal bit order, x^32 included: 33 bits.</summary>
	/// <returns>The polynomial.</returns>
	private static ulong Normal() => Reflect(ReflectedPolynomial, 32) | (1UL << 32);

	/// <summary>Reverses the low bits of a number.</summary>
	/// <param name="value">The number.</param>
	/// <param name="bits">How many of its low bits.</param>
	/// <returns>Those bits, reversed.</returns>
	private static ulong Reflect(ulong value, int bits)
	{
		var reflected = 0UL;
		for (var i = 0; i < bits; i++)
		{
			reflected = (reflected << 1) | ((value >> i) & 1);
		}

		return reflected;
	}
}

/// <summary>
/// A CRC-32 path as a theory's data (<see cref="Crc32Tests"/>), skipped with the reason where this build or machine hasn't
/// the path.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class PathDataAttribute : DataAttribute
{
	/// <summary>Creates the attribute, skipping the path's test where the path can't run.</summary>
	/// <param name="path">The path's name.</param>
	public PathDataAttribute(string path)
	{
		Path = path;
		var reason = Crc32Tests.WhyNot(path);
		if (reason is not null)
		{
			Skip = reason;
		}
	}

	/// <summary>The path's name.</summary>
	public string Path { get; }

	/// <summary>The theory's data: the path's name.</summary>
	/// <param name="testMethod">The theory.</param>
	/// <returns>One row, the path's name.</returns>
	public override IEnumerable<object[]> GetData(MethodInfo testMethod)
	{
		return [[Path]];
	}
}
