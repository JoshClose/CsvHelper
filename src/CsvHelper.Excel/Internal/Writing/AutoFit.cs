using System;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The widths of a sheet's held cells, and its columns' widths from them once the held rows are written
/// (<see cref="ExcelSheetOptions.AutoFit"/>): each cell's text in pixels at 96 DPI, without the padding
/// (<see cref="ColumnWidths.TextPixels"/>), as text or as a typed cell's.
/// </summary>
/// <remarks>
/// <para>
/// With auto-fit, a column is as wide as its widest cell, as Excel's AutoFit makes it, narrower than the default column
/// when its cells are (Phase 5's decision 4). Text is at most <see cref="ExcelSheetOptions.MaxAutoFitWidth"/> wide, and
/// typed cells (numbers, booleans, dates, times and durations) as wide as they need, up to 255 characters, since a typed
/// cell too narrow shows <c>########</c> where text is cut or spills into the next cell.
/// </para>
/// <para>
/// Without auto-fit, only the first data record's typed cells in a format are measured (Phase 4's dates, times and
/// durations, and the cells of a column's <see cref="ExcelColumnOptions.Format"/>), and a column gets a width only when the
/// default column is too narrow for them.
/// </para>
/// <para>
/// A record's cells are measured as it is written; a record that is discarded drops them. A cell in a range merged across
/// two columns or more (<see cref="ExcelWriterExtensions.MergeCells"/>) counts nothing, as in Excel's AutoFit, so each
/// cell keeps its row, and the ranges merged by the time the widths are known leave theirs out.
/// </para>
/// </remarks>
internal sealed class AutoFit
{
	// The cells measured, in the order written; and where the current record's start.
	private Cell[] cells = new Cell[64];
	private int count;
	private int recordStart;

	/// <summary>
	/// The pixels of a text cell's text: on one line, with its line feeds; or, in a column whose text wraps, its widest
	/// line, since Excel breaks lines there.
	/// </summary>
	/// <param name="utf8">The text, valid UTF-8.</param>
	/// <param name="bold">Whether it is in bold.</param>
	/// <param name="wrap">Whether it wraps.</param>
	/// <returns>The pixels.</returns>
	public static double TextPixels(ReadOnlySpan<byte> utf8, bool bold, bool wrap)
	{
		if (!wrap && IsAscii(utf8))
		{
			// Most text: measured as it is, without a string.
			var sum = 0.0;
			foreach (var b in utf8)
			{
				sum += ColumnWidths.CharPixels((char)b, bold);
			}

			return sum;
		}

		var text = Encoding.UTF8.GetString(utf8.ToArray()).AsSpan();
		if (!wrap)
		{
			return ColumnWidths.TextPixels(text, bold);
		}

		var widest = 0.0;
		while (true)
		{
			var end = text.IndexOf('\n');
			widest = Math.Max(widest, ColumnWidths.TextPixels(end < 0 ? text : text.Slice(0, end), bold));
			if (end < 0)
			{
				return widest;
			}

			text = text.Slice(end + 1);
		}
	}

	/// <summary>Whether a text is ASCII alone.</summary>
	/// <param name="utf8">The text.</param>
	/// <returns>Whether every byte is below 0x80.</returns>
	private static bool IsAscii(ReadOnlySpan<byte> utf8)
	{
		foreach (var b in utf8)
		{
			if (b >= 0x80)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Measures a cell of the current record.</summary>
	/// <param name="column">The cell's column.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="pixels">Its text's pixels, without the padding.</param>
	/// <param name="typed">Whether it is a typed cell, which <see cref="ExcelSheetOptions.MaxAutoFitWidth"/> doesn't cap.</param>
	public void Add(int column, long row, double pixels, bool typed) => Add(new Cell(column, row, pixels, typed ? CellWidth.Typed : CellWidth.Text));

	/// <summary>
	/// Notes a formula cell of the current record: its value isn't known, so its column is at least the default column,
	/// as without auto-fit.
	/// </summary>
	/// <param name="column">The cell's column.</param>
	/// <param name="row">The cell's row.</param>
	public void AddFormula(int column, long row) => Add(new Cell(column, row, 0, CellWidth.Default));

	/// <summary>
	/// Adds a filter button to each of the current record's cells, the header's, in its columns: its empty fields too,
	/// which have the button alone.
	/// </summary>
	/// <param name="columns">The header's fields.</param>
	/// <param name="row">The header's row.</param>
	public void AddFilterButtons(int columns, long row)
	{
		var measured = new bool[columns];
		for (var i = recordStart; i < count; i++)
		{
			var cell = cells[i];
			cells[i] = cell with { Pixels = cell.Pixels + ColumnWidths.FilterButtonPixels };
			measured[cell.Column] = true;
		}

		for (var column = 0; column < columns; column++)
		{
			if (!measured[column])
			{
				Add(column, row, ColumnWidths.FilterButtonPixels, typed: false);
			}
		}
	}

	/// <summary>Adds a cell measured.</summary>
	/// <param name="cell">The cell.</param>
	private void Add(Cell cell)
	{
		if (count == cells.Length)
		{
			Array.Resize(ref cells, cells.Length * 2);
		}

		cells[count++] = cell;
	}

	/// <summary>Keeps the current record's cells: the next ones are another record's.</summary>
	public void EndRecord() => recordStart = count;

	/// <summary>Drops the current record's cells, when it is discarded.</summary>
	public void DiscardRecord() => count = recordStart;

	/// <summary>The columns' widths, from the cells measured.</summary>
	/// <param name="fit">Whether this is auto-fit, rather than the first data record's typed cells.</param>
	/// <param name="maxTextWidth">The widest auto-fit makes a column for its text (<see cref="ExcelSheetOptions.MaxAutoFitWidth"/>'s width).</param>
	/// <param name="merged">
	/// The sheet's merged ranges, whose cells count nothing when a range is across two columns or more, as in Excel's
	/// AutoFit (<see cref="MergedRanges.HidesWidth"/>); or <see langword="null"/> for none.
	/// </param>
	/// <returns>Each column's width, by its index, or 0 for none; <see langword="null"/> when no cell counts.</returns>
	public double[]? Widths(bool fit, double maxTextWidth, MergedRanges? merged = null)
	{
		if (merged is not null)
		{
			var kept = 0;
			for (var i = 0; i < count; i++)
			{
				if (!merged.HidesWidth(cells[i].Row, cells[i].Column))
				{
					cells[kept++] = cells[i];
				}
			}

			count = kept;
			recordStart = Math.Min(recordStart, count);
		}

		if (count == 0)
		{
			return null;
		}

		var columns = 0;
		for (var i = 0; i < count; i++)
		{
			columns = Math.Max(columns, cells[i].Column + 1);
		}

		// The widest text and typed cell of each column, -1 for none; and whether it has a formula.
		var text = new double[columns];
		var typed = new double[columns];
		var formulas = new bool[columns];
		text.AsSpan().Fill(-1);
		typed.AsSpan().Fill(-1);
		for (var i = 0; i < count; i++)
		{
			var cell = cells[i];
			if (cell.Kind == CellWidth.Default)
			{
				formulas[cell.Column] = true;

				continue;
			}

			var widest = cell.Kind == CellWidth.Typed ? typed : text;
			widest[cell.Column] = Math.Max(widest[cell.Column], cell.Pixels);
		}

		var widths = new double[columns];
		for (var column = 0; column < columns; column++)
		{
			if (!fit)
			{
				widths[column] = typed[column] < 0 ? 0 : ColumnWidths.OfPixels(typed[column]);

				continue;
			}

			var width = text[column] < 0 ? 0 : Math.Min(ColumnWidths.Fit(text[column]), maxTextWidth);
			if (typed[column] >= 0)
			{
				width = Math.Max(width, ColumnWidths.Fit(typed[column]));
			}

			if (formulas[column])
			{
				width = Math.Max(width, ColumnWidths.Default);
			}

			widths[column] = Math.Min(width, ColumnWidths.Widest);
		}

		return widths;
	}

	/// <summary>A cell measured.</summary>
	/// <param name="Column">Its column.</param>
	/// <param name="Row">Its row.</param>
	/// <param name="Pixels">Its text's pixels, without the padding.</param>
	/// <param name="Kind">What its width is.</param>
	private readonly record struct Cell(int Column, long Row, double Pixels, CellWidth Kind);

	/// <summary>What a cell measured gives its column.</summary>
	private enum CellWidth
	{
		/// <summary>Its text's width, at most the cap.</summary>
		Text,

		/// <summary>A typed cell's width, which the cap doesn't apply to.</summary>
		Typed,

		/// <summary>The default column's width at least: a formula's, whose value isn't known.</summary>
		Default,
	}
}
