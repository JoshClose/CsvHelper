using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CsvHelper;

/// <summary>
/// Used to replace replace characters in a <see cref="TextReader"/>.
/// </summary>
public class ReplaceTextReader : TextReader
{
	private readonly TextReader reader;
	private readonly int maxReplacementLength;

	private int charsRead = -1;
	private char[] unusedChars;
	private int unusedCharsLength;
	private ReplacementPosition[] replacementPositions;
	private int replacementPositionsLength;
	private Replacement[] replacements;
	private bool needsSort;

	public ReplaceTextReader(TextReader reader, string from, string to) : this(reader, new Replacements { { from, to } }) { }

	public ReplaceTextReader(TextReader reader, Replacements replacements)
	{
		this.reader = reader;

		maxReplacementLength = replacements.Count > 0 
			? replacements.Max(r => Math.Max(r.From.Length, r.To.Length)) 
			: 1;
		this.replacements = replacements.ToArray();
		ValidateReplacements();
		needsSort = this.replacements.Length > 1;

		unusedChars = ArrayPool<char>.Shared.Rent(0x1000);
		replacementPositions = ArrayPool<ReplacementPosition>.Shared.Rent(0x400);
	}

	public override int Peek()
	{
		if (unusedCharsLength > 0)
		{
			return unusedChars[0];
		}

		return reader.Peek();
	}

#if !NET6_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER

	public override int Read(char[] buffer, int index, int count)
	{
		return base.Read(buffer, index, count);
	}

	public override int ReadBlock(char[] buffer, int index, int count)
	{
		return base.ReadBlock(buffer, index, count);
	}

#endif

	public
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		override
#endif
		int Read(Span<char> buffer) => ReadBlock(buffer);

	public
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		override
#endif
		int ReadBlock(Span<char> buffer)
	{
		var bufferPosition = 0;
		var charsPosition = 0;
		var charsLength = 0;
		var charsRead = -1;

		replacementPositionsLength = 0;
#if DEBUG && NET6_0_OR_GREATER
		Array.Clear(replacementPositions);
#endif

		// Make the temp buffer large enough to pull in more characters in case the
		// entire temp buffer is special characters. We need to fill the entire
		// incoming buffer with characters.
		Span<char> chars = stackalloc char[buffer.Length * maxReplacementLength];
		Span<int> charsSort = stackalloc int[buffer.Length * maxReplacementLength];

		if (unusedCharsLength > 0)
		{
			// Copy the chars that were left over from the last read.
			new Span<char>(unusedChars, 0, unusedCharsLength).CopyTo(chars);
			charsLength = unusedCharsLength;
			unusedCharsLength = 0;
		}

		while (bufferPosition < buffer.Length && charsRead != 0)
		{
			charsLength -= charsPosition;
			chars = chars.Slice(charsPosition);
			charsRead = reader.ReadBlock(chars.Slice(charsLength));
			charsLength += charsRead;
			if (needsSort)
			{
				charsSort.Fill(-1);
			}

			if (charsLength == 0)
			{
				return bufferPosition;
			}

			for (var i = 0; i < replacements.Length; i++)
			{
				var replacement = replacements[i];
				FindReplacements(ref chars, ref replacement, ref replacementPositions, ref replacementPositionsLength, ref charsSort);
			}

			var replacementsLength = replacementPositionsLength;
			if (needsSort)
			{
				var charsSortLength = 0;
				SortReplacements(ref charsSort, ref charsSortLength);
				replacementsLength = charsSortLength;
			}

			ReplacementPosition prevPosition = default;

			for (var i = 0; i < replacementsLength; i++)
			{
				var replacementPosition = replacementPositions[needsSort ? charsSort[i] : i];

				// Read up until the special char.
				var start = prevPosition.Index + prevPosition.Length;
				var length = replacementPosition.Index - start;
				chars.Slice(start, length).CopyTo(buffer.Slice(bufferPosition));
				bufferPosition += length;

				// Add the replacement char.
				replacementPosition.Replacement.CopyTo(buffer.Slice(bufferPosition));
				bufferPosition += replacementPosition.Replacement.Length;

				prevPosition = replacementPosition;
			}

			charsPosition = prevPosition.Index + prevPosition.Length;

			if (bufferPosition < buffer.Length)
			{
				// Read the rest of the chars up to the buffer length.
				var remainingCharLength = charsLength - (prevPosition.Index + prevPosition.Length);
				var remainingBufferLength = buffer.Length - bufferPosition;
				var remainingLength = Math.Min(remainingCharLength, remainingBufferLength);
				chars.Slice(prevPosition.Index + prevPosition.Length, remainingLength).CopyTo(buffer.Slice(bufferPosition));
				bufferPosition += remainingLength;
				charsPosition += remainingLength;
			}
		}

		// Copy the unused temp chars.
		if (charsPosition < charsLength)
		{
			if (unusedChars.Length < buffer.Length)
			{
				ArrayExtensions.Resize(ref unusedChars, buffer.Length);
			}

			unusedCharsLength = charsLength - charsPosition;
			chars.Slice(charsPosition, unusedCharsLength).CopyTo(unusedChars);
		}

		return bufferPosition;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			ArrayPool<char>.Shared.Return(unusedChars);
			ArrayPool<ReplacementPosition>.Shared.Return(replacementPositions);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void FindReplacements(ref Span<char> chars, ref Replacement replacement, ref ReplacementPosition[] replacementPositions, ref int replacementLength, ref Span<int> charsSort)
	{
		var from = replacement.From;
		var to = replacement.To;

		if (from.Length == 0)
		{
			return;
		}

		var position = 0;
		var charsLeft = chars.Slice(0);
		do
		{
			var index = charsLeft.IndexOf(from);
			if (index == -1)
			{
				break;
			}

			charsSort[index + position] = replacementLength;

			replacementLength++;
			if (replacementLength > replacementPositions.Length)
			{
				ArrayExtensions.Resize(ref replacementPositions);
			}

			ref var specialCharPosition = ref replacementPositions[replacementLength - 1];
			specialCharPosition.Index = index + position;
			specialCharPosition.Length = from.Length;
			specialCharPosition.Replacement = to;

			var length = index + from.Length;
			position += length;
			charsLeft = charsLeft.Slice(length);
		}
		while (charsLeft.Length > 0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void SortReplacements(ref Span<int> charsSort, ref int charsSortLength)
	{
		for (var i = 0; i < charsSort.Length; i++)
		{
			if (charsSort[i] != -1)
			{
				charsSort[charsSortLength] = charsSort[i];
				charsSortLength++;
			}
		}
	}

	private void ValidateReplacements()
	{
		foreach (var replacement in replacements)
		{
			if (replacement.From.Length == 0)
			{
				throw new CsvConfigurationException($"{nameof(Replacement)} {nameof(replacement.From)} cannot be empty.");
			}

			if (replacement.From.SequenceEqual(replacement.To))
			{
				throw new CsvConfigurationException($"{nameof(Replacement)} {nameof(replacement.From)} and {nameof(replacement.To)} cannot be the same.");
			}
		}

		if (replacements.GroupBy(r => new string(r.From)).Any(g => g.Count() > 1))
		{
			throw new CsvConfigurationException($"{nameof(Replacement)} {nameof(Replacement.From)} cannot be the same.");
		}

		if (replacements.Any(r1 => replacements.Any(r2 => !r1.Equals(r2) && new string(r2.From).Contains(new string(r1.From)))))
		{
			throw new CsvConfigurationException($"{nameof(Replacement)} {nameof(Replacement.From)} cannot contain any other {nameof(Replacement.From)}.");
		}
	}

	[DebuggerDisplay("Index = {Index}, Length = {Length}, Replacement = {Replacement}")]
	private record struct ReplacementPosition
	(
		int Index,
		int Length,
		char[] Replacement
	);
}
