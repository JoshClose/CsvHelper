using System.Globalization;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// <c>.xlsx</c> → CSV over the corpus (Phase 6): every worksheet of every workbook of every producer, converted by
/// <see cref="ExcelConvert.ToCsvFiles"/>, reads back as the sheet reader reads it; and converting Excel's own orders
/// allocates the same for a hundred rows as for a thousand.
/// </summary>
public class ConversionCorpusTests
{
	/// <summary>Every record a row, the header's too, so that the CSV and the reader are compared record for record.</summary>
	private static readonly ExcelReaderOptions EveryRow = new() { Csv = new CsvReaderOptions { HasHeader = false } };

	/// <summary>Every workbook, sync and async, as theory data.</summary>
	public static IEnumerable<object[]> WorkbookModes => CorpusTests.WorkbookModes;

	[Theory]
	[MemberData(nameof(WorkbookModes))]
	public async Task ConvertsEveryWorksheetAsTheReaderReadsIt(string file, bool async)
	{
		var folder = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}");
		try
		{
			var options = new ExcelToCsvOptions { Excel = EveryRow };
			var paths = async
				? await ExcelConvert.ToCsvFilesAsync(CorpusFiles.PathOf(file), folder, options)
				: ExcelConvert.ToCsvFiles(CorpusFiles.PathOf(file), folder, options);
			using var workbook = ExcelWorkbook.Open(CorpusFiles.PathOf(file));
			var sheets = workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet).ToList();
			var kinds = CorpusFiles.Read(file, out _);
			Assert.Equal(sheets.Count, paths.Count);
			foreach (var (sheet, path) in sheets.Zip(paths, (s, p) => (s, p)))
			{
				using var expected = workbook.OpenReader(sheet, EveryRow);
				using var csv = CsvReader.FromFile(path, new CsvReaderOptions { HasHeader = false });
				var cells = kinds[sheet.Name];
				var records = 0;
				while (expected.Read())
				{
					records++;
					Assert.True(csv.Read(), $"{sheet.Name}: the CSV ends before record {records}");
					Assert.True(expected.FieldCount == csv.FieldCount, $"{sheet.Name}: record {records} has {expected.FieldCount} fields, and the CSV's {csv.FieldCount}");
					for (var i = 0; i < csv.FieldCount; i++)
					{
						// A number cell is rounded to 15 significant digits; every other field is the reader's text, and so is a
						// number cell whose text isn't a number (a producer's #N/A). The record's row is its line, empty rows being
						// skipped, and field i is column i.
						var text = csv.GetString(i);
						var number = cells.TryGetValue((expected.LineNumber, i), out var cell) && cell.Kind == SheetCellKind.Number && TryDouble(expected.GetString(i), out _);
						Assert.True(
							number ? SameNumber(expected, i, text) : expected.GetString(i) == text,
							$"{new SheetLocation(sheet.Name).Cell(i, expected.LineNumber)}: {(number ? "the number " : "")}{CellValue.Show(expected.GetString(i))} written as {CellValue.Show(text)}");
					}
				}

				Assert.False(csv.Read(), $"{sheet.Name}: the CSV has more than the reader's {records} records");
			}
		}
		finally
		{
			if (Directory.Exists(folder))
			{
				Directory.Delete(folder, true);
			}
		}
	}

#if NET10_0_OR_GREATER
	[Fact]
	public void AllocatesTheSameForAHundredOrdersAsForAThousand()
	{
		// Excel's own orders: dates, numbers, booleans and shared strings. Opening the workbook allocates the same either way.
		var path = CorpusFiles.PathOf("excel/orders-1000.xlsx");
		string last;
		using (var workbook = ExcelWorkbook.Open(path))
		using (var reader = workbook.OpenReader(EveryRow))
		{
			Assert.True(reader.Read());
			last = CellReference.ColumnName(reader.FieldCount - 1);
		}

		long Allocated(int rows)
		{
			using var reader = ExcelReader.FromFile(path, EveryRow with { Range = $"A1:{last}{rows + 1}" });
			using var writer = CsvWriter.Create(Stream.Null);

			// The first record warms the reader's and the writer's buffers.
			Assert.True(reader.Read());
			var before = GC.GetAllocatedBytesForCurrentThread();
			ExcelConvert.ToCsv(reader, writer);

			return GC.GetAllocatedBytesForCurrentThread() - before;
		}

		// Each size is measured twice, and the smaller kept: the runtime allocates once on the thread at a moment the JIT's
		// tiering sets (OSR with dynamic PGO), which can fall in one pass but not in both, where an allocation per row would
		// be in both. See ExcelToCsvTests.AllocatesTheSameForAnyNumberOfRows.
		Allocated(1000);
		var few = Math.Min(Allocated(100), Allocated(100));
		var many = Math.Min(Allocated(1000), Allocated(1000));
		Assert.True(many - few < 4096, $"100 orders allocated {few:N0} bytes and 1,000 orders {many:N0}.");
	}
#endif

	/// <summary>
	/// Whether a number cell's CSV field is its value rounded to 15 significant digits: the same as the reader's
	/// <see cref="decimal"/>, which it rounds so, within 1E-20 to 1E+20; and beyond, the same as the double rounded to 15
	/// digits.
	/// </summary>
	/// <param name="reader">The sheet's reader, on the record.</param>
	/// <param name="index">The field.</param>
	/// <param name="text">The CSV's field.</param>
	/// <returns>Whether it is.</returns>
	private static bool SameNumber(CsvReader reader, int index, string text)
	{
		if (!TryDouble(reader.GetString(index), out var number) || !TryDouble(text, out var written))
		{
			return false;
		}

		if (number == 0 || Math.Abs(number) is >= 1E-20 and < 1E+20)
		{
			return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) && exact == reader.Parse<decimal>(index);
		}

		return TryDouble(number.ToString("E14", CultureInfo.InvariantCulture), out var rounded) && written == rounded;
	}

	/// <summary>
	/// Reads a number as a double, a number past a double's range as an infinity, as .NET reads it: the largest doubles
	/// round to 15 digits past it (<c>1.79769313486232E+308</c>, as Excel shows <see cref="double.MaxValue"/>), which
	/// .NET Framework's parsing refuses.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="value">The number.</param>
	/// <returns>Whether the text is a number.</returns>
	private static bool TryDouble(string text, out double value)
	{
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
		{
			return true;
		}

		var digits = text.TrimStart('-');
		var exponent = digits.IndexOfAny(['E', 'e']);
		if (exponent > 0 && decimal.TryParse(digits.Substring(0, exponent), NumberStyles.Float, CultureInfo.InvariantCulture, out _)
			&& int.TryParse(digits.Substring(exponent + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var power) && power > 300)
		{
			value = text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;

			return true;
		}

		return false;
	}
}
