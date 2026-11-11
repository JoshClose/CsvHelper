using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// A block of cells of a sheet, such as <c>B5:F200</c>: its first and last rows and columns, numbered from 1 as Excel's
/// <c>Range.Row</c> and <c>Range.Column</c> number them (column <c>A</c> is 1). Whole columns (<c>B:F</c>) and whole rows
/// (<c>5:200</c>) are blocks that reach the sheet's edges. The default value is the one cell <c>A1</c>.
/// </summary>
public readonly struct ExcelRange : IEquatable<ExcelRange>
{
	// Each counted from 0, so that the default value is A1.
	private readonly int firstRow;
	private readonly int firstColumn;
	private readonly int lastRow;
	private readonly int lastColumn;

	/// <summary>Creates a block of cells.</summary>
	/// <param name="firstRow">The first row: 1 to 1,048,576.</param>
	/// <param name="firstColumn">The first column: 1 (<c>A</c>) to 16,384 (<c>XFD</c>).</param>
	/// <param name="lastRow">The last row: from <paramref name="firstRow"/> to 1,048,576.</param>
	/// <param name="lastColumn">The last column: from <paramref name="firstColumn"/> to 16,384.</param>
	/// <exception cref="ArgumentOutOfRangeException">A row or a column is not one of a sheet's, or a last one is before its first.</exception>
	public ExcelRange(int firstRow, int firstColumn, int lastRow, int lastColumn)
	{
		CheckRow(firstRow, 1, nameof(firstRow));
		CheckColumn(firstColumn, 1, nameof(firstColumn));
		CheckRow(lastRow, firstRow, nameof(lastRow));
		CheckColumn(lastColumn, firstColumn, nameof(lastColumn));
		this.firstRow = firstRow - 1;
		this.firstColumn = firstColumn - 1;
		this.lastRow = lastRow - 1;
		this.lastColumn = lastColumn - 1;
	}

	/// <summary>The first row: 1 to 1,048,576.</summary>
	public int FirstRow => firstRow + 1;

	/// <summary>The first column: 1 (<c>A</c>) to 16,384 (<c>XFD</c>).</summary>
	public int FirstColumn => firstColumn + 1;

	/// <summary>The last row, at or below <see cref="FirstRow"/>.</summary>
	public int LastRow => lastRow + 1;

	/// <summary>The last column, at or right of <see cref="FirstColumn"/>.</summary>
	public int LastColumn => lastColumn + 1;

	/// <summary>The number of rows.</summary>
	public int RowCount => lastRow - firstRow + 1;

	/// <summary>The number of columns.</summary>
	public int ColumnCount => lastColumn - firstColumn + 1;

	/// <summary>The first column counted from 0, as the sheet scanner counts columns.</summary>
	internal int FirstColumnIndex => firstColumn;

	/// <summary>The last column counted from 0.</summary>
	internal int LastColumnIndex => lastColumn;

	/// <summary>
	/// Parses a block of cells in Excel's A1 form: a cell (<c>B5</c>), two corners (<c>B5:F200</c>, in either order), whole
	/// columns (<c>B:F</c>) or whole rows (<c>5:200</c>), with or without <c>$</c>, letters of either case, and white space
	/// around it. A sheet's name in front is not part of a block; see <see cref="ExcelReaderOptions.Range"/>, which takes one.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The block.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException"><paramref name="text"/> is not a block of cells of a sheet.</exception>
	public static ExcelRange Parse(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		if (!AreaReference.TryParseArea(text.AsSpan(), out var range))
		{
			throw new FormatException($"\"{text}\" is not a range of cells, such as B5:F200, B:F or 5:200.");
		}

		return range;
	}

	/// <summary>Parses a block of cells in Excel's A1 form. See <see cref="Parse"/>.</summary>
	/// <param name="text">The text, or <see langword="null"/>.</param>
	/// <param name="range">The block; the default value when the text is not one.</param>
	/// <returns>Whether <paramref name="text"/> is a block of cells of a sheet.</returns>
	public static bool TryParse(string? text, out ExcelRange range)
	{
		if (text is null)
		{
			range = default;

			return false;
		}

		return AreaReference.TryParseArea(text.AsSpan(), out range);
	}

	/// <summary>Whether two blocks are the same cells.</summary>
	/// <param name="left">A block.</param>
	/// <param name="right">Another block.</param>
	/// <returns>Whether they are equal.</returns>
	public static bool operator ==(ExcelRange left, ExcelRange right) => left.Equals(right);

	/// <summary>Whether two blocks are different cells.</summary>
	/// <param name="left">A block.</param>
	/// <param name="right">Another block.</param>
	/// <returns>Whether they differ.</returns>
	public static bool operator !=(ExcelRange left, ExcelRange right) => !left.Equals(right);

	/// <summary>Whether this block is the same cells as another.</summary>
	/// <param name="other">The other block.</param>
	/// <returns>Whether their rows and columns are the same.</returns>
	public bool Equals(ExcelRange other) =>
		firstRow == other.firstRow && firstColumn == other.firstColumn && lastRow == other.lastRow && lastColumn == other.lastColumn;

	/// <summary>Whether this block is the same cells as an object.</summary>
	/// <param name="obj">The object.</param>
	/// <returns>Whether the object is an <see cref="ExcelRange"/> of the same cells.</returns>
	public override bool Equals(object? obj) => obj is ExcelRange other && Equals(other);

	/// <summary>A hash code of the block's rows and columns.</summary>
	/// <returns>The hash code.</returns>
	public override int GetHashCode() => unchecked((((firstRow * 397) ^ firstColumn) * 397 ^ lastRow) * 397 ^ lastColumn);

	/// <summary>
	/// The block in Excel's A1 form, as its addresses write it without <c>$</c>: <c>B5:F200</c>, <c>B5</c> for one cell,
	/// <c>B:F</c> for whole columns, and <c>5:200</c> for whole rows (the whole sheet is <c>1:1048576</c>).
	/// </summary>
	/// <returns>The text, which <see cref="Parse"/> reads back.</returns>
	public override string ToString()
	{
		if (firstColumn == 0 && lastColumn == CellReference.MaxColumns - 1)
		{
			return Number(FirstRow) + ":" + Number(LastRow);
		}

		if (firstRow == 0 && lastRow == CellReference.MaxRow - 1)
		{
			return CellReference.ColumnName(firstColumn) + ":" + CellReference.ColumnName(lastColumn);
		}

		var first = CellReference.ColumnName(firstColumn) + Number(FirstRow);
		if (firstRow == lastRow && firstColumn == lastColumn)
		{
			return first;
		}

		return first + ":" + CellReference.ColumnName(lastColumn) + Number(LastRow);
	}

	/// <summary>
	/// The block as its two corners, as a written sheet's merged cells, filter and table name it: <c>B5:F200</c>, and
	/// <c>B1:F1048576</c> for whole columns, which <see cref="ToString"/> writes <c>B:F</c>.
	/// </summary>
	/// <returns>The text.</returns>
	internal string Corners() =>
		CellReference.ColumnName(firstColumn) + Number(FirstRow) + ":" + CellReference.ColumnName(lastColumn) + Number(LastRow);

	/// <summary>Whether a cell is in the block.</summary>
	/// <param name="row">The cell's row, from 1.</param>
	/// <param name="column">The cell's column, from 0.</param>
	/// <returns>Whether it is in the block.</returns>
	internal bool Contains(long row, int column) => row > firstRow && row <= lastRow + 1 && column >= firstColumn && column <= lastColumn;

	/// <summary>A row number as text.</summary>
	/// <param name="number">The number.</param>
	/// <returns>Its digits.</returns>
	private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

	/// <summary>Checks a row of a constructor's.</summary>
	/// <param name="row">The row.</param>
	/// <param name="least">The least it may be.</param>
	/// <param name="paramName">The parameter it came from.</param>
	/// <exception cref="ArgumentOutOfRangeException">The row is out of range.</exception>
	private static void CheckRow(int row, int least, string paramName)
	{
		if (row < least || row > CellReference.MaxRow)
		{
			throw new ArgumentOutOfRangeException(paramName, row, $"A row of this range is from {least} to {CellReference.MaxRow}.");
		}
	}

	/// <summary>Checks a column of a constructor's.</summary>
	/// <param name="column">The column.</param>
	/// <param name="least">The least it may be.</param>
	/// <param name="paramName">The parameter it came from.</param>
	/// <exception cref="ArgumentOutOfRangeException">The column is out of range.</exception>
	private static void CheckColumn(int column, int least, string paramName)
	{
		if (column < least || column > CellReference.MaxColumns)
		{
			throw new ArgumentOutOfRangeException(paramName, column, $"A column of this range is from {least} to {CellReference.MaxColumns}.");
		}
	}
}
