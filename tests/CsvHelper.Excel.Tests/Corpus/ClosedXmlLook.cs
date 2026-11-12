using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// A workbook's look as ClosedXML reads it, in the terms of Excel's answers (<see cref="SheetLook"/>), for the cells Excel
/// answered about: the frozen panes, the sheet's filter, the tables, the merged ranges, the links (through the Open XML
/// SDK), the hidden columns, and each cell's font, wrap and (without a value) number format. ClosedXML knows no cell style,
/// so a cell underlined in the <c>Hyperlink</c> style's colour is taken to be in it; and it doesn't size rows, so its
/// looks have no tall rows.
/// </summary>
internal static class ClosedXmlLook
{
	/// <summary>The <c>Hyperlink</c> cell style's font colour.</summary>
	private static readonly XLColor HyperlinkColor = XLColor.FromHtml("#0563C1");

	/// <summary>
	/// Each sheet's look, in tab order. The sheets are taken in order rather than by name, since ClosedXML doesn't decode a
	/// name's escapes (<c>a_x005F_x0041_b</c> for <c>a_x0041_b</c>).
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="used">Each sheet's used range as Excel answered it, in tab order: the last row and 0-based column.</param>
	/// <returns>Each sheet's look, in tab order.</returns>
	public static List<SheetLook> Read(byte[] workbook, IReadOnlyList<(long Row, int Column)> used)
	{
		using var book = new XLWorkbook(new MemoryStream(workbook));
		var links = Links(workbook);
		var result = new List<SheetLook>();
		foreach (var ((sheet, extent), index) in book.Worksheets.Zip(used, (s, u) => (s, u)).Select((s, i) => (s, i)))
		{
			var view = sheet.SheetView;
			var cells = new Dictionary<string, string>(StringComparer.Ordinal);
			for (var row = 1; row <= extent.Row + 1; row++)
			{
				for (var column = 1; column <= extent.Column + 1; column++)
				{
					var cell = sheet.Cell(row, column);
					if (Look(cell) is { Length: > 0 } look)
					{
						cells[cell.Address.ToString()!] = look;
					}
				}
			}

			result.Add(new SheetLook
			{
				Freeze = view.SplitRow > 0 || view.SplitColumn > 0 ? (view.SplitRow, view.SplitColumn) : null,
				Filter = sheet.AutoFilter.IsEnabled ? sheet.AutoFilter.Range.RangeAddress.ToString() : null,
				Tables =
				[
					.. sheet.Tables.Select(t => new SheetTable(
						t.Name, t.RangeAddress.ToString()!, t.Theme == XLTableTheme.None ? null : t.Theme.Name, t.ShowRowStripes, t.ShowAutoFilter, [.. t.Fields.Select(f => f.Name)])),
				],
				Merged = [.. sheet.MergedRanges.Select(r => r.RangeAddress.ToString()!)],
				Links = links[index],
				HiddenColumns =
				[
					.. Enumerable.Range(1, extent.Column + 1).Where(c => sheet.Column(c).IsHidden).Select(c => CellReference.ColumnName(c - 1)),
				],
				Cells = cells,
			});
		}

		return result;
	}

	/// <summary>
	/// Each sheet's links as Excel shows them, in tab order, read through the Open XML SDK, since ClosedXML drops a link's
	/// <c>location</c> beside its target, as LibreOffice writes a fragment: a link's address and sub-address from its
	/// target by the oracle's rules, its <c>location</c> being the sub-address when it has one.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's links.</returns>
	private static List<List<SheetLink>> Links(byte[] workbook)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), false);
		var part = document.WorkbookPart!;
		var result = new List<List<SheetLink>>();
		foreach (var sheet in part.Workbook!.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
		{
			var worksheet = (WorksheetPart)part.GetPartById(sheet.Id!);
			var targets = worksheet.HyperlinkRelationships.ToDictionary(r => r.Id, r => r.Uri);
			var links = new List<SheetLink>();
			foreach (var link in worksheet.Worksheet!.Descendants<Hyperlink>())
			{
				var location = link.Location?.Value is { Length: > 0 } text ? text : null;
				if (link.Id?.Value is { } id)
				{
					var (address, subAddress) = WrittenOracle.LinkOf(targets[id]);
					links.Add(new SheetLink(link.Reference!.Value!, address, location ?? subAddress));
				}
				else
				{
					links.Add(new SheetLink(link.Reference!.Value!, null, location));
				}
			}

			result.Add(links);
		}

		return result;
	}

	/// <summary>
	/// A cell's look in the terms of Excel's answers: <c>bold</c>, <c>size</c>, <c>font</c>, <c>Hyperlink</c> (or an
	/// underline and a colour), <c>wrap</c>, and <c>format</c> without a value; empty for Normal's.
	/// </summary>
	/// <param name="cell">The cell.</param>
	/// <returns>The look.</returns>
	private static string Look(IXLCell cell)
	{
		var style = cell.Style;
		var font = style.Font;
		var parts = new List<string>();
		if (font.Bold)
		{
			parts.Add("bold");
		}

		if (font.FontSize != 11)
		{
			parts.Add("size " + font.FontSize.ToString(CultureInfo.InvariantCulture));
		}

		if (font.FontName != "Calibri")
		{
			parts.Add("font " + font.FontName);
		}

		var underlined = font.Underline != XLFontUnderlineValues.None;
		var coloured = font.FontColor != XLColor.FromTheme(XLThemeColor.Text1) && font.FontColor != XLColor.Black && font.FontColor != XLColor.FromIndex(64);
		if (underlined && font.Underline == XLFontUnderlineValues.Single && font.FontColor == HyperlinkColor)
		{
			parts.Add("Hyperlink");
		}
		else
		{
			if (underlined)
			{
				parts.Add("underline " + font.Underline);
			}

			if (coloured)
			{
				parts.Add("color " + font.FontColor);
			}
		}

		if (style.Alignment.WrapText)
		{
			parts.Add("wrap");
		}

		if (cell.IsEmpty() && Code(style.NumberFormat) is var code && code != "General")
		{
			parts.Add("format " + code);
		}

		return string.Join(", ", parts);
	}

	/// <summary>A number format's code as Excel's <c>NumberFormat</c> gives it: its own, or a built-in's by its id.</summary>
	/// <param name="format">The format.</param>
	/// <returns>The code.</returns>
	private static string Code(IXLNumberFormat format) => format.Format is { Length: > 0 } code
		? WrittenOracle.ExcelCode(code)
		: WrittenOracle.BuiltIn.TryGetValue((uint)format.NumberFormatId, out var builtIn) ? builtIn : "built-in " + format.NumberFormatId.ToString(CultureInfo.InvariantCulture);
}
