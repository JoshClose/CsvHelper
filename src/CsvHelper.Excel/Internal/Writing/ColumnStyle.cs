using System;

namespace CsvHelper.Excel;

/// <summary>
/// The style that a column's <see cref="ExcelColumnOptions"/> give its cells below the header: its
/// <see cref="ExcelColumnOptions.Format"/> in place of each cell's own number format, and its
/// <see cref="ExcelColumnOptions.Wrap"/>; and its default style (<c>col style</c>), which Excel gives the cells users add
/// later. Excel gives a column's style only to cells not in the file, so each cell written carries it too (checked on
/// 2026-11-08).
/// </summary>
internal sealed class ColumnStyle
{
	private readonly StyleRegistry styles;
	// The format's code and number format id, or null and -1 to keep each cell's own.
	private readonly string? code;
	private readonly int numberFormatId;
	// The last cell format a cell had of its own, and the one it got: a column's cells mostly have one.
	private int lastOwn = -1;
	private int lastStyle;
	// The format's widths, once a cell is measured.
	private NumberFormatWidth? formatWidth;

	/// <summary>Creates a column's style, adding its cell formats to the workbook's.</summary>
	/// <param name="options">The column's options, which have a format or wrap.</param>
	/// <param name="styles">The workbook's cell formats.</param>
	public ColumnStyle(ExcelColumnOptions options, StyleRegistry styles)
	{
		this.styles = styles;
		code = options.Format;
		numberFormatId = code is not null ? styles.NumberFormatId(code) : -1;
		Wrap = options.Wrap;
		Default = styles.IndexOf(new CellXf(Math.Max(numberFormatId, 0), CellFont.Regular, Wrap));
	}

	/// <summary>The column's default style: its format, or General, and its wrap.</summary>
	public int Default { get; }

	/// <summary>Whether the column has a format, which takes the place of its cells' own.</summary>
	public bool HasFormat => numberFormatId >= 0;

	/// <summary>Whether the column's text wraps.</summary>
	public bool Wrap { get; }

	/// <summary>The widths of the column's format, read when a cell is first measured; <see langword="null"/> without a format.</summary>
	public NumberFormatWidth? FormatWidth => code is null ? null : formatWidth ??= new NumberFormatWidth(code);

	/// <summary>Whether a column's options style its cells: they have a format or wrap.</summary>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <returns>Whether they do.</returns>
	public static bool Styles(ExcelColumnOptions? options) => options is not null && (options.Format is not null || options.Wrap);

	/// <summary>The cell format of a cell of the column.</summary>
	/// <param name="own">The cell format the cell has of its own: General, or its date's, time's or duration's.</param>
	/// <returns>Its cell format in the column: the column's number format, if it has one, and its wrap.</returns>
	public int Of(int own)
	{
		if (own != lastOwn)
		{
			var format = styles[own];
			lastStyle = styles.IndexOf(new CellXf(numberFormatId >= 0 ? numberFormatId : format.NumberFormatId, format.Font, Wrap));
			lastOwn = own;
		}

		return lastStyle;
	}
}
