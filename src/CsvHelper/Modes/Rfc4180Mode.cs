using System.Numerics;
using System.Runtime.CompilerServices;

namespace CsvHelper;

internal static class Rfc4180Mode
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void Parse(CsvParserState state, ref char bufferRef, ref int mask)
	{
		/* Invalid Field Rules:
		 * 1. If a field contains an escape but doesn't start with one, it's invalid.
		 * 2. The first escape that isn't preceded by an escape is the end of the field. If there's more chars, it's invalid.
		 * 3. If invalid is detected, read as is and stop the field at the next delimiter or new line.
		 */

		var reader = state.reader;
		var charsRead = state.charsRead;
		var escape = state.options.Escape;
		var delimiter = state.options.Delimiter;
		char? newLine = state.options.NewLine != null ? state.options.NewLine : null;
		var originalBufferPosition = state.bufferPosition;
		int charIndex;
		char c;

		do
		{
			charIndex = BitOperations.TrailingZeroCount(mask) + originalBufferPosition;
			mask = mask & (mask - 1);

			c = Unsafe.Add(ref bufferRef, charIndex);

			if (c == escape)
			{
				state.parsingInEscape = !state.parsingInEscape;

				if (state.parsingFieldIsInvalid)
				{
					// Ignore escapes if the field is invalid.
					continue;
				}

				var prevCharWasEscape = state.parsingFieldPrevEscapeIndex + 1 == charIndex && state.parsingFieldPrevEscapeIndex > 0;

				// Check for invalid data.
				if (state.parsingEscapeMask == 0)
				{
					if (state.parsingFieldStart != charIndex)
					{
						// If the first escaper isn't at the start of the field, it's invalid.
						state.parsingFieldIsInvalid = true;
						continue;
					}
				}
				else
				{
					// Check if the next char is a special character.
					var nextCharIndex = charIndex + 1;
					char nextChar;
					if (nextCharIndex >= charsRead)
					{
						nextChar = (char)reader.Peek();
					}
					else
					{
						nextChar = Unsafe.Add(ref bufferRef, nextCharIndex);
					}

					var nextCharIsSpecial =
						nextChar == escape ||
						nextChar == delimiter ||
						(
							newLine != null
								? nextChar == newLine
								: (nextChar == '\r' || nextChar == '\n')
						);
					if (!nextCharIsSpecial && !prevCharWasEscape)
					{
						// If the next char is not a special character and the previous char isn't an escape, it's invalid.
						state.parsingFieldIsInvalid = true;
					}
				}

				if (!prevCharWasEscape)
				{
					// Only keep track of escapes. The second escape char is data.
					state.parsingEscapeMask = state.parsingEscapeMask | (1 << charIndex - state.parsingFieldStart);
					state.parsingFieldPrevEscapeIndex = charIndex;
				}

				continue;
			}

			if (state.parsingInEscape && !state.parsingFieldIsInvalid)
			{
				// Inside an escaped field.
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
						var atEnd = charIndex + 1 == charsRead;
						var nextChar = atEnd
							? (char)reader.Peek()
							: Unsafe.Add(ref bufferRef, charIndex + 1);

						if (nextChar == '\n')
						{
							// Skip the \n.
							if (atEnd)
							{
								reader.Read();
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

		if (shouldEscape == false)
		{
			// The user doesn't want to escape this field.
			state.AddChars(field);

			return;
		}

		var containsSpecialChars = field.IndexOfAny(specialChars) > -1;
		if (!containsSpecialChars && shouldEscape != true)
		{
			// The field doesn't contain any special chars.
			state.AddChars(field);

			return;
		}

		if (containsSpecialChars || shouldEscape == true)
		{
			// The field is quoted.
			state.AddChars(escape);
		}

		while (field.Length > 0)
		{
			// Only quotes need to be escaped.
			var index = field.IndexOfAny(escape);

			if (index == -1)
			{
				state.AddChars(field);
				break;
			}

			state.AddChars(field.Slice(0, index));
			state.AddChars(escape);
			state.AddChars(field.Slice(index, 1));

			field = field.Slice(index + 1);
		}

		state.AddChars(escape);
	}
}
