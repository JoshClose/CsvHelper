using System;

namespace CsvHelper;

/// <summary>Picks the most likely separator by looking at how consistently candidates appear per line.</summary>
internal static class CsvSeparatorDetector
{
	private const int MaxLines = 32;
	private const int CandidateCount = 4;

	private static ReadOnlySpan<byte> Candidates => [(byte)',', (byte)';', (byte)'\t', (byte)'|'];

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
	/// <param name="quote">The quote character, or -1 without quoting.</param>
	/// <param name="comment">The comment prefix, or -1 without comments.</param>
	/// <param name="escape">The escape character, or -1 without escaping.</param>
	/// <returns>The separator; a comma when no candidate appears.</returns>
	public static byte Detect(ReadOnlySpan<byte> data, int quote, int comment, int escape)
	{
		Span<int> counts = stackalloc int[CandidateCount * MaxLines];
		Span<int> current = stackalloc int[CandidateCount];
		counts.Clear();
		current.Clear();
		var candidates = Candidates;
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
					current.CopyTo(counts.Slice(lines * CandidateCount, CandidateCount));
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

			var k = candidates.IndexOf(c);
			if (k >= 0) current[k]++;
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
		for (var k = 0; k < CandidateCount; k++)
		{
			var candidate = candidates[k];
			if (candidate == quote || candidate == comment || candidate == escape) continue;
			int bestCount = 0, bestFrequency = 0;
			for (var line = 0; line < lines; line++)
			{
				var value = counts[line * CandidateCount + k];
				if (value == 0) continue;
				var frequency = 0;
				for (var other = 0; other < lines; other++)
				{
					if (counts[other * CandidateCount + k] == value) frequency++;
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

		return best < 0 ? (byte)',' : candidates[best];
	}
}
