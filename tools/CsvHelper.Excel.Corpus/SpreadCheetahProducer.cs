using System;
using System.Collections.Generic;
using System.IO;
using SpreadCheetah;
using SpreadCheetah.Styling;
using SpreadCheetah.Worksheets;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from SpreadCheetah 1.28.0.</summary>
internal static class SpreadCheetahProducer
{
	/// <summary>
	/// SpreadCheetah 1.28.0: <c>spreadcheetah/basic.xlsx</c>, a streaming writer: rows in order, sheet states, styles for
	/// the dates, and formulas with the results Excel computes.
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		DotNetProducers.Save(Path.Combine(folder, "basic.xlsx"), path =>
		{
			using var stream = File.Create(path);
			using var spreadsheet = Spreadsheet.CreateNewAsync(stream).AsTask().GetAwaiter().GetResult();
			var styles = new Dictionary<string, StyleId>();
			foreach (var spec in DotNetProducers.Worksheets)
			{
				var options = new WorksheetOptions
				{
					Visibility = spec.State switch
					{
						"hidden" => WorksheetVisibility.Hidden,
						"veryHidden" => WorksheetVisibility.VeryHidden,
						_ => WorksheetVisibility.Visible,
					},
				};
				spreadsheet.StartWorksheetAsync(spec.Name, options).AsTask().GetAwaiter().GetResult();
				var next = 1;
				foreach (var (row, cells) in Basic.Rows(spec, "control", "long", "formula", "date"))
				{
					// The rows it skips are written as empty rows.
					for (; next < row; next++)
					{
						spreadsheet.AddRowAsync(Array.Empty<Cell>()).AsTask().GetAwaiter().GetResult();
					}

					var values = new List<Cell>();
					foreach (var cell in cells)
					{
						while (values.Count < Basic.ColumnIndex(cell.Column) - 1)
						{
							values.Add(new Cell());
						}

						values.Add(cell.Type switch
						{
							CellType.Text => new Cell(cell.Text),
							CellType.Number => new Cell(cell.Number!.Value),
							CellType.Bool => new Cell(cell.Bool!.Value),
							CellType.Formula => DotNetProducers.Result(cell.Formula!, row) is double number
								? new Cell(new Formula(cell.Formula), new DataCell(number))
								: new Cell(new Formula(cell.Formula), new DataCell((string)DotNetProducers.Result(cell.Formula!, row))),
							CellType.Date => cell.Date is { } date ? new Cell(date, StyleFor(spreadsheet, styles, cell.Format!)) : new Cell(cell.Number!.Value, StyleFor(spreadsheet, styles, cell.Format!)),
							_ => new Cell(),
						});
					}

					spreadsheet.AddRowAsync(values).AsTask().GetAwaiter().GetResult();
					next = row + 1;
				}
			}

			spreadsheet.FinishAsync().AsTask().GetAwaiter().GetResult();
		});
	}

	/// <summary>The style of a date format in a SpreadCheetah spreadsheet, added the first time.</summary>
	/// <param name="spreadsheet">The spreadsheet.</param>
	/// <param name="styles">The styles added so far.</param>
	/// <param name="format">The number format.</param>
	/// <returns>The style.</returns>
	private static StyleId StyleFor(Spreadsheet spreadsheet, Dictionary<string, StyleId> styles, string format)
	{
		if (!styles.TryGetValue(format, out var style))
		{
			style = spreadsheet.AddStyle(new Style { Format = NumberFormat.Custom(format) });
			styles[format] = style;
		}

		return style;
	}
}
