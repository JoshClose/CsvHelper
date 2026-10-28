#if NET && !NET10_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace CsvHelper;

/// <summary>Static members that .NET 8 and 9 lack, as C# 14 static extension members.</summary>
internal static class PolyfillVectorExtensions
{
	extension(Vector128)
	{
		/// <summary>
		/// Shuffles bytes with the byte shuffle of the platform (SSSE3 PSHUFB, AdvSimd TBL); stands in for
		/// <c>Vector128.ShuffleNative(Vector128&lt;byte&gt;, Vector128&lt;byte&gt;)</c>. As there, an index out of range
		/// gives a byte that depends on the platform. The compactors that call it run only where SSSE3 or AdvSimd is supported.
		/// </summary>
		/// <param name="vector">The bytes to shuffle.</param>
		/// <param name="indices">The index in <paramref name="vector"/> of each byte of the result.</param>
		/// <returns>The shuffled bytes.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector128<byte> ShuffleNative(Vector128<byte> vector, Vector128<byte> indices)
		{
			if (Ssse3.IsSupported)
			{
				return Ssse3.Shuffle(vector, indices);
			}

			if (AdvSimd.Arm64.IsSupported)
			{
				return AdvSimd.Arm64.VectorTableLookup(vector, indices);
			}

			return Vector128.Shuffle(vector, indices);
		}
	}
}
#endif
