using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Security;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The corpus with raw skips on and off (Phase 9's step 2): every worksheet of every workbook the corpus tests read, read
/// through the public API with merged cells filled, with hidden rows skipped, from an area below its first rows, and with
/// all three, once with the workbook's raw skips (<c>ExcelWorkbook.rawSkips</c>) and once token by token, gives the same
/// records, or the same exception and message.
/// </summary>
public class RawSkipCorpusTests
{
	/// <summary>The readings compared: each skips rows, or <c>sheetData</c> in the read that finds the merged ranges, and all of them together.</summary>
	private static readonly (string Name, ExcelReaderOptions Options)[] Readings =
	[
		("merged cells filled", Options() with { FillMergedCells = true }),
		("hidden rows skipped", Options() with { SkipHiddenRows = true }),
		("rows 3 on", Options() with { Range = "3:1048576" }),
		("merged cells filled and hidden rows skipped in rows 10 on", Options() with { FillMergedCells = true, SkipHiddenRows = true, Range = "10:1048576" }),
	];

	[Theory]
	[MemberData(nameof(CorpusFiles.WorkbookData), MemberType = typeof(CorpusFiles))]
	public void ReadsTheSameWithRawSkipsAndWithout(string file)
	{
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
		{
			foreach (var (name, options) in Readings)
			{
				workbook.rawSkips = true;
				var raw = Read(workbook, sheet, options);
				workbook.rawSkips = false;
				var tokens = Read(workbook, sheet, options);

				var difference = FirstDifference(raw, tokens);
				Assert.True(difference is null, $"{file}, {sheet.Name}, {name}: {difference}");
			}
		}
	}

	/// <summary>The options of the readings: no header, and empty rows kept, so that every row is a record.</summary>
	/// <returns>The options.</returns>
	private static ExcelReaderOptions Options() => new() { Csv = new() { HasHeader = false, SkipEmptyLines = false } };

	/// <summary>Reads a sheet's records, each its line number and its fields, and then what it throws, if it throws.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The records, and the exception's type and message last.</returns>
	private static List<string> Read(ExcelWorkbook workbook, ExcelSheet sheet, ExcelReaderOptions options)
	{
		var records = new List<string>();
		try
		{
			using var reader = workbook.OpenReader(sheet, options);
			while (reader.Read())
			{
				records.Add($"{reader.LineNumber}: {Workbooks.Fields(reader)}");
			}
		}
		catch (Exception e) when (FuzzTests.IsExpected(e))
		{
			records.Add($"{e.GetType().Name}: {e.Message}");
		}

		return records;
	}

	/// <summary>The first record two reads differ in, described; <see langword="null"/> when they are the same.</summary>
	/// <param name="raw">The records read with raw skips.</param>
	/// <param name="tokens">The records read token by token.</param>
	/// <returns>The difference, or <see langword="null"/>.</returns>
	private static string? FirstDifference(List<string> raw, List<string> tokens)
	{
		for (var i = 0; i < Math.Max(raw.Count, tokens.Count); i++)
		{
			var withRaw = i < raw.Count ? raw[i] : "(no record)";
			var withTokens = i < tokens.Count ? tokens[i] : "(no record)";
			if (withRaw != withTokens)
			{
				return $"record {i + 1} of {raw.Count} and {tokens.Count}: raw skips read \"{withRaw}\", token skips \"{withTokens}\"";
			}
		}

		return null;
	}
}
