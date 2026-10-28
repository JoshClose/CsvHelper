using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CsvHelper;

/// <summary>
/// Plain C# versions of what .NET Standard 2.0 and .NET Framework lack: bit operations, the 128-bit product,
/// UTF-16 to UTF-8 transcoding, and byte matching 8 bytes at a time in a <see cref="ulong"/>. The polyfills in
/// Polyfills/ forward here. Compiled for every target, so the tests can compare them with the BCL on .NET 10.
/// </summary>
internal static class Portable
{
	private const ulong Lows7 = 0x7F7F_7F7F_7F7F_7F7FUL;
	private const ulong DeBruijn64 = 0x03F7_9D71_B4CB_0A89UL;

	// TrailingZeroCount: the de Bruijn product of the lowest set bit has a distinct top 6 bits for each bit position.
	private static readonly byte[] trailingZeros = CreateTrailingZeros();

	private static byte[] CreateTrailingZeros()
	{
		var table = new byte[64];
		for (var i = 0; i < 64; i++) table[((1UL << i) * DeBruijn64) >> 58] = (byte)i;

		return table;
	}

	/// <summary>The number of set bits, as <c>System.Numerics.BitOperations.PopCount(ulong)</c> returns it.</summary>
	/// <param name="x">The value whose bits are counted.</param>
	/// <returns>The number of bits of <paramref name="x"/> that are 1, from 0 to 64.</returns>
	public static int PopCount(ulong x)
	{
		x -= (x >> 1) & 0x5555_5555_5555_5555UL;
		x = (x & 0x3333_3333_3333_3333UL) + ((x >> 2) & 0x3333_3333_3333_3333UL);
		x = (x + (x >> 4)) & 0x0F0F_0F0F_0F0F_0F0FUL;

		return (int)((x * 0x0101_0101_0101_0101UL) >> 56);
	}

	/// <summary>The number of set bits, as <c>System.Numerics.BitOperations.PopCount(uint)</c> returns it.</summary>
	/// <param name="x">The value whose bits are counted.</param>
	/// <returns>The number of bits of <paramref name="x"/> that are 1, from 0 to 32.</returns>
	public static int PopCount(uint x)
	{
		x -= (x >> 1) & 0x5555_5555u;
		x = (x & 0x3333_3333u) + ((x >> 2) & 0x3333_3333u);
		x = (x + (x >> 4)) & 0x0F0F_0F0Fu;

		return (int)((x * 0x0101_0101u) >> 24);
	}

	/// <summary>
	/// The number of zero bits below the lowest set bit, as <c>System.Numerics.BitOperations.TrailingZeroCount(ulong)</c>
	/// returns it.
	/// </summary>
	/// <param name="x">The value whose low zero bits are counted.</param>
	/// <returns>The index of the lowest set bit of <paramref name="x"/>, or 64 when <paramref name="x"/> is 0.</returns>
	public static int TrailingZeroCount(ulong x) => x == 0 ? 64 : trailingZeros[((x & (0 - x)) * DeBruijn64) >> 58];

	/// <summary>
	/// The number of zero bits below the lowest set bit, as <c>System.Numerics.BitOperations.TrailingZeroCount(uint)</c>
	/// returns it.
	/// </summary>
	/// <param name="x">The value whose low zero bits are counted.</param>
	/// <returns>The index of the lowest set bit of <paramref name="x"/>, or 32 when <paramref name="x"/> is 0.</returns>
	public static int TrailingZeroCount(uint x) => x == 0 ? 32 : trailingZeros[(((ulong)x & (0 - (ulong)x)) * DeBruijn64) >> 58];

	/// <summary>
	/// The number of zero bits above the highest set bit, as <c>System.Numerics.BitOperations.LeadingZeroCount(ulong)</c>
	/// returns it.
	/// </summary>
	/// <param name="x">The value whose high zero bits are counted.</param>
	/// <returns>The number of leading zero bits of <paramref name="x"/>, or 64 when <paramref name="x"/> is 0.</returns>
	public static int LeadingZeroCount(ulong x)
	{
		x |= x >> 1;
		x |= x >> 2;
		x |= x >> 4;
		x |= x >> 8;
		x |= x >> 16;
		x |= x >> 32;

		return 64 - PopCount(x);
	}

	/// <summary>
	/// The number of zero bits above the highest set bit, as <c>System.Numerics.BitOperations.LeadingZeroCount(uint)</c>
	/// returns it.
	/// </summary>
	/// <param name="x">The value whose high zero bits are counted.</param>
	/// <returns>The number of leading zero bits of <paramref name="x"/>, or 32 when <paramref name="x"/> is 0.</returns>
	public static int LeadingZeroCount(uint x)
	{
		x |= x >> 1;
		x |= x >> 2;
		x |= x >> 4;
		x |= x >> 8;
		x |= x >> 16;

		return 32 - PopCount(x);
	}

	/// <summary>Rotates the bits right, as <c>System.Numerics.BitOperations.RotateRight(ulong, int)</c> does.</summary>
	/// <param name="x">The value to rotate.</param>
	/// <param name="n">The number of bits to rotate by; only its low 6 bits are used.</param>
	/// <returns><paramref name="x"/> rotated right by <paramref name="n"/> bits.</returns>
	// C# masks shift counts, so a rotation by 0 (or 64) returns the value unchanged.
	public static ulong RotateRight(ulong x, int n) => (x >> n) | (x << (64 - n));

	/// <summary>Rotates the bits right, as <c>System.Numerics.BitOperations.RotateRight(uint, int)</c> does.</summary>
	/// <param name="x">The value to rotate.</param>
	/// <param name="n">The number of bits to rotate by; only its low 5 bits are used.</param>
	/// <returns><paramref name="x"/> rotated right by <paramref name="n"/> bits.</returns>
	public static uint RotateRight(uint x, int n) => (x >> n) | (x << (32 - n));

	/// <summary>
	/// The 128-bit product of two 64-bit values, from four 32-bit products, as <c>System.Math.BigMul(ulong, ulong, out ulong)</c>
	/// returns it.
	/// </summary>
	/// <param name="a">The first factor.</param>
	/// <param name="b">The second factor.</param>
	/// <param name="low">The low 64 bits of the product.</param>
	/// <returns>The high 64 bits of the product.</returns>
	public static ulong BigMul(ulong a, ulong b, out ulong low)
	{
		ulong al = (uint)a, ah = a >> 32, bl = (uint)b, bh = b >> 32;
		ulong ll = al * bl, lh = al * bh, hl = ah * bl, hh = ah * bh;
		var mid = (ll >> 32) + (uint)lh + (uint)hl;
		low = (mid << 32) | (uint)ll;

		return hh + (lh >> 32) + (hl >> 32) + (mid >> 32);
	}

	/// <summary>The 8 bytes at <paramref name="p"/>, the first one in the lowest bits whatever the byte order.</summary>
	/// <param name="p">The first of the 8 bytes, which need not be aligned.</param>
	/// <returns>The bytes as a little-endian <see cref="ulong"/>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong LoadLittleEndian(ref byte p)
	{
		var v = Unsafe.ReadUnaligned<ulong>(ref p);

		return BitConverter.IsLittleEndian ? v : BinaryPrimitives.ReverseEndianness(v);
	}

	/// <summary><paramref name="value"/> in every byte of a <see cref="ulong"/>.</summary>
	/// <param name="value">The byte to repeat.</param>
	/// <returns>A <see cref="ulong"/> whose 8 bytes are all <paramref name="value"/>, for <see cref="MatchBytes"/>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Repeat(byte value) => 0x0101_0101_0101_0101UL * value;

	/// <summary>
	/// The high bit of each byte of <paramref name="word"/> that equals the corresponding byte of
	/// <paramref name="pattern"/>, all other bits clear. Exact: unlike the usual (x - 1) &amp; ~x test, a borrow cannot
	/// mark the byte above a match.
	/// </summary>
	/// <param name="word">The 8 bytes to search.</param>
	/// <param name="pattern">The bytes to compare them with, usually one byte in every position (<see cref="Repeat"/>).</param>
	/// <returns>Bit 8i + 7 set for each byte i of <paramref name="word"/> that matches; every other bit clear.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong MatchBytes(ulong word, ulong pattern)
	{
		var x = word ^ pattern;

		return ~(((x & Lows7) + Lows7) | x | Lows7);
	}

	/// <summary>
	/// Gathers the high bits of the bytes of <paramref name="highBits"/> (only bits 7, 15, .., 63 may be set) into
	/// bits 0..7: byte i goes to bit i. Each byte's bit lands on its own position of the top byte, without carries.
	/// </summary>
	/// <param name="highBits">A result of <see cref="MatchBytes"/>: only the high bit of each byte may be set.</param>
	/// <returns>An 8-bit mask: bit i is set when the high bit of byte i of <paramref name="highBits"/> is.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong MoveMask(ulong highBits) => (highBits * 0x0002_0408_1020_4081UL) >> 56;

	/// <summary>
	/// The number of ASCII bytes at the start of <paramref name="s"/> (<c>IndexOfAnyExceptInRange(0, 0x7F)</c>, or the
	/// length when every byte is ASCII), 8 bytes at a time.
	/// </summary>
	/// <param name="s">The bytes to check.</param>
	/// <returns>The index of the first byte of <paramref name="s"/> that is 0x80 or above, or its length when there is none.</returns>
	public static int AsciiPrefixLength(ReadOnlySpan<byte> s)
	{
		const ulong Highs = 0x8080_8080_8080_8080UL;
		ref var p = ref MemoryMarshal.GetReference(s);
		var i = 0;
		for (; i <= s.Length - 8; i += 8)
		{
			var high = LoadLittleEndian(ref Unsafe.Add(ref p, i)) & Highs;
			if (high != 0) return i + (TrailingZeroCount(high) >> 3);
		}

		for (; i < s.Length; i++)
		{
			if (Unsafe.Add(ref p, i) >= 0x80) return i;
		}

		return s.Length;
	}

	/// <summary>Bit i is set when byte i of the 64 bytes at <paramref name="p"/> equals the byte in <paramref name="pattern"/>.</summary>
	/// <param name="p">The first of the 64 bytes to search, which need not be aligned.</param>
	/// <param name="pattern">The byte to find, in every byte of the value (<see cref="Repeat"/>).</param>
	/// <returns>A 64-bit mask of the matching bytes, the byte at <paramref name="p"/> in bit 0.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong MatchBits64(ref byte p, ulong pattern) =>
		MoveMask(MatchBytes(LoadLittleEndian(ref p), pattern))
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 8)), pattern)) << 8)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 16)), pattern)) << 16)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 24)), pattern)) << 24)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 32)), pattern)) << 32)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 40)), pattern)) << 40)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 48)), pattern)) << 48)
		| (MoveMask(MatchBytes(LoadLittleEndian(ref Unsafe.Add(ref p, 56)), pattern)) << 56);

	/// <summary>
	/// <c>System.Text.Ascii.FromUtf16</c>: copies chars as bytes while they are ASCII. Done when all of
	/// <paramref name="source"/> was copied, DestinationTooSmall when it did not fit (and everything that fitted was
	/// ASCII), InvalidData at the first non-ASCII char.
	/// </summary>
	/// <param name="source">The chars to copy.</param>
	/// <param name="destination">The bytes to copy them to.</param>
	/// <param name="bytesWritten">The number of bytes written, which is also the number of chars copied.</param>
	/// <returns>
	/// <see cref="OperationStatus.Done"/>, <see cref="OperationStatus.DestinationTooSmall"/> or
	/// <see cref="OperationStatus.InvalidData"/>, as the summary describes.
	/// </returns>
	public static OperationStatus NarrowAscii(ReadOnlySpan<char> source, Span<byte> destination, out int bytesWritten)
	{
		var count = Math.Min(source.Length, destination.Length);
		var success = source.Length <= destination.Length ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
		ref var src = ref MemoryMarshal.GetReference(source);
		ref var dst = ref MemoryMarshal.GetReference(destination);
		var i = 0;
		// Four chars at a time: the 8 bytes hold ASCII chars when no bit above the low 7 of each char is set.
		for (; i <= count - 4; i += 4)
		{
			var v = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref src, i)));
			if ((v & 0xFF80_FF80_FF80_FF80UL) != 0) break;
			Unsafe.Add(ref dst, i) = (byte)Unsafe.Add(ref src, i);
			Unsafe.Add(ref dst, i + 1) = (byte)Unsafe.Add(ref src, i + 1);
			Unsafe.Add(ref dst, i + 2) = (byte)Unsafe.Add(ref src, i + 2);
			Unsafe.Add(ref dst, i + 3) = (byte)Unsafe.Add(ref src, i + 3);
		}

		for (; i < count; i++)
		{
			var c = Unsafe.Add(ref src, i);
			if (c >= 0x80) break;
			Unsafe.Add(ref dst, i) = (byte)c;
		}

		bytesWritten = i;

		return i == count ? success : OperationStatus.InvalidData;
	}

	/// <summary>
	/// <c>System.Text.Unicode.Utf8.FromUtf16</c> with the same results: invalid surrogates become U+FFFD (or stop with
	/// InvalidData), a high surrogate at the end of a block that is not final is left for the next call (NeedMoreData),
	/// and a full destination stops at a whole scalar (DestinationTooSmall).
	/// </summary>
	/// <param name="source">The UTF-16 text to transcode.</param>
	/// <param name="destination">The bytes to write the UTF-8 to.</param>
	/// <param name="charsRead">The number of chars of <paramref name="source"/> that were transcoded.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <param name="replaceInvalidSequences">
	/// Whether an invalid surrogate becomes U+FFFD; otherwise transcoding stops there with
	/// <see cref="OperationStatus.InvalidData"/>.
	/// </param>
	/// <param name="isFinalBlock">
	/// Whether <paramref name="source"/> is the end of the text; otherwise a high surrogate at its end waits for the low
	/// surrogate of the next block.
	/// </param>
	/// <returns>
	/// <see cref="OperationStatus.Done"/> when all of <paramref name="source"/> was transcoded, else the reason it stopped:
	/// <see cref="OperationStatus.DestinationTooSmall"/>, <see cref="OperationStatus.NeedMoreData"/> or
	/// <see cref="OperationStatus.InvalidData"/>.
	/// </returns>
	public static OperationStatus Utf16ToUtf8(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead,
		out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
	{
		int i = 0, d = 0;
		var status = OperationStatus.Done;
		while (i < source.Length)
		{
			uint c = source[i];
			var consumed = 1;
			if (c < 0x80)
			{
				if (d >= destination.Length) { status = OperationStatus.DestinationTooSmall; break; }
				destination[d++] = (byte)c;
				i++;
				continue;
			}

			if (c - 0xD800u < 0x800u)
			{
				// A surrogate: valid only as a high surrogate followed by a low one.
				if (c < 0xDC00 && i + 1 < source.Length && source[i + 1] - 0xDC00u < 0x400u)
				{
					c = 0x10000 + ((c - 0xD800) << 10) + (source[i + 1] - 0xDC00u);
					consumed = 2;
				}
				else if (c < 0xDC00 && i + 1 == source.Length && !isFinalBlock)
				{
					status = OperationStatus.NeedMoreData;
					break;
				}
				else if (replaceInvalidSequences)
				{
					c = 0xFFFD;
				}
				else
				{
					status = OperationStatus.InvalidData;
					break;
				}
			}

			var length = c < 0x800 ? 2 : c < 0x10000 ? 3 : 4;
			if (destination.Length - d < length) { status = OperationStatus.DestinationTooSmall; break; }
			switch (length)
			{
				case 2:
					destination[d] = (byte)(0xC0 | (c >> 6));
					destination[d + 1] = (byte)(0x80 | (c & 0x3F));
					break;
				case 3:
					destination[d] = (byte)(0xE0 | (c >> 12));
					destination[d + 1] = (byte)(0x80 | ((c >> 6) & 0x3F));
					destination[d + 2] = (byte)(0x80 | (c & 0x3F));
					break;
				default:
					destination[d] = (byte)(0xF0 | (c >> 18));
					destination[d + 1] = (byte)(0x80 | ((c >> 12) & 0x3F));
					destination[d + 2] = (byte)(0x80 | ((c >> 6) & 0x3F));
					destination[d + 3] = (byte)(0x80 | (c & 0x3F));
					break;
			}

			d += length;
			i += consumed;
		}

		charsRead = i;
		bytesWritten = d;

		return status;
	}

	/// <summary><c>System.Text.Ascii.EqualsIgnoreCase</c> for UTF-8: equal lengths, all ASCII, equal ignoring case.</summary>
	/// <param name="left">The first bytes to compare.</param>
	/// <param name="right">The second bytes to compare.</param>
	/// <returns>
	/// Whether both have the same length, contain only ASCII, and are equal when ASCII letters are compared ignoring case.
	/// </returns>
	public static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
	{
		if (left.Length != right.Length) return false;
		for (var i = 0; i < left.Length; i++)
		{
			uint a = left[i], b = right[i];
			if ((a | b) >= 0x80) return false;
			if (a == b) continue;
			a |= 0x20;
			if (a != (b | 0x20) || a - 'a' > 'z' - 'a') return false;
		}

		return true;
	}
}
