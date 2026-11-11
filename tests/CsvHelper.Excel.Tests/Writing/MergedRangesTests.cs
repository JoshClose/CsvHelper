using System.Diagnostics;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A written sheet's merged ranges (<see cref="MergedRanges"/>): the one a new range overlaps, found through every level
/// of the index, against a brute-force search over random ranges; many ranges, a tall one among them, in time that grows
/// as <c>n log n</c>; and the cells auto-fit leaves out.
/// </summary>
public class MergedRangesTests
{
	[Theory]
	[InlineData("B2:C3", "C3:D4", true)]
	[InlineData("B2:C3", "A1:B2", true)]
	[InlineData("B2:C3", "A1:D4", true)]
	[InlineData("B2:C3", "C1:C9", true)]
	[InlineData("B1:B9", "A5:C5", true)]
	[InlineData("B2:C3", "D2:E3", false)]
	[InlineData("B2:C3", "B4:C5", false)]
	[InlineData("B2:C3", "A1:A9", false)]
	[InlineData("A1:XFD1", "A2:XFD1048576", false)]
	[InlineData("A1:XFD1", "XFD1:XFD2", true)]
	public void FindsTheRangeANewOneOverlaps(string first, string second, bool overlaps)
	{
		var merged = new MergedRanges();
		merged.Add(ExcelRange.Parse(first));

		var found = merged.Overlapping(ExcelRange.Parse(second));

		Assert.Equal(overlaps ? ExcelRange.Parse(first) : null, found);
	}

	[Fact]
	public void FindsOverlapsAsABruteForceSearchDoes()
	{
		// Random ranges in a small grid, so that many overlap, across levels of every size.
		var random = new Random(4);
		for (var round = 0; round < 50; round++)
		{
			var merged = new MergedRanges();
			var kept = new List<ExcelRange>();
			for (var i = 0; i < 300; i++)
			{
				var row = random.Next(1, 200);
				var column = random.Next(1, 30);
				var range = new ExcelRange(row, column, row + random.Next(0, random.Next(2) == 0 ? 3 : 60), column + random.Next(0, 4));
				var expected = kept.Where(k => k.FirstRow <= range.LastRow && range.FirstRow <= k.LastRow && k.FirstColumn <= range.LastColumn && range.FirstColumn <= k.LastColumn).ToList();

				var found = merged.Overlapping(range);

				if (expected.Count == 0)
				{
					Assert.Null(found);
					merged.Add(range);
					kept.Add(range);
				}
				else
				{
					Assert.Contains(found!.Value, expected);
				}
			}

			Assert.Equal(kept, merged.Ranges);
			Assert.Equal(kept.Count, merged.Count);
		}
	}

	[Fact]
	public void AddsManyRangesBesideATallOneQuickly()
	{
		// A label merged down a whole column, and a range merged in each row beside it: each search meets the label and
		// its row's ranges, not every range above it.
		var merged = new MergedRanges();
		var watch = Stopwatch.StartNew();
		merged.Add(ExcelRange.Parse("A2:A200001"));
		for (var row = 2; row <= 200001; row++)
		{
			var range = new ExcelRange(row, 2, row, 3);
			Assert.Null(merged.Overlapping(range));
			merged.Add(range);
		}

		Assert.Equal(new ExcelRange(5000, 2, 5000, 3), merged.Overlapping(ExcelRange.Parse("C5000:D5000")));
		Assert.Equal(ExcelRange.Parse("A2:A200001"), merged.Overlapping(ExcelRange.Parse("A200001")));
		Assert.NotNull(merged.Overlapping(ExcelRange.Parse("A200001:XFD200001")));
		Assert.Null(merged.Overlapping(ExcelRange.Parse("D2:XFD1048576")));
		Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"200,001 ranges took {watch.Elapsed}.");
	}

	[Fact]
	public void LeavesOutCellsMergedAcrossColumnsFromAutoFit()
	{
		// As Excel's AutoFit: a range across columns counts nothing; one down a column counts each cell, below its first too.
		var merged = new MergedRanges();
		merged.Add(ExcelRange.Parse("A4:C4"));
		merged.Add(ExcelRange.Parse("D2:D3"));
		merged.Add(ExcelRange.Parse("8:9"));

		Assert.True(merged.HidesWidth(4, 0));
		Assert.True(merged.HidesWidth(4, 2));
		Assert.False(merged.HidesWidth(4, 3));
		Assert.False(merged.HidesWidth(2, 3));
		Assert.False(merged.HidesWidth(3, 3));
		Assert.True(merged.HidesWidth(9, 16383));
		Assert.False(merged.HidesWidth(10, 0));
		Assert.False(merged.HidesWidth(1048577, 0));
	}
}
