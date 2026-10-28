using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif

namespace CsvHelper;

/// <summary>
/// Writes short ASCII fields with overlapping vector loads and stores (a plain loop without SIMD), checking for special
/// characters.
/// </summary>
internal static class AsciiField
{
	/// <summary>
	/// Copies up to 32 UTF-16 chars as bytes to <paramref name="dst"/>. Returns 0 when the field needs no
	/// quoting or escaping, 1 when it contains the separator, <paramref name="quote"/> (the quote, or the escape
	/// character in Escape mode), CR or LF, and -1 when it is not ASCII (the caller must then encode it properly;
	/// the destination holds garbage).
	/// </summary>
	/// <param name="value">The chars, at most 32.</param>
	/// <param name="dst">Receives the bytes; there must be room for <c>value.Length</c> of them.</param>
	/// <param name="separator">The separator.</param>
	/// <param name="quote">The quote, or the escape character in Escape mode.</param>
	/// <returns>0, 1 or -1, as above.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int TryNarrow(ReadOnlySpan<char> value, ref byte dst, byte separator, byte quote)
	{
		var len = value.Length;
		ref var src = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(value));
#if NET
		if (len >= 8)
		{
			var nonAscii = Vector128.Create((ushort)0xFF80);
			var a = Vector128.LoadUnsafe(ref src);
			var d = Vector128.LoadUnsafe(ref src, (nuint)(len - 8));
			if (len <= 16)
			{
				if (((a | d) & nonAscii) != Vector128<ushort>.Zero) return -1;
				var bytes = Vector128.Narrow(a, d).AsUInt64();
				Unsafe.WriteUnaligned(ref dst, bytes.ToScalar());
				Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, len - 8), bytes.GetElement(1));

				return HasSpecial(bytes.AsByte(), separator, quote) ? 1 : 0;
			}

			var b = Vector128.LoadUnsafe(ref src, 8);
			var c = Vector128.LoadUnsafe(ref src, (nuint)(len - 16));
			if (((a | b | c | d) & nonAscii) != Vector128<ushort>.Zero) return -1;
			var head = Vector128.Narrow(a, b);
			var tail = Vector128.Narrow(c, d);
			head.StoreUnsafe(ref dst);
			tail.StoreUnsafe(ref Unsafe.Add(ref dst, len - 16));

			return HasSpecial(head, separator, quote) || HasSpecial(tail, separator, quote) ? 1 : 0;
		}
#endif

		var special = 0;
		for (var i = 0; i < len; i++)
		{
			int ch = Unsafe.Add(ref src, i);
			if (ch >= 0x80) return -1;
			Unsafe.Add(ref dst, i) = (byte)ch;
			if (ch == separator || ch == quote || ch == '\r' || ch == '\n') special = 1;
		}

		return special;
	}

#if NET
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool HasSpecial(Vector128<byte> v, byte separator, byte quote) =>
		(Vector128.Equals(v, Vector128.Create(separator))
		 | Vector128.Equals(v, Vector128.Create(quote))
		 | Vector128.Equals(v, Vector128.Create((byte)'\r'))
		 | Vector128.Equals(v, Vector128.Create((byte)'\n'))) != Vector128<byte>.Zero;
#endif
}
