using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;
using MiniExcelLibs;
using SylvanCsvReader = Sylvan.Data.Csv.CsvDataReader;
using SylvanCsvWriter = Sylvan.Data.Csv.CsvDataWriter;
using SylvanExcelReader = Sylvan.Data.Excel.ExcelDataReader;
using SylvanExcelWriter = Sylvan.Data.Excel.ExcelDataWriter;
using SylvanWorkbookType = Sylvan.Data.Excel.ExcelWorkbookType;

namespace CsvHelper.Benchmarks;

/// <summary>
/// The <c>.xlsx</c> → CSV benchmarks' base: a workbook converted to CSV by each library, written to a stream that counts
/// and drops its bytes (see <see cref="ExcelWriteBenchmarkBase"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExcelConvert.ToCsv(Stream, Stream, ExcelToCsvOptions)"/> at its defaults, and under
/// <see cref="ExcelText.Formatted"/>, for what the text Excel shows costs; the string path it avoids, a
/// <see cref="CsvReader.GetString(int)"/> into <see cref="CsvWriter.WriteField(string)"/> for each field; Sylvan.Data.Excel
/// 0.5.8's reader into Sylvan.Data.Csv 1.4.4's <c>CsvDataWriter</c> (the baseline), each with its defaults; and MiniExcel
/// 1.46.0's <c>ConvertXlsxToCsv</c>.
/// </para>
/// <para>
/// The MB/s column is of the CSV <see cref="CsvHelper_ToCsv"/> writes, and the Written column the size of what each
/// writes.
/// </para>
/// </remarks>
public abstract class ExcelToCsvBenchmarkBase : ExcelWriteBenchmarkBase
{
	/// <summary>The options of <see cref="CsvHelper_ToCsvFormatted"/>: the defaults, with the text Excel shows.</summary>
	private static readonly ExcelToCsvOptions Formatted = new() { Excel = new ExcelReaderOptions { Text = ExcelText.Formatted } };

	/// <summary>The workbook every library converts.</summary>
	private byte[] workbook = [];

	/// <summary>
	/// Reads the workbook, and converts it once with <see cref="CsvHelper_ToCsv"/> for
	/// <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		workbook = Workbook();
		DataBytes = CsvHelper_ToCsv();
	}

	/// <summary>Sylvan.Data.Excel's reader, every column a string, into Sylvan.Data.Csv's writer.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("ToCsv")]
	public long Sylvan_ToCsv()
	{
		var stream = NewOutput();
		using (var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml))
		using (var text = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
		using (var writer = SylvanCsvWriter.Create(text))
		{
			writer.Write(reader);
		}

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: <see cref="ExcelConvert.ToCsv(Stream, Stream, ExcelToCsvOptions)"/>, each field's text as UTF-8.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToCsv")]
	public long CsvHelper_ToCsv()
	{
		var stream = NewOutput();
		ExcelConvert.ToCsv(NewWorkbookStream(), stream);

		return stream.Count;
	}

	/// <summary>
	/// CsvHelper.Excel: <see cref="ExcelConvert.ToCsv(Stream, Stream, ExcelToCsvOptions)"/> under
	/// <see cref="ExcelText.Formatted"/>, each field the text Excel shows, as UTF-8.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToCsv")]
	public long CsvHelper_ToCsvFormatted()
	{
		var stream = NewOutput();
		ExcelConvert.ToCsv(NewWorkbookStream(), stream, Formatted);

		return stream.Count;
	}

	/// <summary>
	/// The string path <see cref="ExcelConvert.ToCsv(Stream, Stream, ExcelToCsvOptions)"/> avoids: the reader's header, then
	/// each field through <see cref="CsvReader.GetString(int)"/> into <see cref="CsvWriter.WriteField(string)"/>. A number
	/// is its stored text, which the conversion rounds to 15 significant digits.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToCsv")]
	public long CsvHelper_ToCsvStrings()
	{
		var stream = NewOutput();
		using (var reader = ExcelReader.Create(NewWorkbookStream()))
		using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { ValidateFieldCount = false }, leaveOpen: true))
		{
			foreach (var name in reader.Headers)
			{
				writer.WriteField(name);
			}

			writer.EndRecord();
			while (reader.Read())
			{
				for (var i = 0; i < reader.FieldCount; i++)
				{
					writer.WriteField(reader.GetString(i));
				}

				writer.EndRecord();
			}
		}

		return stream.Count;
	}

	/// <summary>MiniExcel: <c>ConvertXlsxToCsv</c>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToCsv")]
	public long MiniExcel_ToCsv()
	{
		var stream = NewOutput();
		MiniExcel.ConvertXlsxToCsv(NewWorkbookStream(), stream);

		return stream.Count;
	}

	/// <summary>Reads what the last benchmark wrote, which it kept, back as dynamic records, each field its text.</summary>
	/// <returns>The records.</returns>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = CsvReader.Create(stream);

			return reader.GetRecords<dynamic>().ToList();
		}
	}

	/// <summary>Returns the workbook the libraries convert.</summary>
	/// <returns>The workbook's bytes.</returns>
	protected abstract byte[] Workbook();

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewWorkbookStream() => new(workbook, writable: false);
}

/// <summary>
/// NCsvPerf's package assets workbook converted to CSV: a header row and a million rows of 25 short text columns, the
/// workbook <c>ExcelPackageAssetsRead</c> reads, with shared strings, as Excel writes them, or inline strings.
/// </summary>
/// <remarks>See <see cref="ExcelToCsvBenchmarkBase"/> for the libraries and the columns.</remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelPackageAssetsToCsv : ExcelToCsvBenchmarkBase
{
	/// <summary>Gets or sets where the workbook's strings are.</summary>
	[Params(ExcelStrings.Shared, ExcelStrings.Inline)]
	public ExcelStrings Strings { get; set; }

	/// <inheritdoc/>
	protected override byte[] Workbook() => ExcelDataGenerator.PackageAssets(ExcelDataGenerator.PackageAssetRows, Strings);
}

/// <summary>
/// <see cref="OrderRecord"/>'s orders workbook converted to CSV: a header row and a million rows of numbers, dates,
/// booleans and shared strings, the workbook <c>ExcelOrdersRead</c> reads.
/// </summary>
/// <remarks>
/// See <see cref="ExcelToCsvBenchmarkBase"/> for the libraries and the columns. Each writes the dates, booleans and
/// numbers in a text of its own, so their CSVs differ.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelOrdersToCsv : ExcelToCsvBenchmarkBase
{
	/// <inheritdoc/>
	protected override byte[] Workbook() => ExcelDataGenerator.Orders(ExcelDataGenerator.OrderRows);
}

/// <summary>
/// The CSV → <c>.xlsx</c> benchmarks' base: CSV converted to a workbook by each library, written to a stream that counts
/// and drops its bytes (see <see cref="ExcelWriteBenchmarkBase"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExcelConvert.ToExcel(Stream, Stream, CsvToExcelOptions)"/> at its defaults, its columns' types inferred
/// from 1,000 records, and as text (<see cref="CsvToExcelOptions.InferTypes"/> 0); <c>ToExcel&lt;T&gt;</c>, typed through
/// the data's record; Sylvan.Data.Csv 1.4.4's <c>CsvDataReader</c> into Sylvan.Data.Excel 0.5.8's <c>ExcelDataWriter</c>
/// (the baseline), each with its defaults; and MiniExcel 1.46.0's <c>ConvertCsvToXlsx</c>. CsvHelper.Excel's sheets have
/// its defaults' look: a bold, frozen header and auto-fit over 100 rows. Sylvan's and MiniExcel's cells are text, as
/// <c>ToExcel</c>'s are with <see cref="CsvToExcelOptions.InferTypes"/> 0.
/// </para>
/// <para>
/// The MB/s column is of the CSV, and the Written column the size of the workbook each writes.
/// </para>
/// </remarks>
public abstract class CsvToExcelBenchmarkBase : ExcelWriteBenchmarkBase
{
	/// <summary>The options of <see cref="CsvHelper_ToExcelText"/>: every field a text cell.</summary>
	private static readonly CsvToExcelOptions Text = new() { InferTypes = 0 };

	/// <summary>The CSV every library converts, as UTF-8.</summary>
	private byte[] csv = [];

	/// <summary>Builds the CSV, whose size is <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		csv = Encoding.UTF8.GetBytes(Csv());
		DataBytes = csv.Length;
	}

	/// <summary>Sylvan.Data.Csv's reader, every column a string, into Sylvan.Data.Excel's writer.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("ToExcel")]
	public long Sylvan_ToExcel()
	{
		var stream = NewOutput();
		using (var text = new StreamReader(NewCsvStream()))
		using (var reader = SylvanCsvReader.Create(text))
		using (var writer = SylvanExcelWriter.Create(stream, SylvanWorkbookType.ExcelXml))
		{
			writer.Write(reader, "Sheet1");
		}

		return stream.Count;
	}

	/// <summary>
	/// CsvHelper.Excel: <see cref="ExcelConvert.ToExcel(Stream, Stream, CsvToExcelOptions)"/>, its columns' types inferred.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToExcel")]
	public long CsvHelper_ToExcel()
	{
		var stream = NewOutput();
		ExcelConvert.ToExcel(NewCsvStream(), stream);

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: <see cref="ExcelConvert.ToExcel(Stream, Stream, CsvToExcelOptions)"/>, every field a text cell.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToExcel")]
	public long CsvHelper_ToExcelText()
	{
		var stream = NewOutput();
		ExcelConvert.ToExcel(NewCsvStream(), stream, Text);

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: <c>ToExcel&lt;T&gt;</c>, through the data's record.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToExcel")]
	public long CsvHelper_ToExcelTyped()
	{
		var stream = NewOutput();
		ToExcelTyped(NewCsvStream(), stream);

		return stream.Count;
	}

	/// <summary>MiniExcel: <c>ConvertCsvToXlsx</c>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("ToExcel")]
	public long MiniExcel_ToExcel()
	{
		var stream = NewOutput();
		MiniExcel.ConvertCsvToXlsx(NewCsvStream(), stream);

		return stream.Count;
	}

	/// <summary>Returns the CSV the libraries convert.</summary>
	/// <returns>The CSV, with a header.</returns>
	protected abstract string Csv();

	/// <summary>Converts the CSV with <c>ToExcel&lt;T&gt;</c>, through the data's record.</summary>
	/// <param name="csv">The CSV.</param>
	/// <param name="excel">The workbook's stream.</param>
	protected abstract void ToExcelTyped(Stream csv, Stream excel);

	/// <summary>Returns a new stream over the CSV.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewCsvStream() => new(csv, writable: false);
}

/// <summary>
/// NCsvPerf's package assets data converted from CSV to a workbook: a header and a million rows of 25 short text
/// columns, the CSV of the workbook <c>ExcelPackageAssetsRead</c> reads.
/// </summary>
/// <remarks>
/// See <see cref="CsvToExcelBenchmarkBase"/> for the libraries and the columns. <see cref="CsvToExcelBenchmarkBase.CsvHelper_ToExcelTyped"/>
/// reads <see cref="PackageAsset"/>s.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelPackageAssetsToExcel : CsvToExcelBenchmarkBase
{
	/// <summary>Reads what the last benchmark wrote, which it kept, back as package assets.</summary>
	/// <returns>The records.</returns>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<PackageAsset>().ToList();
		}
	}

	/// <inheritdoc/>
	protected override string Csv() => ExcelDataGenerator.PackageAssetsCsv(ExcelDataGenerator.PackageAssetRows);

	/// <inheritdoc/>
	protected override void ToExcelTyped(Stream csv, Stream excel) => ExcelConvert.ToExcel<PackageAsset>(csv, excel);
}

/// <summary>
/// <see cref="OrderRecord"/>'s orders converted from CSV to a workbook: a header and a million rows of numbers, dates,
/// booleans and text, the orders <c>ExcelOrdersRead</c> reads, as CsvHelper writes them.
/// </summary>
/// <remarks>
/// See <see cref="CsvToExcelBenchmarkBase"/> for the libraries and the columns. <see cref="CsvToExcelBenchmarkBase.CsvHelper_ToExcelTyped"/>
/// reads <see cref="OrderRecord"/>s.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelOrdersToExcel : CsvToExcelBenchmarkBase
{
	/// <summary>Reads what the last benchmark wrote, which it kept, back as orders.</summary>
	/// <returns>The orders.</returns>
	public override object ReadBack()
	{
		var (stream, _) = Written();
		using (stream)
		{
			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<OrderRecord>().ToList();
		}
	}

	/// <inheritdoc/>
	protected override string Csv() => ExcelDataGenerator.OrdersCsv(ExcelDataGenerator.OrderRows);

	/// <inheritdoc/>
	protected override void ToExcelTyped(Stream csv, Stream excel) => ExcelConvert.ToExcel<OrderRecord>(csv, excel);
}
