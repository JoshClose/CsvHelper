using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
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
/// Two stages of <see cref="CsvHelper_ToCsv"/>, in a category of their own
/// (<see cref="ConversionWrittenColumn.Stages"/>), split its time: <see cref="Rows"/>, their baseline, reads the records
/// and nothing else; <see cref="TextUtf8"/> also gets each field's text and kind as the conversion does, without
/// writing them. So <c>CsvHelper_ToCsv - Rows</c> is what the conversion costs beyond reading the workbook,
/// <c>TextUtf8 - Rows</c> its reading side and <c>CsvHelper_ToCsv - TextUtf8</c> its writing side.
/// </para>
/// <para>
/// The MB/s column is of the CSV <see cref="CsvHelper_ToCsv"/> writes, and the Written column the size of what each
/// writes: none for the stages (<see cref="ConversionWrittenColumn"/>).
/// </para>
/// </remarks>
public abstract class ExcelToCsvBenchmarkBase : ExcelWriteBenchmarkBase
{
	/// <summary>The room for a field's text that isn't its stored bytes, as the conversion's (<c>SheetToCsv</c>).</summary>
	private const int TextRoom = 64;

	/// <summary>The options of <see cref="CsvHelper_ToCsvFormatted"/>: the defaults, with the text Excel shows.</summary>
	private static readonly ExcelToCsvOptions Formatted = new() { Excel = new ExcelReaderOptions { Text = ExcelText.Formatted } };

	/// <summary>The workbook every library converts.</summary>
	private byte[] workbook = [];

	/// <summary>
	/// Whether the last benchmark was a stage, which writes nothing, so that <see cref="ReadBack"/> doesn't read back what
	/// an earlier benchmark wrote.
	/// </summary>
	private bool staged;

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

	/// <summary>A stage of <see cref="CsvHelper_ToCsv"/>: reads the workbook's records at its defaults, and nothing else.</summary>
	/// <returns>The records read.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory(ConversionWrittenColumn.Stages)]
	public long Rows()
	{
		staged = true;

		using var reader = ExcelReader.Create(NewWorkbookStream());
		var records = 0L;
		while (reader.Read())
		{
			records++;
		}

		return records;
	}

	/// <summary>
	/// A stage of <see cref="CsvHelper_ToCsv"/>: reads the records, and gets each field's text and kind as the conversion
	/// does (<see cref="CsvReader.GetTextUtf8(int, Span{byte}, bool)"/>, numbers rounded to 15 significant digits, and
	/// <c>GetCellKind</c>), without writing them.
	/// </summary>
	/// <returns>The texts' total length, plus the kinds' values.</returns>
	[Benchmark]
	[BenchmarkCategory(ConversionWrittenColumn.Stages)]
	public long TextUtf8()
	{
		staged = true;

		Span<byte> room = stackalloc byte[TextRoom];
		using var reader = ExcelReader.Create(NewWorkbookStream());
		var length = 0L;
		while (reader.Read())
		{
			var count = reader.FieldCount;
			for (var i = 0; i < count; i++)
			{
				length += reader.GetTextUtf8(i, room, roundNumbers: true).Length + (int)reader.GetCellKind(i);
			}
		}

		return length;
	}

	/// <summary>
	/// Reads what the last benchmark wrote, which it kept, back as dynamic records, each field its text; after a stage,
	/// which writes nothing, gives <see cref="ConversionWrittenColumn.NothingWritten"/> instead.
	/// </summary>
	/// <returns>The records, or <see cref="ConversionWrittenColumn.NothingWritten"/>.</returns>
	public override object ReadBack()
	{
		if (staged)
		{
			staged = false;

			return ConversionWrittenColumn.NothingWritten;
		}

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
/// <remarks>See <see cref="ExcelToCsvBenchmarkBase"/> for the libraries, the stages and the columns.</remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(ConversionWrittenConfig))]
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
/// See <see cref="ExcelToCsvBenchmarkBase"/> for the libraries, the stages and the columns. Each writes the dates,
/// booleans and numbers in a text of its own, so their CSVs differ.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(ConversionWrittenConfig))]
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

	/// <summary>Returns a new stream over the CSV, for a benchmark of this class or of a derived one.</summary>
	/// <returns>The stream, read-only.</returns>
	protected MemoryStream NewCsvStream() => new(csv, writable: false);
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
/// <para>
/// See <see cref="CsvToExcelBenchmarkBase"/> for the libraries and the columns. <see cref="CsvToExcelBenchmarkBase.CsvHelper_ToExcelTyped"/>
/// reads <see cref="OrderRecord"/>s. Inference types seven of the twelve columns: <c>Id</c>, <c>Total</c>,
/// <c>Weight</c>, <c>Quantity</c> and <c>Sku</c> as numbers (<c>Weight</c>'s doubles of 16 or 17 significant digits,
/// each checked to be its double's own text), <c>OrderDate</c> as dates and times, and <c>Paid</c> as booleans.
/// </para>
/// <para>
/// So that a change to inference and one to deflate show apart: <see cref="CsvReadFormat"/>, a stage in a category of
/// its own (<see cref="ConversionWrittenColumn.Stages"/>), is the conversion's own work on each field, without its
/// writer; and the stored pair, <see cref="ToExcelTextStored"/> (their baseline) and <see cref="ToExcelStored"/>, are
/// <see cref="CsvToExcelBenchmarkBase.CsvHelper_ToExcelText"/> and
/// <see cref="CsvToExcelBenchmarkBase.CsvHelper_ToExcel"/> without compression. So
/// <c>(CsvHelper_ToExcel - ToExcelStored) - (CsvHelper_ToExcelText - ToExcelTextStored)</c> is what typed cells cost
/// deflate, and <c>ToExcelStored - ToExcelTextStored</c> what they cost the rest.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(ConversionWrittenConfig))]
public class ExcelOrdersToExcel : CsvToExcelBenchmarkBase
{
	/// <summary>The records the columns' types are decided from: the conversion's default.</summary>
	private static readonly int InferTypes = new CsvToExcelOptions().InferTypes;

	/// <summary>The workbook's options without compression.</summary>
	private static readonly ExcelWriterOptions NoCompression = new() { CompressionLevel = CompressionLevel.NoCompression };

	/// <summary>The options of <see cref="ToExcelTextStored"/>: every field a text cell, without compression.</summary>
	private static readonly CsvToExcelOptions TextStored = new() { InferTypes = 0, Excel = NoCompression };

	/// <summary>The options of <see cref="ToExcelStored"/>: the columns' types inferred, without compression.</summary>
	private static readonly CsvToExcelOptions InferredStored = new() { Excel = NoCompression };

	/// <summary>
	/// Whether the last benchmark was a stage, which writes nothing, so that <see cref="ReadBack"/> doesn't read back what
	/// an earlier benchmark wrote.
	/// </summary>
	private bool staged;

	/// <summary>
	/// A stage of <see cref="CsvHelper_ToExcel"/>: the conversion's own work on each field, without its writer. It reads
	/// the CSV's records as the conversion does, decides the columns' types from the first 1,000 as it does, and puts each
	/// field of a typed column into the form the sink reads for its type (<c>ColumnTypes.Format</c>), with no sink.
	/// </summary>
	/// <returns>The total length of what the sink would be handed, plus one for each form handed over as a double.</returns>
	[Benchmark]
	[BenchmarkCategory(ConversionWrittenColumn.Stages)]
	public long CsvReadFormat()
	{
		staged = true;

		Span<byte> room = stackalloc byte[ColumnTypes.MaxFormLength];
		using var reader = CsvReader.Create(NewCsvStream());
		_ = reader.Headers;
		var types = new ColumnTypes(reader.Options.FormatProvider, ExcelWriterOptions.Default);
		var columns = Decide(reader, types, out var held);
		var length = 0L;
		foreach (var record in held)
		{
			for (var i = 0; i < record.Length; i++)
			{
				length += HandedLength(record[i], i < columns.Length ? columns[i] : ColumnType.Text, reader, types, room);
			}
		}

		while (reader.Read())
		{
			var count = reader.FieldCount;
			for (var i = 0; i < count; i++)
			{
				length += HandedLength(reader.GetSpan(i), i < columns.Length ? columns[i] : ColumnType.Text, reader, types, room);
			}
		}

		return length;
	}

	/// <summary><see cref="CsvHelper_ToExcelText"/> without compression: every field a text cell.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Stored")]
	public long ToExcelTextStored()
	{
		var stream = NewOutput();
		ExcelConvert.ToExcel(NewCsvStream(), stream, TextStored);

		return stream.Count;
	}

	/// <summary><see cref="CsvHelper_ToExcel"/> without compression: the columns' types inferred.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Stored")]
	public long ToExcelStored()
	{
		var stream = NewOutput();
		ExcelConvert.ToExcel(NewCsvStream(), stream, InferredStored);

		return stream.Count;
	}

	/// <summary>
	/// Reads what the last benchmark wrote, which it kept, back as orders; after a stage, which writes nothing, gives
	/// <see cref="ConversionWrittenColumn.NothingWritten"/> instead.
	/// </summary>
	/// <returns>The orders, or <see cref="ConversionWrittenColumn.NothingWritten"/>.</returns>
	public override object ReadBack()
	{
		if (staged)
		{
			staged = false;

			return ConversionWrittenColumn.NothingWritten;
		}

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

	/// <summary>
	/// Holds the first records and decides each column's type from them, as the conversion does (<c>CsvToSheet</c>), but
	/// for its check of texts too long for a cell, which none of the orders' is.
	/// </summary>
	/// <param name="reader">The reader, after the header.</param>
	/// <param name="types">The fields' types.</param>
	/// <param name="held">The records held, each field's bytes.</param>
	/// <returns>The columns' types.</returns>
	private static ColumnType[] Decide(CsvReader reader, ColumnTypes types, out List<byte[][]> held)
	{
		held = [];
		var sets = new List<int>();
		while (held.Count < InferTypes && reader.Read())
		{
			var count = reader.FieldCount;
			var record = new byte[count][];
			for (var i = 0; i < count; i++)
			{
				var field = reader.GetSpan(i);
				record[i] = field.ToArray();
				if (i == sets.Count)
				{
					sets.Add(-1);
				}

				if (sets[i] != 0 && !reader.IsNullSpan(field))
				{
					var matches = types.Matches(field);
					sets[i] = sets[i] < 0 ? matches : sets[i] & matches;
				}
			}

			held.Add(record);
		}

		return [.. sets.Select(s => s < 0 ? ColumnType.Text : ColumnTypes.Narrowest(s))];
	}

	/// <summary>
	/// The length of what the conversion hands its sink for a field, as it decides it (<c>CsvToSheet.WriteField</c>):
	/// nothing for a null field; the form of its column's type when the column is typed and the field matches; and
	/// otherwise its text.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="type">Its column's type.</param>
	/// <param name="reader">The reader, whose null values say whether the field is null.</param>
	/// <param name="types">The fields' types.</param>
	/// <param name="room">Room for a form, of <see cref="ColumnTypes.MaxFormLength"/> bytes.</param>
	/// <returns>The length, plus one when the form is handed over as a double.</returns>
	private static int HandedLength(ReadOnlySpan<byte> field, ColumnType type, CsvReader reader, ColumnTypes types, Span<byte> room)
	{
		if (reader.IsNullSpan(field))
		{
			return 0;
		}

		if (type != ColumnType.Text)
		{
			var length = types.Format(field, type, room, out var cellType);
			if (length >= 0)
			{
				return length + (cellType == typeof(double) ? 1 : 0);
			}
		}

		return field.Length;
	}
}

/// <summary>
/// Adds <see cref="ConversionWrittenColumn"/> to a class of conversion benchmarks with stages, in place of
/// <see cref="WrittenSizeConfig"/>.
/// </summary>
public sealed class ConversionWrittenConfig : ManualConfig
{
	/// <summary>Initializes a new instance of the <see cref="ConversionWrittenConfig"/> class, with the column.</summary>
	public ConversionWrittenConfig()
	{
		AddColumn(new ConversionWrittenColumn());
	}
}

/// <summary>
/// The Written column of a class of conversion benchmarks with stages: what <see cref="WrittenSizeColumn"/> shows for
/// each benchmark that writes, and <c>-</c> for each stage (a benchmark in the <see cref="Stages"/> category), which
/// writes nothing, so that the host doesn't run it to size it either.
/// </summary>
public sealed class ConversionWrittenColumn : IColumn
{
	/// <summary>The category of the stages: benchmarks that time a part of a conversion and write nothing.</summary>
	public const string Stages = "Stages";

	/// <summary>What a class's <see cref="ExcelWriteBenchmarkBase.ReadBack"/> gives after a stage, for <see cref="QuickRunner.Verify"/>.</summary>
	public const string NothingWritten = "nothing written (a stage)";

	/// <summary>The column that sizes the benchmarks that write.</summary>
	private static readonly WrittenSizeColumn Sizes = new();

	/// <inheritdoc/>
	public string Id => Sizes.Id;

	/// <inheritdoc/>
	public string ColumnName => Sizes.ColumnName;

	/// <inheritdoc/>
	public bool AlwaysShow => Sizes.AlwaysShow;

	/// <inheritdoc/>
	public ColumnCategory Category => Sizes.Category;

	/// <inheritdoc/>
	public int PriorityInCategory => Sizes.PriorityInCategory;

	/// <inheritdoc/>
	public bool IsNumeric => Sizes.IsNumeric;

	/// <inheritdoc/>
	public UnitType UnitType => Sizes.UnitType;

	/// <inheritdoc/>
	public string Legend => $"{Sizes.Legend}; - for a stage, which writes nothing";

	/// <inheritdoc/>
	public bool IsAvailable(Summary summary) => Sizes.IsAvailable(summary);

	/// <inheritdoc/>
	public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => Sizes.IsDefault(summary, benchmarkCase);

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) =>
		benchmarkCase.Descriptor.HasCategory(Stages) ? "-" : Sizes.GetValue(summary, benchmarkCase, style);
}
