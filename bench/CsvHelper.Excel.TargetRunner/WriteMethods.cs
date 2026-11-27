using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CsvHelper.Benchmarks;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>
/// The writes of the package assets or of the orders, as <c>ExcelPackageAssetsWrite.CsvHelper_Write</c> and
/// <c>ExcelOrdersWrite.CsvHelper_Write</c> write them, at the defaults and stored (deflate's share is the difference); and
/// the writer's sheet XML through a <see cref="ZipArchive"/> entry alone, as lane E's <c>archive</c> probe wrote it, for
/// the zip's own share. When named: the default write with <see cref="ExcelWriterOptions.CompressionThreads"/> at
/// <see cref="ParallelDegree"/>.
/// </summary>
internal sealed class WriteMethods
{
	/// <summary>The methods, in their default order.</summary>
	public static readonly string[] Names = ["write", "write-stored", "zip", "zip-stored"];

	/// <summary>The methods run only when named.</summary>
	public static readonly string[] Optional = ["write-threads"];

	/// <summary>The deflate threads of <c>write-threads</c>.</summary>
	public const int ParallelDegree = 8;

	/// <summary>The number of orders: a million, as <c>ExcelDataGenerator.OrderRows</c>.</summary>
	private const int OrderRows = 1_000_000;

	/// <summary>The size of the writes into the zip entry, as the sheet sink makes them (<c>SheetSink.FlushSize</c>).</summary>
	private const int WriteSize = 64 * 1024;

	/// <summary>The zip stage's entry, the writer's first sheet's.</summary>
	private const string SheetEntry = "xl/worksheets/sheet1.xml";

	/// <summary>The date of the zip stage's entry, as the writer dates every entry.</summary>
	private static readonly DateTimeOffset EntryDate = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

	/// <summary>Whether the orders are written rather than the package assets.</summary>
	private readonly bool orders;

	/// <summary>The package assets' rows, when they are written.</summary>
	private readonly string[][] rows = [];

	/// <summary>The orders, when they are written.</summary>
	private readonly OrderRecord[] orderRecords = [];

	/// <summary>The writer's sheet XML, captured when a zip method runs.</summary>
	private byte[] sheetXml = [];

	/// <summary>Builds the data written.</summary>
	/// <param name="data"><c>assets</c> or <c>orders</c>.</param>
	/// <exception cref="ArgumentException">The data is neither.</exception>
	public WriteMethods(string data)
	{
		var watch = Stopwatch.StartNew();
		switch (data)
		{
			case "assets":
				rows = PackageAssetData.Load();
				Console.WriteLine(FormattableString.Invariant($"# data: {rows.Length:N0} package assets rows of {PackageAssetData.Header.Length} fields, loaded in {watch.Elapsed.TotalMilliseconds:F0} ms"));

				break;
			case "orders":
				orders = true;
				orderRecords = ExcelOrders.Generate(OrderRows);
				Console.WriteLine(FormattableString.Invariant($"# data: {orderRecords.Length:N0} orders, checksum {Checksum(orderRecords):x16}, generated in {watch.Elapsed.TotalMilliseconds:F0} ms"));

				break;
			default:
				throw new ArgumentException($"Unknown data '{data}': write assets or orders.");
		}
	}

	/// <summary>Returns the methods named, capturing the sheet XML first if one of them writes it.</summary>
	/// <param name="names">The methods' names, from <see cref="Names"/>.</param>
	/// <returns>The methods, in the order named.</returns>
	public IReadOnlyList<Method> Create(IReadOnlyList<string> names)
	{
		if (names.Contains("zip") || names.Contains("zip-stored"))
		{
			Capture();
		}

		return names.Select(name => name switch
		{
			"write" => new Method(name, "bytes written", () => Write(Options(stored: false))),
			"write-stored" => new Method(name, "bytes written", () => Write(Options(stored: true))),
			"write-threads" => new Method(name, "bytes written", () => Write(Options(stored: false) with { CompressionThreads = ParallelDegree })),
			"zip" => new Method(name, "bytes written", () => Zip(CompressionLevel.Fastest)),
			"zip-stored" => new Method(name, "bytes written", () => Zip(CompressionLevel.NoCompression)),
			_ => throw new ArgumentException($"Unknown write method '{name}'."),
		}).ToList();
	}

	/// <summary>
	/// A checksum of the orders' values, so that runs on different targets can tell they wrote the same orders (.NET
	/// Framework's <c>NextInt64</c> is <c>RandomCompat</c>'s).
	/// </summary>
	/// <param name="records">The orders.</param>
	/// <returns>An FNV-1a hash of every member's value.</returns>
	private static ulong Checksum(OrderRecord[] records)
	{
		var hash = 14695981039346656037UL;
		foreach (var order in records)
		{
			Mix(ref hash, order.Id);
			Mix(ref hash, order.CustomerId.ToString());
			Mix(ref hash, order.Customer);
			Mix(ref hash, order.OrderDate.Ticks);
			Mix(ref hash, order.ShippedAt.UtcTicks);
			foreach (var part in decimal.GetBits(order.Total))
			{
				Mix(ref hash, part);
			}

			Mix(ref hash, BitConverter.DoubleToInt64Bits(order.Weight));
			Mix(ref hash, order.Quantity);
			Mix(ref hash, order.Paid ? 1 : 0);
			Mix(ref hash, order.Status);
			Mix(ref hash, order.Sku);
			Mix(ref hash, order.Notes);
		}

		return hash;
	}

	/// <summary>Mixes a number's eight bytes into an FNV-1a hash.</summary>
	/// <param name="hash">The hash.</param>
	/// <param name="value">The number.</param>
	private static void Mix(ref ulong hash, long value)
	{
		for (var i = 0; i < 8; i++)
		{
			hash = (hash ^ (byte)(value >> (8 * i))) * 1099511628211UL;
		}
	}

	/// <summary>Mixes a string's length and characters into an FNV-1a hash.</summary>
	/// <param name="hash">The hash.</param>
	/// <param name="value">The string.</param>
	private static void Mix(ref ulong hash, string value)
	{
		Mix(ref hash, value.Length);
		foreach (var c in value)
		{
			Mix(ref hash, c);
		}
	}

	/// <summary>
	/// Returns the workbook's options: the defaults (the orders' with <c>ShippedAt</c> as its UTC date), at the default
	/// level or stored.
	/// </summary>
	/// <param name="stored">Whether every entry is stored (<see cref="CompressionLevel.NoCompression"/>).</param>
	/// <returns>The options.</returns>
	private ExcelWriterOptions Options(bool stored)
	{
		var options = orders ? new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc } : new ExcelWriterOptions();

		return stored ? options with { CompressionLevel = CompressionLevel.NoCompression } : options;
	}

	/// <summary>Writes the workbook to a stream that counts and drops its bytes.</summary>
	/// <param name="options">The workbook's options.</param>
	/// <returns>The bytes written.</returns>
	private long Write(ExcelWriterOptions options)
	{
		var output = new DiscardStream();
		WriteTo(output, options);

		return output.Count;
	}

	/// <summary>
	/// Writes the workbook: the package assets' header and each row's fields through the sheet's <see cref="CsvWriter"/>,
	/// in <c>ExcelPackageAssetsWrite.WriteRows</c>'s loop, or the orders through
	/// <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>.
	/// </summary>
	/// <param name="output">The stream written to, left open.</param>
	/// <param name="options">The workbook's options.</param>
	private void WriteTo(Stream output, ExcelWriterOptions options)
	{
		using var workbook = ExcelWorkbookWriter.Create(output, options, leaveOpen: true);
		if (orders)
		{
			workbook.WriteSheet(ExcelOrders.SheetName, orderRecords);

			return;
		}

		using var writer = workbook.AddSheet(PackageAssetData.SheetName);
		writer.WriteHeader(PackageAssetData.Header);
		foreach (var row in rows)
		{
			for (var i = 0; i < row.Length; i++)
			{
				writer.WriteField(row[i]);
			}

			writer.EndRecord();
		}
	}

	/// <summary>Writes the captured sheet XML into a zip entry, in 64 KB writes, to a stream that counts and drops its bytes.</summary>
	/// <param name="level">The entry's compression.</param>
	/// <returns>The bytes written: the package of one entry.</returns>
	private long Zip(CompressionLevel level)
	{
		var output = new DiscardStream();
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
		{
			var entry = zip.CreateEntry(SheetEntry, level);
			entry.LastWriteTime = EntryDate;
			using var stream = entry.Open();
			for (var at = 0; at < sheetXml.Length; at += WriteSize)
			{
				stream.Write(sheetXml, at, Math.Min(WriteSize, sheetXml.Length - at));
			}
		}

		return output.Count;
	}

	/// <summary>
	/// Captures the writer's sheet XML: the workbook written at <see cref="CompressionLevel.NoCompression"/> twice, first
	/// only counted, then kept in a buffer of that size (so that a package past 1 GiB isn't grown to 2 GB by doubling), and
	/// its sheet's entry read back. Both writes go to a <see cref="DiscardStream"/>, the type of stream the timed writes
	/// write to.
	/// </summary>
	private void Capture()
	{
		var watch = Stopwatch.StartNew();
		var options = Options(stored: true);
		var size = Write(options);
		var kept = new byte[checked((int)size)];
		var output = new DiscardStream(kept);
		WriteTo(output, options);

		using (var zip = new ZipArchive(new MemoryStream(kept, 0, checked((int)output.Count), false), ZipArchiveMode.Read))
		{
			var entry = zip.GetEntry(SheetEntry) ?? throw new InvalidOperationException($"The workbook written has no {SheetEntry}.");
			sheetXml = new byte[checked((int)entry.Length)];
			using var stream = entry.Open();
			var at = 0;
			while (at < sheetXml.Length)
			{
				var read = stream.Read(sheetXml, at, Math.Min(1 << 20, sheetXml.Length - at));
				if (read == 0)
				{
					throw new EndOfStreamException();
				}

				at += read;
			}
		}

		Console.WriteLine(FormattableString.Invariant($"# sheet XML: {sheetXml.Length / 1e6:F1} MB of a {size / 1e6:F1} MB stored workbook, captured in {watch.Elapsed.TotalMilliseconds:F0} ms"));
	}
}
