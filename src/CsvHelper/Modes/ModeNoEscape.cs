using System.Numerics;
using System.Runtime.CompilerServices;

namespace CsvHelper;

internal static class ModeNoEscape
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void Parse(CsvParserState state, ref char bufferRef, ref int mask)
	{
		var delimiter = state.options.Delimiter;
		char? newLine = state.options.NewLine != null ? state.options.NewLine : null;
		int charIndex;
		char c;
		var bufferPosition = state.bufferPosition;

		do
		{
			charIndex = BitOperations.TrailingZeroCount(mask) + bufferPosition;
			mask = mask & (mask - 1);

			c = Unsafe.Add(ref bufferRef, charIndex);

			if (c == delimiter)
			{
				state.AddField(charIndex);

				continue;
			}

			if (newLine != null)
			{
				if (c == newLine)
				{
					state.AddField(charIndex);
					state.AddRow();

					continue;
				}
			}
			else
			{
				if (c == '\r' || c == '\n')
				{
					state.AddField(charIndex);

					if (c == '\r')
					{
						var atEnd = charIndex + 1 == state.charsRead;
						var nextChar = atEnd
							? (char)state.reader.Peek()
							: Unsafe.Add(ref bufferRef, charIndex + 1);

						if (nextChar == '\n')
						{
							// Skip the \n.
							if (atEnd)
							{
								state.reader.Read();
							}
							else
							{
								state.bufferPosition++;
								state.parsingFieldStart++;
							}

							mask = mask & (mask - 1);
						}
					}

					state.AddRow();

					continue;
				}
			}

			throw new InvalidOperationException($"Mask char '{c}' was not handled.");
		}
		while (mask != 0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void Escape(CsvSerializerState state, ReadOnlySpan<char> field)
	{
		state.AddChars(field);
	}
}
