using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Writes the workbooks the Excel benchmarks read into the cache before any benchmark runs, and deletes other keys'
/// workbooks when asked.
/// </summary>
/// <remarks>
/// BenchmarkDotNet runs each benchmark in a process of its own, and its host runs setups too. Writing every missing
/// workbook first, in the process that starts them, means that no benchmark process writes one while another reads.
/// The cache never deletes a file of another key by itself (see <see cref="ExcelDataGenerator"/>), so worktrees of
/// different commits keep their workbooks side by side; <see cref="Clean"/> removes them.
/// </remarks>
public static class BenchWorkbooks
{
	/// <summary>Gets the cache key of this build.</summary>
	public static string Key => ExcelDataGenerator.Key;

	/// <summary>Writes every workbook the benchmarks read that the cache doesn't hold under this build's key.</summary>
	public static void Ensure()
	{
		ExcelDataGenerator.PackageAssetsPath(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Shared);
		ExcelDataGenerator.PackageAssetsPath(ExcelDataGenerator.PackageAssetRows, ExcelStrings.Inline);
		ExcelDataGenerator.PackageAssetsReportPath(ExcelDataGenerator.PackageAssetRows);
		ExcelDataGenerator.OrdersPath(ExcelDataGenerator.OrderRows);
		ExcelDataGenerator.OrdersPath(100_000);
		ExcelDataGenerator.OrdersWithErrorsPath(ExcelDataGenerator.OrderRows);
	}

	/// <summary>Deletes every cached file that isn't under this build's key, and leftover temporary files.</summary>
	/// <returns>The number of files deleted.</returns>
	public static int Clean()
	{
		if (!Directory.Exists(ExcelDataGenerator.CacheFolder))
		{
			return 0;
		}

		var deleted = 0;
		foreach (var path in Directory.EnumerateFiles(ExcelDataGenerator.CacheFolder))
		{
			var name = Path.GetFileName(path);
			if (name.Contains(Key, StringComparison.Ordinal) && !name.EndsWith(".tmp", StringComparison.Ordinal))
			{
				continue;
			}

			File.Delete(path);
			Console.WriteLine($"Deleted {name}");
			deleted++;
		}

		return deleted;
	}
}
