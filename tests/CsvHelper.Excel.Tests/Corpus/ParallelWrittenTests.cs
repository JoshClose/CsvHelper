using CsvHelper.Excel.Corpus;
using CsvHelper.Excel.Tests.Api;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The written set (<c>tools/CsvHelper.Excel.Corpus/Written.cs</c>) written with the parallel write methods (Phase 9's
/// step 10): each workbook, synchronously and asynchronously, has its serial write's parts, by the parts' SHA-256
/// (<see cref="Written.PartsHash"/>), which <see cref="WrittenAnswersTests"/> pins to Excel's answers. The set's sheets are
/// small, and core writes a source that ends within its first batch, 64 items, on the calling thread, so a second test
/// writes each workbook with its sheets' records repeated to 300 (<see cref="WrittenWorkbook.Repeated"/>), most of them in
/// the workers, in batches of one item and of their XML's length: every type, format, culture, look and option of the
/// set through the workers, each workbook with its own serial write's parts.
/// </summary>
/// <param name="output">Shows what each repeated workbook's workers did.</param>
public class ParallelWrittenTests(ITestOutputHelper output)
{
	/// <summary>The records each repeated sheet has: past auto-fit's 100 and core's first batch of 64.</summary>
	private const int RepeatedRecords = 300;

	/// <summary>A report length large enough that core's batches hold one item each.</summary>
	private const int OneItemBatches = 1 << 20;

	/// <summary>
	/// Repeated workbooks whose workers must write chunks: sheets of records of every type in Phase 4's look, without
	/// auto-fit; in the default look, with links; and in formats.
	/// </summary>
	private static readonly string[] Reaching = ["types.xlsx", "formats.xlsx", "look.xlsx", "links.xlsx"];

	/// <summary>The workbooks of the set on this target.</summary>
	public static TheoryData<string> Workbooks => WrittenAnswersTests.Workbooks;

	[Theory]
	[MemberData(nameof(Workbooks))]
	public async Task WritesTheWorkbookInParallel(string name)
	{
		var workbook = Written.Workbooks.Single(w => w.Name == name);
		var serial = Writing.Written.Checked(Written.Write(workbook));

		AssertSameParts(serial, Writing.Written.Checked(Written.Write(workbook, new WrittenParallel(3))));
		AssertSameParts(serial, Writing.Written.Checked(await Written.WriteAsync(workbook, new WrittenParallel(-1))));
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public async Task WritesTheRepeatedWorkbookInParallel(string name)
	{
		var workbook = Written.Workbooks.Single(w => w.Name == name).Repeated(RepeatedRecords);
		var serial = Writing.Written.Checked(Written.Write(workbook));
		var single = new WrittenParallel(4, OneItemBatches);
		var batches = new WrittenParallel(2);

		AssertSameParts(serial, Writing.Written.Checked(Written.Write(workbook, single)));
		AssertSameParts(serial, Writing.Written.Checked(await Written.WriteAsync(workbook, batches)));
		output.WriteLine($"{name}: one-item batches, {single.ChunksAppended:N0} chunks appended and {single.ChunksRedone:N0} written again; batches of their XML, {batches.ChunksAppended:N0} and {batches.ChunksRedone:N0}.");
		if (Reaching.Contains(name))
		{
			Assert.True(single.ChunksAppended > 0 && batches.ChunksAppended > 0, $"{name}'s workers wrote no chunk: {single.ChunksAppended} and {batches.ChunksAppended}.");
		}
	}

	[Fact]
	public void RepeatsTheSheetsOfRecordsAndOfRows()
	{
		// A sheet of records and one written by hand repeat their records after the header; one that merges ranges and a
		// template keep theirs.
		var repeated = Written.Workbooks.Single(w => w.Name == "autofit.xlsx").Repeated(RepeatedRecords);
		Assert.All(repeated.Sheets, s => Assert.Equal(RepeatedRecords + 1, s.Rows.Count));
		Assert.Equal(Written.Workbooks.Single(w => w.Name == "autofit.xlsx").Sheets[0].Rows[1], repeated.Sheets[0].Rows[3]);

		var merges = Written.Workbooks.Single(w => w.Name == "merges.xlsx");
		Assert.All(merges.Repeated(RepeatedRecords).Sheets.Zip(merges.Sheets, (r, s) => (r, s)), p => Assert.Same(p.s, p.r));

		var template = Written.Workbooks.Single(w => w.Name == "template.xlsx");
		Assert.All(template.Repeated(RepeatedRecords).Sheets.Zip(template.Sheets, (r, s) => (r, s)), p => Assert.Same(p.s, p.r));
	}

	/// <summary>Fails unless a parallel write's package has the serial write's parts, by their hash, saying where they differ.</summary>
	/// <param name="serial">The serial write's package.</param>
	/// <param name="parallel">The parallel write's.</param>
	private static void AssertSameParts(byte[] serial, byte[] parallel)
	{
		var same = Written.PartsHash(serial) == Written.PartsHash(parallel);
		Assert.True(same, same ? null : "The parallel write's " + ParallelWrites.PartsDifference(serial, parallel));
	}
}
