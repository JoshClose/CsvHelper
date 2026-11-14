using System;

namespace CsvHelper;

/// <summary>Picks the most likely separator by looking at how consistently candidates appear per line.</summary>
internal static class CsvSeparatorDetector
{
	private const int MaxLines = 32;

	// The candidates whose counts fit on the stack; longer lists are counted in an array.
	private const int StackCandidates = 8;

	/// <summary>
	/// Whether <paramref name="data"/> is enough to detect the separator from: 16 KiB, or the 32 lines that
	/// <see cref="Detect"/> looks at. Until then the reader reads more input first, unless the input has ended or its
	/// buffer is full.
	/// </summary>
	/// <param name="data">The start of the input read so far.</param>
	/// <returns>Whether <paramref name="data"/> has at least 16 KiB or 32 line feeds.</returns>
	public static bool HasEnoughData(ReadOnlySpan<byte> data)
	{
		if (data.Length >= 16 * 1024) return true;
		var lines = 0;
		foreach (var b in data)
		{
			if (b == (byte)'\n' && ++lines >= MaxLines) return true;
		}

		return false;
	}

	/// <summary>Picks the separator of <paramref name="data"/>.</summary>
	/// <param name="data">The start of the input.</param>
	/// <param name="candidateText">
	/// The characters to choose from, in order, as <see cref="CsvReaderOptions.SeparatorCandidates"/> lists them: ASCII
	/// characters other than CR and LF.
	/// </param>
	/// <param name="quote">The quote character, or -1 without quoting.</param>
	/// <param name="comment">The comment prefix, or -1 without comments.</param>
	/// <param name="escape">The escape character, or -1 without escaping.</param>
	/// <returns>
	/// The separator; the first candidate when none appears. Candidates that are the quote, the comment prefix or the
	/// escape character are left out, and a comma is returned when that leaves none.
	/// </returns>
	public static byte Detect(ReadOnlySpan<byte> data, string candidateText, int quote, int comment, int escape)
	{
		// The candidates in order, once each, and the position of each by byte value, plus one (0 for other bytes).
		Span<byte> candidates = stackalloc byte[128];
		Span<byte> slots = stackalloc byte[256];
		slots.Clear();
		var candidateCount = 0;
		foreach (var ch in candidateText)
		{
			if (ch >= 128 || ch == quote || ch == comment || ch == escape || slots[ch] != 0) continue;
			candidates[candidateCount] = (byte)ch;
			slots[ch] = (byte)++candidateCount;
		}

		if (candidateCount == 0) return (byte)',';
		candidates = candidates.Slice(0, candidateCount);

		Span<int> counts = candidateCount <= StackCandidates
			? stackalloc int[StackCandidates * MaxLines]
			: new int[candidateCount * MaxLines];
		Span<int> current = stackalloc int[128];
		counts.Clear();
		current = current.Slice(0, candidateCount);
		current.Clear();
		var lines = 0;
		var inQuote = false;
		var lineStart = true;
		var commentLine = false;
		var hasContent = false;

		for (var i = 0; i < data.Length; i++)
		{
			var c = data[i];
			if (inQuote)
			{
				if (c == quote) inQuote = false;
				continue;
			}

			if (c == (byte)'\n' || c == (byte)'\r')
			{
				if (hasContent && !commentLine)
				{
					current.CopyTo(counts.Slice(lines * candidateCount, candidateCount));
					if (++lines == MaxLines) break;
				}

				current.Clear();
				hasContent = false;
				commentLine = false;
				lineStart = true;
				continue;
			}

			if (lineStart && c == comment) commentLine = true;
			lineStart = false;
			hasContent = true;
			if (commentLine) continue;
			if (c == quote)
			{
				inQuote = true;
				continue;
			}

			if (c == escape)
			{
				// The next byte is text, and so is an LF after an escaped CR.
				i++;
				if (i + 1 < data.Length && data[i] == (byte)'\r' && data[i + 1] == (byte)'\n') i++;
				continue;
			}

			var slot = slots[c];
			if (slot != 0) current[slot - 1]++;
		}

		if (lines == 0 && hasContent && !commentLine)
		{
			current.CopyTo(counts);
			lines = 1;
		}

		// Score each candidate by how many lines share its most common non-zero count, so a title line
		// or a few ragged lines do not throw the detection off. Ties go to the earlier candidate.
		var best = -1;
		var bestScore = 0L;
		for (var k = 0; k < candidateCount; k++)
		{
			int bestCount = 0, bestFrequency = 0;
			for (var line = 0; line < lines; line++)
			{
				var value = counts[line * candidateCount + k];
				if (value == 0) continue;
				var frequency = 0;
				for (var other = 0; other < lines; other++)
				{
					if (counts[other * candidateCount + k] == value) frequency++;
				}

				if (frequency > bestFrequency || (frequency == bestFrequency && value > bestCount))
				{
					bestFrequency = frequency;
					bestCount = value;
				}
			}

			if (bestFrequency == 0) continue;
			var score = bestFrequency * 1000L + Math.Min(bestCount, 999);
			if (score > bestScore)
			{
				bestScore = score;
				best = k;
			}
		}

		return candidates[best < 0 ? 0 : best];
	}
}
