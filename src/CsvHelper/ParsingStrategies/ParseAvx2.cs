#if NET6_0_OR_GREATER
using CsvHelper.Configuration;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CsvHelper;

internal class ParseAvx2 : ICsvParsingStrategy
{
	private readonly int vector256ByteCount = Vector256<byte>.Count;
	private readonly Vector256<byte> delimiterVector;
	private readonly Vector256<byte> escapeVector;
	private readonly Vector256<byte> crVector;
	private readonly Vector256<byte> lfVector;
	private readonly Vector256<byte>? newLineVector;
	private readonly CsvModeParse modeParse;

	public ParseAvx2(CsvParserOptions options)
	{
		modeParse = options.ModeParse;

		delimiterVector = Vector256.Create((byte)options.Delimiter);
		escapeVector = Vector256.Create((byte)options.Escape);
		crVector = Vector256.Create((byte)'\r');
		lfVector = Vector256.Create((byte)'\n');
		if (options.NewLine != null)
		{
			newLineVector = Vector256.Create((byte)options.NewLine);
		}
	}

	public void Parse(CsvParserState state)
	{
		// Get a reference to index 0 of the buffer.
		ref var bufferRef = ref MemoryMarshal.GetArrayDataReference(state.buffer);

		while (state.bufferPosition + vector256ByteCount <= state.buffer.Length && state.bufferPosition < state.charsRead)
		{
			// Change index to row start position.
			ref var startRef = ref Unsafe.Add(ref bufferRef, state.bufferPosition);

			// Change to bytes.
			ref var startBytesRef = ref Unsafe.As<char, byte>(ref startRef);

			// Fill left vector with bytes starting at beginning.
			var leftVector = Unsafe.ReadUnaligned<Vector256<short>>(ref startBytesRef);

			// Fill right vector with bytes starting where left ended.
			var rightVector = Unsafe.ReadUnaligned<Vector256<short>>(ref Unsafe.Add(ref startBytesRef, vector256ByteCount));

			// Combine left and right vectors.
			var packedVector = Avx2.PackUnsignedSaturate(leftVector, rightVector);

			// Realign vectors into left then right.
			var dataVector = Avx2.Permute4x64(packedVector.AsInt64(), 0b_11_01_10_00).AsByte();

			// Create masks that show where special characters are located.
			var delimiterPositionVector = Avx2.CompareEqual(dataVector, delimiterVector);
			var escapePositionVector = Avx2.CompareEqual(dataVector, escapeVector);
			Vector256<byte> newLinePositionVector;
			if (newLineVector != null)
			{
				newLinePositionVector = Avx2.CompareEqual(dataVector, newLineVector.Value);
			}
			else
			{
				var crPositionVector = Avx2.CompareEqual(dataVector, crVector);
				var lfPositionVector = Avx2.CompareEqual(dataVector, lfVector);
				newLinePositionVector = Avx2.Or(crPositionVector, lfPositionVector);
			}
			var delimiterCrLfPositionVector = Avx2.Or(delimiterPositionVector, newLinePositionVector);
			var specialCharsPositionVector = Avx2.Or(delimiterCrLfPositionVector, escapePositionVector);

			var specialCharsPositionMask = Avx2.MoveMask(specialCharsPositionVector);

			if (specialCharsPositionMask != 0)
			{
				modeParse(state, ref bufferRef, ref specialCharsPositionMask);
			}

			state.bufferPosition += vector256ByteCount;
		}
	}
}
#endif
