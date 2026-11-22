#if NET
using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Security;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// What reading a shared sheet's strings allocates with <see cref="ExcelReaderOptions.PoolStrings"/> (Phase 9's step 4):
/// pooled, nothing for each repeated value after the first rows, so that 20,000 rows allocate what 10,000 do; unpooled, a
/// string for each field, which the same measure sees. It runs alone, in <see cref="TimedCollection"/>, as the import's
/// allocation test does, since other tests' garbage collections trim the shared array pool a reader rents from.
/// </summary>
/// <param name="output">Shows what the test measured.</param>
[Collection(TimedCollection.Name)]
public class PoolStringsAllocationTests(ITestOutputHelper output)
{
	/// <summary>The columns of the sheet, each a shared string.</summary>
	private const int Columns = 5;

	/// <summary>The rows read before the measure, in which the pool takes each value.</summary>
	private const int FirstRows = 100;

	/// <summary>The rows of the smaller sheet, after its header; the larger has twice as many.</summary>
	private const int Rows = 10_000;

	/// <summary>The most twice the rows may allocate beyond the rows: far less than a byte for each of the 50,000 fields more.</summary>
	private const long Slack = 32 * 1024;

	/// <summary>The smallest a string of the sheet's values takes, with its object header: a field unpooled allocates at least this.</summary>
	private const long StringSize = 30;

	/// <summary>The shared strings, which each column repeats in turn.</summary>
	private static readonly string[] Values = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel"];

	/// <summary>The sheet of <see cref="Rows"/> rows, made once.</summary>
	private static readonly Lazy<byte[]> Few = new(() => Workbook(Rows));

	/// <summary>The sheet of twice <see cref="Rows"/> rows, made once.</summary>
	private static readonly Lazy<byte[]> Many = new(() => Workbook(2 * Rows));

	[OptimizedFact]
	public void ReadsRepeatedStringsWithoutAllocatingPerRow()
	{
		var (few, many) = Measure(pool: true);
		output.WriteLine($"Pooled, every field's string allocated {few:N0} bytes for {Rows:N0} rows and {many:N0} for {2 * Rows:N0}, after the first {FirstRows}.");
		var (fewUnpooled, manyUnpooled) = Measure(pool: false);
		output.WriteLine($"Unpooled, every field's string allocated {fewUnpooled:N0} bytes for {Rows:N0} rows and {manyUnpooled:N0} for {2 * Rows:N0}, after the first {FirstRows}.");

		Assert.True(many - few < Slack, $"Pooled, {Rows:N0} rows allocated {few:N0} bytes and {2 * Rows:N0} rows {many:N0}.");

		// The measure sees a string a field: unpooled, the rows more allocate one for each of their fields.
		Assert.True(manyUnpooled - fewUnpooled >= Rows * Columns * StringSize, $"Unpooled, {Rows:N0} rows allocated {fewUnpooled:N0} bytes and {2 * Rows:N0} rows {manyUnpooled:N0}.");
	}

	/// <summary>
	/// Measures a read of both sheets: each once first, then three times, keeping the smallest, as the import's allocation
	/// test does, since the runtime allocates once at a moment tiering sets.
	/// </summary>
	/// <param name="pool">Whether the reader pools strings.</param>
	/// <returns>The bytes for <see cref="Rows"/> rows and for twice as many.</returns>
	private static (long Few, long Many) Measure(bool pool)
	{
		var options = new ExcelReaderOptions { PoolStrings = pool };
		Allocated(Few.Value, options);
		Allocated(Many.Value, options);
		var few = Math.Min(Allocated(Few.Value, options), Math.Min(Allocated(Few.Value, options), Allocated(Few.Value, options)));
		var many = Math.Min(Allocated(Many.Value, options), Math.Min(Allocated(Many.Value, options), Allocated(Many.Value, options)));

		return (few, many);
	}

	/// <summary>Reads every field of a sheet as a string, and measures what the reads after the first rows allocate.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The bytes the reads after <see cref="FirstRows"/> allocated on this thread.</returns>
	private static long Allocated(byte[] workbook, ExcelReaderOptions options)
	{
		using var reader = ExcelReader.Create(new MemoryStream(workbook, false), options);
		var length = 0L;
		for (var row = 0; row < FirstRows && reader.Read(); row++)
		{
			length += Strings(reader);
		}

		var before = GC.GetAllocatedBytesForCurrentThread();
		while (reader.Read())
		{
			length += Strings(reader);
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.True(length > 0);

		return allocated;
	}

	/// <summary>Gets every field of the current record as a string.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <returns>The strings' total length.</returns>
	private static long Strings(CsvReader reader)
	{
		var length = 0L;
		for (var i = 0; i < reader.FieldCount; i++)
		{
			length += reader.GetString(i).Length;
		}

		return length;
	}

	/// <summary>A workbook of a header and rows of <see cref="Columns"/> shared strings, each column repeating <see cref="Values"/> in turn.</summary>
	/// <param name="rows">The rows after the header.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Workbook(int rows)
	{
		var xml = new StringBuilder("<row r=\"1\">");
		for (var column = 0; column < Columns; column++)
		{
			xml.Append($"<c r=\"{(char)('A' + column)}1\" t=\"inlineStr\"><is><t>C{column}</t></is></c>");
		}

		xml.Append("</row>");
		for (var i = 0; i < rows; i++)
		{
			var r = (i + 2).ToString(CultureInfo.InvariantCulture);
			xml.Append($"<row r=\"{r}\">");
			for (var column = 0; column < Columns; column++)
			{
				xml.Append($"<c r=\"{(char)('A' + column)}{r}\" t=\"s\"><v>{(i + column) % Values.Length}</v></c>");
			}

			xml.Append("</row>");
		}

		return TestPackage.Workbook(new TestSheet("Data", xml: Sheets.Worksheet(xml.ToString())))
			.SharedStrings(Values.Select(v => $"<t>{v}</t>").ToArray())
			.ToArray();
	}
}
#endif
