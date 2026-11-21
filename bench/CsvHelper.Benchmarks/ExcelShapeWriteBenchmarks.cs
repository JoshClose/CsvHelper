using System.Globalization;
using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CsvHelper.Excel;
using DataCell = SpreadCheetah.DataCell;
using Spreadsheet = SpreadCheetah.Spreadsheet;

namespace CsvHelper.Benchmarks;

/// <summary>The sheets <see cref="ExcelShapesWrite"/> writes.</summary>
public enum WriteShape
{
	/// <summary>
	/// 1,000 columns by 1,000 rows of short text, below a header: auto-fit holds back and measures 101 records of 1,000
	/// cells.
	/// </summary>
	Wide,

	/// <summary>
	/// A million records of 16 of the package assets' columns, a third of each record's fields empty in runs, and one record
	/// in ten empty.
	/// </summary>
	Sparse,
}

/// <summary>
/// Sheets of other shapes than the package assets' and the orders', written by SpreadCheetah and CsvHelper.Excel to a
/// stream that counts and drops their bytes: a wide one, for what auto-fit's held records cost when records are wide, and
/// a sparse one, for what empty fields and empty records cost.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WriteShape.Wide"/>: a header of 1,000 names and 1,000 records of 1,000 fields, each a word of 3 to 10
/// lowercase letters drawn from 4,096 made with a fixed seed. Auto-fit holds the header and the first 100 records until it
/// knows the columns' widths: 101,000 cells here, against the package assets' 2,525. <see cref="CsvHelper_Write"/>'s
/// Allocated column less <see cref="CsvHelper_WriteNoAutoFit"/>'s is what the held records cost for 1,000 columns.
/// </para>
/// <para>
/// <see cref="WriteShape.Sparse"/>: the 16 of the package assets' 25 columns that hold a value in nearly every line, so
/// that the sheet's empty fields are the ones made here. In each block of ten records, one chosen at random (with a fixed
/// seed) is empty, all its fields empty; two can follow each other across blocks. In the others the fields alternate
/// between runs of values, 1 to 9 long, and runs of empty fields, 1 to 4 long, the first run empty in one record in
/// three: a third of their fields are empty, some in the first column, some before the record's last value and some after
/// it. Its 16,950 records (NCsvPerf's 1,695 lines ten times, each time blanked differently) repeat to a million, as the
/// package assets' lines do. Written with cell references, as today, an empty field writes no cell and an empty record no
/// row, the next cell's or row's reference skipping them. Without references (Phase 9's decisions 23 and 24), an empty
/// field before a later value of its record becomes an empty cell, and an empty record before a later row an empty row:
/// this sheet times and sizes both.
/// </para>
/// <para>
/// SpreadCheetah 1.28.0 (the baseline) writes each record's fields up to its last value, an empty field as its cell
/// without a value (<c>&lt;c/&gt;</c>) and an empty record as an empty row: the same values in the same cells.
/// CsvHelper.Excel writes each field through its sheet's <see cref="CsvWriter"/>, as <c>ExcelPackageAssetsWrite</c> does:
/// at its defaults, with stored entries (<see cref="CompressionLevel.NoCompression"/>, making the XML without deflating
/// it), and without auto-fit. <see cref="CsvHelper_Write100Rows"/> and <see cref="CsvHelper_Write10000Rows"/> write the
/// first 100 and 10,000 records at the defaults, the wide sheet's 1,000 repeated: both hold the same 100 records back, so
/// they allocate the same unless something grows with the rows.
/// </para>
/// <para>
/// The MB/s column is of the same data as CSV, written by CsvHelper. The Written column is the size of what each writes.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(WrittenSizeConfig))]
public class ExcelShapesWrite : ExcelWriteBenchmarkBase
{
	/// <summary>The wide sheet's number of columns.</summary>
	public const int WideColumns = 1_000;

	/// <summary>The wide sheet's number of records.</summary>
	public const int WideRows = 1_000;

	/// <summary>The sparse sheet's number of records: a million, as the package assets' and the orders' sheets have.</summary>
	public const int SparseRows = 1_000_000;

	/// <summary>The number of different records of the sparse sheet, which then repeat: NCsvPerf's 1,695 lines ten times.</summary>
	public const int SparsePeriod = 16_950;

	/// <summary>The options that read a sheet back whole: every row a record, the header's and empty rows too.</summary>
	private static readonly ExcelReaderOptions EveryRow = new() { Csv = CsvReaderOptions.Default with { HasHeader = false, SkipEmptyLines = false } };

	/// <summary>The sheet's name: the shape's.</summary>
	private string sheetName = "";

	/// <summary>The header.</summary>
	private string[] header = [];

	/// <summary>The records, which share arrays where the shape's records repeat.</summary>
	private string[][] rows = [];

	/// <summary>The first 100 records.</summary>
	private string[][] first100 = [];

	/// <summary>The first 10,000 records, the shape's records repeated when it has fewer.</summary>
	private string[][] first10000 = [];

	/// <summary>Gets or sets the sheet's shape.</summary>
	[Params(WriteShape.Wide, WriteShape.Sparse)]
	public WriteShape Shape { get; set; }

	/// <summary>
	/// Builds the shape's header and records, and writes them as CSV once for <see cref="ExcelWriteBenchmarkBase.DataBytes"/>.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		sheetName = Shape.ToString();
		(header, rows) = Shape == WriteShape.Wide ? Wide() : Sparse();
		first100 = Repeat(rows, 100);
		first10000 = Repeat(rows, 10_000);

		var csv = new CountingStream(keep: false);
		using (var writer = CsvWriter.Create(csv, leaveOpen: true))
		{
			WriteRows(writer, rows);
		}

		DataBytes = csv.Count;
	}

	/// <summary>
	/// SpreadCheetah: a row per record through one array of cells, each record's up to its last value, an empty field as an
	/// empty cell.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public async Task<long> SpreadCheetah_Write()
	{
		var stream = NewOutput();
		await using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream))
		{
			await spreadsheet.StartWorksheetAsync(sheetName);

			var cells = new DataCell[header.Length];
			await spreadsheet.AddRowAsync(cells.AsMemory(0, Fill(cells, header)));
			foreach (var row in rows)
			{
				await spreadsheet.AddRowAsync(cells.AsMemory(0, Fill(cells, row)));
			}

			await spreadsheet.FinishAsync();
		}

		return stream.Count;
	}

	/// <summary>
	/// CsvHelper.Excel: each field through the sheet's <see cref="CsvWriter"/>, at its defaults: inline strings at
	/// <see cref="CompressionLevel.Fastest"/>, and a bold, frozen header with auto-fit over 100 rows.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write() => WriteWorkbook(new ExcelWriterOptions(), rows);

	/// <summary>
	/// CsvHelper.Excel at its defaults with stored entries (<see cref="CompressionLevel.NoCompression"/>): making the XML
	/// without deflating it, and the sheet's XML as the Written size.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteStored() => WriteWorkbook(new ExcelWriterOptions { CompressionLevel = CompressionLevel.NoCompression }, rows);

	/// <summary>
	/// CsvHelper.Excel at its defaults but without auto-fit: <see cref="CsvHelper_Write"/>'s allocation less this one's is
	/// what auto-fit's 100 held records cost.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_WriteNoAutoFit() => WriteWorkbook(new ExcelWriterOptions { Sheets = NoAutoFit }, rows);

	/// <summary>
	/// CsvHelper.Excel at its defaults, the first 100 records: all of them held back by auto-fit, as
	/// <see cref="CsvHelper_Write10000Rows"/> holds its first 100.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write100Rows")]
	public long CsvHelper_Write100Rows() => WriteWorkbook(new ExcelWriterOptions(), first100);

	/// <summary>
	/// CsvHelper.Excel at its defaults, the first 10,000 records: it allocates what <see cref="CsvHelper_Write100Rows"/>
	/// does unless something grows with the rows.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Write10000Rows")]
	public long CsvHelper_Write10000Rows() => WriteWorkbook(new ExcelWriterOptions(), first10000);

	/// <inheritdoc/>
	public override object ReadBack()
	{
		var (stream, isWorkbook) = Written();
		using (stream)
		{
			if (!isWorkbook)
			{
				throw new InvalidOperationException("The last benchmark didn't write a workbook.");
			}

			using var reader = ExcelReader.Create(stream, EveryRow);

			return Describe(reader);
		}
	}

	/// <summary>
	/// Describes a sheet read back, so that two writers' sheets of the same records agree: its records up to the last with
	/// a value, the empty ones among them, its values, and a hash of each record's fields up to its last value.
	/// </summary>
	/// <param name="reader">The sheet's reader, which reads every row as a record.</param>
	/// <returns>The description.</returns>
	/// <remarks>
	/// A record's empty fields after its last value, and empty records after the last with a value, are left out: a writer
	/// may write them or not, and the reader may read a row as wide as the first.
	/// </remarks>
	private static string Describe(CsvReader reader)
	{
		const ulong prime = 1099511628211UL;

		var hash = 14695981039346656037UL;
		var records = 0L;
		var empty = 0L;
		var values = 0L;
		var pending = 0L;
		while (reader.Read())
		{
			var count = reader.FieldCount;
			while (count > 0 && reader.GetString(count - 1).Length == 0)
			{
				count--;
			}

			if (count == 0)
			{
				pending++;

				continue;
			}

			for (; pending > 0; pending--)
			{
				hash = (hash ^ 0x1E) * prime;
				records++;
				empty++;
			}

			for (var i = 0; i < count; i++)
			{
				var field = reader.GetString(i);
				if (field.Length > 0)
				{
					values++;
				}

				foreach (var c in field)
				{
					hash = (hash ^ c) * prime;
				}

				hash = (hash ^ 0x1F) * prime;
			}

			hash = (hash ^ 0x1E) * prime;
			records++;
		}

		return $"{records} records ({empty} empty), {values} values, content hash {hash:X16}";
	}

	/// <summary>
	/// Makes the wide sheet: a header of <see cref="WideColumns"/> names, and <see cref="WideRows"/> records of as many
	/// words of 3 to 10 lowercase letters, drawn from 4,096 made with a fixed seed.
	/// </summary>
	/// <returns>The header and the records.</returns>
	private static (string[] Header, string[][] Rows) Wide()
	{
		var random = new Random(11);
		var words = new string[4096];
		Span<char> letters = stackalloc char[10];
		for (var i = 0; i < words.Length; i++)
		{
			var length = random.Next(3, 11);
			for (var j = 0; j < length; j++)
			{
				letters[j] = (char)('a' + random.Next(26));
			}

			words[i] = new string(letters[..length]);
		}

		var header = new string[WideColumns];
		for (var i = 0; i < header.Length; i++)
		{
			header[i] = "Column" + (i + 1).ToString(CultureInfo.InvariantCulture);
		}

		var rows = new string[WideRows][];
		for (var r = 0; r < rows.Length; r++)
		{
			var row = new string[WideColumns];
			for (var i = 0; i < row.Length; i++)
			{
				row[i] = words[random.Next(words.Length)];
			}

			rows[r] = row;
		}

		return (header, rows);
	}

	/// <summary>
	/// Makes the sparse sheet: the package assets' columns that are empty in fewer than 1% of NCsvPerf's lines (16 of 25),
	/// one record in each block of ten empty, the others blanked in runs, <see cref="SparsePeriod"/> records repeated to
	/// <see cref="SparseRows"/>.
	/// </summary>
	/// <returns>The header and the records.</returns>
	private static (string[] Header, string[][] Rows) Sparse()
	{
		var records = ExcelDataGenerator.PackageAssetRecords(SparsePeriod);
		var lines = records.Skip(1).ToArray();
		var columns = Enumerable.Range(0, records[0].Length)
			.Where(c => lines.Count(line => line[c].Length == 0) * 100 < lines.Length)
			.ToArray();
		var header = columns.Select(c => records[0][c]).ToArray();
		var empty = new string[columns.Length];
		Array.Fill(empty, "");

		var period = new string[SparsePeriod][];
		var random = new Random(23);
		for (var block = 0; block < period.Length; block += 10)
		{
			var emptyRecord = block + random.Next(10);
			for (var i = block; i < block + 10; i++)
			{
				period[i] = i == emptyRecord ? empty : Blank(lines[i], columns, random);
			}
		}

		return (header, Repeat(period, SparseRows));
	}

	/// <summary>
	/// Takes a line's fields in the given columns, alternating between runs of values, 1 to 9 long, and runs of empty fields,
	/// 1 to 4 long, the first run empty one time in three: about a third of the fields empty.
	/// </summary>
	/// <param name="line">The line's fields.</param>
	/// <param name="columns">The columns to take, in order.</param>
	/// <param name="random">The runs' lengths' source.</param>
	/// <returns>The record's fields.</returns>
	private static string[] Blank(string[] line, int[] columns, Random random)
	{
		var fields = new string[columns.Length];
		var gap = random.Next(3) == 0;
		var at = 0;
		while (at < fields.Length)
		{
			var end = Math.Min(fields.Length, at + (gap ? random.Next(1, 5) : random.Next(1, 10)));
			while (at < end)
			{
				fields[at] = gap ? "" : line[columns[at]];
				at++;
			}

			gap = !gap;
		}

		return fields;
	}

	/// <summary>Returns a number of records, the given ones repeated when there are fewer, sharing their arrays.</summary>
	/// <param name="records">The records.</param>
	/// <param name="count">The number of records to return.</param>
	/// <returns>The records.</returns>
	private static string[][] Repeat(string[][] records, int count)
	{
		var result = new string[count][];
		for (var i = 0; i < result.Length; i++)
		{
			result[i] = records[i % records.Length];
		}

		return result;
	}

	/// <summary>Fills an array of SpreadCheetah's cells with a record's fields, an empty field as a cell without a value.</summary>
	/// <param name="cells">The cells, as many as the fields.</param>
	/// <param name="fields">The fields.</param>
	/// <returns>The number of cells to write: up to the last field with a value.</returns>
	private static int Fill(DataCell[] cells, string[] fields)
	{
		var count = 0;
		for (var i = 0; i < fields.Length; i++)
		{
			if (fields[i].Length == 0)
			{
				cells[i] = default;
			}
			else
			{
				cells[i] = new DataCell(fields[i]);
				count = i + 1;
			}
		}

		return count;
	}

	/// <summary>Writes the header and records with CsvHelper.Excel.</summary>
	/// <param name="options">The workbook's options, the sheet's among them.</param>
	/// <param name="records">The records.</param>
	/// <returns>The bytes written.</returns>
	private long WriteWorkbook(ExcelWriterOptions options, string[][] records)
	{
		var stream = NewOutput();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		using (var writer = workbook.AddSheet(sheetName))
		{
			WriteRows(writer, records);
		}

		return stream.Count;
	}

	/// <summary>Writes the header and records, field by field, to CsvHelper's writer or a sheet's.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="records">The records.</param>
	private void WriteRows(CsvWriter writer, string[][] records)
	{
		writer.WriteHeader(header);
		foreach (var record in records)
		{
			for (var i = 0; i < record.Length; i++)
			{
				writer.WriteField(record[i]);
			}

			writer.EndRecord();
		}
	}
}
