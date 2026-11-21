using System;
using System.IO;
using System.Linq;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>
/// A record with one string per column of NCsvPerf's package assets data, as the benchmarks' <c>PackageAsset</c> is: its
/// generated mapper matches the fields to the header by name. The benchmarks' type can't be linked, since its file uses
/// .NET 9's <c>allows ref struct</c>.
/// </summary>
[CsvRecord]
public sealed partial class PackageAsset
{
	/// <summary>The columns, each the field of its header's name.</summary>
	public string ScanId = "", ScanTimestamp = "", Id = "", Version = "", Created = "", ResultType = "", PatternSet = "",
		PropertyAnyValue = "", PropertyCodeLanguage = "", PropertyTargetFrameworkMoniker = "", PropertyLocale = "",
		PropertyManagedAssembly = "", PropertyMSBuild = "", PropertyRuntimeIdentifier = "", PropertySatelliteAssembly = "",
		Path = "", FileName = "", FileExtension = "", TopLevelFolder = "", RoundTripTargetFrameworkMoniker = "",
		FrameworkName = "", FrameworkVersion = "", FrameworkProfile = "", PlatformName = "", PlatformVersion = "";
}

/// <summary>NCsvPerf's package assets data as <c>ExcelPackageAssetsWrite</c> writes it: a header and a million rows of fields.</summary>
internal static class PackageAssetData
{
	/// <summary>The number of data rows: a million, as <c>ExcelDataGenerator.PackageAssetRows</c>.</summary>
	public const int Rows = 1_000_000;

	/// <summary>The sheet's name, as <c>ExcelDataGenerator.SheetName</c>.</summary>
	public const string SheetName = "PackageAssets";

	/// <summary>The header, as <c>DataGenerator.PackageAssetHeader</c>: <see cref="PackageAsset"/>'s fields, in the data's order.</summary>
	public static readonly string[] Header =
	[
		"ScanId", "ScanTimestamp", "Id", "Version", "Created", "ResultType", "PatternSet", "PropertyAnyValue",
		"PropertyCodeLanguage", "PropertyTargetFrameworkMoniker", "PropertyLocale", "PropertyManagedAssembly",
		"PropertyMSBuild", "PropertyRuntimeIdentifier", "PropertySatelliteAssembly", "Path", "FileName",
		"FileExtension", "TopLevelFolder", "RoundTripTargetFrameworkMoniker", "FrameworkName", "FrameworkVersion",
		"FrameworkProfile", "PlatformName", "PlatformVersion",
	];

	/// <summary>
	/// Returns the data rows: NCsvPerf's lines, repeated to a million, as <c>ExcelDataGenerator.PackageAssetRecords</c>
	/// gives them. The rows of each copy of the lines share their arrays.
	/// </summary>
	/// <returns>The rows of fields, without the header.</returns>
	public static string[][] Load()
	{
		// NCsvPerf's lines have no quotes, so each comma ends a field.
		var path = Path.Combine(AppContext.BaseDirectory, "Data", "PackageAssets.csv");
		var fields = File.ReadAllLines(path).Select(line => line.Split(',')).ToArray();
		var rows = new string[Rows][];
		for (var i = 0; i < rows.Length; i++)
		{
			rows[i] = fields[i % fields.Length];
		}

		return rows;
	}
}
