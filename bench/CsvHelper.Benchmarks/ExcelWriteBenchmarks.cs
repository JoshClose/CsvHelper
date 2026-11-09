using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;
using LargeXlsx;
using MiniExcelLibs;
using DataCell = SpreadCheetah.DataCell;
using NumberFormat = SpreadCheetah.Styling.NumberFormat;
using Spreadsheet = SpreadCheetah.Spreadsheet;
using SpreadCheetahOptions = SpreadCheetah.SpreadCheetahOptions;

namespace CsvHelper.Benchmarks;

/// <summary>
/// A stream that counts the bytes written to it and drops them, or keeps them for <see cref="QuickRunner.Verify"/>. It
/// can't seek, as a response body can't, so every writer streams its package rather than seeking back in it.
/// </summary>
/// <param name="keep">Whether to keep the bytes as well as count them.</param>
public sealed class CountingStream(bool keep) : Stream
{
	/// <summary>The bytes written, when they are kept.</summary>
	private readonly MemoryStream? kept = keep ? new MemoryStream() : null;

	/// <summary>Gets the number of bytes written.</summary>
	public long Count { get; private set; }

	/// <summary>Gets a copy of the bytes written, when they are kept; otherwise none.</summary>
	public byte[] Kept => kept?.ToArray() ?? [];

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => true;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		Count += buffer.Length;
		kept?.Write(buffer);
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		Count++;
		kept?.WriteByte(value);
	}

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		Write(buffer.AsSpan(offset, count));

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		Write(buffer.Span);

		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// The Excel write benchmarks' base: each benchmark writes to a new <see cref="CountingStream"/> and returns the number
/// of bytes it wrote, which <see cref="WrittenSizeColumn"/> shows.
/// </summary>
public abstract class ExcelWriteBenchmarkBase
{
	/// <summary>The stream the last benchmark wrote to.</summary>
	private CountingStream? last;

	/// <summary>Gets or sets the size of the same data as CSV, written by CsvHelper, for the MB/s column.</summary>
	public long DataBytes { get; protected set; }

	/// <summary>
	/// Gets or sets whether the benchmarks keep what they write, so that <see cref="ReadBack"/> can read it; otherwise they
	/// count it and drop it.
	/// </summary>
	public bool KeepWritten { get; set; }

	/// <summary>
	/// Reads what the last benchmark wrote, which it kept, back as records: a workbook through CsvHelper.Excel's reader,
	/// CSV through CsvHelper's.
	/// </summary>
	/// <returns>The records.</returns>
	public abstract object ReadBack();

	/// <summary>Returns a new stream for a benchmark to write to.</summary>
	/// <returns>The stream.</returns>
	protected CountingStream NewOutput()
	{
		last = new CountingStream(KeepWritten);

		return last;
	}

	/// <summary>Returns what the last benchmark wrote, which it kept, and whether it is a workbook.</summary>
	/// <returns>A stream over the bytes, and whether they start as a zip package does (<c>PK</c>), not as CSV.</returns>
	/// <exception cref="InvalidOperationException">No benchmark has kept what it wrote.</exception>
	protected (MemoryStream Stream, bool IsWorkbook) Written()
	{
		if (last is null || !KeepWritten)
		{
			throw new InvalidOperationException("No benchmark has kept what it wrote: set KeepWritten first.");
		}

		var bytes = last.Kept;

		return (new MemoryStream(bytes, writable: false), bytes is [(byte)'P', (byte)'K', ..]);
	}
}

/// <summary>
/// NCsvPerf's package assets data written as a workbook: a header row and a million rows of 25 short text columns, the
/// data <c>ExcelPackageAssetsRead</c> reads, written by each library to a stream that counts and drops its bytes.
/// </summary>
/// <remarks>
/// <para>
/// CsvHelper.Excel writes the fields through its sheet's <see cref="CsvWriter"/>, as <c>PackageAssetsWrite</c> writes CSV:
/// with inline strings at <see cref="CompressionLevel.Fastest"/> (its defaults), with shared strings, and with inline
/// strings at <see cref="CompressionLevel.Optimal"/>. SpreadCheetah 1.28.0 (the baseline), LargeXlsx 2.0.2 and MiniExcel
/// 1.46.0 write with their defaults, a text cell per field. SpreadCheetah has only asynchronous methods, which complete
/// synchronously until its buffer is full.
/// </para>
/// <para>
/// The MB/s column is of the same data as CSV, and <see cref="Csv_Write"/> writes that CSV with CsvHelper, as a
/// reference. The Written column is the size of what each writes.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelPackageAssetsWrite : ExcelWriteBenchmarkBase
{
	/// <summary>The header.</summary>
	private string[] header = [];

	/// <summary>The data rows, which share the arrays of each copy of NCsvPerf's lines.</summary>
	private string[][] rows = [];

	/// <summary>Builds the rows, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		var records = ExcelDataGenerator.PackageAssetRecords(ExcelDataGenerator.PackageAssetRows);
		header = records[0];
		rows = records.Skip(1).ToArray();
		DataBytes = Csv_Write();
	}

	/// <summary>SpreadCheetah: a row of text cells per record, through one array of cells.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public async Task<long> SpreadCheetah_Write()
	{
		var stream = NewOutput();
		await using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream))
		{
			await spreadsheet.StartWorksheetAsync(ExcelDataGenerator.SheetName);

			var cells = new DataCell[header.Length];
			await spreadsheet.AddRowAsync(Fill(cells, header));
			foreach (var row in rows)
			{
				await spreadsheet.AddRowAsync(Fill(cells, row));
			}

			await spreadsheet.FinishAsync();
		}

		return stream.Count;
	}

	/// <summary>CsvHelper.Excel: each field through the sheet's <see cref="CsvWriter"/>, with inline strings at <see cref="CompressionLevel.Fastest"/>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write() => WriteWorkbook(ExcelStrings.Inline, CompressionLevel.Fastest);

	/// <summary>CsvHelper.Excel with shared strings.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteShared() => WriteWorkbook(ExcelStrings.Shared, CompressionLevel.Fastest);

	/// <summary>CsvHelper.Excel at <see cref="CompressionLevel.Optimal"/>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteOptimal() => WriteWorkbook(ExcelStrings.Inline, CompressionLevel.Optimal);

	/// <summary>LargeXlsx: a row per record, a string cell per field.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long LargeXlsx_Write()
	{
		var stream = NewOutput();
		using (var writer = new XlsxWriter(stream))
		{
			writer.BeginWorksheet(ExcelDataGenerator.SheetName);
			WriteRow(writer, header);
			foreach (var row in rows)
			{
				WriteRow(writer, row);
			}
		}

		return stream.Count;
	}

	/// <summary>MiniExcel: the rows through a data reader, its header from the reader's names.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long MiniExcel_Write()
	{
		var stream = NewOutput();
		MiniExcel.SaveAs(stream, new StringRowsDataReader(header, rows), sheetName: ExcelDataGenerator.SheetName);

		return stream.Count;
	}

	/// <summary>CsvHelper writing the same data as CSV, for reference.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Csv_Write()
	{
		var stream = NewOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			WriteRows(writer);
		}

		return stream.Count;
	}

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, isWorkbook) = Written();
		using (stream)
		{
			if (!isWorkbook)
			{
				using var csv = CsvReader.Create(stream);

				return csv.GetRecords<PackageAsset>().ToList();
			}

			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<PackageAsset>().ToList();
		}
	}

	/// <summary>Fills an array of SpreadCheetah's cells with a row's fields.</summary>
	/// <param name="cells">The cells, as many as the fields.</param>
	/// <param name="fields">The fields.</param>
	/// <returns>The cells.</returns>
	private static DataCell[] Fill(DataCell[] cells, string[] fields)
	{
		for (var i = 0; i < fields.Length; i++)
		{
			cells[i] = new DataCell(fields[i]);
		}

		return cells;
	}

	/// <summary>Writes a row with LargeXlsx.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="fields">The row's fields.</param>
	private static void WriteRow(XlsxWriter writer, string[] fields)
	{
		writer.BeginRow();
		foreach (var field in fields)
		{
			writer.Write(field);
		}
	}

	/// <summary>Writes the header and the rows with CsvHelper.Excel.</summary>
	/// <param name="strings">Where the strings go.</param>
	/// <param name="level">The compression level.</param>
	/// <returns>The bytes written.</returns>
	private long WriteWorkbook(ExcelStrings strings, CompressionLevel level)
	{
		var stream = NewOutput();
		var options = new ExcelWriterOptions { Strings = strings, CompressionLevel = level };
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		using (var writer = workbook.AddSheet(ExcelDataGenerator.SheetName))
		{
			WriteRows(writer);
		}

		return stream.Count;
	}

	/// <summary>Writes the header and the rows, field by field, to CsvHelper's writer or a sheet's.</summary>
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
/// <see cref="OrderRecord"/>'s orders written as a workbook: a header row and a million rows of numbers, dates, booleans
/// and text, the orders <c>ExcelOrdersRead</c> reads (<see cref="ExcelOrders.Generate"/>), written by each library to a
/// stream that counts and drops its bytes.
/// </summary>
/// <remarks>
/// <para>
/// CsvHelper.Excel writes them with <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>, through the
/// record writer generated for <see cref="OrderRecord"/>, with <see cref="ExcelDateTimeOffsets.Utc"/> so that
/// <c>ShippedAt</c> is a date, as the others write it: with inline strings at <see cref="CompressionLevel.Fastest"/>, with
/// shared strings, and with inline strings at <see cref="CompressionLevel.Optimal"/>.
/// </para>
/// <para>
/// SpreadCheetah 1.28.0 (the baseline), LargeXlsx 2.0.2 and MiniExcel 1.46.0 write each member as the cell of its type,
/// with their defaults: <c>CustomerId</c> as its string, <c>ShippedAt</c> as its UTC date, and the dates in
/// <see cref="ExcelOrders.DateFormat"/> (MiniExcel in its own). SpreadCheetah and LargeXlsx get each row by hand, and
/// MiniExcel through a data reader, as it takes properties and <see cref="OrderRecord"/>'s members are fields.
/// </para>
/// <para>
/// The MB/s column is of the same orders as CSV, and <see cref="Csv_Write"/> writes that CSV with CsvHelper, as a
/// reference. The Written column is the size of what each writes.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelOrdersWrite : ExcelWriteBenchmarkBase
{
	/// <summary>The orders.</summary>
	private OrderRecord[] orders = [];

	/// <summary>Builds the orders, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.</summary>
	[GlobalSetup]
	public void Setup()
	{
		orders = ExcelOrders.Generate(ExcelDataGenerator.OrderRows);
		DataBytes = Csv_Write();
	}

	/// <summary>SpreadCheetah: a row of typed cells per order, through one array of cells.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public async Task<long> SpreadCheetah_Write()
	{
		var stream = NewOutput();
		var options = new SpreadCheetahOptions { DefaultDateTimeFormat = NumberFormat.Custom(ExcelOrders.DateFormat) };
		await using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream, options))
		{
			await spreadsheet.StartWorksheetAsync(ExcelOrders.SheetName);

			var cells = new DataCell[ExcelOrders.Header.Length];
			for (var i = 0; i < cells.Length; i++)
			{
				cells[i] = new DataCell(ExcelOrders.Header[i]);
			}

			await spreadsheet.AddRowAsync(cells);
			foreach (var order in orders)
			{
				cells[0] = new DataCell(order.Id);
				cells[1] = new DataCell(order.CustomerId.ToString());
				cells[2] = new DataCell(order.Customer);
				cells[3] = new DataCell(order.OrderDate);
				cells[4] = new DataCell(order.ShippedAt.UtcDateTime);
				cells[5] = new DataCell(order.Total);
				cells[6] = new DataCell(order.Weight);
				cells[7] = new DataCell(order.Quantity);
				cells[8] = new DataCell(order.Paid);
				cells[9] = new DataCell(order.Status);
				cells[10] = new DataCell(order.Sku);
				cells[11] = new DataCell(order.Notes);
				await spreadsheet.AddRowAsync(cells);
			}

			await spreadsheet.FinishAsync();
		}

		return stream.Count;
	}

	/// <summary>
	/// CsvHelper.Excel: the orders through <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/>, with
	/// inline strings at <see cref="CompressionLevel.Fastest"/>.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write() => WriteWorkbook(ExcelStrings.Inline, CompressionLevel.Fastest);

	/// <summary>CsvHelper.Excel with shared strings: every <c>CustomerId</c> is one of its own.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteShared() => WriteWorkbook(ExcelStrings.Shared, CompressionLevel.Fastest);

	/// <summary>CsvHelper.Excel at <see cref="CompressionLevel.Optimal"/>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteOptimal() => WriteWorkbook(ExcelStrings.Inline, CompressionLevel.Optimal);

	/// <summary>LargeXlsx: a row of typed cells per order, <c>Sku</c> as a double, as it has no <see cref="long"/> cell.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long LargeXlsx_Write()
	{
		var stream = NewOutput();
		var date = XlsxStyle.Default.With(new XlsxNumberFormat(ExcelOrders.DateFormat));
		using (var writer = new XlsxWriter(stream))
		{
			writer.BeginWorksheet(ExcelOrders.SheetName).BeginRow();
			foreach (var name in ExcelOrders.Header)
			{
				writer.Write(name);
			}

			foreach (var order in orders)
			{
				writer.BeginRow()
					.Write(order.Id)
					.Write(order.CustomerId.ToString())
					.Write(order.Customer)
					.Write(order.OrderDate, date)
					.Write(order.ShippedAt.UtcDateTime, date)
					.Write(order.Total)
					.Write(order.Weight)
					.Write(order.Quantity)
					.Write(order.Paid)
					.Write(order.Status)
					.Write((double)order.Sku)
					.Write(order.Notes);
			}
		}

		return stream.Count;
	}

	/// <summary>MiniExcel: the orders through a data reader of their typed values, its header from the reader's names.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long MiniExcel_Write()
	{
		var stream = NewOutput();
		MiniExcel.SaveAs(stream, new OrderRowsDataReader(orders), sheetName: ExcelOrders.SheetName);

		return stream.Count;
	}

	/// <summary>CsvHelper writing the same orders as CSV, for reference.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Csv_Write()
	{
		var stream = NewOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteRecords(orders);
		}

		return stream.Count;
	}

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, isWorkbook) = Written();
		using (stream)
		{
			if (!isWorkbook)
			{
				using var csv = CsvReader.Create(stream);

				return csv.GetRecords<OrderRecord>().ToList();
			}

			using var reader = ExcelReader.Create(stream);

			return reader.GetRecords<OrderRecord>().ToList();
		}
	}

	/// <summary>Writes the orders with CsvHelper.Excel.</summary>
	/// <param name="strings">Where the strings go.</param>
	/// <param name="level">The compression level.</param>
	/// <returns>The bytes written.</returns>
	private long WriteWorkbook(ExcelStrings strings, CompressionLevel level)
	{
		var stream = NewOutput();
		var options = new ExcelWriterOptions { Strings = strings, CompressionLevel = level, DateTimeOffsets = ExcelDateTimeOffsets.Utc };
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			workbook.WriteSheet(ExcelOrders.SheetName, orders);
		}

		return stream.Count;
	}
}

/// <summary>
/// The orders as a data reader of their typed values, for MiniExcel: <c>CustomerId</c> as its string, and
/// <c>ShippedAt</c> as its UTC date.
/// </summary>
/// <param name="orders">The orders.</param>
internal sealed class OrderRowsDataReader(OrderRecord[] orders) : ArrayDataReaderBase(ExcelOrders.Header, orders.Length)
{
	/// <summary>Each column's type.</summary>
	private static readonly Type[] Types =
	[
		typeof(int), typeof(string), typeof(string), typeof(DateTime), typeof(DateTime), typeof(decimal), typeof(double), typeof(int), typeof(bool), typeof(string), typeof(long), typeof(string),
	];

	/// <inheritdoc/>
	public override Type GetFieldType(int ordinal) => Types[ordinal];

	/// <inheritdoc/>
	public override string GetString(int ordinal) => (string)GetValue(ordinal);

	/// <inheritdoc/>
	public override object GetValue(int ordinal)
	{
		var order = orders[Row];

		return ordinal switch
		{
			0 => order.Id,
			1 => order.CustomerId.ToString(),
			2 => order.Customer,
			3 => order.OrderDate,
			4 => order.ShippedAt.UtcDateTime,
			5 => order.Total,
			6 => order.Weight,
			7 => order.Quantity,
			8 => order.Paid,
			9 => order.Status,
			10 => order.Sku,
			11 => order.Notes,
			_ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
		};
	}
}
