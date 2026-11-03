namespace CsvHelper.Excel.Tests.Package;

/// <summary>OPC part names: normalizing, resolving relationship targets, and the names of relationships parts.</summary>
public class PartNameTests
{
	[Theory]
	[InlineData("xl/workbook.xml", "xl/workbook.xml")]
	[InlineData("/xl/workbook.xml", "xl/workbook.xml")]
	[InlineData("xl\\worksheets\\sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("\\xl\\styles.xml", "xl/styles.xml")]
	[InlineData("xl/worksheets/sheet%201.xml", "xl/worksheets/sheet 1.xml")]
	[InlineData("xl/%E6%97%A5%E6%9C%AC.xml", "xl/日本.xml")]
	[InlineData("xl/100%25.xml", "xl/100%.xml")]
	[InlineData("xl/bad%zz.xml", "xl/bad%zz.xml")]
	[InlineData("[Content_Types].xml", "[Content_Types].xml")]
	[InlineData("_rels/.rels", "_rels/.rels")]
	public void Normalizes(string name, string expected)
	{
		Assert.Equal(expected, PartName.Normalize(name));
	}

	[Theory]
	[InlineData("xl/workbook.xml", "worksheets/sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "/xl/worksheets/sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "../docProps/app.xml", "docProps/app.xml")]
	[InlineData("xl/workbook.xml", "./worksheets/./sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "worksheets/../styles.xml", "xl/styles.xml")]
	[InlineData("xl/workbook.xml", "../../../../styles.xml", "styles.xml")]
	[InlineData("xl/workbook.xml", "worksheets//sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "worksheets\\sheet1.xml", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "worksheets/sheet%201.xml", "xl/worksheets/sheet 1.xml")]
	[InlineData("xl/workbook.xml", "worksheets/sheet1.xml#A1", "xl/worksheets/sheet1.xml")]
	[InlineData("xl/workbook.xml", "Worksheets/Sheet1.XML", "xl/Worksheets/Sheet1.XML")]
	[InlineData("", "xl/workbook.xml", "xl/workbook.xml")]
	[InlineData("", "/xl/workbook.xml", "xl/workbook.xml")]
	[InlineData("workbook.xml", "sheet1.xml", "sheet1.xml")]
	[InlineData("a/b/c/part.xml", "../../d.xml", "a/d.xml")]
	public void ResolvesTargets(string source, string target, string expected)
	{
		Assert.Equal(expected, PartName.Resolve(source, target));
	}

	[Theory]
	[InlineData("xl/workbook.xml", "xl/_rels/workbook.xml.rels")]
	[InlineData("xl/worksheets/sheet1.xml", "xl/worksheets/_rels/sheet1.xml.rels")]
	[InlineData("workbook.xml", "_rels/workbook.xml.rels")]
	public void NamesRelationshipsParts(string partName, string expected)
	{
		Assert.Equal(expected, PartName.RelationshipsOf(partName));
	}

	[Theory]
	[InlineData("xl/workbook.xml", "xml")]
	[InlineData("_rels/.rels", "rels")]
	[InlineData("xl/workbook.bin", "bin")]
	[InlineData("xl/a.b/part", "")]
	[InlineData("part", "")]
	public void FindsExtensions(string partName, string expected)
	{
		Assert.Equal(expected, PartName.Extension(partName));
	}

	[Fact]
	public void ComparesIgnoringCase()
	{
		Assert.True(PartName.Comparer.Equals("xl/Worksheets/Sheet1.xml", "XL/worksheets/sheet1.XML"));
		Assert.Equal(PartName.Comparer.GetHashCode("xl/Workbook.xml"), PartName.Comparer.GetHashCode("XL/WORKBOOK.XML"));
	}
}
