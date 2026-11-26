using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Security;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The corpus in segments (Phase 9's step 8, the plan's differential): every worksheet of every workbook the corpus tests
/// read, under each reading option alone and 16 seeded combinations of them, read under
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> at degrees 1, 2, 3, 8 and 16 with segments forced
/// at a byte and at 4 KB, gives what the sequential read with the same options gives: each record's line, each field's
/// stored bytes, text (shown text under <see cref="ExcelText.Formatted"/>) and typed reads, the header, and the error, if
/// one ends the read. Formulas read as text are read serially, as is every read at degree 1: those start no segments.
/// Merged cells filled are read serially when a range fills a field; a sheet without one reads in segments, as unfilled. It runs alone (<see cref="TimedCollection"/>), so that its time limit measures its own reads.
/// </summary>
/// <param name="output">Shows each workbook's reads and what the segment counters added.</param>
[Collection(TimedCollection.Name)]
public class SegmentCorpusTests(ITestOutputHelper output)
{
	/// <summary>The seeded combinations of <see cref="Choices"/> each sheet is read under, besides each alone.</summary>
	private const int Combinations = 16;

	/// <summary>The options the plan lists, each applied by <see cref="Apply"/>: the last two are the serial cases.</summary>
	private static readonly string[] Choices =
	[
		"formatted", "error cells as null", "error cells as errors", "hidden rows skipped", "hidden columns skipped", "empty rows kept", "B2:Z300",
		"a header search", "strings unpooled", "merged cells filled", "formulas as text",
	];

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public async Task ReadsEverySheetInSegmentsAsSequentially(string file)
	{
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		var counts = default(SegmentCounts);
		var reads = 0;
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
		{
			foreach (var (name, options) in Readings())
			{
				var expected = ParallelReads.ReadSequentially(workbook, sheet, options);
				var serial = options.Formulas == ExcelFormulas.FormulaText;
				foreach (var size in ParallelReads.Sizes)
				{
					foreach (var degree in ParallelReads.CorpusDegrees)
					{
						ParallelReads.Force(workbook, size);
						var what = $"{file}, {sheet.Name}, {name}, degree {degree}, segments of {size} bytes";

						var actual = await ParallelReads.WithinAsync(() => ParallelReads.ReadInParallel(workbook, sheet, options, degree), what);

						var difference = ParallelReads.Difference(expected, actual);
						Assert.True(difference is null, $"{what}: {difference}");
						Assert.True(!(serial || degree == 1) || actual.Counts.Starts == 0, $"{what}: read serially, yet {actual.Counts}.");
						counts += actual.Counts;
						reads++;
					}
				}
			}
		}

		output.WriteLine($"{file}: {reads} parallel reads; {counts}.");
	}

	/// <summary>
	/// The readings of each sheet: with no header and empty rows skipped, so that every row with a value is a record, and a
	/// buffer of 256 bytes, so that the segments begin at the first fill; then each of <see cref="Choices"/> alone; then
	/// <see cref="Combinations"/> combinations, each choice in a third of them, from seeds of their own.
	/// </summary>
	/// <returns>Each reading's name and options.</returns>
	private static IEnumerable<(string Name, ExcelReaderOptions Options)> Readings()
	{
		var plain = new ExcelReaderOptions { Csv = new() { HasHeader = false, BufferSize = 256 } };
		yield return ("plain", plain);
		for (var i = 0; i < Choices.Length; i++)
		{
			yield return (Choices[i], Apply(plain, i));
		}

		for (var combination = 1; combination <= Combinations; combination++)
		{
			var random = new Random(1000 + combination);
			var options = plain;
			var names = new List<string>();
			for (var i = 0; i < Choices.Length; i++)
			{
				if (random.Next(3) == 0)
				{
					options = Apply(options, i);
					names.Add(Choices[i]);
				}
			}

			yield return ($"combination {combination} ({string.Join(", ", names)})", options);
		}
	}

	/// <summary>Applies one of <see cref="Choices"/> to options.</summary>
	/// <param name="options">The options.</param>
	/// <param name="choice">The choice's index.</param>
	/// <returns>The options with it.</returns>
	private static ExcelReaderOptions Apply(ExcelReaderOptions options, int choice) => Choices[choice] switch
	{
		"formatted" => options with { Text = ExcelText.Formatted },
		"error cells as null" => options with { ErrorCells = ExcelErrorCells.Null },
		"error cells as errors" => options with { ErrorCells = ExcelErrorCells.Error },
		"hidden rows skipped" => options with { SkipHiddenRows = true },
		"hidden columns skipped" => options with { SkipHiddenColumns = true },
		"empty rows kept" => options with { Csv = options.Csv with { SkipEmptyLines = false } },

		// An area that bounds rows at both ends, which ends inside the larger sheets.
		"B2:Z300" => options with { Range = "B2:Z300" },

		// The first row with a value, of the first 20 read, is the header.
		"a header search" => options with { HeaderRow = ExcelHeaderRow.Find([]), Csv = options.Csv with { HasHeader = true } },
		"strings unpooled" => options with { PoolStrings = false },
		"merged cells filled" => options with { FillMergedCells = true },
		_ => options with { Formulas = ExcelFormulas.FormulaText },
	};
}
