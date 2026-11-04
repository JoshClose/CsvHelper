namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The scanner's formulas: as their values, or as text (<see cref="ExcelFormulas.FormulaText"/>), own, shared, array and
/// data table formulas, and the shared formulas' limit. Each test runs in every <see cref="SheetMode"/>.
/// </summary>
public class FormulaScannerTests
{
	[Fact]
	public async Task ReadsFormulasAsTextWhateverTheCellsType()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f>SUM(B1:C1)</f><v>3</v></c><c r=\"B1\" t=\"str\"><f>\"a\"&amp;\"b\"</f><v>ab</v></c>" +
			"<c r=\"C1\" t=\"b\"><f>TRUE()</f><v>1</v></c><c r=\"D1\" t=\"e\"><f>1/0</f><v>#DIV/0!</v></c><c r=\"E1\"><v>5</v></c>" +
			"<c r=\"G1\" t=\"inlineStr\"><f>F1&amp;\"x\"</f><is><t>yx</t></is></c><c r=\"H1\" t=\"s\"><f>A1</f><v>9</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><f t=\"normal\">A1+1</f><v>4</v></c><c r=\"B2\"><f t=\"shared\" ref=\"B2:B3\">A2</f><v>4</v></c>" +
			"<c r=\"C2\"><f t=\"other\" aca=\"1\" ca=\"1\">A2</f><v>4</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// A cell without a formula reads its value; a shared formula without an si is the cell's own.
			Assert.Equal(
				[
					"1: =SUM(B1:C1) | =\"a\"&\"b\" | =TRUE() | =1/0 | number:5 | () | =F1&\"x\" | =A1",
					"2: =A1+1 | =A2 | =A2 | () | () | () | () | ()",
				],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.FormulaText));

			// As their values, formulas are skipped: the shared string index 9 has no string to name.
			var values = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode));
			Assert.Equal("Data!H1", values.Location);
		});
	}

	[Fact]
	public async Task DecodesFormulasAsValues()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f>  A2 + 1  </f><v>0</v></c><c r=\"B1\"><f xml:space=\"preserve\">  A2 + 1</f><v>0</v></c>" +
			"<c r=\"C1\" t=\"str\"><f>\"_x005F_x0041_\"&amp;\"&lt;\"&amp;\"_x0041_\"</f><v>0</v></c><c r=\"D1\"><f><![CDATA[1<2]]></f><v>1</v></c>" +
			"<c r=\"E1\"><f>1+\r\n2</f><v>3</v></c><c r=\"F1\"><f>&#32;</f><v>4</v></c></row>" +
			"<row r=\"2\" xml:space=\"preserve\"><c r=\"A2\"><f> A1</f><v>0</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// White space around a formula goes unless xml:space="preserve" applies, as for values; a formula that decodes
			// to nothing is an empty field.
			Assert.Equal(
				["1: =A2 + 1 | =  A2 + 1 | =\"_x0041_\"&\"<\"&\"A\" | =1<2 | =1+\n2 | ()", "2: = A1 | () | () | () | () | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.FormulaText));
		});
	}

	[Fact]
	public async Task MovesSharedFormulasToEachCellOfTheirGroup()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"B1\"><f t=\"shared\" ref=\"B1:C3\" si=\"7\">A1*2+$A$1</f><v>1</v></c><c r=\"C1\"><f t=\"shared\" si=\"7\"/><v>2</v></c></row>" +
			"<row r=\"2\"><c r=\"B2\"><f t=\"shared\" si=\"7\">Z9</f><v>3</v></c><c r=\"D2\"><f t=\"shared\" ref=\"D2:D3\" si=\"0\">C2</f><v>4</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"7\"/><v>5</v></c><c r=\"C3\"><f t=\"shared\" si=\"7\"/><v>6</v></c>" +
			"<c r=\"D3\"><f t=\"shared\" si=\"0\"/><v>7</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// A cell's own text is ignored, as Excel ignores it; A1 moved a column left of A wraps to XFD, as in Excel.
			Assert.Equal(
				[
					"1: () | =A1*2+$A$1 | =B1*2+$A$1",
					"2: () | =A2*2+$A$1 | () | =C2",
					"3: =XFD3*2+$A$1 | () | =B3*2+$A$1 | =C3",
				],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.FormulaText));

			Assert.Equal(
				["1: () | number:1 | number:2", "2: () | number:3 | () | number:4", "3: number:5 | () | number:6 | number:7"],
				await Sheets.ReadAsync(xml, mode));
		});
	}

	[Fact]
	public async Task ReadsTheValuesOfCellsWithoutFormulaText()
	{
		var xml = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f t=\"array\" ref=\"A1:A2\">B1:B2*2</f><v>2</v></c><c r=\"B1\"><f t=\"dataTable\" ref=\"B1:B2\" dt2D=\"0\" dtr=\"0\" r1=\"C1\"/><v>7</v></c>" +
			"<c r=\"C1\"><f/><v>3</v></c><c r=\"D1\"><f></f><v>4</v></c><c r=\"E1\"><f>  </f><v>5</v></c><c r=\"F1\"><f t=\"shared\" ref=\"F1:F2\" si=\"0\"/><v>6</v></c>" +
			"<c r=\"G1\"><f t=\"dataTable\" ref=\"G1\">A1</f><v>8</v></c><c r=\"H1\"><f/></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><v>2</v></c><c r=\"F2\"><f t=\"shared\" si=\"0\">C1</f><v>9</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			// An array formula's other cells have no f, and read their values; so do a data table's cells, a cell whose f has
			// no text, and the cells of a group whose defining cell has none. A cell with neither is valueless.
			Assert.Equal(
				["1: =B1:B2*2 | number:7 | number:3 | number:4 | number:5 | number:6 | number:8", "2: number:2 | () | () | () | () | number:9 | ()"],
				await Sheets.ReadAsync(xml, mode, reading: CellReading.FormulaText));
		});
	}

	[Fact]
	public async Task ThrowsForACellOfAGroupThatNoCellBeforeItDefines()
	{
		var missing = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\"><c r=\"B2\"><f t=\"shared\" si=\"3\"/><v>7</v></c></row>");
		var before = Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" si=\"0\"/><v>1</v></c><c r=\"B1\"><f t=\"shared\" ref=\"A1:B1\" si=\"0\">C1</f><v>2</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(missing, mode, reading: CellReading.FormulaText));
			Assert.Equal($"Cell Data!B2 can't be read at byte {missing.IndexOf("<c r=\"B2\"")} of xl/worksheets/sheet1.xml: it uses shared formula 3, which no cell before it defines.", e.Message);
			Assert.Equal("Data!B2", e.Location);
			Assert.Equal(2, e.LineNumber);

			e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(before, mode, reading: CellReading.FormulaText));
			Assert.Equal($"Cell Data!A1 can't be read at byte {before.IndexOf("<c r=\"A1\"")} of xl/worksheets/sheet1.xml: it uses shared formula 0, which no cell before it defines.", e.Message);

			// As values, the cells read theirs.
			Assert.Equal(["1: number:1", "2: () | number:7"], await Sheets.ReadAsync(missing, mode));
		});
	}

	[Fact]
	public async Task ThrowsForASharedFormulaIndexThatIsNotOne()
	{
		await Sheets.InEveryModeAsync(async mode =>
		{
			foreach (var (index, what) in new[] { ("x", "an integer"), ("-1", "a shared formula index"), ("99999999999", "an integer") })
			{
				var xml = Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1\" si=\"{index}\">B1</f><v>1</v></c></row>");
				var e = await Assert.ThrowsAsync<CsvFormatException>(() => Sheets.ReadAsync(xml, mode, reading: CellReading.FormulaText));
				Assert.Equal($"Cell Data!A1 can't be read at byte {xml.IndexOf("si=") + 4} of xl/worksheets/sheet1.xml: the attribute si=\"{index}\" is not {what}.", e.Message);
				Assert.Equal(["1: number:1"], await Sheets.ReadAsync(xml, mode));
			}
		});
	}

	[Fact]
	public async Task ReadsFormulasOfErrorCellsWhateverTheirPolicy()
	{
		var xml = Sheets.Worksheet("<row r=\"1\"><c r=\"A1\" t=\"e\"><f>1/0</f><v>#DIV/0!</v></c><c r=\"B1\" t=\"e\"><v>#N/A</v></c><c r=\"C1\"><v>1</v></c></row>");

		await Sheets.InEveryModeAsync(async mode =>
		{
			var nulls = new CellReading(errorCells: ExcelErrorCells.Null, formulas: ExcelFormulas.FormulaText);
			Assert.Equal(["1: =1/0 | () | number:1"], await Sheets.ReadAsync(xml, mode, reading: nulls));
			Assert.Equal(["1: () | () | number:1"], await Sheets.ReadAsync(xml, mode, reading: new CellReading(errorCells: ExcelErrorCells.Null)));
		});
	}

	[Fact]
	public async Task StopsSharedFormulasAtThePartSizeLimit()
	{
		const int Limit = 16 * 1024;
		var formula = string.Join("+", Enumerable.Repeat("B1", 50));
		var groups = string.Concat(Enumerable.Range(1, 200).Select(n => $"<row r=\"{n}\"><c r=\"A{n}\"><f t=\"shared\" ref=\"A{n}\" si=\"{n}\">{formula}</f><v>0</v></c></row>"));
		var redefined = string.Concat(Enumerable.Range(1, 200).Select(n => $"<row r=\"{n}\"><c r=\"A{n}\"><f t=\"shared\" ref=\"A{n}:A{n + 1}\" si=\"0\">{formula}</f><v>0</v></c></row>"));

		await Sheets.InEveryModeAsync(async mode =>
		{
			// Each group counts its formula and 64 bytes: 200 of them pass 16 KB.
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => Sheets.ReadAsync(Sheets.Worksheet(groups), mode, maxSize: Limit, reading: CellReading.FormulaText));
			Assert.Equal($"The shared formulas of xl/worksheets/sheet1.xml take more than ExcelReaderOptions.MaxPartSize ({Limit} bytes).", e.Message);
			Assert.Equal(200, (await Sheets.ReadAsync(Sheets.Worksheet(groups), mode, maxSize: 1 << 20, reading: CellReading.FormulaText)).Count);

			// A group defined again replaces the one before; formulas as values hold nothing.
			Assert.Equal(200, (await Sheets.ReadAsync(Sheets.Worksheet(redefined), mode, maxSize: Limit, reading: CellReading.FormulaText)).Count);
			Assert.Equal(200, (await Sheets.ReadAsync(Sheets.Worksheet(groups), mode, maxSize: Limit)).Count);
		});
	}
}
