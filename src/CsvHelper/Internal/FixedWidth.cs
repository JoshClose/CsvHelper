using System;
using System.Collections.Generic;

namespace CsvHelper;

/// <summary>Column arithmetic of <see cref="CsvMode.FixedWidth"/> mode, shared by the reader and the writer.</summary>
/// <remarks>
/// Widths count UTF-16 code units, as <see cref="string.Length"/> does. In UTF-8 a 4-byte sequence is two units (a
/// surrogate pair) and an invalid sequence is one unit per maximal invalid subpart, as it decodes to one U+FFFD. A column
/// boundary that falls inside a surrogate pair moves after the pair.
/// </remarks>
internal static class FixedWidth
{
	/// <summary>Checks the widths of the columns and returns their total.</summary>
	/// <param name="widths">The widths.</param>
	/// <param name="name">The name of the option, for exceptions.</param>
	/// <returns>The sum of the widths.</returns>
	public static int Validate(IReadOnlyList<int>? widths, string name)
	{
		if (widths is null || widths.Count == 0)
			throw new ArgumentException("FixedWidth mode needs the widths of the fields.", name);
		var total = 0L;
		for (var i = 0; i < widths.Count; i++)
		{
			if (widths[i] <= 0) throw new ArgumentException($"Field width {i} is {widths[i]}; widths must be positive.", name);
			total += widths[i];
		}

		if (total > int.MaxValue) throw new ArgumentException("The field widths add up to more than int.MaxValue.", name);

		return (int)total;
	}

	/// <summary>The character position where each column starts, and last where the last one ends.</summary>
	/// <param name="widths">The widths of the columns (already validated).</param>
	/// <returns>An array of <c>widths.Count + 1</c> positions.</returns>
	public static int[] Starts(IReadOnlyList<int> widths)
	{
		var starts = new int[widths.Count + 1];
		for (var i = 0; i < widths.Count; i++) starts[i + 1] = starts[i] + widths[i];

		return starts;
	}

	/// <summary>The number of ASCII bytes at the start of <paramref name="s"/>.</summary>
	/// <param name="s">The bytes.</param>
	/// <returns>The index of the first byte that is not ASCII, or the length.</returns>
	public static int AsciiPrefixLength(ReadOnlySpan<byte> s)
	{
#if NET
		var i = s.IndexOfAnyExceptInRange((byte)0, (byte)0x7F);

		return i < 0 ? s.Length : i;
#else
		return Portable.AsciiPrefixLength(s);
#endif
	}

	/// <summary>The byte offset of character position <paramref name="chars"/> in <paramref name="line"/>.</summary>
	/// <param name="line">The UTF-8 text of the line.</param>
	/// <param name="chars">The position, in UTF-16 code units.</param>
	/// <returns>The offset, at most the length of the line.</returns>
	public static int Offset(ReadOnlySpan<byte> line, int chars)
	{
		// Every unit takes at least one byte.
		if (chars >= line.Length) return line.Length;
		var i = AsciiPrefixLength(line.Slice(0, chars));
		if (i == chars) return i;

		return Walk(line, i, i, chars);
	}

	/// <summary>
	/// Fills <paramref name="offsets"/> with the byte offsets in <paramref name="line"/> of the character positions
	/// in <paramref name="chars"/> (increasing), in one pass over the line.
	/// </summary>
	/// <param name="line">The UTF-8 text of the line.</param>
	/// <param name="chars">The positions, in UTF-16 code units.</param>
	/// <param name="offsets">Receives the offsets, each at most the length of the line.</param>
	public static void Offsets(ReadOnlySpan<byte> line, ReadOnlySpan<int> chars, Span<int> offsets)
	{
		var i = 0;
		var units = 0;
		for (var k = 0; k < chars.Length; k++)
		{
			i = Walk(line, i, units, chars[k], out units);
			offsets[k] = i;
		}
	}

	/// <summary>The length of UTF-8 text in UTF-16 code units.</summary>
	/// <param name="utf8">The text.</param>
	/// <returns>The number of UTF-16 code units it decodes to.</returns>
	public static int Utf16Length(ReadOnlySpan<byte> utf8)
	{
		var i = AsciiPrefixLength(utf8);
		if (i == utf8.Length) return i;
		var units = i;
		while (i < utf8.Length)
		{
			if (utf8[i] < 0x80)
			{
				i++;
				units++;
				continue;
			}

			i += Sequence(utf8, i, out var n);
			units += n;
		}

		return units;
	}

	private static int Walk(ReadOnlySpan<byte> s, int i, int units, int chars) => Walk(s, i, units, chars, out _);

	/// <summary>Walks from byte <paramref name="i"/>, which is at unit <paramref name="units"/>, to unit <paramref name="chars"/>.</summary>
	private static int Walk(ReadOnlySpan<byte> s, int i, int units, int chars, out int reached)
	{
		while (i < s.Length && units < chars)
		{
			if (s[i] < 0x80)
			{
				i++;
				units++;
				continue;
			}

			i += Sequence(s, i, out var n);
			units += n;
		}

		reached = units;

		return i;
	}

	/// <summary>
	/// The length in bytes of the UTF-8 sequence that starts with the non-ASCII byte at <paramref name="i"/>, or of the
	/// maximal invalid subpart there, and the number of UTF-16 code units it decodes to.
	/// </summary>
	private static int Sequence(ReadOnlySpan<byte> s, int i, out int units)
	{
		units = 1;
		var b = s[i];
		int need;
		byte lo = 0x80, hi = 0xBF;
		if (b < 0xC2)
		{
			// A continuation byte, or an overlong lead.
			return 1;
		}

		if (b < 0xE0)
		{
			need = 1;
		}
		else if (b < 0xF0)
		{
			need = 2;
			if (b == 0xE0) lo = 0xA0;
			else if (b == 0xED) hi = 0x9F;
		}
		else if (b < 0xF5)
		{
			need = 3;
			if (b == 0xF0) lo = 0x90;
			else if (b == 0xF4) hi = 0x8F;
		}
		else
		{
			return 1;
		}

		var k = 1;
		for (; k <= need; k++)
		{
			if (i + k >= s.Length) return k;
			var c = s[i + k];
			if (c < lo || c > hi) return k;
			lo = 0x80;
			hi = 0xBF;
		}

		if (need == 3) units = 2;

		return k;
	}
}
