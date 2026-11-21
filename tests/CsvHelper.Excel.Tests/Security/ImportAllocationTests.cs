#if NET
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// What the import's limit on errors allocates (Phase 7's step 8): the errors', whatever the sheet's size. It runs alone, in
/// <see cref="TimedCollection"/>: beside the tests that run in parallel, their garbage collections trimmed the shared array
/// pool, so a pass rented its buffers again, and on CI a sheet of 10,000 rows measured 66 KB and 157 KB more than one of
/// 100,000 (net8.0, twice on 2026-11-20), where alone both measure the same bytes.
/// </summary>
/// <param name="output">Shows what the test measured.</param>
[Collection(TimedCollection.Name)]
public class ImportAllocationTests(ITestOutputHelper output)
{
	/// <summary>
	/// A thousand errors allocate a few megabytes, and no more in a sheet of 100,000 rows than in one of 10,000: what the
	/// import holds doesn't grow with the sheet.
	/// </summary>
	[OptimizedFact]
	public void AllocatesForTheErrorsWhateverTheSheetsSize()
	{
		var small = ImportSecurityTests.BadCells(10_000);
		var large = ImportSecurityTests.BadCells(ImportSecurityTests.MillionCells);

		// Both sizes run once first, then three times each, and the smallest is kept, as the writer's allocation tests keep
		// theirs: the runtime allocates once at a moment tiering sets.
		Allocated(small);
		Allocated(large);
		var few = Math.Min(Allocated(small), Math.Min(Allocated(small), Allocated(small)));
		var many = Math.Min(Allocated(large), Math.Min(Allocated(large), Allocated(large)));
		output.WriteLine($"A thousand errors allocated {few:N0} bytes in a sheet of 10,000 rows and {many:N0} in one of 100,000.");

		Assert.True(many - few < 64 * 1024, $"10,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
		Assert.True(many < 8 * 1024 * 1024, $"A thousand errors allocated {many:N0} bytes.");
	}

	/// <summary>Imports a workbook's sheet, which stops at a thousand errors, and measures what the import allocates.</summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <returns>The bytes the import allocated on this thread.</returns>
	private static long Allocated(byte[] zip)
	{
		using var reader = ExcelReader.Create(new MemoryStream(zip));
		reader.ReadHeader();

		var before = GC.GetAllocatedBytesForCurrentThread();
		var result = ExcelReader.Import<TenNumbers>(reader);
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal(1000, result.Errors.Count);

		return allocated;
	}
}
#endif
