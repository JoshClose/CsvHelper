using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;
#endif

namespace CsvHelper.Excel;

/// <summary>
/// Zip's CRC-32 (the reflected polynomial <c>0xEDB88320</c>), for the entries whose CRC no deflater gives: stored entries
/// on .NET 8 and later, every entry before (a deflated entry's CRC comes from <see cref="System.IO.Compression.GZipStream"/>'s
/// trailer on .NET 8 and later, where zlib computes it as it reads, and on .NET Framework that trailer's CRC is a managed
/// table, five times slower than slice-by-8). Each path is testable on its own: <see cref="SliceBy8"/> everywhere, and on
/// .NET 8 and later <c>Fold</c> where PCLMULQDQ is supported and <c>Arm64</c> where ARM64's CRC-32 instructions are.
/// </summary>
internal static class Crc32
{
	/// <summary>The reflected polynomial.</summary>
	private const uint Polynomial = 0xEDB88320;

	/// <summary>
	/// Slice-by-8's eight tables of 256 entries, one after the other: the first is the byte-at-a-time table, and each next
	/// one advances a byte further.
	/// </summary>
	private static readonly uint[] Tables = MakeTables();

	/// <summary>Appends bytes to a CRC, through the fastest path the machine supports.</summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	public static uint Append(uint crc, ReadOnlySpan<byte> data)
	{
#if NET
		if (data.Length >= 64 && Pclmulqdq.IsSupported)
		{
			return Fold(crc, data);
		}

		if (ArmCrc32.Arm64.IsSupported)
		{
			return Arm64(crc, data);
		}
#endif

		return SliceBy8(crc, data);
	}

	/// <summary>Appends bytes to a CRC eight bytes at a time, through eight tables (slice-by-8): 2.6 GB/s on every target.</summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	internal static uint SliceBy8(uint crc, ReadOnlySpan<byte> data)
	{
		ref var table = ref MemoryMarshal.GetArrayDataReference(Tables);
		ref var bytes = ref MemoryMarshal.GetReference(data);
		var length = data.Length;
		var i = 0;
		crc = ~crc;
		if (BitConverter.IsLittleEndian)
		{
			for (; i + 8 <= length; i += 8)
			{
				var one = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref bytes, i)) ^ crc;
				var two = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref bytes, i + 4));
				crc = Unsafe.Add(ref table, 0x700 + (int)(one & 0xFF)) ^ Unsafe.Add(ref table, 0x600 + (int)((one >> 8) & 0xFF))
					^ Unsafe.Add(ref table, 0x500 + (int)((one >> 16) & 0xFF)) ^ Unsafe.Add(ref table, 0x400 + (int)(one >> 24))
					^ Unsafe.Add(ref table, 0x300 + (int)(two & 0xFF)) ^ Unsafe.Add(ref table, 0x200 + (int)((two >> 8) & 0xFF))
					^ Unsafe.Add(ref table, 0x100 + (int)((two >> 16) & 0xFF)) ^ Unsafe.Add(ref table, (int)(two >> 24));
			}
		}

		for (; i < length; i++)
		{
			crc = Unsafe.Add(ref table, (int)((crc ^ Unsafe.Add(ref bytes, i)) & 0xFF)) ^ (crc >> 8);
		}

		return ~crc;
	}

#if NET
	/// <summary>
	/// Appends bytes to a CRC by folding 64 bytes at a time with carry-less multiplication, then Barrett's reduction (Intel's
	/// "Fast CRC Computation for Generic Polynomials Using PCLMULQDQ Instruction", as zlib's and Chromium's
	/// <c>crc32_simd</c> do it), the tail of fewer than 16 bytes by slice-by-8. Callable where
	/// <see cref="Pclmulqdq.IsSupported"/>; fewer than 64 bytes go through slice-by-8.
	/// </summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	internal static uint Fold(uint crc, ReadOnlySpan<byte> data)
	{
		if (data.Length < 64)
		{
			return SliceBy8(crc, data);
		}

		var folded = data.Length & ~15;

		return SliceBy8(~FoldBlocks(~crc, data.Slice(0, folded)), data.Slice(folded));
	}

	/// <summary>
	/// Appends bytes to a CRC with ARM64's CRC-32 instructions, eight bytes at a time and the tail a byte at a time.
	/// Callable where <see cref="ArmCrc32.Arm64.IsSupported"/>.
	/// </summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	internal static uint Arm64(uint crc, ReadOnlySpan<byte> data)
	{
		ref var bytes = ref MemoryMarshal.GetReference(data);
		var length = data.Length;
		var i = 0;
		crc = ~crc;
		for (; i + 8 <= length; i += 8)
		{
			crc = ArmCrc32.Arm64.ComputeCrc32(crc, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref bytes, i)));
		}

		for (; i < length; i++)
		{
			crc = ArmCrc32.ComputeCrc32(crc, Unsafe.Add(ref bytes, i));
		}

		return ~crc;
	}

	/// <summary>
	/// The fold: four 128-bit lanes folded over each 64 bytes, then into one, then over each 16 bytes left, then reduced
	/// to 64 bits and by Barrett's reduction to the CRC.
	/// </summary>
	/// <param name="crc">The CRC register: the CRC so far, inverted.</param>
	/// <param name="data">The bytes: at least 64, a multiple of 16.</param>
	/// <returns>The CRC register after the bytes, to be inverted.</returns>
	private static uint FoldBlocks(uint crc, ReadOnlySpan<byte> data)
	{
		ref var bytes = ref MemoryMarshal.GetReference(data);
		var length = (nuint)data.Length;
		var x1 = Load(ref bytes, 0) ^ Vector128.CreateScalar(crc).AsUInt64();
		var x2 = Load(ref bytes, 16);
		var x3 = Load(ref bytes, 32);
		var x4 = Load(ref bytes, 48);
		nuint offset = 64;

		// x^(4*128+32) and x^(4*128-32) mod P, reflected and shifted: four lanes folded 512 bits ahead.
		var k1k2 = Vector128.Create(0x0154442bd4UL, 0x01c6e41596UL);
		for (; offset + 64 <= length; offset += 64)
		{
			x1 = FoldLane(x1, Load(ref bytes, offset), k1k2);
			x2 = FoldLane(x2, Load(ref bytes, offset + 16), k1k2);
			x3 = FoldLane(x3, Load(ref bytes, offset + 32), k1k2);
			x4 = FoldLane(x4, Load(ref bytes, offset + 48), k1k2);
		}

		// x^(128+32) and x^(128-32) mod P: the lanes folded into one, then 128 bits ahead over each 16 bytes left.
		var k3k4 = Vector128.Create(0x01751997d0UL, 0x00ccaa009eUL);
		x1 = FoldLane(x1, x2, k3k4);
		x1 = FoldLane(x1, x3, k3k4);
		x1 = FoldLane(x1, x4, k3k4);
		for (; offset + 16 <= length; offset += 16)
		{
			x1 = FoldLane(x1, Load(ref bytes, offset), k3k4);
		}

		// 128 bits to 64, with x^64 mod P (k5).
		var low32 = Vector128.Create(~0u, 0, ~0u, 0).AsUInt64();
		x1 = Sse2.ShiftRightLogical128BitLane(x1, 8) ^ Pclmulqdq.CarrylessMultiply(x1, k3k4, 0x10);
		var k5 = Vector128.Create(0x0163cd6124UL, 0UL);
		x1 = Pclmulqdq.CarrylessMultiply(x1 & low32, k5, 0x00) ^ Sse2.ShiftRightLogical128BitLane(x1, 4);

		// Barrett's reduction to 32 bits, with P' and mu.
		var polynomial = Vector128.Create(0x01db710641UL, 0x01f7011641UL);
		var x5 = Pclmulqdq.CarrylessMultiply(x1 & low32, polynomial, 0x10) & low32;
		x1 ^= Pclmulqdq.CarrylessMultiply(x5, polynomial, 0x00);

		return x1.AsUInt32().GetElement(1);
	}

	/// <summary>Folds a lane over the next 16 bytes: its halves multiplied by the two constants, and the bytes added.</summary>
	/// <param name="lane">The lane.</param>
	/// <param name="next">The next 16 bytes.</param>
	/// <param name="constants">The fold's constants: the low half's in the low qword, the high half's in the high.</param>
	/// <returns>The lane, folded.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector128<ulong> FoldLane(Vector128<ulong> lane, Vector128<ulong> next, Vector128<ulong> constants)
	{
		return Pclmulqdq.CarrylessMultiply(lane, constants, 0x00) ^ Pclmulqdq.CarrylessMultiply(lane, constants, 0x11) ^ next;
	}

	/// <summary>Loads 16 bytes.</summary>
	/// <param name="bytes">The first byte of the data.</param>
	/// <param name="offset">The offset of the 16 bytes.</param>
	/// <returns>The bytes, as two qwords.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector128<ulong> Load(ref byte bytes, nuint offset) => Vector128.LoadUnsafe(ref bytes, offset).AsUInt64();
#endif

	/// <summary>Makes slice-by-8's tables.</summary>
	/// <returns>The tables.</returns>
	private static uint[] MakeTables()
	{
		var tables = new uint[8 * 256];
		for (var i = 0u; i < 256; i++)
		{
			var c = i;
			for (var k = 0; k < 8; k++)
			{
				c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
			}

			tables[i] = c;
		}

		for (var i = 0; i < 256; i++)
		{
			for (var t = 1; t < 8; t++)
			{
				var previous = tables[((t - 1) * 256) + i];
				tables[(t * 256) + i] = (previous >> 8) ^ tables[previous & 0xFF];
			}
		}

		return tables;
	}
}
