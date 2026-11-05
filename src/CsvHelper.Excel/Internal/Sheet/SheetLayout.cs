using System;

namespace CsvHelper.Excel;

/// <summary>
/// Which cells of a sheet a reader reads, and the field each column becomes: the rows and columns of an area (a range, a
/// table's data, a defined name's block, or the whole sheet), less its hidden columns when those are skipped. Field 0 is
/// the area's first column that is read. A reader of a whole sheet that skips nothing has no layout, and its field
/// <c>i</c> is column <c>i</c>.
/// </summary>
/// <remarks>
/// It is immutable, so <see cref="SheetRecordSource.Locate"/> reads it from <c>ParallelEnumerate</c>'s workers without
/// locks. An area can have no rows (a table without data rows): its last row is then above its first.
/// </remarks>
internal sealed class SheetLayout
{
	// With hidden columns: the field of each of the area's columns from its first up to its last shown one (-1 for a
	// hidden one), and the column of each field. Null when every column of the area is read.
	private readonly int[]? fields;
	private readonly int[]? columns;
	// With hidden columns: the number of shown columns before each of the area's columns in the map, and after the last.
	private readonly int[]? shownBefore;

	/// <summary>Creates the layout of an area.</summary>
	/// <param name="firstRow">The area's first row, from 1.</param>
	/// <param name="lastRow">The area's last row, from 1; above <paramref name="firstRow"/> for an area without rows.</param>
	/// <param name="firstColumn">The area's first column, from 0.</param>
	/// <param name="lastColumn">The area's last column, from 0, at or after <paramref name="firstColumn"/>.</param>
	public SheetLayout(long firstRow, long lastRow, int firstColumn, int lastColumn)
	{
		FirstRow = firstRow;
		LastRow = lastRow;
		FirstColumn = firstColumn;
		LastColumn = lastColumn;
	}

	/// <summary>Creates the layout of an area without some of its columns.</summary>
	/// <param name="area">The area.</param>
	/// <param name="fields">The field of each column from the area's first, -1 for one that is not read; past them, none is.</param>
	/// <param name="columns">The column of each field.</param>
	private SheetLayout(SheetLayout area, int[] fields, int[] columns)
		: this(area.FirstRow, area.LastRow, area.FirstColumn, area.LastColumn)
	{
		this.fields = fields;
		this.columns = columns;
		shownBefore = new int[fields.Length + 1];
		for (var i = 0; i < fields.Length; i++)
		{
			shownBefore[i + 1] = shownBefore[i] + (fields[i] >= 0 ? 1 : 0);
		}
	}

	/// <summary>The whole sheet: every row and column.</summary>
	public static SheetLayout Sheet { get; } = new(1, CellReference.MaxRow, 0, CellReference.MaxColumns - 1);

	/// <summary>The area's first row, from 1.</summary>
	public long FirstRow { get; }

	/// <summary>The area's last row, from 1; above <see cref="FirstRow"/> for an area without rows.</summary>
	public long LastRow { get; }

	/// <summary>The area's first column, from 0.</summary>
	public int FirstColumn { get; }

	/// <summary>The area's last column, from 0.</summary>
	public int LastColumn { get; }

	/// <summary>The number of fields a row of the area can have: its columns that are read.</summary>
	public int Width => columns?.Length ?? LastColumn - FirstColumn + 1;

	/// <summary>The layout of a block of cells.</summary>
	/// <param name="range">The block.</param>
	/// <returns>The layout.</returns>
	public static SheetLayout Of(ExcelRange range) => new(range.FirstRow, range.LastRow, range.FirstColumnIndex, range.LastColumnIndex);

	/// <summary>The layout without the area's hidden columns: its fields are the shown columns, in order.</summary>
	/// <param name="hidden">The sheet's hidden columns.</param>
	/// <returns>The layout; this one when none of the area's columns is hidden.</returns>
	public SheetLayout Without(HiddenColumns hidden)
	{
		var lastShown = -1;
		var anyHidden = false;
		for (var column = FirstColumn; column <= LastColumn; column++)
		{
			if (hidden.IsHidden(column))
			{
				anyHidden = true;
			}
			else
			{
				lastShown = column;
			}
		}

		if (!anyHidden)
		{
			return this;
		}

		// Up to the last shown column only: "hide every column from H" leaves a short map.
		var fields = new int[lastShown < 0 ? 0 : lastShown - FirstColumn + 1];
		var count = 0;
		for (var i = 0; i < fields.Length; i++)
		{
			fields[i] = hidden.IsHidden(FirstColumn + i) ? -1 : count++;
		}

		var columns = new int[count];
		for (var i = 0; i < fields.Length; i++)
		{
			if (fields[i] >= 0)
			{
				columns[fields[i]] = FirstColumn + i;
			}
		}

		return new SheetLayout(this, fields, columns);
	}

	/// <summary>Where a row is: above the area, in it, or below it.</summary>
	/// <param name="row">The row, from 1.</param>
	/// <returns>The row's place.</returns>
	public RowPlace Place(long row) => row < FirstRow ? RowPlace.Above : row > LastRow ? RowPlace.Below : RowPlace.In;

	/// <summary>The field a column becomes.</summary>
	/// <param name="column">The column, from 0.</param>
	/// <returns>The field, from 0; -1 for a column outside the area, or one that is not read.</returns>
	public int FieldOf(int column)
	{
		var offset = column - FirstColumn;
		if (fields is null)
		{
			return (uint)offset <= (uint)(LastColumn - FirstColumn) ? offset : -1;
		}

		return (uint)offset < (uint)fields.Length ? fields[offset] : -1;
	}

	/// <summary>
	/// The fields that a block of columns becomes: since the fields are the area's columns that are read, in order, those
	/// of the block's columns are consecutive.
	/// </summary>
	/// <param name="firstColumn">The block's first column, from 0.</param>
	/// <param name="lastColumn">The block's last column, from 0, at or after <paramref name="firstColumn"/>.</param>
	/// <param name="firstField">The first field, when there is one.</param>
	/// <param name="lastField">The last field, when there is one.</param>
	/// <returns>Whether any of the block's columns is a field: in the area, and read.</returns>
	public bool TryGetFields(int firstColumn, int lastColumn, out int firstField, out int lastField)
	{
		var first = Math.Max(firstColumn, FirstColumn) - FirstColumn;
		var last = Math.Min(lastColumn, fields is null ? LastColumn : FirstColumn + fields.Length - 1) - FirstColumn;
		if (first > last)
		{
			firstField = -1;
			lastField = -1;

			return false;
		}

		if (shownBefore is null)
		{
			firstField = first;
			lastField = last;

			return true;
		}

		firstField = shownBefore[first];
		lastField = shownBefore[last + 1] - 1;

		return lastField >= firstField;
	}

	/// <summary>The column a field comes from, for locations.</summary>
	/// <param name="field">The field, from 0.</param>
	/// <returns>The column, from 0; -1 for a field the area has no column for.</returns>
	public int ColumnOf(int field)
	{
		if (columns is null)
		{
			return (uint)field < (uint)Width ? FirstColumn + field : -1;
		}

		return (uint)field < (uint)columns.Length ? columns[field] : -1;
	}
}

/// <summary>Where a row is, against an area's rows.</summary>
internal enum RowPlace : byte
{
	/// <summary>Above the area: skipped.</summary>
	Above,

	/// <summary>In the area: read.</summary>
	In,

	/// <summary>Below the area: the area has ended.</summary>
	Below,
}
