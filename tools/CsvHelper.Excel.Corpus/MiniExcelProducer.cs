using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from MiniExcel 1.46.0.</summary>
internal static class MiniExcelProducer
{
	/// <summary>
	/// MiniExcel 1.46.0: <c>miniexcel/basic.xlsx</c>, from rows of values by column letter. It writes values, not
	/// formulas, rich text or sheet states.
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		var sheets = new Dictionary<string, object>();
		foreach (var spec in DotNetProducers.Worksheets)
		{
			var columns = spec.Rows.SelectMany(r => r.Cells).Select(c => Basic.ColumnIndex(c.Column)).DefaultIfEmpty(1).Max();
			var rows = new List<Dictionary<string, object?>>();
			var specRows = Basic.Rows(spec, "control", "long", "date").ToDictionary(r => r.Row, r => r.Cells);
			for (var row = 1; row <= (specRows.Count == 0 ? 0 : specRows.Keys.Max()); row++)
			{
				var values = Enumerable.Range(1, columns).ToDictionary(c => ((char)('A' + c - 1)).ToString(), _ => (object?)null);
				if (specRows.TryGetValue(row, out var cells))
				{
					foreach (var cell in cells)
					{
						values[cell.Column] = cell.Type switch
						{
							CellType.Text => cell.Text,
							CellType.Number => cell.Number,
							CellType.Bool => cell.Bool,
							CellType.Date => cell.Date ?? (object?)cell.Number,
							_ => null,
						};
					}
				}

				rows.Add(values);
			}

			sheets[spec.Name] = rows;
		}

		DotNetProducers.Save(Path.Combine(folder, "basic.xlsx"), path => MiniExcelLibs.MiniExcel.SaveAs(path, sheets, printHeader: false, overwriteFile: true));
	}
}
