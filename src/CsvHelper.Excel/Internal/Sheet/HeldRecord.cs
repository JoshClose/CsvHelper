using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// A record the sheet scanner writes before the rows it reads, once its layout is final: a table's column names as its
/// header record, which the sheet may not show at all, or the header row that <see cref="HeaderSearch"/> found above the
/// rows read. Each field has its column, so that the fields of columns the layout doesn't read are left out, as their
/// cells are, and under <see cref="ExcelText.Formatted"/> the found row's fields keep their styles, so that the header's
/// names are the text the cells show.
/// </summary>
internal sealed class HeldRecord
{
	private readonly byte[][] values;
	private readonly SheetCellKind[] kinds;
	// Each field's style under ExcelText.Formatted, -1 for none; null when no field has one.
	private readonly int[]? styles;
	private readonly int firstColumn;

	/// <summary>Creates the record.</summary>
	/// <param name="values">Each field's value, as UTF-8.</param>
	/// <param name="kinds">Each field's kind.</param>
	/// <param name="styles">Each field's style, -1 for none; or <see langword="null"/> when no field has one.</param>
	/// <param name="firstColumn">The column of the first field, from 0; the others follow it.</param>
	/// <param name="line">The record's row in the sheet, or 0 when the sheet has no row for it.</param>
	private HeldRecord(byte[][] values, SheetCellKind[] kinds, int[]? styles, int firstColumn, long line)
	{
		this.values = values;
		this.kinds = kinds;
		this.styles = styles;
		this.firstColumn = firstColumn;
		Line = line;
	}

	/// <summary>The number of fields, those of columns that aren't read included.</summary>
	public int FieldCount => values.Length;

	/// <summary>The record's row in the sheet, or 0 when the sheet has no row for it.</summary>
	public long Line { get; }

	/// <summary>
	/// A record of names, one per column of an area: the name of each column that has one, and an empty field for each
	/// column past the names.
	/// </summary>
	/// <param name="names">The names, in order; names past <paramref name="width"/> are left out.</param>
	/// <param name="firstColumn">The area's first column, from 0.</param>
	/// <param name="width">The number of columns.</param>
	/// <param name="line">The record's row in the sheet, or 0 when the sheet has no row for it.</param>
	/// <returns>The record.</returns>
	public static HeldRecord Names(IReadOnlyList<string> names, int firstColumn, int width, long line)
	{
		var values = new byte[width][];
		var kinds = new SheetCellKind[width];
		for (var i = 0; i < width; i++)
		{
			var name = i < names.Count ? names[i] : "";
			values[i] = Encoding.UTF8.GetBytes(name);
			kinds[i] = name.Length == 0 ? SheetCellKind.Empty : SheetCellKind.Text;
		}

		return new HeldRecord(values, kinds, null, firstColumn, line);
	}

	/// <summary>A record of a row's cells, one field per column from a first one: such as the header row a search found.</summary>
	/// <param name="values">Each field's value, as UTF-8.</param>
	/// <param name="kinds">Each field's kind.</param>
	/// <param name="styles">
	/// Each field's style under <see cref="ExcelText.Formatted"/>, -1 for none (<see cref="CellFormatter.StyleOf"/>); or
	/// <see langword="null"/> when no field has one.
	/// </param>
	/// <param name="firstColumn">The column of the first field, from 0; the others follow it.</param>
	/// <param name="line">The record's row in the sheet.</param>
	/// <returns>The record.</returns>
	public static HeldRecord Row(byte[][] values, SheetCellKind[] kinds, int[]? styles, int firstColumn, long line) => new(values, kinds, styles, firstColumn, line);

	/// <summary>Writes the record into a target, and ends it: the fields of the columns the layout reads, in order.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the record.</param>
	/// <param name="layout">The scanner's layout, or <see langword="null"/> when it reads every column.</param>
	/// <returns>The number of fields written.</returns>
	public int WriteTo<TTarget>(TTarget target, SheetLayout? layout)
		where TTarget : struct, IRecordTarget
	{
		var written = 0;
		for (var i = 0; i < values.Length; i++)
		{
			if (layout is null || layout.FieldOf(firstColumn + i) >= 0)
			{
				var style = styles is null ? -1 : styles[i];
				if (style < 0)
				{
					target.AddField(values[i].AsSpan(), kinds[i]);
				}
				else
				{
					target.AddField(values[i].AsSpan(), kinds[i], style);
				}

				written++;
			}
		}

		target.EndRecord(Line);

		return written;
	}
}
