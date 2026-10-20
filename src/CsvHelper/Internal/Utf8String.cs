using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif
using System.Text;

namespace CsvHelper;

/// <summary>Creates strings from UTF-8, with a fast path for the short ASCII values typical of CSV fields.</summary>
internal static class Utf8String
{
	private const ulong HighBits = 0x8080_8080_8080_8080UL;

	// new string('\0', n) allocates without filling (the runtime skips the fill for '\0').
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string FastAllocateString(string? unused, int length) => new('\0', length);

	/// <summary>
	/// Decodes <paramref name="utf8"/> to a string. ASCII is widened straight into a newly allocated string: with
	/// overlapping loads and stores up to 32 bytes on .NET and 16 bytes at a time beyond, and 8 bytes at a time from
	/// 8 bytes on the other targets. A value with any non-ASCII byte goes to <see cref="Encoding.UTF8"/>, which replaces
	/// invalid sequences with U+FFFD.
	/// </summary>
	/// <param name="utf8">The UTF-8 bytes.</param>
	/// <returns>The string; <see cref="string.Empty"/> for no bytes.</returns>
	public static string Create(ReadOnlySpan<byte> utf8)
	{
		var len = utf8.Length;
		if (len == 0) return string.Empty;
		ref var src = ref MemoryMarshal.GetReference(utf8);
		// Allocate the string and widen ASCII straight into it. Overlapping loads and stores cover any
		// length without a loop for typical field sizes.
		if (len >= 8)
		{
#if NET
			if (len <= 16)
			{
				var a = Unsafe.ReadUnaligned<ulong>(ref src);
				var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, len - 8));
				if (((a | b) & HighBits) != 0) return Encoding.UTF8.GetString(utf8);
				var s = FastAllocateString(null, len);
				ref var dst = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(s.AsSpan()));
				Vector128.WidenLower(Vector128.CreateScalarUnsafe(a).AsByte()).StoreUnsafe(ref Unsafe.As<byte, ushort>(ref dst));
				Vector128.WidenLower(Vector128.CreateScalarUnsafe(b).AsByte())
					.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (len - 8) * 2)));

				return s;
			}

			if (len <= 32)
			{
				var a = Vector128.LoadUnsafe(ref src);
				var b = Vector128.LoadUnsafe(ref src, (nuint)(len - 16));
				if ((a | b).ExtractMostSignificantBits() != 0) return Encoding.UTF8.GetString(utf8);
				var s = FastAllocateString(null, len);
				ref var dst = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(s.AsSpan()));
				(var a0, var a1) = Vector128.Widen(a);
				(var b0, var b1) = Vector128.Widen(b);
				a0.StoreUnsafe(ref dst);
				a1.StoreUnsafe(ref dst, 8);
				b0.StoreUnsafe(ref dst, (nuint)(len - 16));
				b1.StoreUnsafe(ref dst, (nuint)(len - 8));

				return s;
			}

			return CreateLong(utf8);
#else
			return CreateWide(utf8);
#endif
		}

		if (len >= 4)
		{
			var a = Unsafe.ReadUnaligned<uint>(ref src);
			var b = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, len - 4));
			if (((a | b) & 0x8080_8080u) != 0) return Encoding.UTF8.GetString(utf8);
			var s = FastAllocateString(null, len);
			ref var dst = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(s.AsSpan()));
			Unsafe.WriteUnaligned(ref dst, Widen4(a));
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, (len - 4) * 2), Widen4(b));

			return s;
		}

		{
			int c0 = src;
			int c1 = Unsafe.Add(ref src, len >> 1);
			int c2 = Unsafe.Add(ref src, len - 1);
			if (((c0 | c1 | c2) & 0x80) != 0) return Encoding.UTF8.GetString(utf8);
			var s = FastAllocateString(null, len);
			ref var dst = ref MemoryMarshal.GetReference(s.AsSpan());
			dst = (char)c0;
			Unsafe.Add(ref dst, len >> 1) = (char)c1;
			Unsafe.Add(ref dst, len - 1) = (char)c2;

			return s;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Widen4(uint v)
	{
		ulong x = v;
		x = (x | (x << 16)) & 0x0000_FFFF_0000_FFFFUL;
		x = (x | (x << 8)) & 0x00FF_00FF_00FF_00FFUL;

		return x;
	}

#if NET
	/// <summary>Values longer than 32 bytes: one pass that widens and checks for ASCII together.</summary>
	private static string CreateLong(ReadOnlySpan<byte> utf8)
	{
		var len = utf8.Length;
		ref var src = ref MemoryMarshal.GetReference(utf8);
		// Non-ASCII text usually shows early; checking the start first avoids most wasted allocations.
		if (Vector128.LoadUnsafe(ref src).ExtractMostSignificantBits() != 0) return Encoding.UTF8.GetString(utf8);
		var s = FastAllocateString(null, len);
		ref var dst = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(s.AsSpan()));
		var seen = Vector128<byte>.Zero;
		var i = 0;
		for (; i <= len - 16; i += 16)
		{
			var v = Vector128.LoadUnsafe(ref src, (nuint)i);
			seen |= v;
			(var lo, var hi) = Vector128.Widen(v);
			lo.StoreUnsafe(ref dst, (nuint)i);
			hi.StoreUnsafe(ref dst, (nuint)i + 8);
		}

		if (i < len)
		{
			var v = Vector128.LoadUnsafe(ref src, (nuint)(len - 16));
			seen |= v;
			(var lo, var hi) = Vector128.Widen(v);
			lo.StoreUnsafe(ref dst, (nuint)(len - 16));
			hi.StoreUnsafe(ref dst, (nuint)(len - 8));
		}

		return seen.ExtractMostSignificantBits() == 0 ? s : Encoding.UTF8.GetString(utf8);
	}
#else
	/// <summary>8 bytes or more without SIMD: checks the first 8 bytes before allocating, then widens 8 at a time.</summary>
	private static string CreateWide(ReadOnlySpan<byte> utf8)
	{
		var len = utf8.Length;
		ref var src = ref MemoryMarshal.GetReference(utf8);
		if ((Unsafe.ReadUnaligned<ulong>(ref src) & HighBits) != 0) return Encoding.UTF8.GetString(utf8);
		var s = FastAllocateString(null, len);
		ref var dst = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(s.AsSpan()));
		var seen = 0UL;
		var i = 0;
		for (; i <= len - 8; i += 8)
		{
			seen |= Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, i));
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2), Widen4(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, i))));
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 + 8), Widen4(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, i + 4))));
		}

		if (i < len)
		{
			// The last 8 bytes, overlapping the ones before.
			var j = len - 8;
			seen |= Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref src, j));
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, j * 2), Widen4(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, j))));
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, j * 2 + 8), Widen4(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, j + 4))));
		}

		return (seen & HighBits) == 0 ? s : Encoding.UTF8.GetString(utf8);
	}

	/// <summary>
	/// The string for the <paramref name="length"/> bytes at <paramref name="utf8"/>. Pins them for the call: without
	/// runtime support a span cannot be made from a reference into a movable array otherwise.
	/// </summary>
	/// <param name="utf8">The first byte, which may be in a movable array.</param>
	/// <param name="length">The number of bytes.</param>
	/// <returns>The string, as <see cref="Create(ReadOnlySpan{byte})"/> decodes it.</returns>
	public static unsafe string Create(ref byte utf8, int length)
	{
		fixed (byte* p = &utf8) return Create(new ReadOnlySpan<byte>(p, length));
	}
#endif
}
