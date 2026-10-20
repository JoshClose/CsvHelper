using System.Buffers;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// The code that replaces SIMD and missing BCL members on .NET Standard / .NET Framework. On .NET 10 it is checked
/// against the BCL and the vector code (on .NET 8 the polyfills of the library's netstandard2.1 build, visible to the
/// tests, have the BCL's names); on every target the float formatter and parser are checked against each other.
/// </summary>
public class PortableTests
{
#if NET10_0_OR_GREATER
	[Fact]
	public void BitOperationsMatchTheBcl()
	{
		var rng = new Random(3);
		for (var i = 0; i < 200_000; i++)
		{
			// Values of every bit length, including 0 and all ones.
			var x = (ulong)rng.NextInt64() ^ ((ulong)rng.Next(2) << 63);
			x >>= rng.Next(0, 65) & 63;
			if (i % 1000 == 0) x = i % 2000 == 0 ? 0 : ulong.MaxValue;
			var y = (uint)(x >> rng.Next(0, 33));
			var shift = rng.Next(0, 64);
			Assert.Equal(System.Numerics.BitOperations.PopCount(x), Portable.PopCount(x));
			Assert.Equal(System.Numerics.BitOperations.PopCount(y), Portable.PopCount(y));
			Assert.Equal(System.Numerics.BitOperations.TrailingZeroCount(x), Portable.TrailingZeroCount(x));
			Assert.Equal(System.Numerics.BitOperations.TrailingZeroCount(y), Portable.TrailingZeroCount(y));
			Assert.Equal(System.Numerics.BitOperations.LeadingZeroCount(x), Portable.LeadingZeroCount(x));
			Assert.Equal(System.Numerics.BitOperations.LeadingZeroCount(y), Portable.LeadingZeroCount(y));
			Assert.Equal(System.Numerics.BitOperations.RotateRight(x, shift), Portable.RotateRight(x, shift));
			Assert.Equal(System.Numerics.BitOperations.RotateRight(y, shift & 31), Portable.RotateRight(y, shift & 31));
			var z = (ulong)rng.NextInt64() << rng.Next(0, 2);
			Assert.Equal(Math.BigMul(x, z, out var low), Portable.BigMul(x, z, out var portableLow));
			Assert.Equal(low, portableLow);
		}

		Assert.Equal(Math.BigMul(ulong.MaxValue, ulong.MaxValue, out var l1), Portable.BigMul(ulong.MaxValue, ulong.MaxValue, out var l2));
		Assert.Equal(l1, l2);
	}

	[Fact]
	public void Utf16ToUtf8MatchesTheBcl()
	{
		var rng = new Random(7);
		char[] alphabet = ['a', 'Z', '0', ',', '"', '\n', 'é', 'ÿ', '€', '中', '\uD83D', '\uDE00', '\uDBFF', '\uDC00', '�', '\u0080', '߿', 'ࠀ'];
		var expected = new byte[256];
		var actual = new byte[256];
		for (var i = 0; i < 100_000; i++)
		{
			var chars = new char[rng.Next(0, 40)];
			for (var k = 0; k < chars.Length; k++) chars[k] = alphabet[rng.Next(alphabet.Length)];
			var size = rng.Next(0, 4) == 0 ? rng.Next(0, 12) : 200;
			var final = rng.Next(2) == 0;
			var replace = rng.Next(4) != 0;
			var s1 = System.Text.Unicode.Utf8.FromUtf16(chars, expected.AsSpan(0, size), out var read1, out var written1, replace, final);
			var s2 = Portable.Utf16ToUtf8(chars, actual.AsSpan(0, size), out var read2, out var written2, replace, final);
			var context = $"{TestUtil.Escape(new string(chars))} size {size} final {final} replace {replace}";
			Assert.True(s1 == s2, $"status {s1} vs {s2}: {context}");
			Assert.True(read1 == read2 && written1 == written2, $"read {read1}/{read2} written {written1}/{written2}: {context}");
			Assert.True(expected.AsSpan(0, written1).SequenceEqual(actual.AsSpan(0, written2)), context);
		}
	}

	[Fact]
	public void AsciiHelpersMatchTheBcl()
	{
		var rng = new Random(9);
		var expected = new byte[64];
		var actual = new byte[64];
		for (var i = 0; i < 100_000; i++)
		{
			var chars = new char[rng.Next(0, 40)];
			for (var k = 0; k < chars.Length; k++) chars[k] = rng.Next(30) == 0 ? (char)rng.Next(0x80, 0x3000) : (char)rng.Next(0, 0x80);
			var size = rng.Next(0, 50);
			var s1 = Ascii.FromUtf16(chars, expected.AsSpan(0, size), out var w1);
			var s2 = Portable.NarrowAscii(chars, actual.AsSpan(0, size), out var w2);
			Assert.Equal(s1, s2);
			Assert.Equal(w1, w2);
			Assert.True(expected.AsSpan(0, w1).SequenceEqual(actual.AsSpan(0, w2)));

			var left = Encoding.UTF8.GetBytes(new string(chars, 0, Math.Min(chars.Length, 6)));
			var right = (byte[])left.Clone();
			for (var k = 0; k < right.Length; k++)
			{
				if (rng.Next(3) == 0) right[k] ^= 0x20;
			}

			if (rng.Next(10) == 0 && right.Length > 0) right[rng.Next(right.Length)]++;
			Assert.Equal(Ascii.EqualsIgnoreCase(left, right), Portable.AsciiEqualsIgnoreCase(left, right));
		}
	}

	[Fact]
	public void AsciiPrefixLengthMatchesTheBcl()
	{
		var rng = new Random(11);
		var bytes = new byte[80];
		for (var i = 0; i < 100_000; i++)
		{
			var length = rng.Next(0, bytes.Length + 1);
			for (var k = 0; k < length; k++) bytes[k] = (byte)rng.Next(0, 0x80);
			if (length > 0 && rng.Next(4) != 0) bytes[rng.Next(length)] = (byte)rng.Next(0x80, 0x100);
			var span = bytes.AsSpan(0, length);
			var expected = span.IndexOfAnyExceptInRange((byte)0, (byte)0x7F);
			Assert.Equal(expected < 0 ? length : expected, Portable.AsciiPrefixLength(span));
		}
	}

	[Fact]
	public void Masks64MatchTheVectorMasks()
	{
		var rng = new Random(13);
		byte[] alphabet = [(byte)',', (byte)';', (byte)'\t', (byte)'"', (byte)'\'', (byte)'#', (byte)'\r', (byte)'\n', (byte)'a', 0, 0x80, 0xFF, 0x2C ^ 0x80];
		var block = new byte[64];
		for (var i = 0; i < 100_000; i++)
		{
			for (var k = 0; k < block.Length; k++) block[k] = rng.Next(4) == 0 ? (byte)rng.Next(256) : alphabet[rng.Next(alphabet.Length)];
			byte separator = alphabet[rng.Next(3)], quote = alphabet[3 + rng.Next(2)], comment = alphabet[5];
			var vector = default(Masks128).Create(separator, quote, comment);
			var portable = default(Masks64).Create(separator, quote, comment);
			bool quotes = rng.Next(2) == 0, comments = rng.Next(2) == 0;
			vector.Compute(ref block[0], quotes, comments, out var s1, out var l1, out var c1, out var q1, out var m1);
			portable.Compute(ref block[0], quotes, comments, out var s2, out var l2, out var c2, out var q2, out var m2);
			Assert.Equal((s1, l1, c1, q1, m1), (s2, l2, c2, q2, m2));
		}
	}
#endif

	[Fact]
	public void FormattedFloatsParseBackExactly()
	{
		// Every finite double and float that the formatter writes parses back to the same bits with FastFloat: on .NET
		// Framework, whose own double.ToString and double.Parse are not exact, this checks both against each other.
		var rng = new Random(17);
		var buffer = new byte[32];
		for (var i = 0; i < 300_000; i++)
		{
			var bits = rng.NextInt64() ^ ((long)rng.Next(2) << 63);
			var d = BitConverter.Int64BitsToDouble(bits);
			Assert.True(FloatFormatter.TryFormat(d, buffer, out var written));
			if (double.IsNaN(d) || double.IsInfinity(d)) continue;
			ReadOnlySpan<byte> text = buffer.AsSpan(0, written);
			Assert.True(FastFloat.TryParseDouble(text, out var parsed), Encoding.UTF8.GetString(buffer, 0, written));
			Assert.True(bits == BitConverter.DoubleToInt64Bits(parsed), $"{Encoding.UTF8.GetString(buffer, 0, written)}: 0x{bits:X16}");

			var f = BitConverter.ToSingle(BitConverter.GetBytes((int)bits), 0);
			Assert.True(FloatFormatter.TryFormat(f, buffer, out written));
			if (float.IsNaN(f) || float.IsInfinity(f)) continue;
			Assert.True(FastFloat.TryParseSingle(buffer.AsSpan(0, written), out var parsedSingle), Encoding.UTF8.GetString(buffer, 0, written));
			Assert.True(BitConverter.ToInt32(BitConverter.GetBytes(f), 0) == BitConverter.ToInt32(BitConverter.GetBytes(parsedSingle), 0),
				Encoding.UTF8.GetString(buffer, 0, written));
		}
	}
}
