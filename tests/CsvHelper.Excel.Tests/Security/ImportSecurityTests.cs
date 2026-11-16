using CsvHelper.Excel.Tests.Api;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>Ten whole numbers, columns A to J: a row of text is ten cells at fault.</summary>
[CsvRecord]
public partial class TenNumbers
{
	/// <summary>A whole number.</summary>
	public int A { get; set; }

	/// <summary>A whole number.</summary>
	public int B { get; set; }

	/// <summary>A whole number.</summary>
	public int C { get; set; }

	/// <summary>A whole number.</summary>
	public int D { get; set; }

	/// <summary>A whole number.</summary>
	public int E { get; set; }

	/// <summary>A whole number.</summary>
	public int F { get; set; }

	/// <summary>A whole number.</summary>
	public int G { get; set; }

	/// <summary>A whole number.</summary>
	public int H { get; set; }

	/// <summary>A whole number.</summary>
	public int I { get; set; }

	/// <summary>A whole number.</summary>
	public int J { get; set; }
}

/// <summary>
/// The import's limit on errors (Phase 7's step 8), as an upload of the wrong file meets it: a sheet of a million cells at
/// fault stops at <c>maxErrors</c>, having read a little of the sheet, and the import's memory is the errors', whatever the
/// sheet's size.
/// </summary>
/// <param name="output">Shows what the tests measured.</param>
public class ImportSecurityTests(ITestOutputHelper output)
{
	/// <summary>The rows of the sheet of a million cells at fault, ten each.</summary>
	private const int MillionCells = 100_000;

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsAtMaxErrorsInAMillionBadCells(bool async)
	{
		var zip = BadCells(MillionCells);
		var input = new ReadTrackingStream(zip);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(input) : ExcelWorkbook.Open(input);
		input.Forget();
		using var reader = await workbook.OpenReaderAsync(async);
		var result = async ? await ExcelReader.ImportAsync<TenNumbers>(reader) : ExcelReader.Import<TenNumbers>(reader);

		// A thousand errors, the rows 2 to 101's cells, and no record; the rest of the sheet not read: a small part of its data.
		Assert.Equal(1000, result.Errors.Count);
		Assert.Equal(("Upload!A2", "Upload!J101"), (result.Errors[0].Location, result.Errors[999].Location));
		Assert.Empty(result.Records);
		Assert.False(result.Complete);
		var (start, length) = ZipBytes.DataRange(zip, "xl/worksheets/sheet1.xml");
		var read = input.ReadInto(start, length);
		output.WriteLine($"Read {read:N0} of the sheet's {length:N0} bytes of compressed data.");
		Assert.InRange(read, 1, length / 10);

		// The overloads that open the workbook stop as well, at any maxErrors.
		var one = async ? await ExcelReader.ImportAsync<TenNumbers>(new MemoryStream(zip), maxErrors: 1) : ExcelReader.Import<TenNumbers>(new MemoryStream(zip), maxErrors: 1);
		Assert.Equal(["Upload!A2"], one.Errors.Select(e => e.Location));
		Assert.False(one.Complete);
	}

#if NET
	[OptimizedFact]
	public void AllocatesForTheErrorsWhateverTheSheetsSize()
	{
		var bytes = new Func<byte[], long>(zip =>
		{
			using var reader = ExcelReader.Create(new MemoryStream(zip));
			reader.ReadHeader();
			var before = GC.GetAllocatedBytesForCurrentThread();
			var result = ExcelReader.Import<TenNumbers>(reader);
			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.Equal(1000, result.Errors.Count);

			return allocated;
		});

		// Each size measured twice, and the smaller kept, as the writer's allocation tests do (the runtime allocates once at a
		// moment tiering sets). A thousand errors cost the same in a sheet of 10,000 rows and of 100,000, and a few megabytes.
		var small = BadCells(10_000);
		var large = BadCells(MillionCells);
		bytes(small);
		var few = Math.Min(bytes(small), bytes(small));
		var many = Math.Min(bytes(large), bytes(large));
		output.WriteLine($"A thousand errors allocated {few:N0} bytes in a sheet of 10,000 rows and {many:N0} in one of 100,000.");
		Assert.True(Math.Abs(many - few) < 64 * 1024, $"10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		Assert.True(many < 8 * 1024 * 1024, $"A thousand errors allocated {many:N0} bytes.");
	}
#endif

	/// <summary>A workbook of one sheet, Upload: <see cref="TenNumbers"/>'s header, then rows of text in each column.</summary>
	/// <param name="rows">The rows after the header.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] BadCells(int rows)
	{
		using var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			using var sheet = workbook.AddSheet("Upload");
			foreach (var name in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" })
			{
				sheet.WriteField(name);
			}

			sheet.EndRecord();
			for (var r = 0; r < rows; r++)
			{
				for (var c = 0; c < 10; c++)
				{
					sheet.WriteField("x");
				}

				sheet.EndRecord();
			}
		}

		return stream.ToArray();
	}
}
