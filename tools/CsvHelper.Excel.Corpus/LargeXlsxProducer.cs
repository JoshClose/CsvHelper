using System.Collections.Generic;
using System.IO;
using LargeXlsx;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from LargeXlsx 2.0.2.</summary>
internal static class LargeXlsxProducer
{
	/// <summary>
	/// LargeXlsx 2.0.2: <c>largexlsx/basic.xlsx</c>, a streaming writer with inline strings. (With
	/// <c>requireCellReferences: false</c> it writes the same bytes, references included, so there is no variant without them.)
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		foreach (var name in new[] { "basic.xlsx" })
		{
			DotNetProducers.Save(Path.Combine(folder, name), path =>
			{
				using var stream = File.Create(path);
				using var writer = new XlsxWriter(stream);
				var styles = new Dictionary<string, XlsxStyle>();
				foreach (var spec in DotNetProducers.Worksheets)
				{
					var state = spec.State switch
					{
						"hidden" => XlsxWorksheetState.Hidden,
						"veryHidden" => XlsxWorksheetState.VeryHidden,
						_ => XlsxWorksheetState.Visible,
					};
					writer.BeginWorksheet(spec.Name, state: state);
					var next = 1;
					foreach (var (row, cells) in Basic.Rows(spec, "long", "formula", "date"))
					{
						writer.SkipRows(row - next);
						writer.BeginRow();
						var column = 1;
						foreach (var cell in cells)
						{
							writer.SkipColumns(Basic.ColumnIndex(cell.Column) - column);
							column = Basic.ColumnIndex(cell.Column) + 1;
							switch (cell.Type)
							{
								case CellType.Text:
									writer.Write(cell.Text);

									break;
								case CellType.Number:
									writer.Write(cell.Number!.Value);

									break;
								case CellType.Bool:
									writer.Write(cell.Bool!.Value);

									break;
								case CellType.Formula:
									var result = DotNetProducers.Result(cell.Formula!, row);
									if (result is double number)
									{
										writer.WriteFormula(cell.Formula, result: number);
									}
									else
									{
										writer.WriteFormula(cell.Formula, result: (string)result);
									}

									break;
								case CellType.Date:
									if (!styles.TryGetValue(cell.Format!, out var style))
									{
										style = XlsxStyle.Default.With(new XlsxNumberFormat(cell.Format!));
										styles[cell.Format!] = style;
									}

									if (cell.Date is { } date)
									{
										writer.Write(date, style);
									}
									else
									{
										writer.Write(cell.Number!.Value, style);
									}

									break;
							}
						}

						next = row + 1;
					}
				}
			});
		}
	}
}
