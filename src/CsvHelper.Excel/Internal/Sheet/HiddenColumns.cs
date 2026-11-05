using System;

namespace CsvHelper.Excel;

/// <summary>
/// The columns of a sheet that Excel hides, from its <c>cols</c> and <c>sheetFormatPr</c> (<see cref="HiddenState"/>):
/// 16,384 bits, set as each <c>col</c> is read, so that the last <c>col</c> over a column decides it, and a default for
/// the columns that no <c>col</c> covers.
/// </summary>
internal sealed class HiddenColumns
{
	/// <summary>The words of a set of a bit per column.</summary>
	private const int Words = CellReference.MaxColumns / 64;

	// The columns a col covers, and of those the ones it hides.
	private readonly ulong[] covered = new ulong[Words];
	private readonly ulong[] hidden = new ulong[Words];

	/// <summary>Whether the columns that no <c>col</c> covers are hidden: the sheet's default width is 0.</summary>
	public bool HidesUncovered { get; set; }

	/// <summary>Sets columns as a <c>col</c> says, over what any <c>col</c> before said of them.</summary>
	/// <param name="first">The first column, from 0.</param>
	/// <param name="last">The last column, from 0, at or after <paramref name="first"/> and before <see cref="CellReference.MaxColumns"/>.</param>
	/// <param name="hides">Whether the <c>col</c> hides them.</param>
	public void Set(int first, int last, bool hides)
	{
		var firstWord = first >> 6;
		var lastWord = last >> 6;
		var firstMask = ulong.MaxValue << (first & 63);
		var lastMask = ulong.MaxValue >> (63 - (last & 63));
		if (firstWord == lastWord)
		{
			Set(firstWord, firstMask & lastMask, hides);

			return;
		}

		// A million cols over every column take a few fills of 254 words each, not a loop over every column.
		Set(firstWord, firstMask, hides);
		covered.AsSpan(firstWord + 1, lastWord - firstWord - 1).Fill(ulong.MaxValue);
		hidden.AsSpan(firstWord + 1, lastWord - firstWord - 1).Fill(hides ? ulong.MaxValue : 0);
		Set(lastWord, lastMask, hides);
	}

	/// <summary>Whether a column is hidden.</summary>
	/// <param name="column">The column, from 0.</param>
	/// <returns>Whether it is hidden.</returns>
	public bool IsHidden(int column)
	{
		var word = column >> 6;
		var bit = 1UL << (column & 63);

		return (covered[word] & bit) != 0 ? (hidden[word] & bit) != 0 : HidesUncovered;
	}

	/// <summary>Sets the columns of a word.</summary>
	/// <param name="word">The word.</param>
	/// <param name="mask">Its columns to set.</param>
	/// <param name="hides">Whether they are hidden.</param>
	private void Set(int word, ulong mask, bool hides)
	{
		covered[word] |= mask;
		hidden[word] = hides ? hidden[word] | mask : hidden[word] & ~mask;
	}
}
