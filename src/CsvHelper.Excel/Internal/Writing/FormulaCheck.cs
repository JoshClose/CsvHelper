using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// The checks of a formula that keep it from making a workbook Excel can't open, and its line breaks as Excel takes them.
/// </summary>
/// <remarks>
/// <para>
/// Checked through COM on 2026-11-06 (Excel 365, build 20527), with <c>f</c> elements written by hand. Excel opens no
/// workbook with a formula that:
/// <list type="bullet">
/// <item>has more than 8,192 UTF-16 code units;</item>
/// <item>
/// has a parenthesis, a bracket or a brace that isn't closed, or one closed by another kind, or a closing one that closes
/// nothing (<c>SUM(A1</c>, <c>SUM(A1))</c>, <c>SUM(A1]</c>, <c>Table1[Col</c>, <c>SUM({1,2)</c>);
/// </item>
/// <item>has a string or a quoted sheet name that isn't closed (<c>LEN("abc)</c>, <c>'Data!A1</c>);</item>
/// <item>
/// starts with an operator other than <c>+</c>, <c>-</c> and <c>@</c> (<c>*A1</c>, <c>&amp;A1</c>, <c>%A1</c>), also after
/// white space;
/// </item>
/// <item>ends with an operator (<c>A1+</c>, <c>A1:</c>, <c>A1,</c>, <c>A1@</c>, <c>A1#</c>), also before white space;</item>
/// <item>
/// has a control character other than LF outside its strings: a tab between its tokens, U+0001, or a CR, which a
/// <c>_x000D_</c> escape decodes to. A literal CR is XML's line end, which reads as LF.
/// </item>
/// </list>
/// </para>
/// <para>
/// It opens brackets of every kind inside strings and quoted sheet names, string literals longer than the 255 characters
/// its formula bar takes, spaces and line breaks between tokens and at either end, <c>%</c> at the end, empty arguments
/// (<c>SUM(1,)</c>), and structured references to tables it doesn't have (as <c>#REF!</c>). In a structured reference's
/// brackets an apostrophe escapes the character after it (<c>Table1[a'[b]</c>), so only brackets count there.
/// </para>
/// <para>
/// Formulas it can't parse for other reasons (<c>(*A1)</c>, <c>(A1*)</c>, an external reference without its link, a
/// no-break space between tokens) make a workbook it can't open too, and these checks don't find them. Those that it
/// parses but can't compute open, as <c>#NAME?</c> or <c>#REF!</c>.
/// </para>
/// </remarks>
internal static class FormulaCheck
{
	/// <summary>The most UTF-16 code units a formula has, without its <c>=</c>.</summary>
	public const int MaxLength = 8192;

	/// <summary>The most code units of a formula that a message quotes.</summary>
	private const int ShownLength = 100;

	/// <summary>The brackets kept on the stack before it needs an array.</summary>
	private const int StackSize = 64;

	/// <summary>
	/// Checks a formula, and gives it with its CRs outside its strings as LFs, the only line break Excel takes there (a
	/// CR LF becomes one LF).
	/// </summary>
	/// <param name="formula">The formula, without its <c>=</c>.</param>
	/// <param name="normalized">The formula with its line breaks as LFs, or <see langword="null"/> when it has no CR outside its strings.</param>
	/// <returns>What makes Excel refuse the formula, as a message, or <see langword="null"/> when nothing does.</returns>
	public static string? Check(ReadOnlySpan<char> formula, out string? normalized)
	{
		normalized = null;
		if (formula.Length > MaxLength)
		{
			return $"A formula is at most {MaxLength.ToString("N0", CultureInfo.InvariantCulture)} characters long, and this one has {formula.Length.ToString("N0", CultureInfo.InvariantCulture)}.";
		}

		var first = 0;
		while (first < formula.Length && IsWhiteSpace(formula[first]))
		{
			first++;
		}

		if (first == formula.Length)
		{
			return "A formula can't be empty: a cell without one is a null or default(ExcelFormula) value.";
		}

		if (IsStartOperator(formula[first]))
		{
			return Refused(formula, $"starts with the operator {formula[first]}");
		}

		var problem = Scan(formula, default, out var breaks, out _);
		if (problem is not null)
		{
			return Refused(formula, problem);
		}

		// Every string, quoted name and bracket is closed, so the last character that isn't white space is outside them.
		var last = formula.Length - 1;
		while (IsWhiteSpace(formula[last]))
		{
			last--;
		}

		if (IsEndOperator(formula[last]))
		{
			return Refused(formula, $"ends with the operator {formula[last]}");
		}

		if (breaks > 0)
		{
			var chars = new char[formula.Length];
			Scan(formula, chars, out _, out var written);
			normalized = new string(chars, 0, written);
		}

		return null;
	}

	/// <summary>
	/// Reads a formula's strings, quoted sheet names and brackets, and the characters outside its strings, and copies it
	/// with its CRs outside its strings as LFs.
	/// </summary>
	/// <param name="formula">The formula, which isn't empty.</param>
	/// <param name="output">Where the formula is copied, as long as it; or empty, to check it only.</param>
	/// <param name="breaks">The CRs outside its strings.</param>
	/// <param name="written">The characters copied.</param>
	/// <returns>What makes Excel refuse it, for <see cref="Refused"/>, or <see langword="null"/>.</returns>
	private static string? Scan(ReadOnlySpan<char> formula, Span<char> output, out int breaks, out int written)
	{
		breaks = 0;
		written = 0;
		var copy = !output.IsEmpty;
		Span<char> open = formula.Length <= StackSize ? stackalloc char[StackSize] : new char[formula.Length];
		var depth = 0;
		var quoted = false;
		for (var i = 0; i < formula.Length; i++)
		{
			var c = formula[i];
			if (quoted)
			{
				// A quoted sheet name. Its '', an apostrophe, closes it and opens another, which reads the same.
				if (c == '\'')
				{
					quoted = false;
				}
			}
			else if (depth > 0 && open[depth - 1] == '[')
			{
				// A structured reference's names, or a workbook's: only brackets count, and an apostrophe escapes the
				// character after it.
				if (c == '\'' && i + 1 < formula.Length)
				{
					Copy(output, ref written, copy, "'");
					c = formula[++i];
				}
				else if (c == '[')
				{
					open[depth++] = c;
				}
				else if (c == ']')
				{
					depth--;
				}
			}
			else
			{
				switch (c)
				{
					case '"':
						// A string, whose characters are its value, copied as they are. Its "", a quote, closes it and opens
						// another, which reads the same.
						var end = formula.Slice(i + 1).IndexOf('"');
						if (end < 0)
						{
							return "has a string that isn't closed";
						}

						end += i + 1;
						if (copy)
						{
							formula.Slice(i, end + 1 - i).CopyTo(output.Slice(written));
							written += end + 1 - i;
						}

						i = end;

						continue;
					case '\'':
						quoted = true;

						break;
					case '(' or '[' or '{':
						open[depth++] = c;

						break;
					case ')' or ']' or '}':
						if (depth == 0)
						{
							return $"has a {c} that closes nothing";
						}

						var opener = open[depth - 1];
						if (opener != Opener(c))
						{
							return $"closes a {Kind(opener)} with {c}";
						}

						depth--;

						break;
				}
			}

			if (c == '\r')
			{
				// XML's line end: one LF for CR LF, as for CR alone.
				breaks++;
				if (copy && !(i + 1 < formula.Length && formula[i + 1] == '\n'))
				{
					output[written++] = '\n';
				}

				continue;
			}

			if (c < ' ' && c != '\n')
			{
				return c == '\t' ? "has a tab outside its strings" : $"has U+{(int)c:X4} outside its strings";
			}

			if (copy)
			{
				output[written++] = c;
			}
		}

		if (quoted)
		{
			return "has a quoted sheet name that isn't closed";
		}

		return depth > 0 ? $"has a {Kind(open[depth - 1])} that isn't closed" : null;
	}

	/// <summary>Copies text into the output, when the formula is being copied.</summary>
	/// <param name="output">The output.</param>
	/// <param name="written">The characters copied so far.</param>
	/// <param name="copy">Whether the formula is being copied.</param>
	/// <param name="text">The text.</param>
	private static void Copy(Span<char> output, ref int written, bool copy, string text)
	{
		if (copy)
		{
			text.AsSpan().CopyTo(output.Slice(written));
			written += text.Length;
		}
	}

	/// <summary>A refusal's message, with the formula quoted.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="problem">What makes Excel refuse it, such as <c>has a parenthesis that isn't closed</c>.</param>
	/// <returns>The message.</returns>
	private static string Refused(ReadOnlySpan<char> formula, string problem) =>
		$"The formula \"{Shown(formula)}\" {problem}, and Excel opens no workbook with it.";

	/// <summary>A formula for a message: its first 100 code units and <c>...</c> when it is longer.</summary>
	/// <param name="formula">The formula.</param>
	/// <returns>The text shown.</returns>
	private static string Shown(ReadOnlySpan<char> formula)
	{
		if (formula.Length <= ShownLength)
		{
			return formula.ToString();
		}

		var length = char.IsHighSurrogate(formula[ShownLength - 1]) ? ShownLength - 1 : ShownLength;

		return formula.Slice(0, length).ToString() + "...";
	}

	/// <summary>The bracket that a closing one closes.</summary>
	/// <param name="closer">A closing parenthesis, bracket or brace.</param>
	/// <returns>Its opening one.</returns>
	private static char Opener(char closer) => closer switch
	{
		')' => '(',
		']' => '[',
		_ => '{',
	};

	/// <summary>The name of a kind of bracket, for messages.</summary>
	/// <param name="opener">An opening parenthesis, bracket or brace.</param>
	/// <returns><c>parenthesis</c>, <c>bracket</c> or <c>brace</c>.</returns>
	private static string Kind(char opener) => opener switch
	{
		'(' => "parenthesis",
		'[' => "bracket",
		_ => "brace",
	};

	/// <summary>Whether a character is white space that Excel takes in a formula: a space, LF, or CR (which reads as LF).</summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsWhiteSpace(char c) => c is ' ' or '\n' or '\r';

	/// <summary>Whether a formula can't start with a character: an operator that isn't a sign or <c>@</c>.</summary>
	/// <param name="c">The formula's first character that isn't white space.</param>
	/// <returns>Whether Excel refuses it there.</returns>
	private static bool IsStartOperator(char c) => c is '*' or '/' or '^' or '&' or '=' or '<' or '>' or ':' or ',' or '%';

	/// <summary>Whether a formula can't end with a character: an operator other than <c>%</c>.</summary>
	/// <param name="c">The formula's last character that isn't white space.</param>
	/// <returns>Whether Excel refuses it there.</returns>
	private static bool IsEndOperator(char c) => c is '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>' or ':' or ',' or '@' or '#';
}
