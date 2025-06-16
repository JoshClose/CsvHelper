using System.Numerics;
using System.Runtime.CompilerServices;

namespace CsvHelper;

internal static class EscapeMode
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void Parse(CsvParserState state, ref char bufferRef, ref int mask)
	{
		var escape = state.options.Escape;
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

			if (state.parsingFieldPrevEscapeIndex + 1 == charIndex)
			{
				// Skip the escaped special char.
				continue;
			}

			if (c == escape)
			{
				state.parsingEscapeMask = state.parsingEscapeMask | (1 << charIndex - state.parsingFieldStart);
				state.parsingFieldPrevEscapeIndex = charIndex;

				continue;
			}

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
		var options = state.options;
		var specialChars = new Span<char>(state.specialChars);
		var escape = new Span<char>(state.escape);
		var shouldEscape = options.ShouldEscape?.Invoke(field);

		while (field.Length > 0)
		{
			var index = field.IndexOfAny(specialChars);
			
			if (index == -1 || shouldEscape == false)
			{
				// The field doesn't contain any special chars
				// or the user doesn't want to escape this field.
				state.AddChars(field);

				return;
			}

			state.AddChars(field.Slice(0, index));
			state.AddChars(escape);
			state.AddChars(field.Slice(index, 1));

			field = field.Slice(index + 1);
		}
	}
}
