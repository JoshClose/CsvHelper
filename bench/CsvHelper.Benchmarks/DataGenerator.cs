using System.Globalization;
using System.Text;

namespace CsvHelper.Benchmarks;

/// <summary>Deterministic synthetic data sets modelled on common real-world CSV shapes.</summary>
public static class DataGenerator
{
	public const int DefaultRows = 50_000;

	private static readonly string[] PackageIds =
	[
		"Newtonsoft.Json", "Microsoft.Extensions.Logging", "System.Text.Json", "Serilog", "AutoMapper", "Dapper",
		"xunit", "NUnit", "Moq", "Polly", "FluentValidation", "MediatR", "Swashbuckle.AspNetCore",
		"Microsoft.EntityFrameworkCore", "StackExchange.Redis", "Grpc.Net.Client", "Azure.Storage.Blobs",
		"Microsoft.Data.SqlClient", "NodaTime", "Humanizer.Core", "CsvHelper", "Sep", "Sylvan.Data.Csv",
		"BenchmarkDotNet", "Microsoft.Identity.Client", "System.Reactive", "Castle.Core", "RestSharp",
	];

	private static readonly (string Short, string Moniker, string Name, string Version)[] Frameworks =
	[
		("net45", ".NETFramework,Version=v4.5", ".NETFramework", "4.5.0.0"),
		("net461", ".NETFramework,Version=v4.6.1", ".NETFramework", "4.6.1.0"),
		("netstandard1.3", ".NETStandard,Version=v1.3", ".NETStandard", "1.3.0.0"),
		("netstandard2.0", ".NETStandard,Version=v2.0", ".NETStandard", "2.0.0.0"),
		("netcoreapp3.1", ".NETCoreApp,Version=v3.1", ".NETCoreApp", "3.1.0.0"),
		("net6.0", ".NETCoreApp,Version=v6.0", ".NETCoreApp", "6.0.0.0"),
		("net8.0", ".NETCoreApp,Version=v8.0", ".NETCoreApp", "8.0.0.0"),
	];

	private static readonly string[] PatternSets = ["CompileTimeAssembly", "RuntimeAssembly", "ResourceAssembly", "NativeLibrary", "MSBuildFiles"];
	private static readonly string[] Locales = ["", "", "", "", "en-US", "de", "fr", "ja", "zh-Hans"];
	private static readonly string[] Rids = ["", "", "", "win-x64", "linux-x64", "osx-arm64"];

	public static readonly string[] PackageAssetHeader =
	[
		"ScanId", "ScanTimestamp", "Id", "Version", "Created", "ResultType", "PatternSet", "PropertyAnyValue",
		"PropertyCodeLanguage", "PropertyTargetFrameworkMoniker", "PropertyLocale", "PropertyManagedAssembly",
		"PropertyMSBuild", "PropertyRuntimeIdentifier", "PropertySatelliteAssembly", "Path", "FileName",
		"FileExtension", "TopLevelFolder", "RoundTripTargetFrameworkMoniker", "FrameworkName", "FrameworkVersion",
		"FrameworkProfile", "PlatformName", "PlatformVersion",
	];

	/// <summary>25 short, mostly repetitive text columns (like the NuGet package assets data set).</summary>
	public static string PackageAssets(int rows = DefaultRows, int seed = 42)
	{
		var rng = new Random(seed);
		var sb = new StringBuilder(rows * 330);
		sb.AppendJoin(',', PackageAssetHeader).Append('\n');
		var scanId = Guid.Empty;
		var scanTime = new DateTimeOffset(2020, 11, 28, 1, 50, 41, TimeSpan.Zero);
		for (var r = 0; r < rows; r++)
		{
			if (r % 40 == 0)
			{
				var bytes = new byte[16];
				rng.NextBytes(bytes);
				scanId = new Guid(bytes);
				scanTime = scanTime.AddSeconds(rng.Next(1, 3600)).AddTicks(rng.Next(10_000_000));
			}

			var id = PackageIds[rng.Next(PackageIds.Length)];
			var version = $"{rng.Next(1, 14)}.{rng.Next(0, 10)}.{rng.Next(0, 20)}";
			var created = new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(rng.Next(3_000_000));
			var fw = Frameworks[rng.Next(Frameworks.Length)];
			var patternSet = PatternSets[rng.Next(PatternSets.Length)];
			var locale = Locales[rng.Next(Locales.Length)];
			var rid = Rids[rng.Next(Rids.Length)];
			var managed = rng.Next(3) != 0;
			var fileName = id + (managed ? ".dll" : ".xml");
			var path = $"lib/{fw.Short}/{fileName}";
			sb.Append(scanId.ToString("D")).Append(',')
			  .Append(scanTime.ToString("O", CultureInfo.InvariantCulture)).Append(',')
			  .Append(id).Append(',')
			  .Append(version).Append(',')
			  .Append(created.ToString("O", CultureInfo.InvariantCulture)).Append(',')
			  .Append("Available").Append(',')
			  .Append(patternSet).Append(',')
			  .Append(rng.Next(10) == 0 ? "any" : "").Append(',')
			  .Append(rng.Next(20) == 0 ? "cs" : "").Append(',')
			  .Append(fw.Short).Append(',')
			  .Append(locale).Append(',')
			  .Append(managed ? fileName : "").Append(',')
			  .Append(rng.Next(15) == 0 ? "build" : "").Append(',')
			  .Append(rid).Append(',')
			  .Append(locale.Length > 0 ? id + ".resources.dll" : "").Append(',')
			  .Append(path).Append(',')
			  .Append(fileName).Append(',')
			  .Append(managed ? ".dll" : ".xml").Append(',')
			  .Append("lib").Append(',')
			  .Append('"').Append(fw.Moniker).Append('"').Append(',')
			  .Append(fw.Name).Append(',')
			  .Append(fw.Version).Append(',')
			  .Append("").Append(',')
			  .Append(rid.Length > 0 ? "Windows" : "").Append(',')
			  .Append(rid.Length > 0 ? "10.0" : "")
			  .Append('\n');
		}

		return sb.ToString();
	}

	/// <summary>The header and records of <see cref="PackageAssets"/>, as values.</summary>
	public static List<string[]> PackageAssetRecords(int rows = DefaultRows)
	{
		using var reader = CsvReader.FromText(PackageAssets(rows), new CsvReaderOptions { HasHeader = false });
		var records = new List<string[]>();
		while (reader.Read())
		{
			var record = new string[reader.FieldCount];
			for (var i = 0; i < record.Length; i++) record[i] = reader.GetString(i);
			records.Add(record);
		}

		return records;
	}

	/// <summary>
	/// The records of <see cref="PackageAssets"/> with Windows paths (<c>lib\net45\x.dll</c>), so that values contain
	/// backslashes as well as commas: the records for the Escape mode benchmarks.
	/// </summary>
	public static List<string[]> PackageAssetRecordsWindowsPaths(int rows = DefaultRows)
	{
		var records = PackageAssetRecords(rows);
		for (var r = 1; r < records.Count; r++) records[r][15] = records[r][15].Replace('/', '\\');

		return records;
	}

	/// <summary>
	/// The package assets data set in Escape mode, as MySQL and similar tools write it: nothing quoted, a backslash before
	/// every comma and backslash in a value (the framework moniker and the Windows paths), LF line ends.
	/// </summary>
	public static string PackageAssetsEscaped(int rows = DefaultRows)
	{
		var sb = new StringBuilder(rows * 340);
		foreach (var record in PackageAssetRecordsWindowsPaths(rows))
		{
			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0) sb.Append(',');
				foreach (var c in record[i])
				{
					if (c is ',' or '\\') sb.Append('\\');
					sb.Append(c);
				}
			}

			sb.Append('\n');
		}

		return sb.ToString();
	}

	/// <summary>The package assets data set tab separated, with nothing quoted or escaped (NoEscape mode), LF line ends.</summary>
	public static string PackageAssetsTsv(int rows = DefaultRows)
	{
		var sb = new StringBuilder(rows * 330);
		foreach (var record in PackageAssetRecords(rows)) sb.AppendJoin('\t', record).Append('\n');

		return sb.ToString();
	}

	/// <summary>The width of each column of <see cref="PackageAssetsFixedWidth"/>: its longest value.</summary>
	public static int[] FixedWidths(List<string[]> records)
	{
		var widths = new int[records[0].Length];
		foreach (var record in records)
		{
			for (var i = 0; i < widths.Length; i++) widths[i] = Math.Max(widths[i], record[i].Length);
		}

		return widths;
	}

	/// <summary>
	/// The package assets data set in fixed-width columns, each as wide as its longest value, padded with spaces, LF line
	/// ends.
	/// </summary>
	public static string PackageAssetsFixedWidth(out int[] widths, int rows = DefaultRows)
	{
		var records = PackageAssetRecords(rows);
		widths = FixedWidths(records);
		var sb = new StringBuilder(rows * widths.Sum());
		foreach (var record in records)
		{
			for (var i = 0; i < record.Length; i++) sb.Append(record[i].PadRight(widths[i]));
			sb.Append('\n');
		}

		return sb.ToString();
	}

	public const int FloatColumns = 25;

	/// <summary>3 text columns followed by 25 single precision float columns (like an ML feature file).</summary>
	public static string Floats(int rows = DefaultRows, int seed = 42)
	{
		var rng = new Random(seed);
		var sb = new StringBuilder(rows * 280);
		sb.Append("Set,FileName,DataSplit");
		for (var i = 0; i < FloatColumns; i++) sb.Append(",Feature").Append(i);
		sb.Append('\n');
		string[] splits = ["Train", "Test", "Validation"];
		for (var r = 0; r < rows; r++)
		{
			sb.Append("SetA,img_").Append(r.ToString("D8", CultureInfo.InvariantCulture)).Append(".png,").Append(splits[rng.Next(3)]);
			for (var i = 0; i < FloatColumns; i++)
			{
				var v = (float)(rng.NextDouble() * (rng.Next(4) == 0 ? 1000 : 1)) * (rng.Next(5) == 0 ? -1 : 1);
				sb.Append(',').Append(v.ToString(CultureInfo.InvariantCulture));
			}

			sb.Append('\n');
		}

		return sb.ToString();
	}

	private static readonly string[] FirstNames = ["Ada", "Grace", "Linus", "Barbara", "Ken", "Margaret", "Dennis", "Frances", "Edsger", "Radia"];
	private static readonly string[] LastNames = ["Lovelace", "Hopper", "Torvalds", "Liskov", "Thompson", "Hamilton", "Ritchie", "Allen", "Dijkstra", "Perlman"];
	private static readonly string[] Words =
	[
		"the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "lorem", "ipsum", "dolor", "sit", "amet",
		"consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et",
	];

	/// <summary>Text heavy rows where every text field is quoted and many contain separators, quotes and line breaks.</summary>
	public static string Quoted(int rows = DefaultRows, int seed = 42)
	{
		var rng = new Random(seed);
		var sb = new StringBuilder(rows * 220);
		sb.Append("Id,Name,Email,Address,Notes,Amount,Date\r\n");
		for (var r = 0; r < rows; r++)
		{
			var first = FirstNames[rng.Next(FirstNames.Length)];
			var last = LastNames[rng.Next(LastNames.Length)];
			sb.Append(r).Append(",\"")
			  .Append(last).Append(", ").Append(first).Append("\",\"")
			  .Append(first.ToLowerInvariant()).Append('.').Append(last.ToLowerInvariant()).Append("@example.com\",\"")
			  .Append(rng.Next(1, 9999)).Append(' ').Append(Words[rng.Next(Words.Length)]).Append(" St");
			if (rng.Next(4) == 0) sb.Append("\r\nApt ").Append(rng.Next(1, 300));
			sb.Append("\",\"");
			var words = rng.Next(3, 14);
			for (var w = 0; w < words; w++)
			{
				if (w > 0) sb.Append(rng.Next(6) == 0 ? ", " : " ");
				if (rng.Next(12) == 0) sb.Append("\"\"").Append(Words[rng.Next(Words.Length)]).Append("\"\"");
				else sb.Append(Words[rng.Next(Words.Length)]);
			}

			sb.Append("\",")
			  .Append((rng.Next(1, 1_000_000) / 100.0).ToString("F2", CultureInfo.InvariantCulture)).Append(',')
			  .Append(new DateTime(2020, 1, 1).AddDays(rng.Next(2000)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
			  .Append("\r\n");
		}

		return sb.ToString();
	}
}

/// <summary>Something that can return the string value of a column of the current record.</summary>
public interface IColumnSource
{
	public string Get(int index);
}

/// <summary>
/// A record with one string per column of the package assets data set. CsvHelper can also read it with its
/// generated mapper ([CsvRecord]: the fields are matched to the header by name).
/// </summary>
[CsvRecord]
public sealed partial class PackageAsset
{
	public string ScanId = "", ScanTimestamp = "", Id = "", Version = "", Created = "", ResultType = "", PatternSet = "",
		PropertyAnyValue = "", PropertyCodeLanguage = "", PropertyTargetFrameworkMoniker = "", PropertyLocale = "",
		PropertyManagedAssembly = "", PropertyMSBuild = "", PropertyRuntimeIdentifier = "", PropertySatelliteAssembly = "",
		Path = "", FileName = "", FileExtension = "", TopLevelFolder = "", RoundTripTargetFrameworkMoniker = "",
		FrameworkName = "", FrameworkVersion = "", FrameworkProfile = "", PlatformName = "", PlatformVersion = "";

	/// <summary>Builds a record from a source of column strings. Specialized per source type (no virtual calls).</summary>
	public static PackageAsset Create<T>(ref T s) where T : IColumnSource, allows ref struct => new()
	{
		ScanId = s.Get(0), ScanTimestamp = s.Get(1), Id = s.Get(2), Version = s.Get(3),
		Created = s.Get(4), ResultType = s.Get(5), PatternSet = s.Get(6), PropertyAnyValue = s.Get(7),
		PropertyCodeLanguage = s.Get(8), PropertyTargetFrameworkMoniker = s.Get(9), PropertyLocale = s.Get(10),
		PropertyManagedAssembly = s.Get(11), PropertyMSBuild = s.Get(12), PropertyRuntimeIdentifier = s.Get(13),
		PropertySatelliteAssembly = s.Get(14), Path = s.Get(15), FileName = s.Get(16),
		FileExtension = s.Get(17), TopLevelFolder = s.Get(18), RoundTripTargetFrameworkMoniker = s.Get(19),
		FrameworkName = s.Get(20), FrameworkVersion = s.Get(21), FrameworkProfile = s.Get(22),
		PlatformName = s.Get(23), PlatformVersion = s.Get(24),
	};

	public string Get(int i) => i switch
	{
		0 => ScanId, 1 => ScanTimestamp, 2 => Id, 3 => Version, 4 => Created, 5 => ResultType, 6 => PatternSet,
		7 => PropertyAnyValue, 8 => PropertyCodeLanguage, 9 => PropertyTargetFrameworkMoniker, 10 => PropertyLocale,
		11 => PropertyManagedAssembly, 12 => PropertyMSBuild, 13 => PropertyRuntimeIdentifier, 14 => PropertySatelliteAssembly,
		15 => Path, 16 => FileName, 17 => FileExtension, 18 => TopLevelFolder, 19 => RoundTripTargetFrameworkMoniker,
		20 => FrameworkName, 21 => FrameworkVersion, 22 => FrameworkProfile, 23 => PlatformName, _ => PlatformVersion,
	};
}
