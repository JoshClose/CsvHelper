using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvHelper.Benchmarks;

/// <summary>
/// The package assets data laid out as a report, as people lay out sheets for reading rather than for import: titles
/// above the header, a column of regions merged down their rows, a hidden column, hidden rows, and a table over the data.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExcelReportWorkbook"/> writes the report for the benchmark that reads it (<c>ExcelReportRead</c>), and the
/// corpus tool builds it in Excel through COM, for the test that compares the two workbooks. The test project and the
/// corpus tool link this file.
/// </para>
/// <para>The layout, for the 25 columns of the package assets data:</para>
/// <list type="bullet">
/// <item>row 1: <see cref="Title"/> in <c>B1</c>, merged across the report's columns (<c>B1:AB1</c>);</item>
/// <item>row 2: <see cref="Subtitle"/> in <c>B2</c>; row 3 is empty;</item>
/// <item>row 4, the header, from <c>B4</c>: <see cref="RegionHeader"/>, <see cref="KeyHeader"/>, then the data's
/// columns (<see cref="Fields"/>);</item>
/// <item>from row 5, a row per data row: its region, in the first row of each <see cref="RegionRows"/> rows, merged down
/// them; its key, the data row's number from 1, a number in column <c>C</c>, which is hidden; then its fields, as
/// text;</item>
/// <item>every <see cref="HiddenEvery"/>th data row hidden;</item>
/// <item>a table, <see cref="TableName"/>, over the key and the fields (<c>C4</c> to the last row). Excel merges no cells
/// in a table, and unmerges them when it makes one over merged cells (checked through COM), so the regions are left of
/// it.</item>
/// </list>
/// <para>
/// The header row, the regions and the fields have the text format, as in the package assets workbook, which Excel made
/// from the CSV with every column as text; the key and the titles are in General.
/// </para>
/// </remarks>
internal static class ExcelReport
{
	/// <summary>The sheet's name.</summary>
	public const string SheetName = "Report";

	/// <summary>The name of the table over the key and the fields.</summary>
	public const string TableName = "Assets";

	/// <summary>The first title, in <c>B1</c>, merged across the report's columns.</summary>
	public const string Title = "NuGet package assets";

	/// <summary>The second title, in <c>B2</c>.</summary>
	public const string Subtitle = "From NCsvPerf's PackageAssets.csv";

	/// <summary>The header of the regions' column.</summary>
	public const string RegionHeader = "Region";

	/// <summary>The header of the keys' column, which is hidden.</summary>
	public const string KeyHeader = "Key";

	/// <summary>The sheet row of the header.</summary>
	public const int HeaderRow = 4;

	/// <summary>The report's first column, the regions': <c>B</c>, 1-based.</summary>
	public const int FirstColumn = 2;

	/// <summary>The keys' column, which is hidden: <c>C</c>, 1-based.</summary>
	public const int KeyColumn = FirstColumn + 1;

	/// <summary>The number of data rows of each region, whose cells in the regions' column are merged.</summary>
	public const int RegionRows = 100;

	/// <summary>Every this many data rows, the last is hidden: data rows 50, 100, 150, and so on, counted from 1.</summary>
	public const int HiddenEvery = 50;

	/// <summary>The regions, in turn.</summary>
	public static readonly string[] Regions = ["North", "East", "South", "West"];

	/// <summary>
	/// The data's columns, in order: each header name, and the width Excel gave its column when it made the table, or
	/// <see langword="null"/> for a header that fits Excel's default width beside the table's filter button.
	/// </summary>
	/// <remarks>
	/// Excel widens a column whose header doesn't fit when it makes a table over it; these are its widths for the package
	/// assets header (<c>DataGenerator.PackageAssetHeader</c>) in Aptos Narrow 11, from the corpus file that the tool's
	/// <c>report</c> command saved. Being Excel's measurements, they are known only for these names.
	/// </remarks>
	public static readonly (string Name, string? Width)[] Fields =
	[
		("ScanId", null),
		("ScanTimestamp", "16.36328125"),
		("Id", null),
		("Version", "9.08984375"),
		("Created", "9.453125"),
		("ResultType", "12.08984375"),
		("PatternSet", "11.6328125"),
		("PropertyAnyValue", "17.6328125"),
		("PropertyCodeLanguage", "22.08984375"),
		("PropertyTargetFrameworkMoniker", "30.90625"),
		("PropertyLocale", "15.26953125"),
		("PropertyManagedAssembly", "25.453125"),
		("PropertyMSBuild", "16.7265625"),
		("PropertyRuntimeIdentifier", "24.7265625"),
		("PropertySatelliteAssembly", "24.90625"),
		("Path", null),
		("FileName", "10.81640625"),
		("FileExtension", "14.08984375"),
		("TopLevelFolder", "15.453125"),
		("RoundTripTargetFrameworkMoniker", "32.54296875"),
		("FrameworkName", "17"),
		("FrameworkVersion", "18.36328125"),
		("FrameworkProfile", "17.54296875"),
		("PlatformName", "14.90625"),
		("PlatformVersion", "16.26953125"),
	];

	/// <summary>Gets the number of the report's columns: the regions, the keys and the fields.</summary>
	public static int Columns => Fields.Length + 2;

	/// <summary>Gets the report's last column, 1-based: <c>AB</c>.</summary>
	public static int LastColumn => FirstColumn + Columns - 1;

	/// <summary>Returns the sheet row of a data row.</summary>
	/// <param name="index">The data row's index, from 0.</param>
	/// <returns>The row, from 5.</returns>
	public static int RowOf(int index) => HeaderRow + 1 + index;

	/// <summary>Returns the region a data row starts, if it is the first of its region's rows.</summary>
	/// <param name="index">The data row's index, from 0.</param>
	/// <returns>The region's name, or <see langword="null"/> for the region's other rows, whose cells the merge covers.</returns>
	public static string? RegionOf(int index) => index % RegionRows == 0 ? Regions[(index / RegionRows) % Regions.Length] : null;

	/// <summary>Returns the region of a data row's region, which merged cells show in each of its rows.</summary>
	/// <param name="index">The data row's index, from 0.</param>
	/// <returns>The region's name.</returns>
	public static string ShownRegionOf(int index) => Regions[(index / RegionRows) % Regions.Length];

	/// <summary>Returns whether a data row is hidden.</summary>
	/// <param name="index">The data row's index, from 0.</param>
	/// <returns>Whether its row is hidden.</returns>
	public static bool IsHidden(int index) => index % HiddenEvery == HiddenEvery - 1;

	/// <summary>Returns the sheet rows of each region's merged cells, in order.</summary>
	/// <param name="dataRows">The number of data rows.</param>
	/// <returns>Each region's first and last rows.</returns>
	public static IEnumerable<(int First, int Last)> RegionRanges(int dataRows)
	{
		for (var start = 0; start < dataRows; start += RegionRows)
		{
			yield return (RowOf(start), RowOf(Math.Min(start + RegionRows, dataRows) - 1));
		}
	}

	/// <summary>Returns the block of the header and the data rows: <c>B4:AB</c> and the last row.</summary>
	/// <param name="dataRows">The number of data rows.</param>
	/// <returns>The block, as <c>ExcelReaderOptions.Range</c> takes it.</returns>
	public static string Area(int dataRows) => Block(FirstColumn, HeaderRow, LastColumn, HeaderRow + dataRows);

	/// <summary>Returns the block of the table: its header row and data rows, from the keys' column.</summary>
	/// <param name="dataRows">The number of data rows.</param>
	/// <returns>The block, as the table's <c>ref</c> holds it.</returns>
	public static string TableArea(int dataRows) => Block(KeyColumn, HeaderRow, LastColumn, HeaderRow + dataRows);

	/// <summary>Returns the block of the title's merged cells: row 1, across the report's columns.</summary>
	/// <returns>The block.</returns>
	public static string TitleArea() => Block(FirstColumn, 1, LastColumn, 1);

	/// <summary>Returns a block of cells in Excel's form, such as <c>B4:AB254</c>.</summary>
	/// <param name="firstColumn">The first column, 1-based.</param>
	/// <param name="firstRow">The first row.</param>
	/// <param name="lastColumn">The last column, 1-based.</param>
	/// <param name="lastRow">The last row.</param>
	/// <returns>The block.</returns>
	public static string Block(int firstColumn, int firstRow, int lastColumn, int lastRow) =>
		string.Format(CultureInfo.InvariantCulture, "{0}{1}:{2}{3}", ColumnName(firstColumn), firstRow, ColumnName(lastColumn), lastRow);

	/// <summary>Returns a column's letters: <c>A</c> for 1, <c>AB</c> for 28.</summary>
	/// <param name="column">The column, 1-based.</param>
	/// <returns>Its letters.</returns>
	public static string ColumnName(int column)
	{
		var name = "";
		for (var n = column; n > 0; n = (n - 1) / 26)
		{
			name = (char)('A' + (n - 1) % 26) + name;
		}

		return name;
	}

	/// <summary>
	/// Checks that records can be laid out as the report in Excel's shape: a header of the package assets data's names
	/// (<see cref="Fields"/>), whose widths are Excel's, then at least one data row, each of as many fields.
	/// </summary>
	/// <param name="records">The header, then the data rows.</param>
	/// <exception cref="NotSupportedException">The records are not of that shape, or their last region would be one row,
	/// which Excel doesn't merge.</exception>
	public static void Check(IReadOnlyList<string[]> records)
	{
		if (records.Count < 2 || !records[0].SequenceEqual(Fields.Select(f => f.Name)))
		{
			throw new NotSupportedException("The report lays out a header of the package assets data's names and at least one data row.");
		}

		for (var i = 1; i < records.Count; i++)
		{
			if (records[i].Length != Fields.Length)
			{
				throw new NotSupportedException($"Data row {i} has {records[i].Length} fields; the report's rows have {Fields.Length}.");
			}
		}

		if ((records.Count - 1) % RegionRows == 1)
		{
			throw new NotSupportedException("The last region would be one row, which Excel doesn't merge.");
		}
	}
}
