using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The memory a written sheet's links hold, which Phase 5's step 5 left for step 7 to measure: each distinct target once,
/// about two bytes a UTF-16 code unit, until the sheet ends; and nothing of a record discarded, whose links the cap of
/// 65,530 doesn't count. The tests measure the process's memory, so they run alone (<see cref="TimedCollection"/>).
/// <c>CSVHELPER_EXCEL_LINK_TARGETS</c> sets the number of distinct targets of the first, 2,048 by default; 65,530 measures
/// Excel's worst case, about 1 GiB.
/// </summary>
/// <param name="output">Shows the memory measured.</param>
[Collection(TimedCollection.Name)]
public class LinkMemoryTests(ITestOutputHelper output)
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The most UTF-16 code units a cell's text has.</summary>
	private const int MaxCell = 32767;

	[Fact]
	public void HoldsEachDistinctTargetOnce()
	{
		// A sheet's 65,530 links, which use the distinct targets in turn, each of 8,192 code units, the longest Excel opens.
		var distinct = Math.Min(WriterFuzz.Setting("CSVHELPER_EXCEL_LINK_TARGETS", 2048), SheetLinks.MaxLinks);
		var before = GC.GetTotalMemory(true);
		var links = new SheetLinks();
		for (var i = 0; i < SheetLinks.MaxLinks; i++)
		{
			// Each link's target is a string of its own, as each cell's text is: the links keep the first of each.
			links.Add(i + 1, 0, Target(i % distinct));
			links.EndRecord();
		}

		var held = GC.GetTotalMemory(true) - before;
		Assert.Equal(distinct, links.Targets);
		GC.KeepAlive(links);

		var units = (long)distinct * SheetLinks.MaxTargetLength;
		var each = (double)(held - (12L * SheetLinks.MaxLinks)) / distinct;
		output.WriteLine(
			$"{SheetLinks.MaxLinks:N0} links to {distinct:N0} distinct targets of {SheetLinks.MaxTargetLength:N0} code units ({units:N0} in all): " +
			$"{held:N0} bytes held, about {each:N0} a target; {SheetLinks.MaxLinks:N0} such targets would hold about {each * SheetLinks.MaxLinks / MiB:N0} MiB.");

		// Two bytes a code unit, the strings' and the lookup's few bytes a target, and the links' 12 bytes each, in lists
		// that may have twice the room they use.
		Assert.True(held <= (2 * units) + (128L * distinct) + (24L * SheetLinks.MaxLinks) + MiB, $"{held:N0} bytes held for {units:N0} code units of targets");
	}

	[Fact]
	public void HoldsNothingOfADiscardedRecordsLinks()
	{
		// Each record fails after a link of its own, of 8,192 code units: their targets would hold 32 MiB if they stayed.
		const int failed = 2000;
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false }, Sheets = new ExcelSheetOptions { AutoFit = 0 } };
		var tooLong = new string('x', MaxCell + 1);
		var stream = new MemoryStream();
		long held;
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Links");
			writer.WriteRecord(new Dictionary<string, object?> { ["Link"] = new Uri("https://example.com/first"), ["Note"] = "kept" });
			var before = GC.GetTotalMemory(true);
			for (var i = 0; i < failed; i++)
			{
				var record = new Dictionary<string, object?> { ["Link"] = new Uri(Target(i)), ["Note"] = tooLong };
				Assert.Throws<InvalidOperationException>(() => writer.WriteRecord(record));
			}

			held = GC.GetTotalMemory(true) - before;
			Assert.Equal(1, ExcelWorkbookWriter.SinkOf(writer)!.LinkTargets);
			writer.WriteRecord(new Dictionary<string, object?> { ["Link"] = new Uri("https://example.com/last"), ["Note"] = "kept" });
		}

		output.WriteLine($"{failed:N0} records discarded, each with a link to a target of its own of {SheetLinks.MaxTargetLength:N0} code units: {held:N0} bytes held.");
		Assert.True(held < MiB, $"{held:N0} bytes held");

		var written = stream.ToArray();
		Assert.Equal(["https://example.com/first|kept", "https://example.com/last|kept"], Written.ReadOne(written));
		Assert.Equal(2, Regex.Matches(Written.Entry(written, "xl/worksheets/_rels/sheet1.xml.rels"), "<Relationship ").Count);
	}

	/// <summary>A distinct target of the longest length Excel opens, made anew at each call.</summary>
	/// <param name="number">The target's number.</param>
	/// <returns>The target, of <see cref="SheetLinks.MaxTargetLength"/> code units.</returns>
	private static string Target(int number)
	{
		var start = "https://example.com/" + number.ToString("D5", CultureInfo.InvariantCulture) + "/";

		return start + new string('a', SheetLinks.MaxTargetLength - start.Length);
	}
}
