using System;

namespace CsvHelper.Excel;

/// <summary>
/// Tells which number formats make a number cell a date or a duration: the built-in formats by id, and format codes by
/// their tokens. The rules are Excel's, as <c>tests/CsvHelper.Excel.Tests/Files/excel-formats.json</c> records Excel's
/// answers (see Phase 2 of docs/plans/excel.md).
/// </summary>
internal static class NumberFormats
{
	/// <summary>
	/// The kind of a number cell in a built-in number format that the styles part doesn't define, as Excel (en-US) shows
	/// the ids: dates and times are 14 to 22, 27 to 36, 45, 47, 50 to 58, 71 to 79 and 81; durations (<c>[h]:mm:ss</c>)
	/// are 46 and 80. Every other id, General (0) and the ids that no format has included, is a number.
	/// </summary>
	/// <param name="id">The number format id.</param>
	/// <returns><see cref="SheetCellKind.DateNumber"/>, <see cref="SheetCellKind.Duration"/> or <see cref="SheetCellKind.Number"/>.</returns>
	public static SheetCellKind BuiltIn(int id) => id switch
	{
		>= 14 and <= 22 or >= 27 and <= 36 or 45 or 47 or >= 50 and <= 58 or >= 71 and <= 79 or 81 => SheetCellKind.DateNumber,
		46 or 80 => SheetCellKind.Duration,
		_ => SheetCellKind.Number,
	};

	/// <summary>
	/// The kind of a number cell in a format code, from the tokens of its first section, which is what Excel goes by: a
	/// positive number shows in that section, and Excel takes <c>0;yyyy</c> for a number.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	/// <item><description>
	/// Skipped: quoted text (<c>"days"</c>), the character after <c>\</c>, <c>_</c> or <c>*</c>, brackets other than
	/// elapsed time (<c>[Red]</c>, <c>[$-409]</c>, <c>[&gt;=100]</c>), <c>General</c> in any case, and the <c>E</c> of an
	/// exponent (<c>E+</c> or <c>E-</c> after a digit placeholder).
	/// </description></item>
	/// <item><description>
	/// A date: <c>y</c>, <c>d</c>, <c>e</c> (a year), <c>g</c> (an era), <c>b</c> (a Buddhist year) or a run of three or
	/// more <c>a</c> (a weekday), in any case. A date part makes the format a date even with elapsed time:
	/// <c>yyyy-mm-dd [h]:mm</c> shows the hour of the day.
	/// </description></item>
	/// <item><description>A duration: <c>[h]</c>, <c>[m]</c> or <c>[s]</c>, of any number of letters (<c>[hh]</c>, <c>[hhh]</c>).</description></item>
	/// <item><description>A time: <c>h</c>, <c>s</c>, <c>AM/PM</c> or <c>A/P</c>; and <c>m</c>, a month without them, a minute with them.</description></item>
	/// </list>
	/// </remarks>
	/// <param name="code">The format code.</param>
	/// <returns><see cref="SheetCellKind.DateNumber"/>, <see cref="SheetCellKind.Duration"/> or <see cref="SheetCellKind.Number"/>.</returns>
	public static SheetCellKind Classify(string code)
	{
		var date = false;
		var time = false;
		var elapsed = false;
		var digits = false;
		for (var i = 0; i < code.Length; i++)
		{
			var c = code[i];
			if (c == ';')
			{
				break;
			}

			switch (c)
			{
				case '"':
					var close = code.IndexOf('"', i + 1);
					i = close < 0 ? code.Length : close;

					continue;
				case '\\' or '_' or '*':
					i++;

					continue;
				case '[':
					var end = code.IndexOf(']', i + 1);
					if (end < 0)
					{
						i = code.Length;

						continue;
					}

					elapsed |= IsElapsed(code.AsSpan(i + 1, end - i - 1));
					i = end;

					continue;
				case '0' or '#' or '?':
					digits = true;

					continue;
			}

			var lower = char.ToLowerInvariant(c);
			if (lower == 'g' && Matches(code, i, "general"))
			{
				i += 6;
			}
			else if (lower == 'e' && digits && i + 1 < code.Length && code[i + 1] is '+' or '-')
			{
				i++;
			}
			else if (lower == 'a' && Matches(code, i, "am/pm"))
			{
				time = true;
				i += 4;
			}
			else if (lower == 'a' && Matches(code, i, "a/p"))
			{
				time = true;
				i += 2;
			}
			else if (lower == 'a')
			{
				var run = 1;
				while (i + run < code.Length && char.ToLowerInvariant(code[i + run]) == 'a')
				{
					run++;
				}

				date |= run >= 3;
				i += run - 1;
			}
			else if (lower is 'y' or 'd' or 'e' or 'g' or 'b')
			{
				date = true;
			}
			else if (lower is 'h' or 's' or 'm')
			{
				time = true;
			}
		}

		if (date)
		{
			return SheetCellKind.DateNumber;
		}

		if (elapsed)
		{
			return SheetCellKind.Duration;
		}

		return time ? SheetCellKind.DateNumber : SheetCellKind.Number;
	}

	/// <summary>Whether a bracket's content is elapsed time: one or more of the same letter, <c>h</c>, <c>m</c> or <c>s</c>, in any case.</summary>
	/// <param name="content">The text between the brackets.</param>
	/// <returns>Whether it is <c>h</c>, <c>hh</c>, <c>m</c>, <c>ss</c> and so on.</returns>
	private static bool IsElapsed(ReadOnlySpan<char> content)
	{
		if (content.IsEmpty)
		{
			return false;
		}

		var letter = char.ToLowerInvariant(content[0]);
		if (letter is not ('h' or 'm' or 's'))
		{
			return false;
		}

		foreach (var c in content)
		{
			if (char.ToLowerInvariant(c) != letter)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Whether <paramref name="code"/> has <paramref name="word"/> at <paramref name="index"/>, in any case.</summary>
	/// <param name="code">The format code.</param>
	/// <param name="index">Where to look.</param>
	/// <param name="word">The word, in lower case.</param>
	/// <returns>Whether the word is there.</returns>
	private static bool Matches(string code, int index, string word) =>
		string.Compare(code, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0 && index + word.Length <= code.Length;
}
