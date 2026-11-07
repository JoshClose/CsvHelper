using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Writes the parts of a workbook that come after its sheets, once every sheet's name is known: the shared strings
/// (with <see cref="ExcelStrings.Shared"/>), the styles, the workbook, its relationships, the package's relationships
/// and, last, the content types. Excel and LibreOffice open a package in that order (checked on 2026-11-06).
/// </summary>
/// <remarks>
/// There is no theme, no document properties (so no author and no dates), no views and no <c>calcPr</c>: Excel opens a
/// workbook without them. The styles are Excel's minimal style sheet in Calibri 11, which every Excel since 2007 has and
/// which LibreOffice replaces with Carlito, of the same widths.
/// </remarks>
internal static class PackageParts
{
	/// <summary>The XML declaration that Excel starts each part with, and the line break after it.</summary>
	public static readonly byte[] Declaration = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");

	/// <summary>The part name of a sheet.</summary>
	/// <param name="number">The sheet's place in tab order, from 1.</param>
	/// <returns>The name, such as <c>xl/worksheets/sheet1.xml</c>.</returns>
	public static string SheetPart(int number) => "xl/worksheets/sheet" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".xml";

	/// <summary>Writes the parts after the sheets.</summary>
	/// <param name="package">The package.</param>
	/// <param name="sheetNames">The sheets' names, in tab order: sheet <c>n</c> is <see cref="SheetPart"/>(<c>n</c>).</param>
	/// <param name="strings">The shared strings, or <see langword="null"/> when cells hold their own.</param>
	/// <param name="styles">The cell formats.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the parts are written.</returns>
	public static async ValueTask WriteAsync(PackageWriter package, IReadOnlyList<string> sheetNames, SharedStringTable? strings, StyleRegistry styles, bool async, CancellationToken cancellationToken)
	{
		using var xml = new XmlOutput(4096);
		if (strings is not null)
		{
			await WriteSharedStringsAsync(package, strings, xml, async, cancellationToken).ConfigureAwait(false);
		}

		Start(xml);
		WriteStyles(xml, styles);
		await package.WritePartAsync("xl/styles.xml", xml, async, cancellationToken).ConfigureAwait(false);

		Start(xml);
		WriteWorkbook(xml, sheetNames);
		await package.WritePartAsync("xl/workbook.xml", xml, async, cancellationToken).ConfigureAwait(false);

		Start(xml);
		WriteWorkbookRelationships(xml, sheetNames.Count, strings is not null);
		await package.WritePartAsync("xl/_rels/workbook.xml.rels", xml, async, cancellationToken).ConfigureAwait(false);

		Start(xml);
		xml.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"u8);
		xml.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"u8);
		xml.Append("</Relationships>"u8);
		await package.WritePartAsync("_rels/.rels", xml, async, cancellationToken).ConfigureAwait(false);

		Start(xml);
		WriteContentTypes(xml, sheetNames.Count, strings is not null);
		await package.WritePartAsync("[Content_Types].xml", xml, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Empties the buffer and writes the declaration.</summary>
	/// <param name="xml">The buffer.</param>
	private static void Start(XmlOutput xml)
	{
		xml.Truncate(0);
		xml.Append(Declaration);
	}

	/// <summary>
	/// Writes the shared strings, streamed: each string's <c>si</c>, in the order of its index, with the buffer written out
	/// whenever it passes 64 KB.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="strings">The strings.</param>
	/// <param name="xml">The buffer.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the part is written.</returns>
	private static async ValueTask WriteSharedStringsAsync(PackageWriter package, SharedStringTable strings, XmlOutput xml, bool async, CancellationToken cancellationToken)
	{
		Start(xml);
		xml.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\""u8);
		xml.AppendNumber(strings.References);
		xml.Append("\" uniqueCount=\""u8);
		xml.AppendNumber(strings.Count);
		xml.Append("\">"u8);
		var entry = await package.OpenPartAsync("xl/sharedStrings.xml", async, cancellationToken).ConfigureAwait(false);
		try
		{
			for (var i = 0; i < strings.Count; i++)
			{
				xml.Append("<si>"u8);
				xml.Append(strings[i]);
				xml.Append("</si>"u8);
				if (xml.Length >= SheetSink.FlushSize)
				{
					await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
					xml.Truncate(0);
				}
			}

			xml.Append("</sst>"u8);
			await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await PackageWriter.CloseAsync(entry, async).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Writes the styles: the custom number formats, one font, the two fills and the border every style sheet has, and the
	/// cell formats, the default one first.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="styles">The cell formats.</param>
	private static void WriteStyles(XmlOutput xml, StyleRegistry styles)
	{
		xml.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"u8);
		styles.WriteNumberFormats(xml);
		xml.Append("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts>"u8);
		xml.Append("<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"u8);
		xml.Append("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"u8);
		xml.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"u8);
		styles.WriteCellFormats(xml);
		xml.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"u8);
		xml.Append("</styleSheet>"u8);
	}

	/// <summary>Writes the workbook part: its sheets, in tab order, each with its relationship.</summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="sheetNames">The sheets' names.</param>
	private static void WriteWorkbook(XmlOutput xml, IReadOnlyList<string> sheetNames)
	{
		xml.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"u8);
		for (var i = 0; i < sheetNames.Count; i++)
		{
			xml.Append("<sheet name=\""u8);
			// Valid UTF-16, since the name was checked, so valid UTF-8; Excel decodes _xHHHH_ in names as in text.
			xml.AppendText(Encoding.UTF8.GetBytes(sheetNames[i]), attribute: true);
			xml.Append("\" sheetId=\""u8);
			xml.AppendNumber(i + 1);
			xml.Append("\" r:id=\"rId"u8);
			xml.AppendNumber(i + 1);
			xml.Append("\"/>"u8);
		}

		xml.Append("</sheets></workbook>"u8);
	}

	/// <summary>Writes the workbook's relationships: <c>rId1</c> on for the sheets, then the styles and the shared strings.</summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="sheets">The number of sheets.</param>
	/// <param name="shared">Whether there is a shared-strings part.</param>
	private static void WriteWorkbookRelationships(XmlOutput xml, int sheets, bool shared)
	{
		xml.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"u8);
		for (var i = 1; i <= sheets; i++)
		{
			xml.Append("<Relationship Id=\"rId"u8);
			xml.AppendNumber(i);
			xml.Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet"u8);
			xml.AppendNumber(i);
			xml.Append(".xml\"/>"u8);
		}

		xml.Append("<Relationship Id=\"rId"u8);
		xml.AppendNumber(sheets + 1);
		xml.Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"u8);
		if (shared)
		{
			xml.Append("<Relationship Id=\"rId"u8);
			xml.AppendNumber(sheets + 2);
			xml.Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>"u8);
		}

		xml.Append("</Relationships>"u8);
	}

	/// <summary>Writes the content types: the defaults for relationships and XML, and an override for each part.</summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="sheets">The number of sheets.</param>
	/// <param name="shared">Whether there is a shared-strings part.</param>
	private static void WriteContentTypes(XmlOutput xml, int sheets, bool shared)
	{
		xml.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"u8);
		xml.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"u8);
		xml.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>"u8);
		xml.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"u8);
		for (var i = 1; i <= sheets; i++)
		{
			xml.Append("<Override PartName=\"/xl/worksheets/sheet"u8);
			xml.AppendNumber(i);
			xml.Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"u8);
		}

		xml.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"u8);
		if (shared)
		{
			xml.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>"u8);
		}

		xml.Append("</Types>"u8);
	}
}
