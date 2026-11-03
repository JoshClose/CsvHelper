using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CsvHelper.Excel.Corpus;

/// <summary>The corpus's files from the Open XML SDK 3.5.1.</summary>
internal static class OpenXmlSdkProducer
{
	/// <summary>
	/// The Open XML SDK 3.5.1: <c>openxmlsdk/basic.xlsx</c>, with every element of the main namespace <c>x:</c>-prefixed,
	/// as the SDK writes them. (It rejects an empty prefix in <c>AddNamespaceDeclaration</c>, and writes a root parsed
	/// from XML with the default namespace prefixed too, so there is no variant with the default namespace.)
	/// Shared strings, styles for the dates, rich text and a phonetic run are built from the SDK's elements; formulas get
	/// the results Excel computes.
	/// </summary>
	/// <param name="folder">The producer's folder.</param>
	public static void Write(string folder)
	{
		foreach (var name in new[] { "basic.xlsx" })
		{
			DotNetProducers.Save(Path.Combine(folder, name), path =>
			{
				using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
				var workbookPart = document.AddWorkbookPart();
				var strings = workbookPart.AddNewPart<SharedStringTablePart>();
				strings.SharedStringTable = new SharedStringTable();
				var styles = workbookPart.AddNewPart<WorkbookStylesPart>();
				var formats = new List<string>();
				var sheets = new Sheets();
				uint sheetId = 1;
				foreach (var spec in DotNetProducers.Worksheets)
				{
					var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
					var data = new SheetData();
					foreach (var (row, cells) in Basic.Rows(spec, "long", "rich", "phonetic", "formula", "date"))
					{
						var element = new Row { RowIndex = (uint)row };
						foreach (var cell in cells)
						{
							element.Append(OpenXmlCell(cell, row, strings.SharedStringTable, formats));
						}

						data.Append(element);
					}

					worksheetPart.Worksheet = new Worksheet();
					worksheetPart.Worksheet.Append(data);
					var sheet = new Sheet { Name = spec.Name, SheetId = sheetId++, Id = workbookPart.GetIdOfPart(worksheetPart) };
					if (spec.State != "visible")
					{
						sheet.State = spec.State == "hidden" ? SheetStateValues.Hidden : SheetStateValues.VeryHidden;
					}

					sheets.Append(sheet);
				}

				// cellXfs: General, then one per date format (custom ids from 164).
				var numberFormats = new NumberingFormats(formats.Select((f, i) => new NumberingFormat { NumberFormatId = (uint)(164 + i), FormatCode = f }));
				numberFormats.Count = (uint)formats.Count;
				var cellFormats = new CellFormats(new[] { new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 } }
					.Concat(formats.Select((_, i) => new CellFormat { NumberFormatId = (uint)(164 + i), FontId = 0, FillId = 0, BorderId = 0, ApplyNumberFormat = true })));
				styles.Stylesheet = new Stylesheet();
				styles.Stylesheet.Append(
					numberFormats,
					new Fonts(new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" })),
					new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
					new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())),
					cellFormats);
				workbookPart.Workbook = new Workbook();
				workbookPart.Workbook.Append(sheets);
				document.PackageProperties.Creator = DotNetProducers.Author;
			});
		}
	}

	/// <summary>An Open XML SDK cell for a cell of the basic workbook.</summary>
	/// <param name="cell">The cell.</param>
	/// <param name="row">Its row.</param>
	/// <param name="strings">The shared strings, which text is added to.</param>
	/// <param name="formats">The date formats so far, which the cell's is added to.</param>
	/// <returns>The cell.</returns>
	private static Cell OpenXmlCell(CellSpec cell, int row, SharedStringTable strings, List<string> formats)
	{
		var element = new Cell { CellReference = cell.Reference(row) };
		switch (cell.Type)
		{
			case CellType.Text:
				element.DataType = CellValues.SharedString;
				element.CellValue = new CellValue(AddString(strings, new SharedStringItem(new Text(cell.Text!) { Space = SpaceProcessingModeValues.Preserve })));

				break;
			case CellType.Number:
				element.CellValue = new CellValue(cell.Number!.Value);

				break;
			case CellType.Bool:
				element.DataType = CellValues.Boolean;
				element.CellValue = new CellValue(cell.Bool!.Value);

				break;
			case CellType.Formula:
				var result = DotNetProducers.Result(cell.Formula!, row);
				element.CellFormula = new CellFormula(cell.Formula!);
				if (result is double number)
				{
					element.CellValue = new CellValue(number);
				}
				else
				{
					element.DataType = DotNetProducers.IsError(result) ? CellValues.Error : CellValues.String;
					element.CellValue = new CellValue((string)result);
				}

				break;
			case CellType.Date:
				var index = formats.IndexOf(cell.Format!);
				if (index < 0)
				{
					formats.Add(cell.Format!);
					index = formats.Count - 1;
				}

				element.StyleIndex = (uint)(index + 1);
				element.CellValue = new CellValue(cell.Date?.ToOADate() ?? cell.Number!.Value);

				break;
			case CellType.Rich:
				element.DataType = CellValues.SharedString;
				var runs = cell.Runs!.Select(r => new Run(r.Bold ? new RunProperties(new Bold()) : new RunProperties(), new Text(r.Text) { Space = SpaceProcessingModeValues.Preserve }));
				element.CellValue = new CellValue(AddString(strings, new SharedStringItem(runs)));

				break;
			case CellType.Phonetic:
				element.DataType = CellValues.SharedString;
				var item = new SharedStringItem(
					new Text(cell.Text!),
					new PhoneticRun(new Text(cell.Phonetic!)) { BaseTextStartIndex = 0, EndingBaseIndex = (uint)cell.Text!.Length },
					new PhoneticProperties { FontId = 0, Type = PhoneticValues.FullWidthKatakana });
				element.CellValue = new CellValue(AddString(strings, item));

				break;
		}

		return element;
	}

	/// <summary>Adds a shared string.</summary>
	/// <param name="strings">The shared strings.</param>
	/// <param name="item">The string.</param>
	/// <returns>Its index.</returns>
	private static int AddString(SharedStringTable strings, SharedStringItem item)
	{
		strings.Append(item);

		return strings.Count() - 1;
	}
}
