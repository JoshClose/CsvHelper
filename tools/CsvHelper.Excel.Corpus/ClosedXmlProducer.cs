using System;
using System.IO;
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
	}
}
