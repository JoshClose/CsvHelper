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
/// table, five times slower than slice-by-8). Each path is testable on its own: <see cref="SliceBy8"/> everywhere; on
/// .NET 8 and later <c>Fold</c> where PCLMULQDQ is supported and <c>Arm64</c> where ARM64's CRC-32 instructions are; and on
/// .NET 10 <c>Fold256</c> and <c>Fold512</c> where VPCLMULQDQ is supported on 256- and 512-bit vectors. The folds'
/// constants are x^n mod P for the n each name gives, reflected and shifted left one bit, the form the carry-less products
/// of reflected data need; the tests compute each one and check it.
/// </summary>
internal static class Crc32
{
	/// <summary>The reflected polynomial.</summary>
	private const uint Polynomial = 0xEDB88320;

#if NET
#if NET10_0_OR_GREATER
	/// <summary>x^2080 mod P (16*128+32): a fold 2,048 bits ahead (the 512-bit fold's step of 256 bytes), the low qword's.</summary>
	private const ulong X2080 = 0x011542778a;

	/// <summary>x^2016 mod P (16*128-32): a fold 2,048 bits ahead, the high qword's.</summary>
	private const ulong X2016 = 0x01322d1430;

	/// <summary>x^1056 mod P (8*128+32): a fold 1,024 bits ahead (the 256-bit fold's step of 128 bytes), the low qword's.</summary>
	private const ulong X1056 = 0x01e88ef372;

	/// <summary>x^992 mod P (8*128-32): a fold 1,024 bits ahead, the high qword's.</summary>
	private const ulong X992 = 0x014a7fe880;

#endif
	/// <summary>
	/// x^544 mod P (4*128+32): a fold 512 bits ahead (the 128-bit fold's step of 64 bytes, and the wider folds' lanes folded
	/// down to its four), the low qword's.
	/// </summary>
	private const ulong X544 = 0x0154442bd4;

	/// <summary>x^480 mod P (4*128-32): a fold 512 bits ahead, the high qword's.</summary>
	private const ulong X480 = 0x01c6e41596;

	/// <summary>x^160 mod P (128+32): a fold 128 bits ahead (the lanes folded into one, then each 16 bytes left), the low qword's.</summary>
	private const ulong X160 = 0x01751997d0;

	/// <summary>x^96 mod P (128-32): a fold 128 bits ahead, the high qword's.</summary>
	private const ulong X96 = 0x00ccaa009e;

	/// <summary>x^64 mod P: the reduction of 128 bits to 64.</summary>
	private const ulong X64 = 0x0163cd6124;

	/// <summary>P itself, x^32 included, reflected into 33 bits: Barrett's reduction's P'.</summary>
	private const ulong BarrettPolynomial = 0x01db710641;

	/// <summary>The quotient x^64 / P (33 bits), reflected: Barrett's reduction's mu.</summary>
	private const ulong BarrettQuotient = 0x01f7011641;

#endif
	/// <summary>
	/// Slice-by-8's eight tables of 256 entries, one after the other: the first is the byte-at-a-time table, and each next
	/// one advances a byte further.
	/// </summary>
	private static readonly uint[] Tables = MakeTables();

	/// <summary>
	/// Appends bytes to a CRC, through the widest path the machine supports whose minimum the bytes reach: on .NET 10 the
	/// 512-bit fold from 256 bytes, then the 256-bit fold from 128, each only where the runtime accelerates vectors of
	/// that width (it doesn't on processors that slow down for them); the 128-bit fold from 64; ARM64's instructions; and
	/// slice-by-8 for the rest.
	/// </summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	public static uint Append(uint crc, ReadOnlySpan<byte> data)
	{
#if NET
#if NET10_0_OR_GREATER
		if (data.Length >= 256 && Pclmulqdq.V512.IsSupported && Vector512.IsHardwareAccelerated)
		{
			return Fold512(crc, data);
		}

		if (data.Length >= 128 && Pclmulqdq.V256.IsSupported && Vector256.IsHardwareAccelerated)
		{
			return Fold256(crc, data);
		}

#endif
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

#if NET10_0_OR_GREATER
	/// <summary>
	/// Appends bytes to a CRC as <see cref="Fold"/> does, with VPCLMULQDQ on 256-bit vectors: four lanes of two 128-bit parts
	/// folded over each 128 bytes, then down to <see cref="Fold"/>'s four lanes, which finish as there. Callable where
	/// <see cref="Pclmulqdq.V256.IsSupported"/>; fewer than 128 bytes go through slice-by-8.
	/// </summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	internal static uint Fold256(uint crc, ReadOnlySpan<byte> data)
	{
		if (data.Length < 128)
		{
			return SliceBy8(crc, data);
		}

		var folded = data.Length & ~15;

		return SliceBy8(~FoldBlocks256(~crc, data.Slice(0, folded)), data.Slice(folded));
	}

	/// <summary>
	/// Appends bytes to a CRC as <see cref="Fold"/> does, with VPCLMULQDQ on 512-bit vectors: four lanes of four 128-bit parts
	/// folded over each 256 bytes, then down to <see cref="Fold"/>'s four lanes, which finish as there. Callable where
	/// <see cref="Pclmulqdq.V512.IsSupported"/>; fewer than 256 bytes go through slice-by-8.
	/// </summary>
	/// <param name="crc">The CRC of the bytes before, 0 for none.</param>
	/// <param name="data">The bytes.</param>
	/// <returns>The CRC of the bytes before and these.</returns>
	internal static uint Fold512(uint crc, ReadOnlySpan<byte> data)
	{
		if (data.Length < 256)
		{
			return SliceBy8(crc, data);
		}

		var folded = data.Length & ~15;

		return SliceBy8(~FoldBlocks512(~crc, data.Slice(0, folded)), data.Slice(folded));
	}

#endif
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
		var k1k2 = Vector128.Create(X544, X480);
		for (; offset + 64 <= length; offset += 64)
		{
			x1 = FoldLane(x1, Load(ref bytes, offset), k1k2);
			x2 = FoldLane(x2, Load(ref bytes, offset + 16), k1k2);
			x3 = FoldLane(x3, Load(ref bytes, offset + 32), k1k2);
			x4 = FoldLane(x4, Load(ref bytes, offset + 48), k1k2);
		}

		// x^(128+32) and x^(128-32) mod P: the lanes folded into one, then 128 bits ahead over each 16 bytes left.
		var k3k4 = Vector128.Create(X160, X96);
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
		var k5 = Vector128.Create(X64, 0UL);
		x1 = Pclmulqdq.CarrylessMultiply(x1 & low32, k5, 0x00) ^ Sse2.ShiftRightLogical128BitLane(x1, 4);

		// Barrett's reduction to 32 bits, with P' and mu.
		var polynomial = Vector128.Create(BarrettPolynomial, BarrettQuotient);
		var x5 = Pclmulqdq.CarrylessMultiply(x1 & low32, polynomial, 0x10) & low32;
		x1 ^= Pclmulqdq.CarrylessMultiply(x5, polynomial, 0x00);

		return x1.AsUInt32().GetElement(1);
	}

#if NET10_0_OR_GREATER
	/// <summary>
	/// The 256-bit fold: four lanes of two 128-bit parts loaded from the first 128 bytes and folded over each 128 bytes
	/// after, then the first two folded onto the last two, whose parts go on as <see cref="FinishFold"/>'s four lanes.
	/// </summary>
	/// <param name="crc">The CRC register: the CRC so far, inverted.</param>
	/// <param name="data">The bytes: at least 128, a multiple of 16.</param>
	/// <returns>The CRC register after the bytes, to be inverted.</returns>
	private static uint FoldBlocks256(uint crc, ReadOnlySpan<byte> data)
	{
		ref var bytes = ref MemoryMarshal.GetReference(data);
		var length = (nuint)data.Length;
		var x1 = Load256(ref bytes, 0) ^ Vector256.CreateScalar(crc).AsUInt64();
		var x2 = Load256(ref bytes, 32);
		var x3 = Load256(ref bytes, 64);
		var x4 = Load256(ref bytes, 96);
		nuint offset = 128;

		// x^(8*128+32) and x^(8*128-32) mod P: each part folded 1,024 bits ahead, onto the part 128 bytes on.
		var ahead1024 = Vector256.Create(Vector128.Create(X1056, X992));
		for (; offset + 128 <= length; offset += 128)
		{
			x1 = FoldLane(x1, Load256(ref bytes, offset), ahead1024);
			x2 = FoldLane(x2, Load256(ref bytes, offset + 32), ahead1024);
			x3 = FoldLane(x3, Load256(ref bytes, offset + 64), ahead1024);
			x4 = FoldLane(x4, Load256(ref bytes, offset + 96), ahead1024);
		}

		// x^(4*128+32) and x^(4*128-32) mod P: the first two lanes folded 512 bits ahead, onto the last two.
		var ahead512 = Vector256.Create(Vector128.Create(X544, X480));
		x3 = FoldLane(x1, x3, ahead512);
		x4 = FoldLane(x2, x4, ahead512);

		return FinishFold(x3.GetLower(), x3.GetUpper(), x4.GetLower(), x4.GetUpper(), ref bytes, offset, length);
	}

	/// <summary>
	/// The 512-bit fold: four lanes of four 128-bit parts loaded from the first 256 bytes and folded over each 256 bytes
	/// after, then each folded onto the next, whose parts at the last go on as <see cref="FinishFold"/>'s four lanes.
	/// </summary>
	/// <param name="crc">The CRC register: the CRC so far, inverted.</param>
	/// <param name="data">The bytes: at least 256, a multiple of 16.</param>
	/// <returns>The CRC register after the bytes, to be inverted.</returns>
	private static uint FoldBlocks512(uint crc, ReadOnlySpan<byte> data)
	{
		ref var bytes = ref MemoryMarshal.GetReference(data);
		var length = (nuint)data.Length;
		var x1 = Load512(ref bytes, 0) ^ Vector512.CreateScalar(crc).AsUInt64();
		var x2 = Load512(ref bytes, 64);
		var x3 = Load512(ref bytes, 128);
		var x4 = Load512(ref bytes, 192);
		nuint offset = 256;

		// x^(16*128+32) and x^(16*128-32) mod P: each part folded 2,048 bits ahead, onto the part 256 bytes on.
		var ahead2048 = Vector512.Create(Vector128.Create(X2080, X2016));
		for (; offset + 256 <= length; offset += 256)
		{
			x1 = FoldLane(x1, Load512(ref bytes, offset), ahead2048);
			x2 = FoldLane(x2, Load512(ref bytes, offset + 64), ahead2048);
			x3 = FoldLane(x3, Load512(ref bytes, offset + 128), ahead2048);
			x4 = FoldLane(x4, Load512(ref bytes, offset + 192), ahead2048);
		}

		// x^(4*128+32) and x^(4*128-32) mod P: each lane folded 512 bits ahead, onto the next.
		var ahead512 = Vector512.Create(Vector128.Create(X544, X480));
		x1 = FoldLane(x1, x2, ahead512);
		x1 = FoldLane(x1, x3, ahead512);
		x1 = FoldLane(x1, x4, ahead512);
		var low = x1.GetLower();
		var high = x1.GetUpper();

		return FinishFold(low.GetLower(), low.GetUpper(), high.GetLower(), high.GetUpper(), ref bytes, offset, length);
	}

	/// <summary>
	/// The wider folds' end, <see cref="FoldBlocks"/>'s from its four lanes on: the lanes folded over each 64 bytes left,
	/// then into one, then over each 16 bytes left, then reduced to 64 bits and by Barrett's reduction to the CRC. A copy,
	/// not a part shared with <see cref="FoldBlocks"/>: with its end split into a method, .NET 8's JIT compiles
	/// <see cref="FoldBlocks"/>' loop otherwise (its constant in a register rather than an operand), 19 GB/s over a
	/// 64 MiB span here rather than 22.
	/// </summary>
	/// <param name="x1">The first lane: the 16 bytes at <paramref name="offset"/> - 64, the bytes before folded in.</param>
	/// <param name="x2">The second lane: the 16 bytes at <paramref name="offset"/> - 48, the bytes before folded in.</param>
	/// <param name="x3">The third lane: the 16 bytes at <paramref name="offset"/> - 32, the bytes before folded in.</param>
	/// <param name="x4">The fourth lane: the 16 bytes at <paramref name="offset"/> - 16, the bytes before folded in.</param>
	/// <param name="bytes">The first byte of the data.</param>
	/// <param name="offset">The offset of the bytes after the lanes'.</param>
	/// <param name="length">The data's length: a multiple of 16.</param>
	/// <returns>The CRC register after the bytes, to be inverted.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint FinishFold(Vector128<ulong> x1, Vector128<ulong> x2, Vector128<ulong> x3, Vector128<ulong> x4, ref byte bytes, nuint offset, nuint length)
	{
		// x^(4*128+32) and x^(4*128-32) mod P, reflected and shifted: four lanes folded 512 bits ahead.
		var k1k2 = Vector128.Create(X544, X480);
		for (; offset + 64 <= length; offset += 64)
		{
			x1 = FoldLane(x1, Load(ref bytes, offset), k1k2);
			x2 = FoldLane(x2, Load(ref bytes, offset + 16), k1k2);
			x3 = FoldLane(x3, Load(ref bytes, offset + 32), k1k2);
			x4 = FoldLane(x4, Load(ref bytes, offset + 48), k1k2);
		}

		// x^(128+32) and x^(128-32) mod P: the lanes folded into one, then 128 bits ahead over each 16 bytes left.
		var k3k4 = Vector128.Create(X160, X96);
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
		var k5 = Vector128.Create(X64, 0UL);
		x1 = Pclmulqdq.CarrylessMultiply(x1 & low32, k5, 0x00) ^ Sse2.ShiftRightLogical128BitLane(x1, 4);

		// Barrett's reduction to 32 bits, with P' and mu.
		var polynomial = Vector128.Create(BarrettPolynomial, BarrettQuotient);
		var x5 = Pclmulqdq.CarrylessMultiply(x1 & low32, polynomial, 0x10) & low32;
		x1 ^= Pclmulqdq.CarrylessMultiply(x5, polynomial, 0x00);

		return x1.AsUInt32().GetElement(1);
	}

#endif
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

#if NET10_0_OR_GREATER
	/// <summary>
	/// Folds a lane of two 128-bit parts over the next 32 bytes, each part as
	/// <see cref="FoldLane(Vector128{ulong}, Vector128{ulong}, Vector128{ulong})"/> folds a lane.
	/// </summary>
	/// <param name="lane">The lane.</param>
	/// <param name="next">The next 32 bytes.</param>
	/// <param name="constants">The fold's constants, in each 128-bit part: the low half's in the low qword, the high half's in the high.</param>
	/// <returns>The lane, folded.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector256<ulong> FoldLane(Vector256<ulong> lane, Vector256<ulong> next, Vector256<ulong> constants)
	{
		return Pclmulqdq.V256.CarrylessMultiply(lane, constants, 0x00) ^ Pclmulqdq.V256.CarrylessMultiply(lane, constants, 0x11) ^ next;
	}

	/// <summary>
	/// Folds a lane of four 128-bit parts over the next 64 bytes, each part as
	/// <see cref="FoldLane(Vector128{ulong}, Vector128{ulong}, Vector128{ulong})"/> folds a lane.
	/// </summary>
	/// <param name="lane">The lane.</param>
	/// <param name="next">The next 64 bytes.</param>
	/// <param name="constants">The fold's constants, in each 128-bit part: the low half's in the low qword, the high half's in the high.</param>
	/// <returns>The lane, folded.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector512<ulong> FoldLane(Vector512<ulong> lane, Vector512<ulong> next, Vector512<ulong> constants)
	{
		return Pclmulqdq.V512.CarrylessMultiply(lane, constants, 0x00) ^ Pclmulqdq.V512.CarrylessMultiply(lane, constants, 0x11) ^ next;
	}

	/// <summary>Loads 32 bytes.</summary>
	/// <param name="bytes">The first byte of the data.</param>
	/// <param name="offset">The offset of the 32 bytes.</param>
	/// <returns>The bytes, as four qwords.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector256<ulong> Load256(ref byte bytes, nuint offset) => Vector256.LoadUnsafe(ref bytes, offset).AsUInt64();

	/// <summary>Loads 64 bytes.</summary>
	/// <param name="bytes">The first byte of the data.</param>
	/// <param name="offset">The offset of the 64 bytes.</param>
	/// <returns>The bytes, as eight qwords.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector512<ulong> Load512(ref byte bytes, nuint offset) => Vector512.LoadUnsafe(ref bytes, offset).AsUInt64();
#endif
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
