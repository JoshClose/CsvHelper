using System.Text;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// <see cref="HiddenColumns"/>, the bits of a sheet's hidden columns; <see cref="SheetLayout.Without"/>, the map of
/// fields to the shown columns; and <see cref="HiddenState.ZeroOf(ReadOnlySpan{byte})"/>, the numbers Excel reads.
/// </summary>
public class HiddenColumnsTests
{
	[Fact]
	public void SetsColumnsAsTheLastColSays()
	{
		var hidden = new HiddenColumns();
		hidden.Set(1, 3, true);
		hidden.Set(2, 2, false);
		hidden.Set(63, 64, true);
		hidden.Set(16383, 16383, true);

		Assert.Equal([1, 3, 63, 64, 16383], Hidden(hidden));

		// The default covers only the columns no col covers.
		hidden.HidesUncovered = true;
		Assert.True(hidden.IsHidden(0));
		Assert.False(hidden.IsHidden(2));
		Assert.True(hidden.IsHidden(65));
		Assert.Equal(16384 - 1, Enumerable.Range(0, 16384).Count(hidden.IsHidden));
	}

	[Fact]
	public void MatchesAModelOverRandomCols()
	{
		for (var seed = 0; seed < 200; seed++)
		{
			var random = new Random(seed);
			var hidden = new HiddenColumns { HidesUncovered = random.Next(2) == 0 };
			var covered = new bool[16384];
			var model = new bool[16384];
			for (var cols = random.Next(1, 20); cols > 0; cols--)
			{
				// Ranges within a word, across words, at their edges, and over the whole sheet.
				var first = random.Next(4) == 0 ? random.Next(4) * 64 + random.Next(2) * 63 : random.Next(16384);
				var last = Math.Min(16383, first + (random.Next(3) == 0 ? random.Next(16384) : random.Next(130)));
				var hides = random.Next(2) == 0;
				hidden.Set(first, last, hides);
				for (var column = first; column <= last; column++)
				{
					covered[column] = true;
					model[column] = hides;
				}
			}

			for (var column = 0; column < 16384; column++)
			{
				Assert.True((covered[column] ? model[column] : hidden.HidesUncovered) == hidden.IsHidden(column), $"Seed {seed}, column {column}.");
			}
		}
	}

	[Fact]
	public void MapsFieldsToTheShownColumns()
	{
		var hidden = new HiddenColumns();
		hidden.Set(2, 2, true);
		hidden.Set(7, 16383, true);

		// A whole sheet: A, B, D, E, F and G, and a map only up to G.
		var sheet = SheetLayout.Sheet.Without(hidden);
		Assert.Equal(6, sheet.Width);
		Assert.Equal([0, 1, -1, 2, 3, 4, 5, -1, -1], Enumerable.Range(0, 9).Select(sheet.FieldOf));
		Assert.Equal(-1, sheet.FieldOf(16383));
		Assert.Equal([0, 1, 3, 4, 5, 6, -1, -1], Enumerable.Range(0, 8).Select(sheet.ColumnOf));
		Assert.Equal(-1, sheet.ColumnOf(-1));

		// An area: from its first column.
		var area = new SheetLayout(1, 10, 1, 8).Without(hidden);
		Assert.Equal(5, area.Width);
		Assert.Equal([-1, 0, -1, 1, 2, 3, 4, -1, -1, -1], Enumerable.Range(0, 10).Select(area.FieldOf));
		Assert.Equal([1, 3, 4, 5, 6, -1], Enumerable.Range(0, 6).Select(area.ColumnOf));
		Assert.Equal((1L, 10L, 1, 8), (area.FirstRow, area.LastRow, area.FirstColumn, area.LastColumn));

		// Every column hidden: no fields. None hidden: the same layout.
		var none = new SheetLayout(1, 10, 2, 2).Without(hidden);
		Assert.Equal(0, none.Width);
		Assert.Equal(-1, none.FieldOf(2));
		Assert.Equal(-1, none.ColumnOf(0));
		var shown = new SheetLayout(1, 10, 3, 6);
		Assert.Same(shown, shown.Without(hidden));
	}

	[Theory]
	[InlineData("", true)]
	[InlineData("0", true)]
	[InlineData("00", true)]
	[InlineData("0.0", true)]
	[InlineData("-0", true)]
	[InlineData(".0", true)]
	[InlineData("0.", true)]
	[InlineData("0e+0", true)]
	[InlineData("0E5", true)]
	[InlineData("-0.000e-3", true)]
	[InlineData("1", false)]
	[InlineData("0.2", false)]
	[InlineData("-1", false)]
	[InlineData("9.", false)]
	[InlineData(".5e1", false)]
	[InlineData("1e-9", false)]
	[InlineData("1e+0", false)]
	[InlineData("8.88671875", false)]
	[InlineData(" 0", null)]
	[InlineData("0 ", null)]
	[InlineData("+0", null)]
	[InlineData("abc", null)]
	[InlineData("0x0", null)]
	[InlineData("0,0", null)]
	[InlineData("-", null)]
	[InlineData(".", null)]
	[InlineData("-.", null)]
	[InlineData("e5", null)]
	[InlineData("0e", null)]
	[InlineData("1e+", null)]
	[InlineData("--0", null)]
	[InlineData("0..0", null)]
	[InlineData("1.2.3", null)]
	[InlineData("0e1.5", null)]
	public void ReadsNumbersAsExcelDoes(string text, bool? zero)
	{
		Assert.Equal(zero, HiddenState.ZeroOf(Encoding.UTF8.GetBytes(text)));
	}

	/// <summary>The hidden columns of a set.</summary>
	/// <param name="hidden">The set.</param>
	/// <returns>The columns, in order.</returns>
	private static int[] Hidden(HiddenColumns hidden) => [.. Enumerable.Range(0, 16384).Where(hidden.IsHidden)];
}
