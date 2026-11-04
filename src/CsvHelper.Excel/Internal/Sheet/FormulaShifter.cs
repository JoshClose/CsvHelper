using System;

namespace CsvHelper.Excel;

/// <summary>
/// Moves a shared formula's relative references from the cell that defines it to another cell of its group, as Excel
/// reads the other cells of a shared formula (<c>&lt;f t="shared" si="0"/&gt;</c>): the formula <c>A1*2</c> of
/// <c>B1</c> is <c>A2*2</c> in <c>B2</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What moves:</b> cell references (<c>A1</c>, <c>$A1</c>, <c>A$1</c>, letters of either case, within <c>A1</c> to
/// <c>XFD1048576</c>), and each end of a range of whole columns (<c>A:C</c>) or whole rows (<c>1:3</c>), after a sheet
/// name or not. A part without <c>$</c> moves by the cell's offset, and wraps around the edges of the grid as Excel
/// wraps it: <c>XFD1</c> moved a column right is <c>A1</c>, and <c>B1048576</c> moved a row down is <c>B1</c>. In a
/// moved formula every reference's column is written in capitals, moved or not (<c>a1+$b$1</c> moved a row down is
/// <c>A2+$B$1</c>), as Excel and ClosedXML write them; a row that doesn't move is copied as it is.
/// </para>
/// <para>
/// <b>What doesn't:</b> everything else is copied as it is.
/// <list type="bullet">
/// <item>string literals (<c>"A1"</c>) and quoted sheet names (<c>'Q1 Orders'</c>), with their doubled quotes;</item>
/// <item>brackets, nested, with <c>'</c> escapes in them: structured references (<c>Table1[[#This Row],[Col]]</c>) and workbook indexes (<c>[1]</c>);</item>
/// <item>array constants (<c>{1,2;3,4}</c>);</item>
/// <item>error literals (<c>#REF!</c>, <c>#N/A</c>, <c>#DIV/0!</c>), and a lone <c>#</c>, the spill operator;</item>
/// <item>numbers (<c>1.5E+3</c>), operators and white space;</item>
/// <item>
/// words that aren't references: a function (a word before <c>(</c>, such as <c>LOG10(</c>), a sheet name (a word before
/// <c>!</c>, as in <c>Sheet1:Sheet3!</c>), a table (a word before <c>[</c>), and names (<c>TRUE</c>, <c>MyName</c>,
/// <c>XFE1</c>).
/// </item>
/// </list>
/// </para>
/// <para>
/// It reads the formula in one forward pass, without recursion, so nesting costs nothing. A moved formula is at most
/// <see cref="MaxGrowth"/> times as long as the one it moves.
/// </para>
/// </remarks>
internal static class FormulaShifter
{
	/// <summary>
	/// How many times longer a moved formula can be than the one it moves: <c>A1</c> becomes at most <c>XFD1048576</c>, and
	/// <c>1:1</c> at most <c>1048576:1048576</c>.
	/// </summary>
	public const int MaxGrowth = 5;

	/// <summary>The most letters a column has: <c>XFD</c>.</summary>
	private const int MaxColumnLetters = 3;

	/// <summary>The most digits a row has: <c>1048576</c>.</summary>
	private const int MaxRowDigits = 7;

	/// <summary>Moves a formula's relative references by a cell's offset from the cell that defines it.</summary>
	/// <param name="formula">The formula, as UTF-8, without the <c>=</c>.</param>
	/// <param name="rows">The rows the cell is below the defining cell; negative for above.</param>
	/// <param name="columns">The columns the cell is right of the defining cell; negative for left.</param>
	/// <param name="destination">Where the moved formula goes.</param>
	/// <param name="written">The moved formula's length, or 0 when it doesn't fit.</param>
	/// <returns>
	/// Whether the moved formula fits in <paramref name="destination"/>. It always does in <see cref="MaxGrowth"/> times the
	/// formula's length.
	/// </returns>
	public static bool TryShift(ReadOnlySpan<byte> formula, int rows, int columns, Span<byte> destination, out int written)
	{
		// The offsets within the grid, so that a moved part is the part plus its offset, modulo the grid's size.
		var rowOffset = Modulo(rows, CellReference.MaxRow);
		var columnOffset = Modulo(columns, CellReference.MaxColumns);
		var output = new Output(destination);
		if (rowOffset == 0 && columnOffset == 0)
		{
			output.Write(formula);
			written = output.Fits ? output.Length : 0;

			return output.Fits;
		}

		var i = 0;
		while (i < formula.Length && output.Fits)
		{
			var b = formula[i];
			int end;
			switch (b)
			{
				case (byte)'"':
				case (byte)'\'':
					end = QuotedEnd(formula, i);

					break;
				case (byte)'[':
					end = BracketEnd(formula, i);

					break;
				case (byte)'{':
					end = ArrayEnd(formula, i);

					break;
				case (byte)'#':
					end = ErrorEnd(formula, i);

					break;
				default:
					if (IsDigit(b) || (b == '$' && IsDigitAt(formula, i + 1)))
					{
						if (TryRowRange(formula, i, out var first, out var last, out end))
						{
							WriteRow(formula, first, rowOffset, ref output);
							output.Write((byte)':');
							WriteRow(formula, last, rowOffset, ref output);
							i = end;

							continue;
						}

						// A $ that doesn't start a range is copied alone, and the digits after it are a number.
						end = b == '$' ? i + 1 : NumberEnd(formula, i);
					}
					else if (b == '.' && IsDigitAt(formula, i + 1))
					{
						end = NumberEnd(formula, i);
					}
					else if (IsWordStart(formula, i))
					{
						i = Word(formula, i, rowOffset, columnOffset, ref output);

						continue;
					}
					else
					{
						end = i + 1;
					}

					break;
			}

			output.Write(formula.Slice(i, end - i));
			i = end;
		}

		written = output.Fits ? output.Length : 0;

		return output.Fits;
	}

	/// <summary>
	/// Writes a word: a cell reference or a range of whole columns, moved, or anything else as it is.
	/// </summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the word starts.</param>
	/// <param name="rowOffset">The rows to move by, within the grid.</param>
	/// <param name="columnOffset">The columns to move by, within the grid.</param>
	/// <param name="output">Where to write.</param>
	/// <returns>Where the word, or the range it starts, ends.</returns>
	private static int Word(ReadOnlySpan<byte> formula, int start, int rowOffset, int columnOffset, ref Output output)
	{
		var end = WordEnd(formula, start);
		var next = end < formula.Length ? formula[end] : (byte)0;

		// A function, a sheet name or a table: never a reference, whatever it looks like.
		if (next is (byte)'(' or (byte)'!' or (byte)'[')
		{
			output.Write(formula.Slice(start, end - start));

			return end;
		}

		if (TryCell(formula, start, end, out var column, out var row))
		{
			WriteColumn(column, columnOffset, ref output);
			WriteRow(formula, row, rowOffset, ref output);

			return end;
		}

		if (next == ':' && TryColumn(formula, start, end, out var first) && IsWordStart(formula, end + 1))
		{
			var lastEnd = WordEnd(formula, end + 1);
			var after = lastEnd < formula.Length ? formula[lastEnd] : (byte)0;
			if (after is not ((byte)'(' or (byte)'!' or (byte)'[') && TryColumn(formula, end + 1, lastEnd, out var last))
			{
				WriteColumn(first, columnOffset, ref output);
				output.Write((byte)':');
				WriteColumn(last, columnOffset, ref output);

				return lastEnd;
			}
		}

		output.Write(formula.Slice(start, end - start));

		return end;
	}

	/// <summary>Writes a column of a reference, in capitals: moved when it is relative, else as it is.</summary>
	/// <param name="column">The column.</param>
	/// <param name="offset">The columns to move by, within the grid.</param>
	/// <param name="output">Where to write.</param>
	private static void WriteColumn(Part column, int offset, ref Output output)
	{
		var value = column.Absolute ? column.Value : (column.Value + offset) % CellReference.MaxColumns;
		if (column.Absolute)
		{
			output.Write((byte)'$');
		}

		// Bijective base 26: A is 1 and Z is 26, so AA follows Z.
		Span<byte> letters = stackalloc byte[MaxColumnLetters];
		var i = MaxColumnLetters;
		var n = value + 1;
		while (n > 0)
		{
			n--;
			letters[--i] = (byte)('A' + n % 26);
			n /= 26;
		}

		output.Write(letters.Slice(i));
	}

	/// <summary>Writes a row of a reference: as it is when it is absolute or doesn't move, else moved.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="row">The row.</param>
	/// <param name="offset">The rows to move by, within the grid.</param>
	/// <param name="output">Where to write.</param>
	private static void WriteRow(ReadOnlySpan<byte> formula, Part row, int offset, ref Output output)
	{
		if (row.Absolute || offset == 0)
		{
			output.Write(formula.Slice(row.Start, row.Length));

			return;
		}

		Span<byte> digits = stackalloc byte[MaxRowDigits];
		var i = MaxRowDigits;
		var n = (row.Value - 1 + offset) % CellReference.MaxRow + 1;
		while (n > 0)
		{
			digits[--i] = (byte)('0' + n % 10);
			n /= 10;
		}

		output.Write(digits.Slice(i));
	}

	/// <summary>Reads a cell reference: <c>[$]</c>letters<c>[$]</c>digits, the whole of a word, within the grid.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the word starts.</param>
	/// <param name="end">Where the word ends.</param>
	/// <param name="column">The column: its text, and its value from 0.</param>
	/// <param name="row">The row: its text, and its value from 1.</param>
	/// <returns>Whether the word is a cell reference.</returns>
	private static bool TryCell(ReadOnlySpan<byte> formula, int start, int end, out Part column, out Part row)
	{
		row = default;
		var i = start;

		return TryColumnPart(formula, ref i, end, out column) && TryRowPart(formula, ref i, end, out row) && i == end;
	}

	/// <summary>Reads a column of a range of whole columns: <c>[$]</c>letters, the whole of a word, within the grid.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the word starts.</param>
	/// <param name="end">Where the word ends.</param>
	/// <param name="column">The column: its text, and its value from 0.</param>
	/// <returns>Whether the word is a column.</returns>
	private static bool TryColumn(ReadOnlySpan<byte> formula, int start, int end, out Part column)
	{
		var i = start;

		return TryColumnPart(formula, ref i, end, out column) && i == end;
	}

	/// <summary>
	/// Reads a range of whole rows, <c>[$]</c>digits<c>:[$]</c>digits, each within the grid, and not followed by a word or a
	/// number's point.
	/// </summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the range starts: at a digit or <c>$</c>.</param>
	/// <param name="first">The first row.</param>
	/// <param name="last">The last row.</param>
	/// <param name="end">Where the range ends.</param>
	/// <returns>Whether a range starts there.</returns>
	private static bool TryRowRange(ReadOnlySpan<byte> formula, int start, out Part first, out Part last, out int end)
	{
		last = default;
		end = start;
		if (!TryRowPart(formula, ref end, formula.Length, out first) || end >= formula.Length || formula[end] != ':')
		{
			return false;
		}

		end++;
		if (!TryRowPart(formula, ref end, formula.Length, out last))
		{
			return false;
		}

		// The point is a word's character too.
		return end == formula.Length || !IsWordPart(formula[end]);
	}

	/// <summary>Reads a column's part of a reference: <c>[$]</c> and 1 to 3 letters, up to <c>XFD</c>.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="i">Where the part starts; moved past it when it is one.</param>
	/// <param name="end">Where the part must end by.</param>
	/// <param name="column">The column: its text, and its value from 0.</param>
	/// <returns>Whether a column is there.</returns>
	private static bool TryColumnPart(ReadOnlySpan<byte> formula, ref int i, int end, out Part column)
	{
		column = default;
		var start = i;
		var absolute = i < end && formula[i] == '$';
		var j = absolute ? i + 1 : i;
		var value = 0;
		var letters = 0;
		while (j < end && letters <= MaxColumnLetters && IsLetter(formula[j]))
		{
			value = value * 26 + (formula[j] | 0x20) - 'a' + 1;
			letters++;
			j++;
		}

		if (letters == 0 || letters > MaxColumnLetters || value > CellReference.MaxColumns)
		{
			return false;
		}

		column = new Part(start, j - start, absolute, value - 1);
		i = j;

		return true;
	}

	/// <summary>Reads a row's part of a reference: <c>[$]</c> and 1 to 7 digits, from 1 to 1,048,576.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="i">Where the part starts; moved past it when it is one.</param>
	/// <param name="end">Where the part must end by.</param>
	/// <param name="row">The row: its text, and its value from 1.</param>
	/// <returns>Whether a row is there.</returns>
	private static bool TryRowPart(ReadOnlySpan<byte> formula, ref int i, int end, out Part row)
	{
		row = default;
		var start = i;
		var absolute = i < end && formula[i] == '$';
		var j = absolute ? i + 1 : i;
		var value = 0;
		var digits = 0;
		while (j < end && digits <= MaxRowDigits && IsDigit(formula[j]))
		{
			value = value * 10 + formula[j] - '0';
			digits++;
			j++;
		}

		if (digits == 0 || digits > MaxRowDigits || value < 1 || value > CellReference.MaxRow)
		{
			return false;
		}

		row = new Part(start, j - start, absolute, value);
		i = j;

		return true;
	}

	/// <summary>Where a quoted text ends: a string literal or a quoted sheet name, whose quote is doubled inside it.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the opening quote is.</param>
	/// <returns>The position after the closing quote, or the formula's end when there is none.</returns>
	private static int QuotedEnd(ReadOnlySpan<byte> formula, int start)
	{
		var quote = formula[start];
		var i = start + 1;
		while (i < formula.Length)
		{
			if (formula[i] == quote)
			{
				if (i + 1 < formula.Length && formula[i + 1] == quote)
				{
					i += 2;

					continue;
				}

				return i + 1;
			}

			i++;
		}

		return formula.Length;
	}

	/// <summary>Where brackets end: nested, with <c>'</c> escaping the character after it.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the opening bracket is.</param>
	/// <returns>The position after the matching bracket, or the formula's end when there is none.</returns>
	private static int BracketEnd(ReadOnlySpan<byte> formula, int start)
	{
		var depth = 0;
		var i = start;
		while (i < formula.Length)
		{
			switch (formula[i])
			{
				case (byte)'\'':
					i++;

					break;
				case (byte)'[':
					depth++;

					break;
				case (byte)']':
					depth--;
					if (depth == 0)
					{
						return i + 1;
					}

					break;
			}

			i++;
		}

		return formula.Length;
	}

	/// <summary>Where an array constant ends, with the string literals in it.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the opening brace is.</param>
	/// <returns>The position after the closing brace, or the formula's end when there is none.</returns>
	private static int ArrayEnd(ReadOnlySpan<byte> formula, int start)
	{
		var i = start + 1;
		while (i < formula.Length)
		{
			if (formula[i] == '"')
			{
				i = QuotedEnd(formula, i);

				continue;
			}

			if (formula[i] == '}')
			{
				return i + 1;
			}

			i++;
		}

		return formula.Length;
	}

	/// <summary>
	/// Where an error literal ends: <c>#</c>, then letters, digits, <c>/</c> and <c>_</c>, then a <c>!</c> or <c>?</c>
	/// (<c>#REF!</c>, <c>#N/A</c>, <c>#DIV/0!</c>, <c>#NAME?</c>, <c>#GETTING_DATA</c>). A <c>#</c> without them is the spill
	/// operator, alone.
	/// </summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the <c>#</c> is.</param>
	/// <returns>The position after the literal.</returns>
	private static int ErrorEnd(ReadOnlySpan<byte> formula, int start)
	{
		var i = start + 1;
		while (i < formula.Length && (IsLetter(formula[i]) || IsDigit(formula[i]) || formula[i] is (byte)'/' or (byte)'_'))
		{
			i++;
		}

		if (i > start + 1 && i < formula.Length && formula[i] is (byte)'!' or (byte)'?')
		{
			i++;
		}

		return i;
	}

	/// <summary>Where a number ends: digits, a point and digits, and an exponent (<c>E+5</c>, <c>e-3</c>, <c>E5</c>).</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the number starts: at a digit, or a point before one.</param>
	/// <returns>The position after the number.</returns>
	private static int NumberEnd(ReadOnlySpan<byte> formula, int start)
	{
		var i = start;
		while (i < formula.Length && IsDigit(formula[i]))
		{
			i++;
		}

		if (i < formula.Length && formula[i] == '.')
		{
			i++;
			while (i < formula.Length && IsDigit(formula[i]))
			{
				i++;
			}
		}

		if (i < formula.Length && (formula[i] | 0x20) == 'e')
		{
			var j = i + 1;
			if (j < formula.Length && formula[j] is (byte)'+' or (byte)'-')
			{
				j++;
			}

			if (IsDigitAt(formula, j))
			{
				i = j;
				while (i < formula.Length && IsDigit(formula[i]))
				{
					i++;
				}
			}
		}

		return i;
	}

	/// <summary>Where a word ends: its letters, digits, <c>_</c>, <c>.</c>, <c>\</c>, <c>$</c>, <c>?</c> and non-ASCII characters.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="start">Where the word starts.</param>
	/// <returns>The position after the word.</returns>
	private static int WordEnd(ReadOnlySpan<byte> formula, int start)
	{
		var i = start;
		while (i < formula.Length && IsWordPart(formula[i]))
		{
			i++;
		}

		return i;
	}

	/// <summary>
	/// Whether a word starts at a position: a letter, <c>_</c>, <c>\</c> or a non-ASCII character, or a <c>$</c> before a
	/// letter (<c>$A1</c>).
	/// </summary>
	/// <param name="formula">The formula.</param>
	/// <param name="i">The position.</param>
	/// <returns>Whether a word starts there.</returns>
	private static bool IsWordStart(ReadOnlySpan<byte> formula, int i)
	{
		if (i >= formula.Length)
		{
			return false;
		}

		var b = formula[i];
		if (b == '$')
		{
			return i + 1 < formula.Length && IsLetter(formula[i + 1]);
		}

		return IsLetter(b) || b is (byte)'_' or (byte)'\\' || b >= 0x80;
	}

	/// <summary>Whether a byte can be in a word.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it can.</returns>
	private static bool IsWordPart(byte b) => IsLetter(b) || IsDigit(b) || b is (byte)'_' or (byte)'.' or (byte)'\\' or (byte)'$' or (byte)'?' || b >= 0x80;

	/// <summary>Whether a byte is an ASCII letter.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsLetter(byte b) => (uint)((b | 0x20) - 'a') <= 'z' - 'a';

	/// <summary>Whether a byte is an ASCII digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsDigit(byte b) => (uint)(b - '0') <= 9;

	/// <summary>Whether the formula has a digit at a position.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="i">The position, which can be past the end.</param>
	/// <returns>Whether it has.</returns>
	private static bool IsDigitAt(ReadOnlySpan<byte> formula, int i) => i < formula.Length && IsDigit(formula[i]);

	/// <summary>A value modulo a size, from 0 to the size less one, for negative values too.</summary>
	/// <param name="value">The value.</param>
	/// <param name="size">The size.</param>
	/// <returns>The value modulo the size.</returns>
	private static int Modulo(int value, int size)
	{
		var m = value % size;

		return m < 0 ? m + size : m;
	}

	/// <summary>A column or row of a reference: where its text is, whether it has a <c>$</c>, and its value.</summary>
	private readonly struct Part
	{
		/// <summary>Creates the part.</summary>
		/// <param name="start">Where its text starts, at its <c>$</c> if it has one.</param>
		/// <param name="length">Its text's length.</param>
		/// <param name="absolute">Whether it has a <c>$</c>.</param>
		/// <param name="value">A column's value from 0, or a row's from 1.</param>
		public Part(int start, int length, bool absolute, int value)
		{
			Start = start;
			Length = length;
			Absolute = absolute;
			Value = value;
		}

		/// <summary>Where its text starts, at its <c>$</c> if it has one.</summary>
		public int Start { get; }

		/// <summary>Its text's length.</summary>
		public int Length { get; }

		/// <summary>Whether it has a <c>$</c>, so it doesn't move.</summary>
		public bool Absolute { get; }

		/// <summary>A column's value from 0, or a row's from 1.</summary>
		public int Value { get; }
	}

	/// <summary>The moved formula being written: it stops writing once something doesn't fit.</summary>
	private ref struct Output
	{
		private readonly Span<byte> destination;
		private int length;
		private bool overflowed;

		/// <summary>Creates the output.</summary>
		/// <param name="destination">Where the formula goes.</param>
		public Output(Span<byte> destination)
		{
			this.destination = destination;
			length = 0;
			overflowed = false;
		}

		/// <summary>The bytes written.</summary>
		public readonly int Length => length;

		/// <summary>Whether everything written so far fit.</summary>
		public readonly bool Fits => !overflowed;

		/// <summary>Writes bytes, if they fit.</summary>
		/// <param name="bytes">The bytes: copied, so they can be on the caller's stack.</param>
		public void Write(scoped ReadOnlySpan<byte> bytes)
		{
			if (overflowed || bytes.Length > destination.Length - length)
			{
				overflowed = true;

				return;
			}

			bytes.CopyTo(destination.Slice(length));
			length += bytes.Length;
		}

		/// <summary>Writes a byte, if it fits.</summary>
		/// <param name="b">The byte.</param>
		public void Write(byte b)
		{
			if (overflowed || length == destination.Length)
			{
				overflowed = true;

				return;
			}

			destination[length++] = b;
		}
	}
}
