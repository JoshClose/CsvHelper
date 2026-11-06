using System.Text;
using CsvHelper.Excel.Tests.Api;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// Every corpus workbook's tables, defined names, merged ranges, and hidden rows and columns, as CsvHelper.Excel reads
/// them, against Excel 365's answers (<see cref="ExcelAnswers"/>, from the corpus tool's <c>oracle</c> command); and every
/// worksheet read through the public API with <see cref="ExcelReaderOptions.SkipHiddenRows"/>,
/// <see cref="ExcelReaderOptions.SkipHiddenColumns"/> and <see cref="ExcelReaderOptions.FillMergedCells"/>, against the
/// cells Excel shows and its merged ranges. They agree except where listed, each with its reason.
/// </summary>
/// <remarks>
/// Excel's hidden rows and columns cover the whole grid. A reader can only show which of the rows the file has are
/// hidden, so rows are compared from 1 to the file's last row, and columns over the whole grid.
/// </remarks>
public class StructureAnswersTests
{
	private const string Unions = "Excel's RefersToRange gives a union (Report!$B$5,Report!$B$8) as a range of two areas; CsvHelper.Excel's Range is one block of cells (decision 5)";

	private const string DynamicRanges = "Excel's RefersToRange evaluates a formula that gives a range (OFFSET); CsvHelper.Excel's Range is a reference alone (decision 5)";

	private const string TableReferences = "Excel's RefersToRange resolves a table reference (Sales[Qty]) to the column's data cells; CsvHelper.Excel doesn't resolve table references (decision 5)";

	private const string DeletedSheet = "Excel shows a name to a deleted sheet, which it stores as #REF!, as #REF!#REF!";

	private const string LowercaseDeleted = "LibreOffice writes a name to a deleted sheet as #ref!, which Excel shows as #REF!#REF!";

	/// <summary>The messy workbooks whose names Excel answers for: openpyxl's it can't open (<see cref="ExcelAnswersTests.Refused"/>).</summary>
	private static readonly string[] MessyFiles = ["closedxml/messy.xlsx", "excel/messy.xlsx", "libreoffice/messy.xlsx", "poi/messy.xlsx", "xlsxwriter/messy.xlsx"];

	/// <summary>The defined names where Excel's answers differ from CsvHelper.Excel's, and why.</summary>
	private static readonly (string File, string Name, string Reason)[] NameDifferences =
	[
		.. MessyFiles.SelectMany(f => new[]
		{
			(f, "Union", Unions),
			(f, "Dynamic", DynamicRanges),
			(f, "TableRef", TableReferences),
			(f, "Deleted", f == "libreoffice/messy.xlsx" ? LowercaseDeleted : DeletedSheet),
		}),
	];

	/// <summary>The switches each worksheet is read with, alone and together.</summary>
	private static readonly (bool Rows, bool Columns, bool Merged)[] Switches = [(true, true, false), (false, false, true), (true, true, true)];

	/// <summary>Every answered workbook, sync and async.</summary>
	public static IEnumerable<object[]> AnsweredModes =>
		ExcelAnswersTests.AnsweredWorkbooks.SelectMany(w => new[] { new object[] { w[0], false }, new object[] { w[0], true } });

	[Theory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheTablesAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));

		// Excel's Name is the part's name attribute, and DisplayName the one formulas use, which ExcelTable.Name is.
		Assert.All(answers.Tables, t => Assert.Equal(t.Name, t.DisplayName));
		Assert.Equal(
			answers.Tables.Select(t => CorpusFiles.TableText(t.Sheet, t.DisplayName, t.Range, t.ShowHeaders ? 1 : 0, t.ShowTotals ? 1 : 0, t.Columns)).OrderBy(t => t, StringComparer.Ordinal),
			CorpusFiles.Tables(workbook));
	}

	[Theory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheNamesAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		// Excel adds a hidden name of #NAME? for each newer function a formula stores with its _xlfn. prefix, which no file
		// of the corpus stores.
		var added = answers.Names.Where(n => n.Name.StartsWith("_xlfn.", StringComparison.Ordinal)).ToList();
		Assert.All(added, n => Assert.Equal("hidden =#NAME?", $"{(n.Visible ? "shown" : "hidden")} {n.RefersTo}"));
		var excel = answers.Names.Except(added).ToDictionary(n => n.Name, n => $"{(n.Visible ? "shown" : "hidden")} {n.RefersTo} {(n.Range is null ? "no range" : n.Sheet + "!" + n.Range)}");
		var ours = workbook.DefinedNames.ToDictionary(n => n.ToString(), n => $"{CorpusFiles.NameText(n)} {(n.Range is { } range ? n.Sheet!.Name + "!" + range : "no range")}");

		Assert.Equal(excel.Keys.OrderBy(k => k, StringComparer.Ordinal), ours.Keys.OrderBy(k => k, StringComparer.Ordinal));
		var differences = excel.Keys.Where(k => excel[k] != ours[k]).OrderBy(k => k, StringComparer.Ordinal).Select(k => $"{k}: ours {ours[k]}, Excel {excel[k]}").ToList();
		var listed = NameDifferences.Where(d => d.File == file).Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal);
		Assert.True(
			listed.SequenceEqual(differences.Select(d => d.Substring(0, d.IndexOf(':')))),
			$"{file}: expected differences in [{string.Join(", ", listed)}], found:\n{string.Join("\n", differences)}");
	}

	[Theory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheMergedRangesAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var ours = CorpusFiles.Structures(file);

		Assert.Equal(answers.Structures.Keys.OrderBy(k => k, StringComparer.Ordinal), ours.Keys.OrderBy(k => k, StringComparer.Ordinal));
		foreach (var sheet in ours.Keys)
		{
			Assert.True(
				answers.Structures[sheet].Merged.SequenceEqual(ours[sheet].Merged),
				$"{file}, {sheet}: Excel [{string.Join(", ", answers.Structures[sheet].Merged)}], ours [{string.Join(", ", ours[sheet].Merged)}]");
		}
	}

	[Theory]
	[MemberData(nameof(ExcelAnswersTests.AnsweredWorkbooks), MemberType = typeof(ExcelAnswersTests))]
	public void ReadsTheHiddenRowsAndColumnsAsExcelDoes(string file)
	{
		var answers = ExcelAnswers.Load(file)!;
		var ours = CorpusFiles.Structures(file);
		var differences = new List<string>();
		foreach (var pair in ours)
		{
			var (sheet, mine) = (pair.Key, pair.Value);
			var excel = answers.Structures[sheet];
			for (var row = 1L; row <= mine.LastRow; row++)
			{
				if (excel.IsRowHidden(row) != mine.HiddenRows.Contains(row))
				{
					differences.Add($"{sheet} row {row}: ours {(mine.HiddenRows.Contains(row) ? "hidden" : "shown")}, Excel {(excel.IsRowHidden(row) ? "hidden" : "shown")}");
				}
			}

			for (var column = 0; column < mine.HiddenColumns.Length; column++)
			{
				if (excel.IsColumnHidden(column) != mine.HiddenColumns[column])
				{
					differences.Add($"{sheet} column {column + 1}: ours {(mine.HiddenColumns[column] ? "hidden" : "shown")}, Excel {(excel.IsColumnHidden(column) ? "hidden" : "shown")}");
				}
			}
		}

		Assert.True(differences.Count == 0, $"{file}:\n{string.Join("\n", differences.Take(20))}");
	}

	[Theory]
	[MemberData(nameof(AnsweredModes))]
	public async Task ReadsWithTheSwitchesAsExcelShowsTheCells(string file, bool async)
	{
		var answers = ExcelAnswers.Load(file)!;
		var cells = CorpusFiles.Read(file, out _);
		using var workbook = async ? await ExcelWorkbook.OpenAsync(CorpusFiles.PathOf(file)) : ExcelWorkbook.Open(CorpusFiles.PathOf(file));
		var differences = new List<string>();
		foreach (var (rows, columns, merged) in Switches)
		{
			var options = new ExcelReaderOptions
			{
				SkipHiddenRows = rows,
				SkipHiddenColumns = columns,
				FillMergedCells = merged,
				Csv = new() { HasHeader = false, SkipEmptyLines = false },
			};
			var switches = $"{(rows ? "rows " : "")}{(columns ? "columns " : "")}{(merged ? "merged" : "")}".Trim();
			foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
			{
				var excel = answers.Structures[sheet.Name];
				var expected = Shown(cells[sheet.Name], excel, rows, columns, merged);

				// Field i is the i-th column Excel shows, and record n's line is its row; with empty rows kept, the rows are
				// each row Excel shows, up to the last one read.
				var fieldColumns = Enumerable.Range(0, CellReference.MaxColumns).Where(c => !(columns && excel.IsColumnHidden(c))).ToList();
				var location = new SheetLocation(sheet.Name);
				var read = new HashSet<(long, int)>();
				var lines = new List<long>();
				using var reader = await workbook.OpenReaderAsync(async, sheet, options);
				while (async ? await reader.ReadAsync() : reader.Read())
				{
					var row = reader.LineNumber;
					lines.Add(row);
					for (var field = 0; field < reader.FieldCount; field++)
					{
						var column = fieldColumns[field];
						read.Add((row, column));
						var stored = Encoding.UTF8.GetString(reader.GetSpan(field).ToArray());
						var text = expected.TryGetValue((row, column), out var value) ? value.Text : "";
						if (stored != text)
						{
							differences.Add($"{switches}: {location.Cell(column, row)}: ours {CellValue.Show(stored)}, Excel {CellValue.Show(text)}");
						}
					}
				}

				var shownRows = Enumerable.Range(1, lines.Count == 0 ? 0 : (int)lines[lines.Count - 1]).Select(r => (long)r).Where(r => !(rows && excel.IsRowHidden(r)));
				if (!shownRows.SequenceEqual(lines))
				{
					differences.Add($"{switches}: {location.QuotedName}: ours rows [{string.Join(", ", lines)}], Excel's shown rows [{string.Join(", ", shownRows)}]");
				}

				foreach (var key in expected.Keys.Where(k => !read.Contains(k)).OrderBy(k => k.Row).ThenBy(k => k.Column))
				{
					differences.Add($"{switches}: {location.Cell(key.Column, key.Row)}: ours unread, Excel {expected[key]}");
				}
			}
		}

		Assert.True(differences.Count == 0, $"{file}:\n{string.Join("\n", differences.Take(30))}");
	}

	/// <summary>
	/// The cells Excel shows with the switches: each cell of a merged range as its top-left cell, or nothing when that has
	/// no value; then without the cells of hidden rows and columns.
	/// </summary>
	/// <param name="cells">The sheet's cells, read without the switches.</param>
	/// <param name="excel">Excel's hidden rows and columns and merged ranges.</param>
	/// <param name="rows">Whether hidden rows are skipped.</param>
	/// <param name="columns">Whether hidden columns are skipped.</param>
	/// <param name="merged">Whether merged cells are filled.</param>
	/// <returns>The cells, by row and 0-based column.</returns>
	private static Dictionary<(long Row, int Column), CellValue> Shown(
		Dictionary<(long Row, int Column), CellValue> cells, ExcelSheetStructure excel, bool rows, bool columns, bool merged)
	{
		var shown = new Dictionary<(long Row, int Column), CellValue>(cells);
		foreach (var range in merged ? excel.Merged.Select(ExcelRange.Parse) : Enumerable.Empty<ExcelRange>())
		{
			var hasTop = cells.TryGetValue((range.FirstRow, range.FirstColumn - 1), out var top);
			for (long row = range.FirstRow; row <= range.LastRow; row++)
			{
				for (var column = range.FirstColumn - 1; column < range.LastColumn; column++)
				{
					if (hasTop)
					{
						shown[(row, column)] = top;
					}
					else
					{
						shown.Remove((row, column));
					}
				}
			}
		}

		return shown
			.Where(c => !(rows && excel.IsRowHidden(c.Key.Row)) && !(columns && excel.IsColumnHidden(c.Key.Column)))
			.ToDictionary(c => c.Key, c => c.Value);
	}
}
