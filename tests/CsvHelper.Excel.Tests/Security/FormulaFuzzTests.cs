using System.Text;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded property fuzzing of <see cref="FormulaShifter"/>: random formulas of the grammar's pieces (references of every
/// form, ranges, sheet names, structured references, arrays, strings, errors, numbers, functions and operators, and
/// stray bytes), moved by random offsets, at the grid's edges too. A moved formula always fits in
/// <see cref="FormulaShifter.MaxGrowth"/> times the formula's length, fits exactly in its own length and not in one byte
/// less, is the formula itself when it doesn't move, and moved twice is the formula moved once by both offsets, which
/// holds only if the shifter reads a moved formula's pieces as it read the formula's. <c>CSVHELPER_EXCEL_FUZZ_FORMULAS</c>
/// sets the number of formulas.
/// </summary>
/// <param name="output">Shows what the run covered.</param>
public class FormulaFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of formulas a run moves, unless <c>CSVHELPER_EXCEL_FUZZ_FORMULAS</c> says otherwise.</summary>
	private const int DefaultFormulas = 20_000;

	/// <summary>The pieces formulas are made of.</summary>
	private static readonly string[] Pieces =
	[
		"A1", "$A1", "A$1", "$A$1", "a1", "xfd1048576", "XFD1", "A1048576", "AA10", "ZZ99", "XFE1", "A0", "A01", "A1:B2", "$A$1:B$2",
		"A:A", "$A:B", "a:xfd", "1:1", "$1:$2", "10:20", "1048576:1", "Sheet1!A1", "'Q1 Orders'!B2", "'it''s'!C3", "Sheet1:Sheet3!C3",
		"[1]Sheet1!A1", "Table1[Col]", "Table1[[#This Row],[Col]]", "T[['x]]]", "{1,2;3,4}", "{\"a}\",1}", "\"A1\"", "\"say \"\"A1\"\"\"",
		"#REF!", "#N/A", "#DIV/0!", "#NAME?", "#GETTING_DATA", "#", "A1#", "1.5E+3", "1E5", "1e-3", ".5", "12", "1E ", "SUM(", "LOG10(",
		"IF(", "_xlfn.XLOOKUP(", ")", "(", ",", ";", "+", "-", "*", "/", "^", "&", "=", "<>", "<=", ":", "!", " ", "\n", "TRUE", "MyName",
		"R1C1", "A1B", "$", "'", "\"", "[", "]", "{", "}", "é", "日本", "\\x", "_a1", "?", ".",
	];

	[Fact]
	public void MovesRandomFormulasConsistently()
	{
		var count = int.TryParse(Environment.GetEnvironmentVariable("CSVHELPER_EXCEL_FUZZ_FORMULAS"), out var value) ? value : DefaultFormulas;
		var random = new Random(1);
		var moved = 0;
		for (var n = 0; n < count; n++)
		{
			var formula = Formula(random);
			var (rows, columns) = (Offset(random, CellReference.MaxRow), Offset(random, CellReference.MaxColumns));
			var (moreRows, moreColumns) = (Offset(random, CellReference.MaxRow), Offset(random, CellReference.MaxColumns));
			var describe = $"Formula {n} \"{Show(formula)}\" by ({rows}, {columns}) then ({moreRows}, {moreColumns})";

			// It always fits in MaxGrowth times its length, exactly in its own, and not in one byte less.
			var once = Shift(formula, rows, columns, describe);
			if (once.Length > 0)
			{
				Assert.False(FormulaShifter.TryShift(formula, rows, columns, new byte[once.Length - 1], out var written), $"{describe}: it fit in less than its length.");
				Assert.Equal(0, written);
			}

			Assert.True(FormulaShifter.TryShift(formula, rows, columns, new byte[once.Length], out var exact), $"{describe}: it didn't fit in its own length.");
			Assert.Equal(once.Length, exact);

			// Unmoved, it is itself.
			Assert.True(Shift(formula, 0, 0, describe).AsSpan().SequenceEqual(formula), $"{describe}: unmoved, it changed.");

			// Moved twice, it is moved once by both offsets: but where the first move wrote a row or a column anew (in
			// capitals, without leading zeros) and both moves together don't move it, which copies it as it is.
			var bothRows = (long)rows + moreRows;
			var bothColumns = (long)columns + moreColumns;
			var rowsKept = Modulo(bothRows, CellReference.MaxRow) == 0 && Modulo(rows, CellReference.MaxRow) != 0;
			var allKept = rowsKept || (Modulo(bothRows, CellReference.MaxRow) == 0 && Modulo(bothColumns, CellReference.MaxColumns) == 0 && (Modulo(rows, CellReference.MaxRow) != 0 || Modulo(columns, CellReference.MaxColumns) != 0));
			if (!allKept)
			{
				var twice = Shift(Shift(formula, rows, columns, describe), moreRows, moreColumns, describe);
				var both = Shift(formula, (int)Modulo(bothRows, CellReference.MaxRow), (int)Modulo(bothColumns, CellReference.MaxColumns), describe);
				Assert.True(twice.AsSpan().SequenceEqual(both), $"{describe}: moved twice, \"{Show(twice)}\"; moved once by both, \"{Show(both)}\".");
				moved++;
			}
		}

		output.WriteLine($"{count} formulas, {moved} of them moved twice and compared.");
	}

	/// <summary>Moves a formula in room for its most growth, which it must fit in.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="rows">The rows to move by.</param>
	/// <param name="columns">The columns to move by.</param>
	/// <param name="describe">The case, for a failure.</param>
	/// <returns>The moved formula.</returns>
	private static byte[] Shift(byte[] formula, int rows, int columns, string describe)
	{
		var destination = new byte[FormulaShifter.MaxGrowth * formula.Length];
		Assert.True(FormulaShifter.TryShift(formula, rows, columns, destination, out var written), $"{describe}: it didn't fit in {FormulaShifter.MaxGrowth} times its length.");

		return destination.AsSpan(0, written).ToArray();
	}

	/// <summary>
	/// A random formula: up to 30 pieces, and sometimes a random byte among them. Where a piece ending in a digit or a
	/// point meets one starting with a letter, a space (the intersection operator) goes between them, as Excel's grammar
	/// has an operator after a number: a reference right after a number (<c>20A1</c>, <c>$2B1</c>, which Excel can't
	/// write), moved to column E, would read as the number's exponent (<c>20E1</c>), and so move no further.
	/// </summary>
	/// <param name="random">The random numbers.</param>
	/// <returns>The formula's UTF-8.</returns>
	private static byte[] Formula(Random random)
	{
		var bytes = new List<byte>();
		for (var n = random.Next(31); n > 0; n--)
		{
			var piece = random.Next(20) == 0 ? [(byte)random.Next(256)] : Encoding.UTF8.GetBytes(Pieces[random.Next(Pieces.Length)]);
			if (bytes.Count > 0 && (IsDigit(bytes[bytes.Count - 1]) || bytes[bytes.Count - 1] == '.') && IsLetter(piece[0]))
			{
				bytes.Add((byte)' ');
			}

			bytes.AddRange(piece);
		}

		return [.. bytes];
	}

	/// <summary>Whether a byte is an ASCII letter.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsLetter(byte b) => (uint)((b | 0x20) - 'a') <= 'z' - 'a';

	/// <summary>Whether a byte is an ASCII digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsDigit(byte b) => (uint)(b - '0') <= 9;

	/// <summary>A random offset: none, small, at the grid's size or past it, either way.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="size">The grid's rows or columns.</param>
	/// <returns>The offset.</returns>
	private static int Offset(Random random, int size) => random.Next(6) switch
	{
		0 => 0,
		1 => random.Next(-3, 4),
		2 => random.Next(-100, 101),
		3 => (size - 1) * (random.Next(2) == 0 ? 1 : -1),
		4 => random.Next(-3 * size, 3 * size),
		_ => size * random.Next(-2, 3),
	};

	/// <summary>A value modulo a size, from 0 to the size less one, for negative values too.</summary>
	/// <param name="value">The value.</param>
	/// <param name="size">The size.</param>
	/// <returns>The value modulo the size.</returns>
	private static long Modulo(long value, int size)
	{
		var m = value % size;

		return m < 0 ? m + size : m;
	}

	/// <summary>A formula's text for a failure, with control characters as code points.</summary>
	/// <param name="formula">The formula's UTF-8.</param>
	/// <returns>The text to show.</returns>
	private static string Show(byte[] formula) => string.Concat(Encoding.UTF8.GetString(formula).Select(c => c < ' ' ? $"\\u{(int)c:X4}" : c.ToString()));
}
