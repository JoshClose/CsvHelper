using CsvHelper.Configuration;
using System.Runtime.InteropServices;

namespace CsvHelper;

internal class ParseIndexOfAny : ICsvParsingStrategy
{
	private readonly int vector256ByteCount = 32; // Vector256<byte>.Count;
	private readonly CsvModeParse parseMode;
	private char[] specialChars;

	public ParseIndexOfAny(CsvParserOptions options)
	{
		parseMode = options.ModeParse;

		var specialChars = new List<char>
		{
			options.Delimiter,
			options.Escape
		};
		if (options.NewLine != null)
		{
			specialChars.Add(options.NewLine.Value);
		}
		else
		{
			specialChars.Add('\r');
			specialChars.Add('\n');
		}

		this.specialChars = specialChars.ToArray();
	}

	public void Parse(CsvParserState state)
	{
#if NET6_0_OR_GREATER
		ref var bufferRef = ref MemoryMarshal.GetArrayDataReference(state.buffer);
		var bufferChars = MemoryMarshal.CreateReadOnlySpan(ref bufferRef, state.buffer.Length);
		var specialChars = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(this.specialChars), this.specialChars.Length);
#else
		ref var bufferRef = ref state.buffer[0];
		var bufferChars = new ReadOnlySpan<char>(state.buffer);
		var specialChars = new ReadOnlySpan<char>(this.specialChars);
#endif

		while (state.bufferPosition + vector256ByteCount <= state.buffer.Length && state.bufferPosition < state.charsRead)
		{
			var chars = bufferChars.Slice(state.bufferPosition, vector256ByteCount);
			var specialCharsPositionMask = 0;

			while (chars.Length > 0)
			{
				var index = chars.IndexOfAny(specialChars);
				if (index < 0)
				{
					break;
				}

				var usedCharsLength = vector256ByteCount - chars.Length;
				specialCharsPositionMask = specialCharsPositionMask | (1 << index + usedCharsLength);

				chars = chars.Slice(index + 1);
			}

			if (specialCharsPositionMask != 0)
			{
				parseMode(state, ref bufferRef, ref specialCharsPositionMask);
			}

			state.bufferPosition += vector256ByteCount;
		}
	}
}
