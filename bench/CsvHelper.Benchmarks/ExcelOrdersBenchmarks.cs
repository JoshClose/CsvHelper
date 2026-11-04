using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;
using EdrFactory = ExcelDataReader.ExcelReaderFactory;
using EdrReader = ExcelDataReader.IExcelDataReader;
using SylvanExcelReader = Sylvan.Data.Excel.ExcelDataReader;
using SylvanWorkbookType = Sylvan.Data.Excel.ExcelWorkbookType;

namespace CsvHelper.Benchmarks;

/// <summary>An order's columns, by index, through a reader's typed getters, for one builder and one checksum.</summary>
internal interface IOrderSource
{
	/// <summary>Gets a column as an <see cref="int"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	int GetInt32(int index);

	/// <summary>Gets a column as a <see cref="long"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	long GetInt64(int index);

	/// <summary>Gets a column as a <see cref="double"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	double GetDouble(int index);

	/// <summary>Gets a column as a <see cref="decimal"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	decimal GetDecimal(int index);

	/// <summary>Gets a column as a <see cref="bool"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	bool GetBoolean(int index);

	/// <summary>Gets a column as a <see cref="DateTime"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	DateTime GetDateTime(int index);

	/// <summary>Gets a column as a <see cref="DateTimeOffset"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	DateTimeOffset GetDateTimeOffset(int index);

	/// <summary>Gets a column as a <see cref="Guid"/>.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	Guid GetGuid(int index);

	/// <summary>Gets a column as a <see cref="string"/>, an empty cell as an empty string.</summary>
	/// <param name="index">The column.</param>
	/// <returns>The value.</returns>
	string GetString(int index);
}

/// <summary>An order's columns from CsvHelper's reader, through <see cref="CsvReader.Parse{T}(int)"/>.</summary>
/// <param name="reader">The reader, on a record.</param>
internal readonly struct CsvOrderSource(CsvReader reader) : IOrderSource
{
	/// <inheritdoc/>
	public int GetInt32(int index) => reader.Parse<int>(index);

	/// <inheritdoc/>
	public long GetInt64(int index) => reader.Parse<long>(index);

	/// <inheritdoc/>
	public double GetDouble(int index) => reader.Parse<double>(index);

	/// <inheritdoc/>
	public decimal GetDecimal(int index) => reader.Parse<decimal>(index);

	/// <inheritdoc/>
	public bool GetBoolean(int index) => reader.Parse<bool>(index);

	/// <inheritdoc/>
	public DateTime GetDateTime(int index) => reader.Parse<DateTime>(index);

	/// <inheritdoc/>
	public DateTimeOffset GetDateTimeOffset(int index) => reader.Parse<DateTimeOffset>(index);

	/// <inheritdoc/>
	public Guid GetGuid(int index) => reader.Parse<Guid>(index);

	/// <inheritdoc/>
	public string GetString(int index) => reader.GetString(index);
}

/// <summary>
/// An order's columns from Sylvan.Data.Excel's typed getters. A date cell has no offset, so a
/// <see cref="DateTimeOffset"/> is its date at offset zero, as CsvHelper.Excel reads it.
/// </summary>
/// <param name="reader">The reader, on a row.</param>
internal readonly struct SylvanOrderSource(SylvanExcelReader reader) : IOrderSource
{
	/// <inheritdoc/>
	public int GetInt32(int index) => reader.GetInt32(index);

	/// <inheritdoc/>
	public long GetInt64(int index) => reader.GetInt64(index);

	/// <inheritdoc/>
	public double GetDouble(int index) => reader.GetDouble(index);

	/// <inheritdoc/>
	public decimal GetDecimal(int index) => reader.GetDecimal(index);

	/// <inheritdoc/>
	public bool GetBoolean(int index) => reader.GetBoolean(index);

	/// <inheritdoc/>
	public DateTime GetDateTime(int index) => reader.GetDateTime(index);

	/// <inheritdoc/>
	public DateTimeOffset GetDateTimeOffset(int index) => new(reader.GetDateTime(index), TimeSpan.Zero);

	/// <inheritdoc/>
	public Guid GetGuid(int index) => reader.GetGuid(index);

	/// <inheritdoc/>
	public string GetString(int index) => reader.GetString(index);
}

/// <summary>
/// An order's columns from ExcelDataReader's typed getters, which unbox the value it read (<c>GetInt32</c> is
/// <c>(int)GetValue(i)</c>), so on a number cell, a boxed <see cref="double"/>, all but <c>GetDouble</c> throw: the
/// integer and decimal columns convert <c>GetDouble</c>, and the Guid is parsed from its string. A
/// <see cref="DateTimeOffset"/> is the date at offset zero.
/// </summary>
/// <param name="reader">The reader, on a row.</param>
internal readonly struct ExcelDataReaderOrderSource(EdrReader reader) : IOrderSource
{
	/// <inheritdoc/>
	public int GetInt32(int index) => (int)reader.GetDouble(index);

	/// <inheritdoc/>
	public long GetInt64(int index) => (long)reader.GetDouble(index);

	/// <inheritdoc/>
	public double GetDouble(int index) => reader.GetDouble(index);

	/// <inheritdoc/>
	public decimal GetDecimal(int index) => (decimal)reader.GetDouble(index);

	/// <inheritdoc/>
	public bool GetBoolean(int index) => reader.GetBoolean(index);

	/// <inheritdoc/>
	public DateTime GetDateTime(int index) => reader.GetDateTime(index);

	/// <inheritdoc/>
	public DateTimeOffset GetDateTimeOffset(int index) => new(reader.GetDateTime(index), TimeSpan.Zero);

	/// <inheritdoc/>
	public Guid GetGuid(int index) => Guid.Parse(reader.GetString(index));

	/// <inheritdoc/>
	public string GetString(int index) => reader.GetString(index) ?? "";
}

/// <summary>
/// <see cref="OrderRecord"/>'s orders as a workbook: a header row and a million rows of numbers, dates, booleans and
/// shared strings, each member in the type of cell Excel gives it (see <see cref="ExcelOrders"/>), read by each library
/// from the same bytes. Records = an <see cref="OrderRecord"/> per row; Values = every field through its typed getter,
/// folded into a checksum.
/// </summary>
/// <remarks>
/// <para>
/// The MB/s column is of the same orders as CSV, and the <c>Csv_</c> methods read that CSV with CsvHelper, as a
/// reference. Sylvan.Data.Excel is each category's baseline.
/// </para>
/// <para>
/// MiniExcel isn't here. Its <c>Query&lt;T&gt;</c> fills properties, and <see cref="OrderRecord"/>'s members are
/// fields; with the same members as properties, MiniExcel 1.46.0 reads <c>ShippedAt</c> without its milliseconds and at
/// the local offset, another instant, so it doesn't give the same orders.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ExcelOrdersRead : ReadBenchmarkBase
{
	/// <summary>The workbook every library reads.</summary>
	private byte[] workbook = [];

	/// <inheritdoc/>
	protected override string Generate() => ExcelDataGenerator.OrdersCsv(ExcelDataGenerator.OrderRows);

	/// <inheritdoc/>
	protected override void AfterSetup()
	{
		// ExcelDataReader needs the code pages on .NET, which has only the UTF encodings built in.
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		workbook = ExcelDataGenerator.Orders(ExcelDataGenerator.OrderRows);

		// The CSV's bytes are kept for the reference benchmarks and the MB/s column; its string isn't needed.
		Text = "";
	}

	// ----- Records -----

	/// <summary>Sylvan.Data.Excel: an order per row, from its typed getters. It reads the header row when it opens.</summary>
	/// <returns>The orders.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<OrderRecord> Sylvan_Records()
	{
		using var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml);
		var list = new List<OrderRecord>();
		var source = new SylvanOrderSource(reader);
		while (reader.Read())
		{
			list.Add(Create(ref source));
		}

		return list;
	}

	/// <summary>CsvHelper.Excel: the orders, through the mapper generated for <see cref="OrderRecord"/>.</summary>
	/// <returns>The orders.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<OrderRecord> CsvHelper_Records()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());

		return reader.GetRecords<OrderRecord>().ToList();
	}

	/// <summary>ExcelDataReader: an order per row after the header row, from its typed getters.</summary>
	/// <returns>The orders.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<OrderRecord> ExcelDataReader_Records()
	{
		using var reader = EdrFactory.CreateOpenXmlReader(NewWorkbookStream());
		reader.Read();

		var list = new List<OrderRecord>();
		var source = new ExcelDataReaderOrderSource(reader);
		while (reader.Read())
		{
			list.Add(Create(ref source));
		}

		return list;
	}

	/// <summary>CsvHelper reading the same orders as CSV, for reference.</summary>
	/// <returns>The orders.</returns>
	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<OrderRecord> Csv_Records()
	{
		using var reader = CsvReader.Create(NewStream());

		return reader.GetRecords<OrderRecord>().ToList();
	}

	// ----- Values -----

	/// <summary>Sylvan.Data.Excel: every field through its typed getter.</summary>
	/// <returns>The checksum of the values.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Values")]
	public long Sylvan_Values()
	{
		using var reader = SylvanExcelReader.Create(NewWorkbookStream(), SylvanWorkbookType.ExcelXml);
		var checksum = 0L;
		var source = new SylvanOrderSource(reader);
		while (reader.Read())
		{
			checksum = Fold(checksum, ref source);
		}

		return checksum;
	}

	/// <summary>CsvHelper.Excel: every field through <see cref="CsvReader.Parse{T}(int)"/>, the strings through <c>GetString</c>.</summary>
	/// <returns>The checksum of the values.</returns>
	[Benchmark]
	[BenchmarkCategory("Values")]
	public long CsvHelper_Values()
	{
		using var reader = ExcelReader.Create(NewWorkbookStream());
		var checksum = 0L;
		var source = new CsvOrderSource(reader);
		while (reader.Read())
		{
			checksum = Fold(checksum, ref source);
		}

		return checksum;
	}

	/// <summary>ExcelDataReader: every field through its typed getter, after the header row.</summary>
	/// <returns>The checksum of the values.</returns>
	[Benchmark]
	[BenchmarkCategory("Values")]
	public long ExcelDataReader_Values()
	{
		using var reader = EdrFactory.CreateOpenXmlReader(NewWorkbookStream());
		reader.Read();

		var checksum = 0L;
		var source = new ExcelDataReaderOrderSource(reader);
		while (reader.Read())
		{
			checksum = Fold(checksum, ref source);
		}

		return checksum;
	}

	/// <summary>CsvHelper reading the same orders as CSV, for reference.</summary>
	/// <returns>The checksum of the values.</returns>
	[Benchmark]
	[BenchmarkCategory("Values")]
	public long Csv_Values()
	{
		using var reader = CsvReader.Create(NewStream());
		var checksum = 0L;
		var source = new CsvOrderSource(reader);
		while (reader.Read())
		{
			checksum = Fold(checksum, ref source);
		}

		return checksum;
	}

	/// <summary>Builds an order from a row's typed values.</summary>
	/// <typeparam name="TSource">The reader's source.</typeparam>
	/// <param name="source">The source, on a row.</param>
	/// <returns>The order.</returns>
	private static OrderRecord Create<TSource>(ref TSource source)
		where TSource : struct, IOrderSource
	{
		return new OrderRecord
		{
			Id = source.GetInt32(0),
			CustomerId = source.GetGuid(1),
			Customer = source.GetString(2),
			OrderDate = source.GetDateTime(3),
			ShippedAt = source.GetDateTimeOffset(4),
			Total = source.GetDecimal(5),
			Weight = source.GetDouble(6),
			Quantity = source.GetInt32(7),
			Paid = source.GetBoolean(8),
			Status = source.GetString(9),
			Sku = source.GetInt64(10),
			Notes = source.GetString(11),
		};
	}

	/// <summary>Folds a row's typed values into a checksum: the numbers, the dates' ticks, and the strings' lengths.</summary>
	/// <typeparam name="TSource">The reader's source.</typeparam>
	/// <param name="checksum">The checksum so far.</param>
	/// <param name="source">The source, on a row.</param>
	/// <returns>The checksum with the row.</returns>
	private static long Fold<TSource>(long checksum, ref TSource source)
		where TSource : struct, IOrderSource
	{
		unchecked
		{
			checksum = (checksum * 31) + source.GetInt32(0);
			checksum = (checksum * 31) + source.GetGuid(1).GetHashCode();
			checksum = (checksum * 31) + source.GetString(2).Length;
			checksum = (checksum * 31) + source.GetDateTime(3).Ticks;
			checksum = (checksum * 31) + source.GetDateTimeOffset(4).UtcTicks;
			checksum = (checksum * 31) + decimal.ToOACurrency(source.GetDecimal(5));
			checksum = (checksum * 31) + BitConverter.DoubleToInt64Bits(source.GetDouble(6));
			checksum = (checksum * 31) + source.GetInt32(7);
			checksum = (checksum * 31) + (source.GetBoolean(8) ? 1 : 0);
			checksum = (checksum * 31) + source.GetString(9).Length;
			checksum = (checksum * 31) + source.GetInt64(10);
			checksum = (checksum * 31) + source.GetString(11).Length;
		}

		return checksum;
	}

	/// <summary>Returns a new stream over the workbook.</summary>
	/// <returns>The stream, read-only.</returns>
	private MemoryStream NewWorkbookStream() => new(workbook, writable: false);
}
