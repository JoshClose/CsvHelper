#if NET8_0_OR_GREATER
using CsvHelper.Configuration;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CsvHelper;

internal class Vector256Strategy : ICsvParsingStrategy
{
	private readonly int vector256ByteCount = Vector256<byte>.Count;
	private readonly Vector256<byte> delimiterVector;
	private readonly Vector256<byte> escapeVector;
	private readonly Vector256<byte> crVector;
	private readonly Vector256<byte> lfVector;
	private readonly Vector256<byte>? newLineVector;
	private readonly CsvModeParse parseMode;

	public Vector256Strategy(CsvParserOptions options)
	{
		parseMode = options.ModeParse;

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
			var leftVector = Unsafe.ReadUnaligned<Vector256<ushort>>(ref startBytesRef);

			// Fill right vector with bytes starting where left ended.
			var rightVector = Unsafe.ReadUnaligned<Vector256<ushort>>(ref Unsafe.Add(ref startBytesRef, vector256ByteCount));

			// Combine left and right vectors.
			var dataVector = Vector256.Narrow(leftVector, rightVector);

			// Create masks that show where special characters are located.
			var delimiterPositionVector = Vector256.Equals(dataVector, delimiterVector);
			var escapePositionVector = Vector256.Equals(dataVector, escapeVector);
			Vector256<byte> newLinePositionVector;
			if (newLineVector != null)
			{
				newLinePositionVector = Vector256.Equals(dataVector, newLineVector.Value);
			}
			else
			{
				var crPositionVector = Vector256.Equals(dataVector, crVector);
				var lfPositionVector = Vector256.Equals(dataVector, lfVector);
				newLinePositionVector = crPositionVector | lfPositionVector;
			}
			var delimiterCrLfPositionVector = delimiterPositionVector | newLinePositionVector;
			var specialCharsPositionVector = delimiterCrLfPositionVector | escapePositionVector;

			var specialCharsPositionMask = (int)Vector256.ExtractMostSignificantBits(specialCharsPositionVector);

			if (specialCharsPositionMask != 0)
			{
				parseMode(state, ref bufferRef, ref specialCharsPositionMask);
			}

			state.bufferPosition += vector256ByteCount;
		}
	}
}
#endif
