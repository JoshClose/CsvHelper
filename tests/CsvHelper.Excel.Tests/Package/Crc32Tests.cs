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
/// constants against their generator; the check value; the GZip trailer's CRC of deflated entries; the GZip header
/// check; and the combine of two pieces' CRCs (zlib's <c>crc32_combine</c>) against the whole's at random splits, with
/// empty pieces, and at lengths past 2^32 through identities and an independent computation.
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

	/// <summary>
	/// The lengths the combine is checked at without the data: small ones, around 2^31 and 2^32 (where a length passes 32
	/// bits, and zlib's table of powers wraps), far past them, and the largest a <see cref="long"/> holds.
	/// </summary>
	private static readonly long[] CombinedLengths =
	[
		0, 1, 7, 8, 255, 65_536, int.MaxValue, 1L << 31, (1L << 32) - 1, 1L << 32, (1L << 32) + 1, (3L << 32) + 12_345,
		1L << 40, (1L << 61) - 1, long.MaxValue,
	];

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

	/// <summary>
	/// Combining: random data cut at random (empty pieces among them, from one piece to 20), each piece's CRC from 0, and the
	/// pieces combined in order with the CRC of those before, or as a tree (each half's CRC combined with the next half's),
	/// give the whole's.
	/// </summary>
	[Fact]
	public void CombinesAtRandomSplits()
	{
		var bytes = ZipPackages.RandomBytes(300_000, 21);
		var random = new Random(22);
		for (var round = 0; round < 64; round++)
		{
			var length = round % 4 == 0 ? random.Next(200) : random.Next(bytes.Length + 1);
			var cuts = Enumerable.Range(0, random.Next(20)).Select(_ => random.Next(length + 1)).Append(0).Append(length).OrderBy(cut => cut).ToArray();
			var whole = Reference(bytes.AsSpan(0, length));

			var crc = 0u;
			for (var i = 0; i + 1 < cuts.Length; i++)
			{
				crc = Crc32.Combine(crc, Reference(bytes.AsSpan(cuts[i], cuts[i + 1] - cuts[i])), cuts[i + 1] - cuts[i]);
			}

			// The round rides along, so that a failure names it.
			Assert.Equal((round, whole), (round, crc));
			Assert.Equal((round, whole), (round, CombineTree(bytes, cuts, 0, cuts.Length - 1)));
		}
	}

	/// <summary>
	/// Combining with an empty piece changes nothing, at any length: an empty second piece (CRC 0, length 0) keeps the first
	/// piece's CRC, and an empty first piece (CRC 0) gives the second's, whatever the second's length, past 2^32 too.
	/// </summary>
	[Fact]
	public void CombinesWithEmptyPieces()
	{
		var random = new Random(23);
		var data = ZipPackages.RandomBytes(1_000, 24);

		Assert.Equal(Reference(data), Crc32.Combine(Reference(data), Reference([]), 0));
		Assert.Equal(Reference(data), Crc32.Combine(Reference([]), Reference(data), data.Length));
		foreach (var length in CombinedLengths)
		{
			for (var i = 0; i < 8; i++)
			{
				var crc = (uint)random.Next() ^ ((uint)random.Next() << 16);

				// The length rides along, so that a failure names it.
				Assert.Equal((length, crc), (length, Crc32.Combine(crc, 0, 0)));
				Assert.Equal((length, crc), (length, Crc32.Combine(0, crc, length)));
			}
		}
	}

	/// <summary>
	/// Combining at lengths past 2^32, without the data: it is associative (two pieces combined, then a third, as the first
	/// with the other two combined), linear in the second CRC, and the same as this test's own computation, which works in
	/// the normal bit order, multiplying by x^(8n) mod P by squaring over the whole of n (no table, no wrap), and which is
	/// checked against data at small lengths first.
	/// </summary>
	[Fact]
	public void CombinesLengthsPastFourGigabytes()
	{
		var bytes = ZipPackages.RandomBytes(5_000, 25);
		var random = new Random(26);
		for (var round = 0; round < 200; round++)
		{
			var length = random.Next(bytes.Length + 1);
			var cut = random.Next(length + 1);

			Assert.Equal(Reference(bytes.AsSpan(0, length)), ExpectedCombine(Reference(bytes.AsSpan(0, cut)), Reference(bytes.AsSpan(cut, length - cut)), length - cut));
		}

		var crcs = Enumerable.Range(0, 3).Select(_ => (uint)random.Next() ^ ((uint)random.Next() << 16)).ToArray();
		var (a, b, c) = (crcs[0], crcs[1], crcs[2]);
		foreach (var second in CombinedLengths)
		{
			// The lengths ride along, so that a failure names them.
			Assert.Equal((second, ExpectedCombine(a, b, second)), (second, Crc32.Combine(a, b, second)));
			Assert.Equal((second, Crc32.Combine(a, 0, second) ^ b), (second, Crc32.Combine(a, b, second)));
			foreach (var third in CombinedLengths)
			{
				if (second > long.MaxValue - third)
				{
					continue;
				}

				Assert.Equal((second, third, Crc32.Combine(Crc32.Combine(a, b, second), c, third)), (second, third, Crc32.Combine(a, Crc32.Combine(b, c, third), second + third)));
			}
		}
	}

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

	/// <summary>The CRC of the pieces between two cuts, combined as a tree: the first half's with the second half's.</summary>
	/// <param name="bytes">The data.</param>
	/// <param name="cuts">The cuts, in order, the first 0 and the last the data's length.</param>
	/// <param name="from">The first cut of the pieces.</param>
	/// <param name="to">The last cut of the pieces.</param>
	/// <returns>The CRC of the bytes from the first cut to the last.</returns>
	private static uint CombineTree(byte[] bytes, int[] cuts, int from, int to)
	{
		if (to - from == 1)
		{
			return Reference(bytes.AsSpan(cuts[from], cuts[to] - cuts[from]));
		}

		var middle = (from + to) / 2;

		return Crc32.Combine(CombineTree(bytes, cuts, from, middle), CombineTree(bytes, cuts, middle, to), cuts[to] - cuts[middle]);
	}

	/// <summary>
	/// The CRC of two pieces from theirs, as this test computes it apart from zlib's method: the first CRC, as a polynomial
	/// in the normal bit order (reflected), times x^(8n) mod P, computed as (x^8)^n by squaring over n's bits, reflected
	/// back, plus the second CRC.
	/// </summary>
	/// <param name="first">The first piece's CRC.</param>
	/// <param name="second">The second piece's CRC.</param>
	/// <param name="secondLength">The second piece's length.</param>
	/// <returns>The CRC of both.</returns>
	private static uint ExpectedCombine(uint first, uint second, long secondLength)
	{
		var power = 1UL;
		var square = 1UL << 8;
		for (var n = secondLength; n != 0; n >>= 1)
		{
			if ((n & 1) != 0)
			{
				power = MultiplyModP(power, square);
			}

			square = MultiplyModP(square, square);
		}

		return (uint)Reflect(MultiplyModP(Reflect(first, 32), power), 32) ^ second;
	}

	/// <summary>Multiplies two polynomials of degree below 32 in the normal bit order, carry-less, and reduces the product mod P.</summary>
	/// <param name="a">The first.</param>
	/// <param name="b">The second.</param>
	/// <returns>The product mod P: below 2^32.</returns>
	private static ulong MultiplyModP(ulong a, ulong b)
	{
		var product = 0UL;
		for (var i = 0; i < 32; i++)
		{
			if (((b >> i) & 1) != 0)
			{
				product ^= a << i;
			}
		}

		for (var bit = 63; bit >= 32; bit--)
		{
			if (((product >> bit) & 1) != 0)
			{
				product ^= Normal() << (bit - 32);
			}
		}

		return product;
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
