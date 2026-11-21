using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CsvHelper.Benchmarks;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>
/// The reads of a workbook, as lane C's rows probe timed them: its first sheet inflated alone, <c>Rows</c>, the records,
/// and <c>Rows</c> of a copy with every entry stored, so that <c>rows</c> less <c>rows-stored</c> is inflate's share of a read.
/// </summary>
internal sealed class ReadMethods
{
	/// <summary>The methods, in their default order.</summary>
	public static readonly string[] Names = ["inflate", "rows", "records", "rows-stored"];

	/// <summary>The size of each read from the inflating stream, as lane C's probes read it.</summary>
	private const int ReadSize = 64 * 1024;

	/// <summary>The workbook's bytes.</summary>
	private readonly byte[] workbook;

	/// <summary>The name of the workbook's first sheet's entry.</summary>
	private readonly string sheetEntry;

	/// <summary>Whether the workbook holds the orders (<see cref="OrderRecord"/>) rather than the package assets.</summary>
	private readonly bool orders;

	/// <summary>The buffer <see cref="Inflate"/> reads into.</summary>
	private readonly byte[] buffer = new byte[ReadSize];

	/// <summary>The stored copy's buffer, when <c>rows-stored</c> runs; its first <see cref="storedLength"/> bytes are the copy.</summary>
	private byte[] stored = [];

	/// <summary>The stored copy's length.</summary>
	private int storedLength;

	/// <summary>Reads a workbook, finds its first sheet's entry and tells whose data it holds by its header.</summary>
	/// <param name="path">The workbook's path.</param>
	/// <exception cref="ArgumentException">The file doesn't exist, has no sheet, or holds neither data.</exception>
	public ReadMethods(string path)
	{
		if (!File.Exists(path))
		{
			throw new ArgumentException($"No workbook at {path}.");
		}

		workbook = File.ReadAllBytes(path);
		using (var zip = new ZipArchive(new MemoryStream(workbook, false), ZipArchiveMode.Read))
		{
			var sheets = zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.IndexOf('/', 14) < 0).ToList();
			var sheet = sheets.FirstOrDefault(e => e.FullName == "xl/worksheets/sheet1.xml") ?? sheets.FirstOrDefault()
				?? throw new ArgumentException($"{path} has no worksheet.");
			sheetEntry = sheet.FullName;
			Console.WriteLine(FormattableString.Invariant($"# workbook {Path.GetFileName(path)}: {workbook.Length / 1e6:F1} MB; {sheetEntry} {sheet.Length / 1e6:F1} MB of XML, {sheet.CompressedLength / 1e6:F1} MB deflated"));
		}

		using (var reader = ExcelReader.Create(new MemoryStream(workbook, false)))
		{
			var headers = reader.Headers;
			orders = headers.Contains("CustomerId");
			if (!orders && !headers.Contains("ScanId"))
			{
				throw new ArgumentException($"{path}'s header is neither the package assets' (ScanId, ...) nor the orders' (Id, CustomerId, ...).");
			}
		}

		Console.WriteLine($"# records: {(orders ? nameof(OrderRecord) : nameof(PackageAsset))}");
	}

	/// <summary>Returns the methods named, making the stored copy first if one of them reads it.</summary>
	/// <param name="names">The methods' names, from <see cref="Names"/>.</param>
	/// <returns>The methods, in the order named.</returns>
	public IReadOnlyList<Method> Create(IReadOnlyList<string> names)
	{
		if (names.Contains("rows-stored"))
		{
			Store();
		}

		return names.Select(name => name switch
		{
			"inflate" => new Method(name, "bytes inflated", Inflate),
			"rows" => new Method(name, "fields", () => Rows(workbook, workbook.Length)),
			"records" => new Method(name, "records", orders ? ReadOrders : ReadAssets),
			"rows-stored" => new Method(name, "fields", () => Rows(stored, storedLength)),
			_ => throw new ArgumentException($"Unknown read method '{name}'."),
		}).ToList();
	}

	/// <summary>Sums the fields of every record after the header: the <c>Read</c> loop, nothing mapped.</summary>
	/// <param name="bytes">The buffer that holds the workbook.</param>
	/// <param name="length">The workbook's length.</param>
	/// <returns>The number of fields.</returns>
	private static long Rows(byte[] bytes, int length)
	{
		using var reader = ExcelReader.Create(new MemoryStream(bytes, 0, length, false));
		var fields = 0L;
		while (reader.Read())
		{
			fields += reader.FieldCount;
		}

		return fields;
	}

	/// <summary>Inflates the first sheet's entry to its end, 64 KB a read, keeping nothing.</summary>
	/// <returns>The entry's length.</returns>
	private long Inflate()
	{
		using var zip = new ZipArchive(new MemoryStream(workbook, false), ZipArchiveMode.Read);
		using var stream = zip.GetEntry(sheetEntry)!.Open();
		var total = 0L;
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			total += read;
		}

		return total;
	}

	/// <summary>Reads the package assets' records.</summary>
	/// <returns>The number of records.</returns>
	private long ReadAssets()
	{
		using var reader = ExcelReader.Create(new MemoryStream(workbook, false));

		return reader.GetRecords<PackageAsset>().ToList().Count;
	}

	/// <summary>Reads the orders' records.</summary>
	/// <returns>The number of records.</returns>
	private long ReadOrders()
	{
		using var reader = ExcelReader.Create(new MemoryStream(workbook, false));

		return reader.GetRecords<OrderRecord>().ToList().Count;
	}

	/// <summary>
	/// Makes the stored copy: every entry of the workbook re-zipped at <see cref="CompressionLevel.NoCompression"/>, in
	/// memory, so that reading it inflates nothing.
	/// </summary>
	private void Store()
	{
		var watch = Stopwatch.StartNew();
		using var input = new ZipArchive(new MemoryStream(workbook, false), ZipArchiveMode.Read);

		// Sized from the entries, so that a copy past 1 GiB isn't grown to 2 GB by doubling.
		var capacity = 64 * 1024L;
		foreach (var entry in input.Entries)
		{
			capacity += entry.Length + (2 * entry.FullName.Length) + 256;
		}

		var output = new MemoryStream(checked((int)capacity));
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
		{
			foreach (var entry in input.Entries)
			{
				var copy = zip.CreateEntry(entry.FullName, CompressionLevel.NoCompression);
				using var from = entry.Open();
				using var to = copy.Open();
				from.CopyTo(to);
			}
		}

		stored = output.GetBuffer();
		storedLength = checked((int)output.Length);
		Console.WriteLine(FormattableString.Invariant($"# stored copy: {storedLength / 1e6:F1} MB, made in {watch.Elapsed.TotalMilliseconds:F0} ms"));
	}
}
