using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Auto-fit's measures (<see cref="AutoFit"/>): a text cell's pixels on one line or by its widest line, in bold, and the
/// columns' widths from the cells measured, with the cap on text, typed cells uncapped, the filter buttons, and a
/// discarded record's cells dropped.
/// </summary>
public class AutoFitTests
{
	[Fact]
	public void MeasuresTextOnOneLineUnlessItWraps()
	{
		// Excel shows a line feed in a cell that doesn't wrap on the same line, 8 pixels wide, and a CR as nothing; a cell
		// that wraps breaks its lines there.
		var text = "short\na much longer second line"u8;
		var line = ColumnWidths.TextPixels("a much longer second line".AsSpan());

		Assert.Equal(ColumnWidths.TextPixels("short".AsSpan()) + 8 + line, AutoFit.TextPixels(text, bold: false, wrap: false), 6);
		Assert.Equal(line, AutoFit.TextPixels(text, bold: false, wrap: true));
		Assert.Equal(line, AutoFit.TextPixels("a much longer second line\r\nshort"u8, bold: false, wrap: true));
		Assert.Equal(AutoFit.TextPixels("ab"u8, false, false), AutoFit.TextPixels("a\rb"u8, false, false));
	}

	[Fact]
	public void MeasuresAsciiAndOtherTextAlike()
	{
		// ASCII is measured byte by byte, other text decoded; both by the same table.
		var ascii = "Kitten, Pluto"u8;
		var other = Encoding.UTF8.GetBytes("Kitten, Pluto" + (char)0x00E9);

		Assert.Equal(AutoFit.TextPixels(ascii, true, false) + ColumnWidths.TextPixels(((char)0x00E9).ToString().AsSpan(), bold: true), AutoFit.TextPixels(other, true, false), 6);
		Assert.Equal(ColumnWidths.TextPixels("Kitten, Pluto".AsSpan(), bold: true), AutoFit.TextPixels(ascii, true, false));
		Assert.True(AutoFit.TextPixels(ascii, true, false) > AutoFit.TextPixels(ascii, false, false));
	}

	[Fact]
	public void FitsEachColumnToItsWidestCell()
	{
		var fit = new AutoFit();
		fit.Add(0, 20, typed: false);
		fit.Add(2, 30, typed: true);
		fit.EndRecord();
		fit.Add(0, 50, typed: false);
		fit.Add(2, 10, typed: true);
		fit.EndRecord();

		var widths = fit.Widths(fit: true, ColumnWidths.FromCharacters(60))!;

		// Column B has no cell, and no width; text and typed cells as wide as their widest, narrower than the default too.
		Assert.Equal([ColumnWidths.Fit(50), 0, ColumnWidths.Fit(30)], widths);
		Assert.True(widths[2] < ColumnWidths.Default);
	}

	[Fact]
	public void CapsTextButNotTypedCells()
	{
		var fit = new AutoFit();
		fit.Add(0, 1000, typed: false);
		fit.Add(1, 1000, typed: true);
		fit.Add(2, 1000, typed: false);
		fit.Add(2, 30, typed: true);
		fit.Add(3, 5000, typed: true);
		fit.EndRecord();
		var cap = ColumnWidths.FromCharacters(10);

		var widths = fit.Widths(fit: true, cap)!;

		// A typed cell too narrow shows ########, text is cut or spills: at most 255 characters for either.
		Assert.Equal([cap, ColumnWidths.Fit(1000), cap, ColumnWidths.Widest], widths);
	}

	[Fact]
	public void GivesTypedCellsAWidthPastTheDefaultWithoutAutoFit()
	{
		var fit = new AutoFit();
		fit.Add(0, ColumnWidths.DateTimePixels, typed: true);
		fit.Add(1, ColumnWidths.TimePixels, typed: true);
		fit.EndRecord();

		// Phase 4's widths: none for a time, which fits the default column.
		Assert.Equal([ColumnWidths.DateTime, 0], fit.Widths(fit: false, ColumnWidths.FromCharacters(60))!);
	}

	[Fact]
	public void AddsFilterButtonsToTheHeader()
	{
		// Each of the header's fields has a button, its empty ones too; the records' cells don't.
		var fit = new AutoFit();
		fit.Add(0, 40, typed: false);
		fit.Add(2, 20, typed: false);
		fit.AddFilterButtons(4);
		fit.EndRecord();
		fit.Add(0, 50, typed: false);
		fit.EndRecord();

		var widths = fit.Widths(fit: true, ColumnWidths.FromCharacters(60))!;

		Assert.Equal(
			[ColumnWidths.Fit(57), ColumnWidths.Fit(ColumnWidths.FilterButtonPixels), ColumnWidths.Fit(37), ColumnWidths.Fit(ColumnWidths.FilterButtonPixels)],
			widths);
	}

	[Fact]
	public void GivesAColumnOfFormulasTheDefaultWidthAtLeast()
	{
		// A formula's value isn't known: its column is the default column, or its other cells' if wider.
		var fit = new AutoFit();
		fit.Add(0, 20, typed: false);
		fit.AddFormula(0);
		fit.AddFormula(1);
		fit.Add(1, 200, typed: false);
		fit.EndRecord();

		Assert.Equal([ColumnWidths.Default, ColumnWidths.Fit(200)], fit.Widths(fit: true, ColumnWidths.FromCharacters(60))!);
	}

	[Fact]
	public void DropsADiscardedRecordsCells()
	{
		var fit = new AutoFit();
		fit.Add(0, 20, typed: false);
		fit.EndRecord();
		fit.Add(0, 90, typed: false);
		fit.Add(1, 90, typed: false);
		fit.DiscardRecord();
		fit.Add(0, 30, typed: false);
		fit.EndRecord();

		Assert.Equal([ColumnWidths.Fit(30)], fit.Widths(fit: true, ColumnWidths.FromCharacters(60))!);
		Assert.Null(new AutoFit().Widths(fit: true, 100));
	}
}
