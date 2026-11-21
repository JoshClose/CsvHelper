using System.Text;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// The workbook's raw skips (<c>ExcelWorkbook.rawSkips</c>, Phase 9's step 2) reaching the reads a reader opens: the read
/// that finds the merged ranges skips <c>sheetData</c>, and the sheet's scanner the rows it skips whole, by a raw search
/// when it is on and token by token when it is off, as XML that can't be read in what they skip shows.
/// </summary>
public class RawSkipReadingTests
{
	/// <summary>What is wrong with markup that starts with <c>&lt;!</c> and is neither a comment nor a CDATA section.</summary>
	private const string Bang = "'<!' starts neither a comment nor a CDATA section";

	/// <summary>What is wrong with a <c>&lt;</c> in an attribute value.</summary>
	private const string LessThan = "an attribute value has '<', which XML doesn't allow";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FindsTheRowsErrorsInThePreReadOnlyWithoutRawSkips(bool async)
	{
		// Row 5's XML can't be read, between its cells or in an attribute of a cell's v. With merged cells filled, token by
		// token the read that finds the ranges meets it first, when the reader opens, for the sheet; raw, it doesn't read the
		// rows, and the reader meets it at row 5 or its cell.
		var rows = string.Concat(Enumerable.Range(1, 4).Select(r => $"<row r=\"{r}\"><c r=\"A{r}\"><v>{r}</v></c></row>"));
		var cases = new (string Row, string At, int From, string Detail, string Subject, string Location)[]
		{
			("<row r=\"5\"><c r=\"A5\"><v>5</v></c><!x><c r=\"B5\"><v>55</v></c></row>", "<!x>", 0, Bang, "Row Data!5:5", "Data!5:5"),
			("<row r=\"5\"><c r=\"A5\"><v>5</v></c><c r=\"B5\"><v note=\"a<b\">55</v></c></row>", "a<b", 1, LessThan, "Cell Data!B5", "Data!B5"),
		};
		var options = new ExcelReaderOptions { FillMergedCells = true, Csv = new() { HasHeader = false } };

		foreach (var (row, at, from, detail, subject, location) in cases)
		{
			var xml = MergedScannerTests.Worksheet(rows + row, "A1:B1");
			var offset = OffsetOf(xml, at) + from;
			using var workbook = await Workbooks.OpenAsync(TestPackage.Workbook(new TestSheet("Data", xml: xml)), async);

			workbook.rawSkips = false;
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => workbook.OpenReaderAsync(async, options));
			Assert.Equal($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
			Assert.Null(e.Location);

			workbook.rawSkips = true;
			using var reader = await workbook.OpenReaderAsync(async, options);
			e = await Assert.ThrowsAsync<CsvFormatException>(() => reader.ReadAllAsync(async));
			Assert.StartsWith($"{subject} can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
			Assert.Equal(location, e.Location);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeavesSkippedRowsUncheckedOnlyWithRawSkips(bool async)
	{
		// A hidden row whose XML can't be read after its first cell, skipped with hidden rows, or above the area: raw, the
		// shown rows read; token by token, the skip meets it, for the sheet.
		var cases = new (string Fault, string At, int From, string Detail)[]
		{
			("<!x>", "<!x>", 0, Bang),
			("<c r=\"B2\" p=\"a<b\"/>", "a<b", 1, LessThan),
		};
		var readings = new (ExcelReaderOptions Options, string[] Records)[]
		{
			(new ExcelReaderOptions { SkipHiddenRows = true, Csv = new() { HasHeader = false } }, new[] { "1", "3" }),
			(new ExcelReaderOptions { Range = "A3:A3", Csv = new() { HasHeader = false } }, new[] { "3" }),
		};

		foreach (var (fault, at, from, detail) in cases)
		{
			var xml = Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\"><v>1</v></c></row><row r=\"2\" hidden=\"1\"><c r=\"A2\"><v>2</v></c>{fault}</row><row r=\"3\"><c r=\"A3\"><v>3</v></c></row>");
			var offset = OffsetOf(xml, at) + from;
			using var workbook = await Workbooks.OpenAsync(TestPackage.Workbook(new TestSheet("Data", xml: xml)), async);

			foreach (var (options, records) in readings)
			{
				workbook.rawSkips = true;
				using (var reader = await workbook.OpenReaderAsync(async, options))
				{
					Assert.Equal(records, await reader.ReadAllAsync(async));
				}

				workbook.rawSkips = false;
				using (var reader = await workbook.OpenReaderAsync(async, options))
				{
					var e = await Assert.ThrowsAsync<CsvFormatException>(() => reader.ReadAllAsync(async));
					Assert.StartsWith($"Sheet Data can't be read at byte {offset} of {Sheets.PartName}: {detail}.", e.Message);
				}
			}
		}
	}

	/// <summary>The byte offset in a part of the first occurrence of a text.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="text">The text.</param>
	/// <returns>The offset.</returns>
	private static long OffsetOf(string xml, string text) => Encoding.UTF8.GetByteCount(xml.Substring(0, xml.IndexOf(text, StringComparison.Ordinal)));
}
