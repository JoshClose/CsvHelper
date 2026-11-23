using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The position helper of <see cref="Written"/>, which the writer's tests read its sheets through: cells and rows placed
/// as SpreadsheetML places them, from a sheet written with references and from the same sheet written without them, as
/// Phase 9's step 5 writes it (no <c>r</c>; a gap before a later value in its record a placeholder, <c>&lt;c/&gt;</c> or
/// <c>&lt;c s="5"/&gt;</c> in its column's style; an empty record before a later row a <c>&lt;row/&gt;</c>).
/// </summary>
public class WrittenTests
{
	/// <summary>A worksheet's XML before its rows: column B and C's default style is 5.</summary>
	private const string Start =
		"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
		"<worksheet xmlns=\"" + TestPackage.MainNamespace + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
		"<cols><col min=\"2\" max=\"3\" width=\"9.140625\" style=\"5\"/></cols><sheetData>";

	/// <summary>A worksheet's XML after its rows.</summary>
	private const string End = "</sheetData><pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>";

	/// <summary>
	/// A sheet as written with references: a protected title in row 1; a header in row 2 whose field in B is empty; a
	/// record in row 3 whose fields in A and C are empty; two empty records; a record in row 6.
	/// </summary>
	private const string WithReferences =
		Start +
		"<row r=\"1\" s=\"1\" customFormat=\"1\"><c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Title</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" s=\"2\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"C2\" s=\"2\" t=\"inlineStr\"><is><t>Note</t></is></c></row>" +
		"<row r=\"3\"><c r=\"B3\"><v>1.5</v></c><c r=\"D3\" s=\"3\" t=\"s\"><v>0</v></c></row>" +
		"<row r=\"6\"><c r=\"A6\" s=\"4\"><v>45306</v></c></row>" +
		End;

	/// <summary>
	/// The same sheet written without references: the header's gap in B a placeholder in its column's style, the record's
	/// gap in A a bare placeholder, its gap in C one in its column's style, and the empty records' rows before row 6.
	/// </summary>
	private const string WithoutReferences =
		Start +
		"<row s=\"1\" customFormat=\"1\"><c s=\"1\" t=\"inlineStr\"><is><t>Title</t></is></c></row>" +
		"<row><c s=\"2\" t=\"inlineStr\"><is><t>Name</t></is></c><c s=\"5\"/><c s=\"2\" t=\"inlineStr\"><is><t>Note</t></is></c></row>" +
		"<row><c/><c><v>1.5</v></c><c s=\"5\"/><c s=\"3\" t=\"s\"><v>0</v></c></row>" +
		"<row/><row/>" +
		"<row><c s=\"4\"><v>45306</v></c></row>" +
		End;

	/// <summary>The cells with a value of both sheets, by reference, each as written without its <c>r</c>.</summary>
	private static readonly Dictionary<string, string> Values = new()
	{
		["A1"] = "<c s=\"1\" t=\"inlineStr\"><is><t>Title</t></is></c>",
		["A2"] = "<c s=\"2\" t=\"inlineStr\"><is><t>Name</t></is></c>",
		["C2"] = "<c s=\"2\" t=\"inlineStr\"><is><t>Note</t></is></c>",
		["B3"] = "<c><v>1.5</v></c>",
		["D3"] = "<c s=\"3\" t=\"s\"><v>0</v></c>",
		["A6"] = "<c s=\"4\"><v>45306</v></c>",
	};

	[Fact]
	public void PlacesTheCellsOfASheetWithoutReferences()
	{
		var rows = Written.Rows(XDocument.Parse(WithoutReferences));

		Assert.Equal([1L, 2, 3, 4, 5, 6], rows.Select(r => r.Number));
		Assert.Equal(
			["A1", "A2", "B2:5", "C2", "A3:0", "B3", "C3:5", "D3", "A6"],
			rows.SelectMany(r => r.Cells).Select(c => c.IsEmpty ? $"{c.Reference}:{c.Style}" : c.Reference));
		Assert.Equal([0, 0], rows.Where(r => r.Number is 4 or 5).Select(r => r.Cells.Count));
		Assert.Equal(("1", "1"), ((string?)rows[0].Element.Attribute("s"), (string?)rows[0].Element.Attribute("customFormat")));
	}

	[Fact]
	public void PlacesBothShapesValuesAlike()
	{
		foreach (var sheet in new[] { WithReferences, WithoutReferences })
		{
			var cells = Written.Cells(XDocument.Parse(sheet)).Where(c => !c.IsEmpty).ToList();
			Assert.Equal(Values.Keys, cells.Select(c => c.Reference));
			Assert.All(Values, v => Assert.Equal(v.Value, Written.CellXml(sheet, v.Key)));
			Assert.Equal("3", (string?)Written.Cell(XDocument.Parse(sheet), "D3")!.Attribute("s"));
		}

		// The references' sheet has no cell where the other has a placeholder, and no row for an empty record.
		Assert.Null(Written.CellXml(WithReferences, "A3"));
		Assert.Equal("<c/>", Written.CellXml(WithoutReferences, "A3"));
		Assert.Equal("<c s=\"5\"/>", Written.CellXml(WithoutReferences, "B2"));
		Assert.Equal("<c s=\"5\"/>", Written.CellXml(WithoutReferences, "C3"));
		Assert.Null(Written.CellXml(WithoutReferences, "E3"));
		Assert.Null(Written.Row(XDocument.Parse(WithReferences), 4));
		Assert.Equal("<row/>", Written.RowXml(WithoutReferences, 4));
		Assert.Equal("<row/>", Written.RowXml(WithoutReferences, 5));
		Assert.Null(Written.RowXml(WithoutReferences, 7));
	}

	[Fact]
	public void GivesRowsAsWrittenWithoutTheirReferences()
	{
		const string Record = "<row><c/><c><v>1.5</v></c><c s=\"5\"/><c s=\"3\" t=\"s\"><v>0</v></c></row>";

		Assert.Equal(Record, Written.RowXml(WithoutReferences, 3));
		Assert.Equal("<row><c><v>1.5</v></c><c s=\"3\" t=\"s\"><v>0</v></c></row>", Written.RowXml(WithReferences, 3));
		Assert.Equal("<row s=\"1\" customFormat=\"1\"><c s=\"1\" t=\"inlineStr\"><is><t>Title</t></is></c></row>", Written.RowXml(WithReferences, 1));
		Assert.Equal(Written.RowXml(WithReferences, 1), Written.RowXml(WithoutReferences, 1));
		var (start, length) = Written.CellSpan(WithoutReferences, "D3")!.Value;
		Assert.Equal((WithoutReferences.IndexOf("<c s=\"3\" t=\"s\">", StringComparison.Ordinal), "<c s=\"3\" t=\"s\"><v>0</v></c>"), (start, WithoutReferences.Substring(start, length)));
	}

	[Fact]
	public void ReadsASheetWithoutRows()
	{
		Assert.Empty(Written.Rows(XDocument.Parse(Start.Replace("<sheetData>", "<sheetData/>") + End.Replace("</sheetData>", ""))));
		Assert.Empty(Written.Rows(XDocument.Parse(Start + End)));
		Assert.Null(Written.RowXml(Start + End, 1));
	}

	[Theory]
	[InlineData("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row><c><v>2</v></c></row>", "mixes 1 rows with r and 1 without")]
	[InlineData("<row><c r=\"A1\"><v>1</v></c><c><v>2</v></c></row>", "mixes 1 cells with r and 1 without")]
	[InlineData("<row r=\"2\"><c r=\"A2\"><v>1</v></c></row><row r=\"2\"><c r=\"B2\"><v>2</v></c></row>", "Row 2 follows row 2.")]
	[InlineData("<row><c r=\"B1\"><v>1</v></c><c r=\"A1\"><v>2</v></c></row>", "A cell in column 0 of row 1 follows one in column 1.")]
	[InlineData("<row r=\"3\"><c r=\"A2\"><v>1</v></c></row>", "The cell A2 is in row 3.")]
	public void RefusesWhatNeitherShapeWrites(string rows, string problem)
	{
		var e = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Written.Rows(XDocument.Parse(Start + rows + End)));

		Assert.Contains(problem, e.Message);
	}
}
