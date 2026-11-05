namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Defined names, as the workbook part lists them: name, scope, visibility and text. Each test loads synchronously and
/// asynchronously.
/// </summary>
public class DefinedNamesTests
{
	/// <summary>The defined names of a workbook Excel 365 saved on 2026-11-04 (sheets Messy, Tables and Hidden All), as it wrote them.</summary>
	public static readonly string[] ExcelsNames =
	[
		"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"2\" hidden=\"1\">'Hidden All'!$A$1:$A$6</definedName>",
		"<definedName name=\"Const\">0.05</definedName>",
		"<definedName name=\"Data\" localSheetId=\"1\">Tables!$A$1:$B$3</definedName>",
		"<definedName name=\"Data\">Messy!$B$4:$F$14</definedName>",
		"<definedName name=\"Dyn\">OFFSET(Messy!$B$4,0,0,COUNTA(Messy!$B:$B),5)</definedName>",
		"<definedName name=\"HiddenName\" hidden=\"1\">Messy!$B$5</definedName>",
		"<definedName name=\"Local\" localSheetId=\"0\">Messy!$B$5:$C$6</definedName>",
		"<definedName name=\"_xlnm.Print_Area\" localSheetId=\"0\">Messy!$A$1:$F$14</definedName>",
		"<definedName name=\"Rel\">Messy!B2</definedName>",
		"<definedName name=\"Spaced\">'Hidden All'!$A$1:$A$6</definedName>",
		"<definedName name=\"TableRef\">Sales[Qty]</definedName>",
		"<definedName name=\"ThreeD\">Messy:Tables!$A$1</definedName>",
		"<definedName name=\"ToGone\">#REF!</definedName>",
		"<definedName name=\"Union\">Messy!$B$5,Messy!$C$6</definedName>",
		"<definedName name=\"WholeCol\">Messy!$B:$C</definedName>",
		"<definedName name=\"WholeRow\">Messy!$4:$4</definedName>",
	];

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsExcelsNames(bool async)
	{
		var package = TestPackage.Workbook("Messy", "Tables", "Hidden All").DefinedNames(ExcelsNames);

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(
			[
				"_xlnm._FilterDatabase 2 hidden 'Hidden All'!$A$1:$A$6",
				"Const - shown 0.05",
				"Data 1 shown Tables!$A$1:$B$3",
				"Data - shown Messy!$B$4:$F$14",
				"Dyn - shown OFFSET(Messy!$B$4,0,0,COUNTA(Messy!$B:$B),5)",
				"HiddenName - hidden Messy!$B$5",
				"Local 0 shown Messy!$B$5:$C$6",
				"_xlnm.Print_Area 0 shown Messy!$A$1:$F$14",
				"Rel - shown Messy!B2",
				"Spaced - shown 'Hidden All'!$A$1:$A$6",
				"TableRef - shown Sales[Qty]",
				"ThreeD - shown Messy:Tables!$A$1",
				"ToGone - shown #REF!",
				"Union - shown Messy!$B$5,Messy!$C$6",
				"WholeCol - shown Messy!$B:$C",
				"WholeRow - shown Messy!$4:$4",
			],
			workbook.DefinedNames.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsWorkbooksWithoutNames(bool async)
	{
		Assert.Empty((await TestPackage.Workbook("Data").LoadWorkbookAsync(async)).DefinedNames);
		Assert.Empty((await TestPackage.Workbook("Data").DefinedNames().LoadWorkbookAsync(async)).DefinedNames);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DecodesTheText(bool async)
	{
		var package = TestPackage.Workbook("Data").DefinedNames(
			"<definedName name=\"Entities\">'Q1 &amp; Orders'!$A$1</definedName>",
			"<definedName name=\"CData\"><![CDATA['Q1 & Orders'!$A$1]]></definedName>",
			"<definedName name=\"Split\">'Q1 &amp; <!-- a comment -->Orders'!$A<![CDATA[$1]]></definedName>",
			"<definedName name=\"Escape\">\"_x0041_\"</definedName>",
			"<definedName name=\"Spaces\">\r\n\t Data!$A$1 \t\r\n</definedName>",
			"<definedName name=\"Element\">Data!<x>skipped</x>$A$1</definedName>",
			"<definedName name=\"Empty\"/>",
			"<definedName name=\"Blank\">   </definedName>",
			"<definedName name=\"Named &amp; Encoded\">1</definedName>");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(
			[
				"Entities - shown 'Q1 & Orders'!$A$1",
				"CData - shown 'Q1 & Orders'!$A$1",
				"Split - shown 'Q1 & Orders'!$A$1",
				"Escape - shown \"A\"",
				"Spaces - shown Data!$A$1",
				"Element - shown Data!$A$1",
				"Empty - shown ",
				"Blank - shown ",
				"Named & Encoded - shown 1",
			],
			workbook.DefinedNames.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepsTheFirstOfANameInAScope(bool async)
	{
		var package = TestPackage.Workbook("One", "Two").DefinedNames(
			"<definedName name=\"Data\">One!$A$1</definedName>",
			"<definedName name=\"DATA\">One!$B$2</definedName>",
			"<definedName name=\"data\" localSheetId=\"0\">One!$C$3</definedName>",
			"<definedName name=\"Data\" localSheetId=\"1\">Two!$D$4</definedName>",
			"<definedName name=\"Data\" localSheetId=\"1\">Two!$E$5</definedName>",
			"<definedName name=\"Other\">One!$F$6</definedName>");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(
			["Data - shown One!$A$1", "data 0 shown One!$C$3", "Data 1 shown Two!$D$4", "Other - shown One!$F$6"],
			workbook.DefinedNames.Select(Describe));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsPrefixedAndStrictNames(bool async)
	{
		var package = TestPackage.Workbook("Data").Set(
			"xl/workbook.xml",
			TestPackage.Declaration + $"<x:workbook xmlns:x=\"{TestPackage.StrictMainNamespace}\" xmlns:rel=\"{TestPackage.StrictRelationshipsNamespace}\">" +
			"<x:sheets><x:sheet name=\"Data\" sheetId=\"1\" rel:id=\"rId1\"/></x:sheets>" +
			"<x:definedNames><x:definedName name=\"Area\" x:localSheetId=\"0\" hidden=\"true\">Data!$A$1:$B$2</x:definedName></x:definedNames></x:workbook>");

		var workbook = await package.LoadWorkbookAsync(async);

		Assert.Equal(["Area 0 hidden Data!$A$1:$B$2"], workbook.DefinedNames.Select(Describe));
	}

	[Theory]
	[InlineData("<definedName>A!$A$1</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: a defined name has no name\.$")]
	[InlineData("<definedName name=\"\">A!$A$1</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: a defined name has no name\.$")]
	[InlineData("<definedName name=\"X\" localSheetId=\"-1\">A!$A$1</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: the attribute localSheetId=""-1"" is not a sheet's index \(0 or more\)\.$")]
	[InlineData("<definedName name=\"X\" localSheetId=\"one\">A!$A$1</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: the attribute localSheetId=""one"" is not an integer\.$")]
	[InlineData("<definedName name=\"X\" hidden=\"yes\">A!$A$1</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: the attribute hidden=""yes"" is not a boolean\.$")]
	[InlineData("<definedName name=\"X\" localSheetId=\"2\">A!$A$1</definedName>", @"^The defined name ""X"" of xl/workbook\.xml has the localSheetId 2, which is not a sheet's index: its sheets are 0 to 1\.$")]
	[InlineData("<definedName name=\"X\">&bad;</definedName>", @"^The XML of xl/workbook\.xml can't be read at byte \d+: '&bad;' is neither a predefined entity nor a valid character reference\.$")]
	public async Task RejectsNamesThatAreNotOnes(string name, string message)
	{
		var package = TestPackage.Workbook("A", "B").DefinedNames(name);

		foreach (var async in new[] { false, true })
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => package.LoadWorkbookAsync(async));
			Assert.Matches(message, e.Message);
		}
	}

	/// <summary>Describes a name for comparisons: <c>name scope visibility text</c>, with <c>-</c> for the workbook's scope.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The description.</returns>
	private static string Describe(WorkbookName name) =>
		$"{name.Name} {name.LocalSheetId?.ToString() ?? "-"} {(name.Hidden ? "hidden" : "shown")} {name.Text}";
}
