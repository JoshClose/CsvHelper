using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Runs a class of Excel write benchmarks that use several threads as <see cref="Ccd0GcConfig"/> does (on CCD0, once with
/// workstation GC and once with server GC, with lock contentions, work items and CPU time per call), with the
/// <see cref="WrittenSizeColumn"/>.
/// </summary>
public sealed class Ccd0GcWrittenConfig : ManualConfig
{
	/// <summary>Initializes the configuration.</summary>
	public Ccd0GcWrittenConfig()
	{
		AddJob(BenchConfig.BaseJob.WithGcServer(false).WithId("Workstation"));
		AddJob(BenchConfig.BaseJob.WithGcServer(true).WithId("Server"));
		AddJob(Job.Default.WithAffinity((IntPtr)Ccd0Config.Mask).WithId("Ccd0").AsMutator());
		AddDiagnoser(BenchmarkDotNet.Diagnosers.ThreadingDiagnoser.Default);
		AddDiagnoser(CpuTimeDiagnoser.Default);
		AddColumn(new WrittenSizeColumn());
	}
}

/// <summary>
/// The package assets written as <c>ExcelPackageAssetsWrite.CsvHelper_Write</c> writes them (the header and each row's
/// fields through the sheet's <see cref="CsvWriter"/>, at the defaults) with <see cref="ExcelWriterOptions.CompressionThreads"/>
/// at 1, 2, 4, 8 and 16: Phase 9's parallel deflate (step 9). On CCD0, so that the threads have cores of their own.
/// </summary>
/// <remarks>
/// At 1 the sheet deflates on the writing thread, as the core-4 class times it; above, on net10.0 its XML is cut in 1 MB
/// chunks deflated at zlib's level 2 on up to that many threads beside the writing thread, which writes them in order.
/// The CPU column shows what the threads cost.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(Ccd0GcWrittenConfig))]
public class ExcelPackageAssetsThreadsWrite : ExcelWriteBenchmarkBase
{
	/// <summary>The header.</summary>
	private string[] header = [];

	/// <summary>The data rows, which share the arrays of each copy of NCsvPerf's lines.</summary>
	private string[][] rows = [];

	/// <summary>Gets or sets the deflate threads.</summary>
	[Params(1, 2, 4, 8, 16)]
	public int CompressionThreads { get; set; }

	/// <summary>Builds the rows, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		var records = ExcelDataGenerator.PackageAssetRecords(ExcelDataGenerator.PackageAssetRows);
		header = records[0];
		rows = records.Skip(1).ToArray();
		var counting = new CountingStream(keep: false);
		using (var writer = CsvWriter.Create(counting, leaveOpen: true))
		{
			WriteRows(writer);
		}

		DataBytes = counting.Count;
	}

	/// <summary>The defaults, with <see cref="CompressionThreads"/> deflate threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long CsvHelper_Write()
	{
		var stream = NewOutput();
		using (var workbook = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { CompressionThreads = CompressionThreads }, leaveOpen: true))
		using (var writer = workbook.AddSheet(ExcelDataGenerator.SheetName))
		{
			WriteRows(writer);
		}

		return stream.Count;
	}

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<PackageAsset>().ToList();
		}
	}

	/// <summary>Writes the header and the rows, field by field, as <c>ExcelPackageAssetsWrite</c>'s loop does.</summary>
	/// <param name="writer">The writer.</param>
	private void WriteRows(CsvWriter writer)
	{
		writer.WriteHeader(header);
		foreach (var row in rows)
		{
			for (var i = 0; i < row.Length; i++)
			{
				writer.WriteField(row[i]);
			}

			writer.EndRecord();
		}
	}
}

/// <summary>
/// The orders written as <c>ExcelOrdersWrite.CsvHelper_Write</c> writes them (<see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>
/// with <see cref="ExcelDateTimeOffsets.Utc"/>, at the defaults) with <see cref="ExcelWriterOptions.CompressionThreads"/> at
/// 1, 2, 4, 8 and 16, as <see cref="ExcelPackageAssetsThreadsWrite"/> writes the package assets.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Ccd0GcWrittenConfig))]
public class ExcelOrdersThreadsWrite : ExcelWriteBenchmarkBase
{
	/// <summary>The orders.</summary>
	private OrderRecord[] orders = [];

	/// <summary>Gets or sets the deflate threads.</summary>
	[Params(1, 2, 4, 8, 16)]
	public int CompressionThreads { get; set; }

	/// <summary>Builds the orders, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		orders = ExcelOrders.Generate(ExcelDataGenerator.OrderRows);
		var counting = new CountingStream(keep: false);
		using (var writer = CsvWriter.Create(counting, leaveOpen: true))
		{
			writer.WriteRecords(orders);
		}

		DataBytes = counting.Count;
	}

	/// <summary>The defaults, with <see cref="CompressionThreads"/> deflate threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long CsvHelper_Write()
	{
		var stream = NewOutput();
		var options = new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc, CompressionThreads = CompressionThreads };
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			workbook.WriteSheet(ExcelOrders.SheetName, orders);
		}

		return stream.Count;
	}

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<OrderRecord>().ToList();
		}
	}
}
