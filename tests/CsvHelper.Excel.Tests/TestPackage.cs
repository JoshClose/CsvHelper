using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests;

/// <summary>A sheet of a <see cref="TestPackage.Workbook(TestSheet[])"/>.</summary>
/// <param name="name">The sheet's name, as it goes in the XML (escape it there if it needs to be).</param>
/// <param name="state">The sheet's <c>state</c> attribute, or <see langword="null"/> for none.</param>
/// <param name="type">The last segment of the sheet's relationship type: worksheet, chartsheet, dialogsheet or xlMacrosheet.</param>
/// <param name="xml">The sheet's part, or <see langword="null"/> for an empty one of its kind.</param>
internal sealed class TestSheet(string name, string? state = null, string type = "worksheet", string? xml = null)
{
	/// <summary>The sheet's name, as it goes in the XML.</summary>
	public string Name => name;

	/// <summary>The sheet's <c>state</c> attribute, or <see langword="null"/> for none.</summary>
	public string? State => state;

	/// <summary>The last segment of the sheet's relationship type.</summary>
	public string Type => type;

	/// <summary>The sheet's part, or <see langword="null"/> for an empty one of its kind.</summary>
	public string? Xml => xml;
}

/// <summary>
/// Builds packages for tests: zip archives of named entries with exactly the bytes given, so that tests control the entry
/// names, the compression, byte order marks and missing parts. <see cref="Workbook(TestSheet[])"/> starts from the parts
/// Excel writes. A large entry can be streamed into the zip (<see cref="SetStreamed"/>), so that a test of a zip bomb
/// never holds the inflated bytes.
/// </summary>
internal sealed class TestPackage
{
	/// <summary>The transitional SpreadsheetML namespace.</summary>
	public const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The strict SpreadsheetML namespace.</summary>
	public const string StrictMainNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";

	/// <summary>The transitional namespace of relationship ids and types.</summary>
	public const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

	/// <summary>The strict namespace of relationship ids and types.</summary>
	public const string StrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";

	/// <summary>The namespace of relationships parts.</summary>
	public const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

	/// <summary>The namespace of the content types part.</summary>
	public const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

	private readonly List<(string Name, byte[]? Data, Action<Stream>? Write, CompressionLevel Level)> entries = [];

	/// <summary>The names of the entries, in order.</summary>
	public IEnumerable<string> Names => entries.Select(e => e.Name);

	/// <summary>The type URI of a relationship.</summary>
	/// <param name="name">The last segment: officeDocument, worksheet, chartsheet, dialogsheet, xlMacrosheet, styles or sharedStrings.</param>
	/// <param name="strict">Whether to use the strict URI rather than the transitional one.</param>
	/// <returns>The URI.</returns>
	public static string RelationshipType(string name, bool strict = false) => name switch
	{
		"xlMacrosheet" => "http://schemas.microsoft.com/office/2006/relationships/xlMacrosheet",
		_ => (strict ? StrictRelationshipsNamespace : RelationshipsNamespace) + "/" + name,
	};

	/// <summary>A workbook with worksheets of these names.</summary>
	/// <param name="names">The names.</param>
	/// <returns>The package.</returns>
	public static TestPackage Workbook(params string[] names) => Workbook(names.Select(n => new TestSheet(n)).ToArray());

	/// <summary>
	/// A workbook with the parts Excel writes: content types, the package's relationships, the workbook part with the
	/// sheets, its relationships (the sheets as <c>rId1</c>, <c>rId2</c>, ..., then the styles), a styles part, and a part
	/// per sheet.
	/// </summary>
	/// <param name="sheets">The sheets.</param>
	/// <returns>The package.</returns>
	public static TestPackage Workbook(params TestSheet[] sheets)
	{
		var package = new TestPackage();
		var overrides = new StringBuilder();
		var sheetElements = new StringBuilder();
		var relationships = new StringBuilder();
		for (var i = 0; i < sheets.Length; i++)
		{
			var sheet = sheets[i];
			var (folder, contentType, root) = sheet.Type switch
			{
				"chartsheet" => ("chartsheets", "application/vnd.openxmlformats-officedocument.spreadsheetml.chartsheet+xml", "chartsheet"),
				"dialogsheet" => ("dialogsheets", "application/vnd.openxmlformats-officedocument.spreadsheetml.dialogsheet+xml", "dialogsheet"),
				"xlMacrosheet" => ("macrosheets", "application/vnd.ms-excel.macrosheet+xml", "worksheet"),
				_ => ("worksheets", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", "worksheet"),
			};
			var part = $"xl/{folder}/sheet{i + 1}.xml";
			overrides.Append($"<Override PartName=\"/{part}\" ContentType=\"{contentType}\"/>");
			var state = sheet.State is null ? "" : $" state=\"{sheet.State}\"";
			sheetElements.Append($"<sheet name=\"{sheet.Name}\" sheetId=\"{i + 1}\"{state} r:id=\"rId{i + 1}\"/>");
			relationships.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"{RelationshipType(sheet.Type)}\" Target=\"{folder}/sheet{i + 1}.xml\"/>");
			package.Set(part, sheet.Xml ?? $"<{root} xmlns=\"{MainNamespace}\"><sheetData/></{root}>");
		}

		relationships.Append($"<Relationship Id=\"rId{sheets.Length + 1}\" Type=\"{RelationshipType("styles")}\" Target=\"styles.xml\"/>");
		package.Set(
			"[Content_Types].xml",
			Declaration + $"<Types xmlns=\"{ContentTypesNamespace}\">" +
			"<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
			"<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
			"<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
			overrides +
			"<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
			"</Types>");
		package.Set(
			"_rels/.rels",
			Declaration + $"<Relationships xmlns=\"{PackageRelationshipsNamespace}\">" +
			$"<Relationship Id=\"rId1\" Type=\"{RelationshipType("officeDocument")}\" Target=\"xl/workbook.xml\"/>" +
			"</Relationships>");
		package.Set(
			"xl/workbook.xml",
			Declaration + $"<workbook xmlns=\"{MainNamespace}\" xmlns:r=\"{RelationshipsNamespace}\">" +
			"<fileVersion appName=\"xl\"/><workbookPr defaultThemeVersion=\"202300\"/><bookViews><workbookView activeTab=\"0\"/></bookViews>" +
			$"<sheets>{sheetElements}</sheets><calcPr calcId=\"191029\"/></workbook>");
		package.Set("xl/_rels/workbook.xml.rels", Declaration + $"<Relationships xmlns=\"{PackageRelationshipsNamespace}\">{relationships}</Relationships>");
		package.Set(
			"xl/styles.xml",
			Declaration + $"<styleSheet xmlns=\"{MainNamespace}\">" +
			"<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/></numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/></font></fonts>" +
			"<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\"/></cellStyleXfs>" +
			"<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" xfId=\"0\"/><xf numFmtId=\"14\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" applyNumberFormat=\"1\"/></cellXfs>" +
			"</styleSheet>");

		return package;
	}

	/// <summary>The XML declaration Excel writes.</summary>
	public const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n";

	/// <summary>The content type of a shared strings part.</summary>
	public const string SharedStringsContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml";

	/// <summary>
	/// Adds a shared strings part as Excel writes it, with <c>count</c> and <c>uniqueCount</c>; see <see cref="SharedStringsPart"/>.
	/// </summary>
	/// <param name="items">The content of each <c>si</c>, such as <c>&lt;t&gt;a&lt;/t&gt;</c>.</param>
	/// <returns>The package.</returns>
	public TestPackage SharedStrings(params string[] items) => SharedStringsPart(
		Declaration + $"<sst xmlns=\"{MainNamespace}\" count=\"{items.Length}\" uniqueCount=\"{items.Length}\">" +
		string.Concat(items.Select(i => $"<si>{i}</si>")) + "</sst>");

	/// <summary>
	/// Sets <c>xl/sharedStrings.xml</c>, and adds the workbook's relationship to it (after the others, as Excel numbers
	/// them) and its content type, unless they are there already.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <returns>The package.</returns>
	public TestPackage SharedStringsPart(string xml)
	{
		var relationships = Text("xl/_rels/workbook.xml.rels");
		if (!relationships.Contains(RelationshipType("sharedStrings")))
		{
			var id = relationships.Split(["<Relationship "], StringSplitOptions.None).Length;
			Replace(
				"xl/_rels/workbook.xml.rels",
				"</Relationships>",
				$"<Relationship Id=\"rId{id}\" Type=\"{RelationshipType("sharedStrings")}\" Target=\"sharedStrings.xml\"/></Relationships>");
			Replace("[Content_Types].xml", "</Types>", $"<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"{SharedStringsContentType}\"/></Types>");
		}

		return Set("xl/sharedStrings.xml", xml);
	}

	/// <summary>Sets an entry to XML text, encoded as UTF-8 without a byte order mark: replaces the entry of that exact name, or adds one.</summary>
	/// <param name="name">The entry's name, exactly as it goes in the zip.</param>
	/// <param name="xml">The XML.</param>
	/// <param name="level">The compression of the entry.</param>
	/// <returns>The package.</returns>
	public TestPackage Set(string name, string xml, CompressionLevel level = CompressionLevel.Optimal) => Set(name, Encoding.UTF8.GetBytes(xml), level);

	/// <summary>Sets an entry to bytes: replaces the entry of that exact name, or adds one.</summary>
	/// <param name="name">The entry's name, exactly as it goes in the zip.</param>
	/// <param name="data">The bytes.</param>
	/// <param name="level">The compression of the entry.</param>
	/// <returns>The package.</returns>
	public TestPackage Set(string name, byte[] data, CompressionLevel level = CompressionLevel.Optimal) => Put((name, data, null, level));

	/// <summary>
	/// Sets an entry to what a callback writes into the zip entry's stream when the package is written: replaces the entry of
	/// that exact name, or adds one. The bytes are compressed as they are written, so a large entry is never held whole.
	/// </summary>
	/// <param name="name">The entry's name, exactly as it goes in the zip.</param>
	/// <param name="write">Writes the entry's bytes.</param>
	/// <param name="level">The compression of the entry.</param>
	/// <returns>The package.</returns>
	public TestPackage SetStreamed(string name, Action<Stream> write, CompressionLevel level = CompressionLevel.Optimal) => Put((name, null, write, level));

	/// <summary>Adds an entry, even when one has that name already.</summary>
	/// <param name="name">The entry's name, exactly as it goes in the zip.</param>
	/// <param name="xml">The XML.</param>
	/// <returns>The package.</returns>
	public TestPackage Add(string name, string xml)
	{
		entries.Add((name, Encoding.UTF8.GetBytes(xml), null, CompressionLevel.Optimal));

		return this;
	}

	/// <summary>Removes an entry.</summary>
	/// <param name="name">The entry's exact name.</param>
	/// <returns>The package.</returns>
	public TestPackage Remove(string name)
	{
		Assert.Equal(1, entries.RemoveAll(e => e.Name == name));

		return this;
	}

	/// <summary>Renames an entry, keeping its place.</summary>
	/// <param name="from">The entry's exact name.</param>
	/// <param name="to">The new name.</param>
	/// <returns>The package.</returns>
	public TestPackage Rename(string from, string to)
	{
		var index = entries.FindIndex(e => e.Name == from);
		Assert.True(index >= 0, $"No entry {from}.");
		entries[index] = (to, entries[index].Data, entries[index].Write, entries[index].Level);

		return this;
	}

	/// <summary>Moves an entry to the end of the zip, as LibreOffice writes the content types.</summary>
	/// <param name="name">The entry's exact name.</param>
	/// <returns>The package.</returns>
	public TestPackage MoveLast(string name)
	{
		var index = entries.FindIndex(e => e.Name == name);
		var entry = entries[index];
		entries.RemoveAt(index);
		entries.Add(entry);

		return this;
	}

	/// <summary>Edits an entry's XML.</summary>
	/// <param name="name">The entry's exact name.</param>
	/// <param name="edit">Makes the new XML from the old.</param>
	/// <returns>The package.</returns>
	public TestPackage Edit(string name, Func<string, string> edit) => Set(name, edit(Text(name)));

	/// <summary>Replaces text in an entry's XML, which must be there.</summary>
	/// <param name="name">The entry's exact name.</param>
	/// <param name="oldText">The text to replace.</param>
	/// <param name="newText">The text to put in its place.</param>
	/// <returns>The package.</returns>
	public TestPackage Replace(string name, string oldText, string newText) => Edit(name, xml =>
	{
		Assert.Contains(oldText, xml);

		return xml.Replace(oldText, newText);
	});

	/// <summary>An entry's text, as UTF-8.</summary>
	/// <param name="name">The entry's exact name.</param>
	/// <returns>The text.</returns>
	public string Text(string name) => Encoding.UTF8.GetString(entries.Single(e => e.Name == name).Data ?? throw new InvalidOperationException($"{name} is streamed."));

	/// <summary>Writes the package as a zip archive.</summary>
	/// <returns>The bytes of the archive.</returns>
	public byte[] ToArray()
	{
		var output = new MemoryStream();
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
		{
			foreach (var (name, data, write, level) in entries)
			{
				using var stream = zip.CreateEntry(name, level).Open();
				if (data is not null)
				{
					stream.Write(data, 0, data.Length);
				}
				else
				{
					write!(stream);
				}
			}
		}

		return output.ToArray();
	}

	/// <summary>Sets an entry: replaces the entry of that exact name, or adds one.</summary>
	/// <param name="entry">The entry.</param>
	/// <returns>The package.</returns>
	private TestPackage Put((string Name, byte[]? Data, Action<Stream>? Write, CompressionLevel Level) entry)
	{
		var index = entries.FindIndex(e => e.Name == entry.Name);
		if (index < 0)
		{
			entries.Add(entry);
		}
		else
		{
			entries[index] = entry;
		}

		return this;
	}

	/// <summary>Writes the package as a zip archive in a seekable, read-only stream.</summary>
	/// <returns>The stream, at its start.</returns>
	public MemoryStream ToStream() => new(ToArray(), false);
}
