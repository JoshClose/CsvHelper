#if NET
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CsvHelper;

/// <summary>
/// Turns a <see cref="ulong"/> of one bit per byte into an AVX-512 vector mask (AVX-512 VBMI), for the compresses of
/// <see cref="CompressCompactor"/> and <see cref="CompressFlattener"/>.
/// </summary>
internal static class VectorMask
{
	/// <summary>Byte i is i / 8: spreads byte j of a <see cref="ulong"/> over bytes 8j to 8j + 7.</summary>
	public static readonly Vector512<byte> SpreadIndex = Vector512.Create(Bytes(i => (byte)(i / 8)));

	/// <summary>Byte i is 1 &lt;&lt; (i % 8): the bit of byte i in its spread byte of the mask.</summary>
	public static readonly Vector512<byte> BitOfByte = Vector512.Create(Bytes(i => (byte)(1 << (i % 8))));

	/// <summary>
	/// A vector mask with all bits set in the bytes whose bit is set in <paramref name="bits"/>. The tables are parameters, so
	/// that a caller loads them once and keeps them in registers across its loop.
	/// </summary>
	/// <param name="bits">The bits, one per byte.</param>
	/// <param name="spreadIndex"><see cref="SpreadIndex"/>.</param>
	/// <param name="bitOfByte"><see cref="BitOfByte"/>.</param>
	/// <returns>The mask.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector512<byte> FromBits(ulong bits, Vector512<byte> spreadIndex, Vector512<byte> bitOfByte)
	{
		var spread = Avx512Vbmi.PermuteVar64x8(Vector512.Create(bits).AsByte(), spreadIndex);

		return Vector512.Equals(spread & bitOfByte, bitOfByte);
	}

	private static byte[] Bytes(Func<int, byte> value)
	{
		var bytes = new byte[64];
		for (var i = 0; i < bytes.Length; i++)
		{
			bytes[i] = value(i);
		}

		return bytes;
	}
}
#endif
