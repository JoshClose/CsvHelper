using System.IO;
using OfficeOpenXml;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from EPPlus 4.5.3.3.</summary>
internal static class EpplusProducer
{
	/// <summary>
	/// EPPlus 4.5.3.3: <c>epplus/basic.xlsx</c>, with the chartsheet (EPPlus can write one), and the formulas calculated by
	/// its engine so that they have results.
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		using var package = new ExcelPackage();
		package.Workbook.Properties.Author = DotNetProducers.Author;
		package.Workbook.Properties.LastModifiedBy = DotNetProducers.Author;
		foreach (var spec in DotNetProducers.Worksheets)
		{
			var sheet = package.Workbook.Worksheets.Add(spec.Name);
			sheet.Hidden = spec.State switch
			{
				"hidden" => eWorkSheetHidden.Hidden,
				"veryHidden" => eWorkSheetHidden.VeryHidden,
				_ => eWorkSheetHidden.Visible,
			};
			foreach (var (row, cells) in Basic.Rows(spec, "control", "long", "rich", "formula", "date"))
			{
				foreach (var cell in cells)
				{
					var target = sheet.Cells[cell.Reference(row)];
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
							target.Formula = cell.Formula;

							break;
						case CellType.Date:
							target.Value = cell.Date is { } date ? date : cell.Number!.Value;
							target.Style.Numberformat.Format = cell.Format;

							break;
						case CellType.Rich:
							foreach (var run in cell.Runs!)
							{
								target.RichText.Add(run.Text).Bold = run.Bold;
							}

							break;
					}
				}
			}
		}

		var chart = package.Workbook.Worksheets.AddChart("Chart", OfficeOpenXml.Drawing.Chart.eChartType.ColumnClustered);
		var orders = package.Workbook.Worksheets["Q1 Orders"];
		chart.Chart.Series.Add(orders.Cells["B2:B4"], orders.Cells["A2:A4"]);
		package.Workbook.Worksheets.MoveAfter("Chart", "Data");
		package.Workbook.Calculate();
		DotNetProducers.Save(Path.Combine(folder, "basic.xlsx"), path => package.SaveAs(new FileInfo(path)));
	}
}
