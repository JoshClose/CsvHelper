using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>The orders <c>GetRecords</c> read with each record that doesn't read skipped, and how many it skipped.</summary>
/// <param name="Records">The orders that read, in the sheet's order.</param>
/// <param name="Skipped">The records skipped.</param>
public sealed record SkippedOrders(List<OrderRecord> Records, int Skipped);

/// <summary>
/// <see cref="OrderRecord"/>'s orders imported from a workbook: a header row and a million rows of numbers, dates,
/// booleans and shared strings, the workbook <c>ExcelOrdersRead</c> reads, through
/// <see cref="ExcelReader.Import{T}(Stream, ExcelReaderOptions?, int)"/> against
/// <see cref="CsvReader.GetRecords{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Valid: every cell reads. <see cref="CsvHelper_GetRecords"/>, the baseline, reads the orders as <c>ExcelOrdersRead</c>
/// does; <see cref="CsvHelper_Import"/> imports them, keeping its result.
/// </para>
/// <para>
/// Errors: one cell in a hundred is the text <c>n/a</c> in a typed member's column
/// (<see cref="ExcelDataGenerator.OrdersWithErrors"/>), 120,000 cells in 119,774 rows.
/// <see cref="CsvHelper_GetRecordsSkipping"/>, the baseline, reads them with <see cref="CsvReaderOptions.OnRecordError"/>
/// skipping each record that doesn't read, as a reader without the import would: it knows one member a record.
/// <see cref="CsvHelper_ImportErrors"/> imports them with no cap on errors, so that it reads every row, and gives an error
/// for every cell at fault, with its place.
/// </para>
/// <para>
/// The MB/s column is of the same valid orders as CSV.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelOrdersImport : ReadBenchmarkBase
{
	/// <summary>The reader's options with each record that doesn't read skipped; the count is the benchmark's.</summary>
	private ExcelReaderOptions skipping = ExcelReaderOptions.Default;

	/// <summary>The records <see cref="CsvHelper_GetRecordsSkipping"/> skipped in its current call.</summary>
	private int skipped;

	/// <summary>The workbook whose every cell reads.</summary>
	private byte[] workbook = [];

	/// <summary>The workbook with one cell in a hundred at fault.</summary>
	private byte[] faulty = [];

	/// <inheritdoc/>
	protected override string Generate() => ExcelDataGenerator.OrdersCsv(ExcelDataGenerator.OrderRows);

	/// <inheritdoc/>
	protected override void AfterSetup()
	{
		workbook = ExcelDataGenerator.Orders(ExcelDataGenerator.OrderRows);
		faulty = ExcelDataGenerator.OrdersWithErrors(ExcelDataGenerator.OrderRows);
		skipping = new ExcelReaderOptions
		{
			Csv = CsvReaderOptions.Default with
			{
				OnRecordError = _ =>
				{
					skipped++;

					return true;
				},
			},
		};

		// The CSV's bytes are kept for the MB/s column; its string isn't needed.
		Text = "";
	}

	// ----- Valid -----

	/// <summary>CsvHelper.Excel: the orders through <see cref="CsvReader.GetRecords{T}"/>, as <c>ExcelOrdersRead</c> reads them.</summary>
	/// <returns>The orders.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Valid")]
	public List<OrderRecord> CsvHelper_GetRecords()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.GetRecords<OrderRecord>().ToList();
	}

	/// <summary>CsvHelper.Excel: the orders through <see cref="ExcelReader.Import{T}(Stream, ExcelReaderOptions?, int)"/>.</summary>
	/// <returns>The import's result.</returns>
	[Benchmark]
	[BenchmarkCategory("Valid")]
	public ExcelImportResult<OrderRecord> CsvHelper_Import() => ExcelReader.Import<OrderRecord>(NewWorkbookStream());

	// ----- Errors -----

	/// <summary>
	/// CsvHelper.Excel: the orders through <see cref="CsvReader.GetRecords{T}"/>, each record that doesn't read skipped by
	/// <see cref="CsvReaderOptions.OnRecordError"/>.
	/// </summary>
	/// <returns>The orders that read, and how many records were skipped.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Errors")]
	public SkippedOrders CsvHelper_GetRecordsSkipping()
	{
		skipped = 0;

		using var reader = ExcelReader.Create(NewFaultyStream(), skipping);
		var records = reader.GetRecords<OrderRecord>().ToList();

		return new SkippedOrders(records, skipped);
	}

	/// <summary>
	/// CsvHelper.Excel: the orders through <see cref="ExcelReader.Import{T}(Stream, ExcelReaderOptions?, int)"/>, with no cap
	/// on errors, so that every row is read and every cell at fault is an error.
	/// </summary>
	/// <returns>The import's result.</returns>
	[Benchmark]
	[BenchmarkCategory("Errors")]
	public ExcelImportResult<OrderRecord> CsvHelper_ImportErrors() => ExcelReader.Import<OrderRecord>(NewFaultyStream(), maxErrors: int.MaxValue);

	/// <summary>Returns a new stream over the workbook whose every cell reads.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewWorkbookStream() => new(workbook, writable: false);

	/// <summary>Returns a new stream over the workbook with cells at fault.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewFaultyStream() => new(faulty, writable: false);
}
