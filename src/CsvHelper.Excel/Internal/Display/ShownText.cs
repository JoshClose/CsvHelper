using System;

namespace CsvHelper.Excel;

/// <summary>
/// The text a cell shows, written as UTF-8 into a span: what doesn't fit is counted, not written, so that a caller can
/// ask for the room it needs (core's <c>CsvCellFormatter.TryFormat</c>), and the characters are counted as UTF-16 units,
/// as Excel counts the 255 it shows a number in at most.
/// </summary>
internal ref struct ShownText
{
	private readonly Span<byte> destination;

	/// <summary>Creates a text written into a span; an empty span only counts.</summary>
	/// <param name="destination">Where the text goes.</param>
	public ShownText(Span<byte> destination)
	{
		this.destination = destination;
		Length = 0;
		Chars = 0;
	}

	/// <summary>The bytes of the text so far, written or not.</summary>
	public int Length { get; private set; }

	/// <summary>The UTF-16 units of the text so far.</summary>
	public int Chars { get; private set; }

	/// <summary>Whether the text so far fits the span.</summary>
	public readonly bool Fits => Length <= destination.Length;

	/// <summary>The text written, when it fits.</summary>
	public readonly ReadOnlySpan<byte> Written => destination.Slice(0, Math.Min(Length, destination.Length));

	/// <summary>Appends an ASCII character.</summary>
	/// <param name="ascii">The character.</param>
	public void Append(char ascii)
	{
		if (Length < destination.Length)
		{
			destination[Length] = (byte)ascii;
		}

		Length++;
		Chars++;
	}

	/// <summary>Appends a decimal digit.</summary>
	/// <param name="digit">The digit, 0 to 9.</param>
	public void AppendDigit(int digit) => Append((char)('0' + digit));

	/// <summary>Appends spaces.</summary>
	/// <param name="count">How many.</param>
	public void AppendSpaces(int count)
	{
		for (var i = 0; i < count; i++)
		{
			Append(' ');
		}
	}

	/// <summary>Appends UTF-8 text.</summary>
	/// <param name="utf8">The text.</param>
	public void Append(scoped ReadOnlySpan<byte> utf8)
	{
		if (Length + utf8.Length <= destination.Length)
		{
			utf8.CopyTo(destination.Slice(Length));
		}

		Length += utf8.Length;
		Chars += CountChars(utf8);
	}

	/// <summary>How many UTF-16 units UTF-8 text has: one a character, two one past U+FFFF.</summary>
	/// <param name="utf8">The text.</param>
	/// <returns>The units.</returns>
	public static int CountChars(ReadOnlySpan<byte> utf8)
	{
		var count = 0;
		foreach (var b in utf8)
		{
			// A lead byte or ASCII starts a character; a four-byte lead (0xF0 and up) starts a surrogate pair.
			if ((b & 0xC0) != 0x80)
			{
				count += b >= 0xF0 ? 2 : 1;
			}
		}

		return count;
	}
}
