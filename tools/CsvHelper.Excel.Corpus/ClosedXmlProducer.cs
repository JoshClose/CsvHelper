using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from ClosedXML 0.105.1.</summary>
internal static class ClosedXmlProducer
{
	/// <summary>ClosedXML 0.105.1: <c>closedxml/basic.xlsx</c>, with formulas evaluated before saving so that they have results.</summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		using var workbook = new XLWorkbook();
		workbook.Properties.Author = DotNetProducers.Author;
		workbook.Properties.LastModifiedBy = DotNetProducers.Author;
		foreach (var spec in DotNetProducers.Worksheets)
		{
			var sheet = workbook.Worksheets.Add(spec.Name);
			sheet.Visibility = spec.State switch
			{
				"hidden" => XLWorksheetVisibility.Hidden,
				"veryHidden" => XLWorksheetVisibility.VeryHidden,
				_ => XLWorksheetVisibility.Visible,
			};
			foreach (var (row, cells) in Basic.Rows(spec, "control", "long", "rich", "phonetic", "formula", "date"))
			{
				foreach (var cell in cells)
				{
					var target = sheet.Cell(cell.Reference(row));
					switch (cell.Type)
					{
						case CellType.Text:
							target.Value = cell.Text;

							break;
						case CellType.Number:
							target.Value = cell.Number!.Value;

							break;
						case CellType.Bool:
							target.Value = cell.Bool!.Value;

							break;
						case CellType.Formula:
							target.FormulaA1 = cell.Formula;

							break;
						case CellType.Date:
							target.Value = cell.Date is { } date ? date : cell.Number!.Value;
							target.Style.NumberFormat.Format = cell.Format;

							break;
						case CellType.Rich:
							var rich = target.CreateRichText();
							foreach (var run in cell.Runs!)
							{
								rich.AddText(run.Text).SetBold(run.Bold);
							}

							break;
						case CellType.Phonetic:
							target.Value = cell.Text;
							target.GetRichText().Phonetics.Add(cell.Phonetic!, 0, cell.Text!.Length);

							break;
					}
				}
			}
		}

		DotNetProducers.Save(Path.Combine(folder, "basic.xlsx"), path => workbook.SaveAs(path, new SaveOptions { EvaluateFormulasBeforeSaving = true }));
		WriteTyped(folder);
		WriteMessy(folder);
	}

	/// <summary>
	/// ClosedXML 0.105.1: <c>closedxml/messy.xlsx</c>, with merged ranges, hidden rows and columns, a collapsed group, the
	/// tables (ClosedXML makes the totals row, and hides the header row), the filter, which ClosedXML applies, the columns
	/// from D hidden, and the defined names it takes, its formulas evaluated before saving. It writes no chartsheet, and
	/// has no way to hide the rows a sheet lacks (<c>zeroHeight</c>).
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	private static void WriteMessy(string folder)
	{
		using var workbook = new XLWorkbook();
		workbook.Properties.Author = DotNetProducers.Author;
		workbook.Properties.LastModifiedBy = DotNetProducers.Author;
		foreach (var spec in Messy.Sheets.Where(s => s.Kind == "worksheet"))
		{
			var sheet = workbook.Worksheets.Add(spec.Name);

			// Each table's cells, then the table, then the rest: as in Excel, a totals row inserts cells in its table's columns,
			// which would move the cells below it. ClosedXML writes the totals row.
			var cells = Basic.Rows(spec, "formula", "table").SelectMany(r => r.Cells.Select(c => (r.Row, Cell: c))).ToList();
			foreach (var table in spec.Tables)
			{
				var corners = Messy.Corners(table.Range);
				var first = table.HideHeader == true ? corners.FirstRow - 1 : corners.FirstRow;
				var data = corners.LastRow - (table.TotalsLabel is not null ? 1 : 0);
				foreach (var (row, cell) in cells.Where(c => c.Row >= corners.FirstRow && c.Row <= data && Messy.InColumns(table.Range, c.Cell.Column)))
				{
					WriteMessyCell(sheet, row, cell);
				}

				for (var i = 0; table.HideHeader == true && i < table.Columns!.Length; i++)
				{
					sheet.Cell(first, corners.FirstColumn + i).Value = table.Columns[i];
				}

				var created = sheet.Range(first, corners.FirstColumn, data, corners.LastColumn).CreateTable(table.Name);
				if (table.TotalsLabel is not null)
				{
					created.ShowTotalsRow = true;
					created.Field(0).TotalsRowLabel = table.TotalsLabel;
					created.Field(table.TotalsSum!).TotalsRowFunction = XLTotalsRowFunction.Sum;
				}

				if (table.HideHeader == true)
				{
					created.ShowHeaderRow = false;
				}
			}

			foreach (var (row, cell) in cells.Where(c => !spec.Tables.Any(t => Messy.InTable(t, c.Row, c.Cell.Column))))
			{
				WriteMessyCell(sheet, row, cell);
			}

			foreach (var merge in spec.Merges ?? [])
			{
				sheet.Range(merge).Merge();
			}

			foreach (var column in spec.HiddenColumns ?? [])
			{
				sheet.Column(column).Hide();
			}

			foreach (var row in spec.HiddenRows ?? [])
			{
				sheet.Row(row).Hide();
			}

			foreach (var group in spec.Groups ?? [])
			{
				var rows = sheet.Rows(group.First, group.Last);
				rows.Group();
				rows.Collapse();
			}

			if (spec.Filter is { } filter)
			{
				sheet.Range(filter.Range).SetAutoFilter().Column(filter.Column).NotEqualTo(filter.Hides);
			}

			if (spec.HideColumnsFrom is { } fromColumn)
			{
				sheet.Columns(Basic.ColumnIndex(fromColumn), XLHelper.MaxColumnNumber).Hide();
			}

			if (spec.HideRowsFrom is not null)
			{
				Console.WriteLine($"  {spec.Name} hides no rows past its data: ClosedXML can't write zeroHeight");
			}
		}

		foreach (var name in Messy.Names)
		{
			var scope = name.Sheet is null ? null : workbook.Worksheet(name.Sheet);
			var area = name.RefersTo[(name.RefersTo.IndexOf('!', StringComparison.Ordinal) + 1)..].Replace("$", "", StringComparison.Ordinal);
			if (name.Name == "Print_Area")
			{
				scope!.PageSetup.PrintAreas.Add(area);

				continue;
			}

			if (name.Name == "Print_Titles")
			{
				var rows = area.Split(':');
				scope!.PageSetup.SetRowsToRepeatAtTop(int.Parse(rows[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(rows[1], System.Globalization.CultureInfo.InvariantCulture));

				continue;
			}

			try
			{
				var added = scope is null ? workbook.DefinedNames.Add(name.Name, name.RefersTo) : scope.DefinedNames.Add(name.Name, name.RefersTo);
				if (name.Hidden == true)
				{
					added.Visible = false;
				}
			}
			catch (ArgumentException e)
			{
				Console.WriteLine($"  left out the name {name.Name}, which ClosedXML doesn't take: {name.RefersTo} ({e.Message})");
			}
		}

		DotNetProducers.Save(Path.Combine(folder, "messy.xlsx"), path => workbook.SaveAs(path, new SaveOptions { EvaluateFormulasBeforeSaving = true }));
	}

	/// <summary>Writes a cell of the messy workbook: its text, number or formula.</summary>
	/// <param name="sheet">The worksheet.</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="cell">The cell.</param>
	private static void WriteMessyCell(IXLWorksheet sheet, int row, CellSpec cell)
	{
		var target = sheet.Cell(cell.Reference(row));
		switch (cell.Type)
		{
			case CellType.Text:
				target.Value = cell.Text;

				break;
			case CellType.Number:
				target.Value = cell.Number!.Value;

				break;
			case CellType.Formula:
				target.FormulaA1 = cell.Formula;

				break;
		}
	}

	/// <summary>
	/// ClosedXML 0.105.1: <c>closedxml/typed.xlsx</c>, with built-in format ids, error constants, formulas, a legacy array
	/// and the defined name, its formulas evaluated before saving; without the dynamic arrays and the table.
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	private static void WriteTyped(string folder)
	{
		using var workbook = new XLWorkbook();
		workbook.Properties.Author = DotNetProducers.Author;
		workbook.Properties.LastModifiedBy = DotNetProducers.Author;
		foreach (var spec in Typed.Sheets)
		{
			workbook.Worksheets.Add(spec.Name);
		}

		foreach (var (name, value) in Typed.Names)
		{
			workbook.DefinedNames.Add(name, value);
		}

		foreach (var spec in Typed.Sheets)
		{
			var sheet = workbook.Worksheet(spec.Name);
			foreach (var (row, cells) in Basic.Rows(spec, "formatId", "error", "formula", "array", "name", "newer", "date"))
			{
				foreach (var cell in cells)
				{
					var target = sheet.Cell(cell.Reference(row));
					switch (cell.Type)
					{
						case CellType.Text:
							target.Value = cell.Text;

							break;
						case CellType.Number:
							target.Value = cell.Number!.Value;

							break;
						case CellType.Bool:
							target.Value = cell.Bool!.Value;

							break;
						case CellType.Error:
							target.Value = Error(cell.Text!);

							break;
						case CellType.Date:
							target.Value = cell.Date is { } date ? date : cell.Number!.Value;
							if (cell.FormatId is { } id)
							{
								target.Style.NumberFormat.NumberFormatId = id;
							}
							else
							{
								target.Style.NumberFormat.Format = cell.Format;
							}

							break;
						case CellType.Formula:
							if (cell.Array is { } array)
							{
								sheet.Range(array).FormulaArrayA1 = cell.Formula;
							}
							else
							{
								target.FormulaA1 = cell.Formula;
							}

							break;
					}
				}
			}
		}

		DotNetProducers.Save(Path.Combine(folder, "typed.xlsx"), path => workbook.SaveAs(path, new SaveOptions { EvaluateFormulasBeforeSaving = true }));
	}

	/// <summary>ClosedXML's error value of an error's text.</summary>
	/// <param name="text">The text, such as <c>#N/A</c>.</param>
	/// <returns>The error.</returns>
	private static XLError Error(string text) => text switch
	{
		"#NULL!" => XLError.NullValue,
		"#DIV/0!" => XLError.DivisionByZero,
		"#VALUE!" => XLError.IncompatibleValue,
		"#REF!" => XLError.CellReference,
		"#NAME?" => XLError.NameNotRecognized,
		"#NUM!" => XLError.NumberInvalid,
		"#N/A" => XLError.NoValueAvailable,
		_ => throw new ArgumentException($"{text} is not an error ClosedXML has.", nameof(text)),
	};
}
