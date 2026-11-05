namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The sheet scanner skipping hidden rows and columns: every form Excel hides and every form it doesn't (as checked
/// through COM on 2026-11-04), the rows a sheet lacks, fields on the shown columns, Excel's own shapes, the cols Excel
/// can't open, shared formulas defined in hidden cells, and a held record's hidden columns. Each test runs in every
/// <see cref="SheetMode"/>.
/// </summary>
public class HiddenScannerTests
{
	/// <summary>Rows that Excel hides, by their attributes.</summary>
	private static readonly string[] HiddenRows =
	[
		" hidden=\"1\"", " hidden=\"true\"", " hidden=\"&#49;\"", " ht=\"0\" customHeight=\"1\"", " ht=\"0.0\" customHeight=\"true\"",
		" ht=\"-0\" customHeight=\"1\"", " ht=\".0\" customHeight=\"1\"", " ht=\"0.\" customHeight=\"1\"", " ht=\"0e+0\" customHeight=\"1\"",
		" ht=\"\" customHeight=\"1\"", " ht=\"&#48;\" customHeight=\"&#49;\"", " ht=\"0\" customHeight=\"1\" hidden=\"0\"", " hidden=\"1\" ht=\"15\" customHeight=\"1\"",
		" outlineLevel=\"1\" hidden=\"1\"",
	];

	/// <summary>
	/// Rows that Excel shows, by their attributes. Excel ignores a value it can't read; it does hide a height of 0.2 (too
	/// small to show a pixel), which depends on the screen and is not read as hidden.
	/// </summary>
	private static readonly string[] ShownRows =
	[
		"", " hidden=\"0\"", " hidden=\"false\"", " hidden=\" 1 \"", " hidden=\"TRUE\"", " hidden=\"yes\"", " hidden=\"2\"", " ht=\"0\"",
		" ht=\"0\" customHeight=\"0\"", " ht=\"0\" customHeight=\"yes\"", " ht=\" 0\" customHeight=\"1\"", " ht=\"+0\" customHeight=\"1\"",
		" ht=\"abc\" customHeight=\"1\"", " ht=\"0x0\" customHeight=\"1\"", " ht=\"0,0\" customHeight=\"1\"", " ht=\"0.75\" customHeight=\"1\"",
		" customHeight=\"1\"", " ht=\"1e+0\" customHeight=\"1\"", " ht=\"0.2\" customHeight=\"1\"", " collapsed=\"1\"",
	];

	/// <summary>Columns that Excel hides, by the attributes of their <c>col</c>.</summary>
	private static readonly string[] HiddenColumns =
	[
		" width=\"9\" hidden=\"1\"", " width=\"9\" hidden=\"true\"", " width=\"9\" hidden=\"&#49;\"", " width=\"0\"", " width=\"0.0\" customWidth=\"1\"",
		" width=\"\"", "", " style=\"0\"", " width=\"abc\"", " width=\" 9 \"", " width=\"+9\"", " width=\"-0\"", " width=\"0\" hidden=\"false\"",
		" width=\"0\" hidden=\"1\" customWidth=\"1\"",
	];

	/// <summary>
	/// Columns that Excel shows, by the attributes of their <c>col</c>. Excel hides a width of 1e-9 (too narrow to show a
	/// pixel), which depends on the font and is not read as hidden.
	/// </summary>
	private static readonly string[] ShownColumns =
	[
		" width=\"9\"", " width=\"9\" hidden=\"0\"", " width=\"9\" hidden=\"yes\"", " width=\"9\" hidden=\" 1\"", " width=\"9\" hidden=\"TRUE\"",
		" width=\"9\" hidden=\"2\"", " width=\"-1\"", " width=\"9.\"", " width=\".5e1\"", " width=\"&#57;\"", " width=\"1e-9\"",
	];

	/// <summary>A cell whose text is its reference, such as <c>B2</c>.</summary>
	/// <param name="reference">The cell's reference.</param>
	/// <returns>The cell's XML.</returns>
	private static string Cell(string reference) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{reference}</t></is></c>";

	/// <summary>A row of cells whose texts are their references.</summary>
	/// <param name="row">The row.</param>
	/// <param name="columns">The columns' letters, such as <c>ABC</c>.</param>
	/// <param name="attributes">The row's other attributes, with a space before each.</param>
	/// <returns>The row's XML.</returns>
	private static string Row(int row, string columns, string attributes = "") =>
		$"<row r=\"{row}\"{attributes}>" + string.Concat(columns.Select(c => Cell($"{c}{row}"))) + "</row>";

	[Fact]
	public async Task SkipsTheRowsExcelHides()
	{
		var forms = HiddenRows.Select(a => (Attributes: a, Hidden: true)).Concat(ShownRows.Select(a => (Attributes: a, Hidden: false))).ToList();
		var xml = Sheets.Worksheet(string.Concat(forms.Select((f, i) => Row(i + 1, "A", f.Attributes))));
		var shown = forms.Select((f, i) => (f.Hidden, Record: $"{i + 1}: A{i + 1}")).Where(f => !f.Hidden).Select(f => f.Record).ToList();
		var all = forms.Select((_, i) => $"{i + 1}: A{i + 1}").ToList();

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(shown, await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
			Assert.Equal(shown, await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: true)));

			// Skipping only hidden columns, or nothing, reads every row.
			Assert.Equal(all, await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: false, columns: true)));
			Assert.Equal(all, await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task WritesTheMissingRowsAroundHiddenOnes()
	{
		// Missing rows before a hidden row are written; the hidden row is not, with cells or without, with r or without.
		var xml = Sheets.Worksheet(
			Row(1, "A") + Row(4, "A", " hidden=\"1\"") + Row(6, "A") + "<row r=\"7\" hidden=\"1\"/>" + Row(9, "AB") +
			$"<row hidden=\"1\">{Cell("A12")}</row><row hidden=\"1\"/><row>{Cell("A14")}</row><row hidden=\"true\"></row>{Row(16, "A")}");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var reading = CellReading.Hidden(rows: true, columns: false);
			Assert.Equal(
				["1: A1", "2: ()", "3: ()", "5: ()", "6: A6", "8: ()", "9: A9 | B9", "10: ()", "11: ()", "14: A14", "16: A16"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));
			Assert.Equal(["1: A1", "6: A6", "9: A9 | B9", "14: A14", "16: A16"], await Sheets.ReadAsync(xml, mode, reading: reading));

			// In an area, from its first row.
			Assert.Equal(["5: ()", "6: A6", "8: ()", "9: A9"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Hidden(true, false, "A5:A9")));
		});
	}

	[Fact]
	public async Task HidesTheRowsTheFileLacks()
	{
		var rows = "<row r=\"2\"/>" + Row(4, "A") + Row(5, "A", " hidden=\"1\"") + Row(7, "A", " ht=\"15\" customHeight=\"1\"");
		var hiding = new[]
		{
			"zeroHeight=\"1\"", "zeroHeight=\"true\"", "zeroHeight=\"&#49;\"", "defaultRowHeight=\"0\" customHeight=\"1\"",
			"defaultRowHeight=\"\" customHeight=\"true\"", "defaultRowHeight=\"0.0\" customHeight=\"1\"",
		};
		var showing = new[] { "zeroHeight=\"0\"", "defaultRowHeight=\"0\"", "defaultRowHeight=\"0\" customHeight=\"yes\"", "defaultRowHeight=\"15\" customHeight=\"1\"", "" };

		await Sheets.InEveryModeAsync(async mode =>
		{
			var reading = CellReading.Hidden(rows: true, columns: false);
			foreach (var format in hiding)
			{
				// The rows it has show, unless they are hidden; an empty one is still a record.
				var xml = Sheets.Worksheet(format, "", rows);
				Assert.Equal(["2: ()", "4: A4", "7: A7"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));
				Assert.Equal(["4: A4", "7: A7"], await Sheets.ReadAsync(xml, mode, reading: reading));

				// Read as any other when hidden rows aren't skipped, hidden columns or not.
				Assert.Equal(["1: ()", "2: ()", "3: ()", "4: A4", "5: A5", "6: ()", "7: A7"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false));
				Assert.Equal(["1: ()", "2: ()", "3: ()", "4: A4", "5: A5", "6: ()", "7: A7"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Hidden(rows: false, columns: true)));
			}

			foreach (var format in showing)
			{
				var xml = Sheets.Worksheet(format, "", rows);
				Assert.Equal(["1: ()", "2: ()", "3: ()", "4: A4", "6: ()", "7: A7"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: reading));
			}
		});
	}

	[Fact]
	public async Task SkipsTheColumnsExcelHides()
	{
		// Column A and the column after the last col are covered by none, so they show; each col covers one column from B.
		var forms = HiddenColumns.Select(a => (Attributes: a, Hidden: true)).Concat(ShownColumns.Select(a => (Attributes: a, Hidden: false))).ToList();
		var cols = string.Concat(forms.Select((f, i) => $"<col min=\"{i + 2}\" max=\"{i + 2}\"{f.Attributes}/>"));
		var names = Enumerable.Range(0, forms.Count + 2).Select(CellReference.ColumnName).ToList();
		var xml = Sheets.Worksheet("", cols, "<row r=\"1\">" + string.Concat(names.Select(n => Cell(n + "1"))) + "</row>");
		var shown = names.Where((_, i) => i == 0 || i == forms.Count + 1 || !forms[i - 1].Hidden).Select(n => n + "1");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal([$"1: {string.Join(" | ", shown)}"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: false, columns: true)));
			Assert.Equal([$"1: {string.Join(" | ", names.Select(n => n + "1"))}"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
		});
	}

	[Fact]
	public async Task HidesTheColumnsNoColCovers()
	{
		var row = Row(1, "ABCDEFGHIJ");
		var hiding = new[] { "defaultColWidth=\"0\"", "defaultColWidth=\"\"", "defaultColWidth=\"0.0\"" };
		var showing = new[] { "defaultColWidth=\" 0 \"", "defaultColWidth=\"abc\"", "defaultColWidth=\"+0\"", "defaultColWidth=\"9\"", "" };

		await Sheets.InEveryModeAsync(async mode =>
		{
			var reading = CellReading.Hidden(rows: false, columns: true);
			foreach (var format in hiding)
			{
				Assert.Equal(["1: B1 | D1"], await Sheets.ReadAsync(Sheets.Worksheet(format, "<col min=\"2\" max=\"2\" width=\"9\"/><col min=\"4\" max=\"4\" width=\"9\"/>", row), mode, reading: reading));
			}

			foreach (var format in showing)
			{
				Assert.Equal(["1: A1 | B1 | D1 | E1 | F1 | G1 | H1 | I1 | J1"], await Sheets.ReadAsync(Sheets.Worksheet(format, "<col min=\"3\" max=\"3\" width=\"0\"/>", row), mode, reading: reading));
			}

			// As Excel writes a sheet whose columns are hidden from H to the last.
			var excel = Sheets.Worksheet(
				"defaultColWidth=\"0\"", "<col min=\"1\" max=\"7\" width=\"8.88671875\" customWidth=\"1\"/><col min=\"8\" max=\"16384\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>", row);
			Assert.Equal(["1: A1 | B1 | C1 | D1 | E1 | F1 | G1"], await Sheets.ReadAsync(excel, mode, reading: reading));
		});
	}

	[Fact]
	public async Task LetsTheLastColDecide()
	{
		var row = Row(1, "ABCDEF");
		var cases = new (string Cols, string Record)[]
		{
			// Overlapping: the later col decides for the columns it covers.
			("<col min=\"2\" max=\"4\" width=\"9\" hidden=\"1\"/><col min=\"3\" max=\"3\" width=\"9\"/>", "1: A1 | C1 | E1 | F1"),
			("<col min=\"3\" max=\"3\" width=\"9\"/><col min=\"2\" max=\"4\" width=\"9\" hidden=\"1\"/>", "1: A1 | E1 | F1"),

			// Out of order, and in two cols elements: Excel takes both.
			("<col min=\"3\" max=\"3\" width=\"9\" hidden=\"1\"/><col min=\"2\" max=\"2\" width=\"9\" hidden=\"1\"/>", "1: A1 | D1 | E1 | F1"),
			("<col min=\"2\" max=\"2\" width=\"9\" hidden=\"1\"/></cols><cols><col min=\"5\" max=\"5\" width=\"9\" hidden=\"1\"/>", "1: A1 | C1 | D1 | F1"),

			// A max past the last column is the last, as Excel takes it.
			("<col min=\"3\" max=\"16385\" width=\"9\" hidden=\"1\"/>", "1: A1 | B1"),
			("<col min=\"1\" max=\"16384\" width=\"9\" hidden=\"1\"/><col min=\"6\" max=\"6\" width=\"9\"/>", "1: F1"),

			// An empty cols, and a col with content, which is skipped.
			("<col min=\"2\" max=\"2\" width=\"9\" hidden=\"1\"><extLst/></col>", "1: A1 | C1 | D1 | E1 | F1"),
		};

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var (cols, record) in cases)
			{
				Assert.Equal([record], await Sheets.ReadAsync(Sheets.Worksheet("", cols, row), mode, reading: CellReading.Hidden(rows: false, columns: true)));
			}

			Assert.Equal(["1: A1 | B1 | C1 | D1 | E1 | F1"], await Sheets.ReadAsync(Sheets.Worksheet("", "", row).Replace("<sheetData>", "<cols/><sheetData>"), mode, reading: CellReading.Hidden(rows: false, columns: true)));
		});
	}

	[Fact]
	public async Task PlacesFieldsOnTheShownColumns()
	{
		// C is hidden: gaps count shown columns only, and rows are padded to the first record's width in fields.
		var cols = "<col min=\"3\" max=\"3\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>";
		var xml = Sheets.Worksheet("", cols, Row(1, "ABDE") + Row(2, "AE") + Row(3, "B") + Row(4, "C") + Row(5, "CD"));

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: A1 | B1 | D1 | E1", "2: A2 | () | () | E2", "3: () | B3 | () | ()", "5: () | () | D5 | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: false, columns: true)));
			Assert.Equal(
				["1: A1 | B1 | D1 | E1", "2: A2 | () | () | E2", "3: () | B3 | () | ()", "4: ()", "5: () | () | D5 | ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Hidden(rows: false, columns: true)));

			// In an area: its shown columns, from its first.
			Assert.Equal(["1: B1 | D1", "3: B3 | ()", "5: () | D5"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(false, true, "B1:D5")));
			Assert.Equal(["1: D1 | E1", "2: () | E2", "5: D5 | ()"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(false, true, "C:E")));
			Assert.Empty(await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(false, true, "C1:C5")));
		});
	}

	[Fact]
	public async Task LeavesHiddenCellsUndecoded()
	{
		// An undefined entity throws only in a cell that is decoded.
		var cols = "<col min=\"2\" max=\"2\" width=\"0\" hidden=\"1\"/>";
		var xml = Sheets.Worksheet("", cols,
			"<row r=\"1\"><c r=\"A1\" t=\"str\"><v>a</v></c><c r=\"B1\" t=\"str\"><v>&bad;</v></c></row>" +
			"<row r=\"2\" hidden=\"1\"><c r=\"A2\" t=\"str\"><v>&bad;</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(["1: a"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: true)));
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: false, columns: true)));
			Assert.Equal("Data!A2", e.Location);
			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
			Assert.Equal("Data!B1", e.Location);
		});
	}

	[Fact]
	public async Task ReadsWhatExcelShows()
	{
		// Excel's shapes from the probes: a filter's hidden rows, a collapsed group, a hidden column D, every column from H
		// hidden, and every row below 9 hidden, with the rows up to 9 written empty.
		var xml = TestPackage.Declaration +
			$"<worksheet xmlns=\"{TestPackage.MainNamespace}\" xmlns:r=\"{TestPackage.RelationshipsNamespace}\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\" mc:Ignorable=\"x14ac\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\">" +
			"<sheetPr filterMode=\"1\"><outlinePr summaryBelow=\"0\"/></sheetPr><dimension ref=\"A1:XFD9\"/><sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>" +
			"<sheetFormatPr defaultColWidth=\"0\" defaultRowHeight=\"14.4\" zeroHeight=\"1\" x14ac:dyDescent=\"0.3\"/>" +
			"<cols><col min=\"1\" max=\"3\" width=\"8.88671875\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>" +
			"<col min=\"5\" max=\"7\" width=\"8.88671875\" customWidth=\"1\"/><col min=\"8\" max=\"16384\" width=\"8.88671875\" hidden=\"1\" customWidth=\"1\"/></cols>" +
			"<sheetData>" +
			Row(1, "ABCDEH", " spans=\"1:8\" x14ac:dyDescent=\"0.3\"") +
			Row(2, "ABCDE", " spans=\"1:5\" hidden=\"1\" x14ac:dyDescent=\"0.3\"") +
			Row(3, "ABCDE", " spans=\"1:5\" x14ac:dyDescent=\"0.3\"") +
			Row(4, "ABCDE", " spans=\"1:5\" outlineLevel=\"1\" hidden=\"1\" x14ac:dyDescent=\"0.3\"") +
			Row(5, "ABCDE", " spans=\"1:5\" collapsed=\"1\" x14ac:dyDescent=\"0.3\"") +
			"<row r=\"7\" spans=\"1:5\" x14ac:dyDescent=\"0.3\"/><row r=\"8\" spans=\"1:5\" x14ac:dyDescent=\"0.3\"/><row r=\"9\" spans=\"1:5\" x14ac:dyDescent=\"0.3\"/>" +
			"</sheetData><autoFilter ref=\"A1:E5\"><filterColumn colId=\"0\"><filters><filter val=\"A1\"/></filters></filterColumn></autoFilter>" +
			"<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>";

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["1: A1 | B1 | C1 | E1", "3: A3 | B3 | C3 | E3", "5: A5 | B5 | C5 | E5", "7: ()", "8: ()", "9: ()"],
				await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Hidden(rows: true, columns: true)));
			Assert.Equal(
				["1: A1 | B1 | C1 | D1 | E1 | () | () | H1", "2: A2 | B2 | C2 | D2 | E2 | () | () | ()", "3: A3 | B3 | C3 | D3 | E3 | () | () | ()",
				"4: A4 | B4 | C4 | D4 | E4 | () | () | ()", "5: A5 | B5 | C5 | D5 | E5 | () | () | ()"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task RejectsColsExcelCantOpen()
	{
		var row = Row(1, "ABC");
		// Each at the col's tag, or at the value that is wrong.
		var cases = new (string Col, string At, string Detail)[]
		{
			("<col max=\"2\" width=\"9\"/>", "<col ", "a col has no min"),
			("<col min=\"2\" width=\"9\"/>", "<col ", "a col has no max"),
			("<col min=\"0\" max=\"2\" width=\"9\"/>", "min=\"", "the attribute min=\"0\" is not a column's number (1 to 16384)"),
			("<col min=\"16385\" max=\"16390\" width=\"9\"/>", "min=\"", "the attribute min=\"16385\" is not a column's number (1 to 16384)"),
			("<col min=\"2\" max=\"2.0\" width=\"9\"/>", "max=\"", "the attribute max=\"2.0\" is not an integer"),
			("<col min=\"3\" max=\"2\" width=\"9\"/>", "<col ", "a col's max, 2, is before its min, 3"),
		};

		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var (col, at, detail) in cases)
			{
				var xml = Sheets.Worksheet("", col, row);
				var offset = xml.IndexOf(at) + (at == "<col " ? 0 : at.Length);
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: false, columns: true)));
				Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
				Assert.Null(e.Location);
				Assert.Equal(0, e.LineNumber);

				// Not read unless hidden columns are skipped.
				Assert.Equal(["1: A1 | B1 | C1"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: false)));
				Assert.Equal(["1: A1 | B1 | C1"], await Sheets.ReadAsync(xml, mode));
			}
		});
	}

	[Fact]
	public async Task ReadsSharedFormulasDefinedInHiddenCells()
	{
		// Row 1 is hidden and defines a group down column A; column B is hidden and defines a group across B and C.
		var cols = "<col min=\"2\" max=\"2\" width=\"0\" hidden=\"1\"/>";
		var xml = Sheets.Worksheet("", cols,
			"<row r=\"1\" hidden=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A3\" si=\"0\">C1*2</f><v>2</v></c><c r=\"C1\"><v>1</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/><v>4</v></c><c r=\"B2\"><f t=\"shared\" ref=\"B2:C3\" si=\"1\">$A2+1</f><v>5</v></c><c r=\"C2\"><f t=\"shared\" si=\"1\"/><v>5</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\"/><v>6</v></c><c r=\"B3\"><f t=\"shared\" si=\"1\"/><v>7</v></c><c r=\"C3\"><f t=\"shared\" si=\"1\"/><v>7</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			Assert.Equal(
				["2: =C2*2 | =$A2+1", "3: =C3*2 | =$A3+1"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: true, formulas: ExcelFormulas.FormulaText)));
			Assert.Equal(
				["2: number:4 | number:5", "3: number:6 | number:7"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(rows: true, columns: true)));

			// A hidden row without r, numbered by its first cell, is read for its definitions too.
			var unnumbered = xml.Replace("<row r=\"1\" hidden=\"1\">", "<row hidden=\"1\">");
			Assert.Equal(
				["2: =C2*2 | =$A2+1", "3: =C3*2 | =$A3+1"],
				await Sheets.ReadAsync(unnumbered, mode, reading: CellReading.Hidden(rows: true, columns: true, formulas: ExcelFormulas.FormulaText)));
		});
	}

	[Fact]
	public async Task LeavesHiddenColumnsOutOfTheHeldRecord()
	{
		var cols = "<col min=\"2\" max=\"2\" width=\"0\" hidden=\"1\"/>";
		var xml = Sheets.Worksheet("", cols, Row(1, "ABC") + Row(2, "ABC") + Row(3, "B"));
		var held = HeldRecord.Names(["Id", "Secret", "Name"], 0, 3, 1);

		await Sheets.InEveryModeAsync(async mode =>
		{
			// The names of shown columns, which set the width.
			Assert.Equal(["1: Id | Name", "2: A2 | C2"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Hidden(false, true, "A2:C3", held: held)));
			Assert.Equal(["1: Id | Name", "2: A2 | C2", "3: ()"], await Sheets.ReadAsync(xml, mode, skipEmptyRows: false, reading: CellReading.Hidden(false, true, "A2:C3", held: held)));
			Assert.Equal(["1: Id | Secret | Name", "2: A2 | B2 | C2", "3: () | B3 | ()"], await Sheets.ReadAsync(xml, mode, reading: CellReading.Area("A2:C3", held: held)));

			// A sheet without rows still writes it.
			Assert.Equal(["1: Id | Name"], await Sheets.ReadAsync(Sheets.Worksheet("", cols, ""), mode, reading: CellReading.Hidden(false, true, "A2:C3", held: held)));
			Assert.Equal(["1: Id | Name"], await Sheets.ReadAsync(Sheets.Worksheet("", cols, "").Replace("<sheetData></sheetData>", "<sheetData/>"), mode, reading: CellReading.Hidden(false, true, "A2:C3", held: held)));
		});
	}
}
