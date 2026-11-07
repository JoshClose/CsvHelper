using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// The Excel read benchmarks' data, as CSV text and as workbooks: NCsvPerf's package assets data
/// (<c>Data/PackageAssets.csv</c>, see <c>Data/README.md</c>), plain and laid out as a report (<see cref="ExcelReport"/>),
/// and the orders of <see cref="ExcelOrders"/>.
/// </summary>
/// <remarks>
/// <para>
/// The package assets rows are NCsvPerf's 1,695 lines repeated the way its <c>LineCache</c> repeats them: as many whole
/// copies as fit, then the first lines again. A header row, <see cref="DataGenerator.PackageAssetHeader"/>, comes first,
/// so that records can be read by name.
/// </para>
/// <para>
/// Writing a workbook of a million rows takes seconds, so the workbooks are cached under <c>%TEMP%\csvhelper-bench</c>,
/// keyed by the data, the row count, the strings variant, and a hash of the CSV file and of the generator's source
/// (embedded in this assembly), so that a change to either writes them again.
/// </para>
/// </remarks>
public static class ExcelDataGenerator
{
	/// <summary>The number of data rows the benchmarks read: NCsvPerf's PackageAssets benchmark reads a million lines.</summary>
	public const int PackageAssetRows = 1_000_000;

	/// <summary>The name of the workbook's sheet, the one Excel gave it when it opened NCsvPerf's file.</summary>
	public const string SheetName = "PackageAssets";

	/// <summary>The number of orders the typed benchmark reads: a million, as the package assets benchmark reads.</summary>
	public const int OrderRows = 1_000_000;

	/// <summary>The prefix of the generator's source files among this assembly's resources.</summary>
	private const string SourcePrefix = "ExcelGenerator/";

	/// <summary>NCsvPerf's lines, read once.</summary>
	private static readonly Lazy<string[]> Lines = new(() => File.ReadAllLines(DataPath("PackageAssets.csv")));

	/// <summary>The cache key's hash of the CSV file and the generator's source, computed once.</summary>
	private static readonly Lazy<string> SourceHash = new(HashSources);

	/// <summary>Returns the path of a file in the benchmarks' <c>Data</c> folder.</summary>
	/// <param name="name">The file's name.</param>
	/// <returns>Its path in the output folder.</returns>
	public static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

	/// <summary>Returns the data rows: NCsvPerf's lines, repeated to a row count.</summary>
	/// <param name="rows">The number of rows.</param>
	/// <returns>The lines, without the header.</returns>
	public static List<string> PackageAssetLines(int rows)
	{
		var source = Lines.Value;
		var lines = new List<string>(rows);
		for (var copy = 0; copy < rows / source.Length; copy++)
		{
			lines.AddRange(source);
		}

		lines.AddRange(source.Take(rows % source.Length));

		return lines;
	}

	/// <summary>Returns the header and data rows as CSV text, with LF line ends.</summary>
	/// <param name="rows">The number of data rows.</param>
	/// <returns>The text.</returns>
	public static string PackageAssetsCsv(int rows)
	{
		var builder = new StringBuilder(rows * 310);
		builder.AppendJoin(',', DataGenerator.PackageAssetHeader).Append('\n');

		foreach (var line in PackageAssetLines(rows))
		{
			builder.Append(line).Append('\n');
		}

		return builder.ToString();
	}

	/// <summary>Returns the header and data rows as fields. The rows of each copy of the data share their arrays.</summary>
	/// <param name="rows">The number of data rows.</param>
	/// <returns>The rows of fields.</returns>
	public static List<string[]> PackageAssetRecords(int rows)
	{
		// NCsvPerf's lines have no quotes, so each comma ends a field.
		var fields = Lines.Value.Select(line => line.Split(',')).ToArray();
		var records = new List<string[]>(rows + 1) { DataGenerator.PackageAssetHeader };
		for (var i = 0; i < rows; i++)
		{
			records.Add(fields[i % fields.Length]);
		}

		return records;
	}

	/// <summary>Returns the header and data rows as a workbook, from the cache or written into it.</summary>
	/// <param name="rows">The number of data rows.</param>
	/// <param name="strings">Where the cells' strings go.</param>
	/// <returns>The workbook's bytes.</returns>
	public static byte[] PackageAssets(int rows, ExcelStrings strings) =>
		Cached(
			$"packageassets-{rows}-{strings.ToString().ToLowerInvariant()}-",
			stream => ExcelTextWorkbook.Write(stream, SheetName, PackageAssetRecords(rows), strings, CompressionLevel.Optimal));

	/// <summary>
	/// Returns the header and data rows laid out as a report (see <see cref="ExcelReport"/>), from the cache or written
	/// into it.
	/// </summary>
	/// <param name="rows">The number of data rows.</param>
	/// <returns>The workbook's bytes.</returns>
	public static byte[] PackageAssetsReport(int rows) =>
		Cached($"packageassets-report-{rows}-", stream => ExcelReportWorkbook.Write(stream, PackageAssetRecords(rows), CompressionLevel.Optimal));

	/// <summary>Returns the orders as the cells hold them, as CSV text with a header, written by CsvHelper.</summary>
	/// <param name="rows">The number of orders.</param>
	/// <returns>The text.</returns>
	public static string OrdersCsv(int rows)
	{
		using var writer = new StringWriter(CultureInfo.InvariantCulture);
		using (var csv = CsvWriter.Create(writer, leaveOpen: true))
		{
			csv.WriteRecords(ExcelOrders.Generate(rows));
		}

		return writer.ToString();
	}

	/// <summary>Returns the orders as a workbook, from the cache or written into it.</summary>
	/// <param name="rows">The number of orders.</param>
	/// <returns>The workbook's bytes.</returns>
	public static byte[] Orders(int rows) =>
		Cached(
			$"orders-{rows}-",
			stream => ExcelTypedWorkbook.Write(stream, ExcelOrders.SheetName, ExcelOrders.Rows(ExcelOrders.Generate(rows)), ExcelOrders.Formats, CompressionLevel.Optimal));

	/// <summary>Returns a workbook from the cache, or writes it and caches it.</summary>
	/// <param name="prefix">The start of the cached file's name: the data, the row count and the variant.</param>
	/// <param name="write">Writes the workbook.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Cached(string prefix, Action<Stream> write)
	{
		var folder = Path.Combine(Path.GetTempPath(), "csvhelper-bench");
		var path = Path.Combine(folder, $"{prefix}{SourceHash.Value}.xlsx");
		if (File.Exists(path))
		{
			return File.ReadAllBytes(path);
		}

		Directory.CreateDirectory(folder);

		using var stream = new MemoryStream();
		write(stream);
		var bytes = stream.ToArray();

		// Written under a name of its own and then moved, so that a run stopped halfway leaves no partial workbook.
		var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
		File.WriteAllBytes(temporary, bytes);
		File.Move(temporary, path, overwrite: true);

		foreach (var stale in Directory.EnumerateFiles(folder, $"{prefix}*.xlsx"))
		{
			if (!string.Equals(stale, path, StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(stale);
			}
		}

		return bytes;
	}

	/// <summary>Hashes the CSV file and the generator's source files, for the cache key.</summary>
	/// <returns>The first 8 bytes of their SHA-256, in hexadecimal.</returns>
	/// <exception cref="InvalidOperationException">The source files aren't embedded.</exception>
	private static string HashSources()
	{
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		hash.AppendData(File.ReadAllBytes(DataPath("PackageAssets.csv")));

		var assembly = typeof(ExcelDataGenerator).Assembly;
		var sources = assembly.GetManifestResourceNames()
			.Where(name => name.StartsWith(SourcePrefix, StringComparison.Ordinal))
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToList();
		if (sources.Count == 0)
		{
			throw new InvalidOperationException("The generator's source files aren't embedded in the benchmark assembly.");
		}

		foreach (var name in sources)
		{
			using var resource = assembly.GetManifestResourceStream(name)!;
			using var copy = new MemoryStream();
			resource.CopyTo(copy);
			hash.AppendData(Encoding.UTF8.GetBytes(name));
			hash.AppendData(copy.ToArray());
		}

		return Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant();
	}
}
