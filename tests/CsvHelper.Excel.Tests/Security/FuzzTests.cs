using System.IO.Compression;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// A record of typed members by position, for the fuzzer's typed reading: whatever cell a field holds goes into each kind
/// of member in turn, as the fields move along the record's members.
/// </summary>
[CsvRecord]
public partial class FuzzRecord
{
	/// <summary>Field 0, as text.</summary>
	[CsvColumn(Index = 0)]
	public string? Text { get; set; }

	/// <summary>Field 1, as a <see cref="double"/>.</summary>
	[CsvColumn(Index = 1)]
	public double? Number { get; set; }

	/// <summary>Field 2, as a <see cref="decimal"/>.</summary>
	[CsvColumn(Index = 2)]
	public decimal? Exact { get; set; }

	/// <summary>Field 3, as a <see cref="long"/>.</summary>
	[CsvColumn(Index = 3)]
	public long? Whole { get; set; }

	/// <summary>Field 4, as a <see cref="DateTime"/>.</summary>
	[CsvColumn(Index = 4)]
	public DateTime? Date { get; set; }

	/// <summary>Field 5, as a <see cref="TimeSpan"/>.</summary>
	[CsvColumn(Index = 5)]
	public TimeSpan? Duration { get; set; }

	/// <summary>Field 6, as a <see cref="bool"/>.</summary>
	[CsvColumn(Index = 6)]
	public bool? Flag { get; set; }

	/// <summary>Field 7, as a <see cref="DateTimeOffset"/>.</summary>
	[CsvColumn(Index = 7)]
	public DateTimeOffset? Moment { get; set; }

	/// <summary>Field 8, as an <see cref="int"/>.</summary>
	[CsvColumn(Index = 8)]
	public int? Small { get; set; }

#if NET
	/// <summary>Field 9, as a <see cref="DateOnly"/>.</summary>
	[CsvColumn(Index = 9)]
	public DateOnly? Day { get; set; }

	/// <summary>Field 10, as a <see cref="TimeOnly"/>.</summary>
	[CsvColumn(Index = 10)]
	public TimeOnly? Time { get; set; }
#endif
}

/// <summary>
/// Seeded mutation fuzzing, as the plan's "Fuzzing" describes: workbooks from synthetic packages and from the corpus,
/// mutated in an XML part or in their zip bytes, each read through the public API to the end of every worksheet, with
/// formulas read as values or as text, each policy for error cells, and several significant digits. A seed reads its
/// fields as strings, as typed values (where <c>TryParse</c> and <c>Parse</c> must agree), or as typed records. Each input
/// must finish within five seconds, and either read or throw <see cref="InvalidDataException"/>,
/// <see cref="CsvFormatException"/> or <see cref="NotSupportedException"/>; a field may only fail to parse, with
/// <see cref="FormatException"/> or <see cref="OverflowException"/>, and an error cell fail to read when error cells fail
/// every read. <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> sets the number of inputs, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the
/// first seed, so that a failure can be replayed alone.
/// </summary>
/// <param name="output">Shows how the inputs ended.</param>
public class FuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of inputs a run reads, unless <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> says otherwise.</summary>
	private const int DefaultInputs = 3000;

	/// <summary>The records of each sheet whose fields are parsed as typed values; the rest are read as strings, for time.</summary>
	private const int ParsedRecords = 64;

	/// <summary>The types a field is parsed as, of which <c>Parse</c> is called for one in turn: exceptions are slow.</summary>
	private const int ParsedTypes = 11;

	/// <summary>How long one input may take.</summary>
	private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Markup and values the XML mutations insert: the scanner's delimiters, its elements, formulas and their grammar's
	/// delimiters, ISO 8601 texts, and edge cases.
	/// </summary>
	private static readonly string[] Tokens =
	[
		"<", ">", "&", "\"", "'", "/", "=", "<row>", "</row>", "<row r=\"1048576\">", "<c>", "</c>", "<c r=\"XFD1048576\">", "<c r=\"A0\">",
		"<v>", "</v>", "<v/>", "<is>", "</is>", "<t>", "</t>", "<r>", "</r>", "<si>", "</si>", "<rPh>", "<sheetData>", "</sheetData>",
		"<x:c>", "<![CDATA[", "]]>", "<!--", "-->", "<?pi?>", "<!DOCTYPE x>", "&amp;", "&#0;", "&#x10FFFF;", "&#xD800;", "&bogus;",
		"_x0041_", "_xD83D_", "_x00", " xml:space=\"preserve\"", " t=\"s\"", " t=\"inlineStr\"", " t=\"b\"", " t=\"e\"", " t=\"d\"",
		" s=\"99999\"", " s=\"1\"", " s=\"3\"", " r=\"\"", " r=\"A1\"", "\uFEFF", "\u0000", "\r\n",
		"<f>", "</f>", "<f/>", "<f t=\"shared\" ref=\"A1:XFD1048576\" si=\"0\">", "<f t=\"shared\" si=\"0\"/>", "<f t=\"array\" ref=\"A1:A2\">",
		"<f t=\"dataTable\" ref=\"A1\"/>", " t=\"shared\"", " si=\"0\"", " si=\"1\"", " si=\"-1\"", " si=\"2147483648\"", " ref=\"A1\"",
		"$", "[", "]", "{", "}", "(", ")", "!", ":", "#", "#REF!", "A1", "$A$1", "XFD1048576", "1048576:1048576", "A:XFD", "'Q1 Orders'!",
		"_xlfn.", "P", "T", "PT36H0M0.000S", "P1DT12H", "9999-12-31T23:59:59.999", "1899-12-31", "1900-02-29", "1904-01-01", "24:00",
		".99999999999999999999", "Z",
	];

	/// <summary>Values the XML mutations put in place of a run of digits.</summary>
	private static readonly string[] Numbers =
	[
		"0", "-1", "1048577", "2147483648", "99999999999999999999", "1E+308", "1E+309", "4.9E-324", "-0", "NaN", "60", "2958466", "",
	];

	/// <summary>The significant digits a seed reads numbers to.</summary>
	private static readonly int?[] Digits = [15, 17, null, 1, 28];

	/// <summary>The workbooks the inputs start from, loaded once.</summary>
	private static readonly Lazy<List<Source>> Sources = new(LoadSources);

	/// <summary>How a seed reads each sheet's fields.</summary>
	private enum ReadMode
	{
		/// <summary>Each field as a string.</summary>
		Strings,

		/// <summary>Each field as a string, then as each type a cell can convert to, with <c>TryParse</c> and <c>Parse</c>.</summary>
		Fields,

		/// <summary>Each record as a <see cref="FuzzRecord"/>, the records that can't be read going to <c>OnRecordError</c>.</summary>
		Records,
	}

	[Fact]
	public async Task ReadsOrRejectsEveryMutatedWorkbook()
	{
		var inputs = Setting("CSVHELPER_EXCEL_FUZZ_INPUTS", DefaultInputs);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var outcomes = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var failures = new List<string>();
		for (var seed = first; seed < first + inputs && failures.Count < 5; seed++)
		{
			var input = Mutate(seed);
			var settings = Settings.Of(seed);
			var run = Task.Run(() => ReadAsync(input.Zip, settings));
			using (var stop = new CancellationTokenSource())
			{
				var finished = await Task.WhenAny(run, Task.Delay(TimeLimit, stop.Token));
				stop.Cancel();
				if (finished != run)
				{
					// The input's thread goes on; the run stops here, since it may be using a core.
					failures.Add($"{input.Describe(seed, settings)}: still running after {TimeLimit.TotalSeconds} s");

					break;
				}
			}

			try
			{
				var outcome = $"{settings.Mode}: {await run}";
				outcomes[outcome] = outcomes.TryGetValue(outcome, out var count) ? count + 1 : 1;
			}
			catch (Exception e)
			{
				failures.Add($"{input.Describe(seed, settings)}: {e.GetType().FullName}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(12))}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + inputs - 1}:");
		foreach (var pair in outcomes)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		Assert.True(failures.Count == 0, string.Join("\n\n", failures) + "\n\nReplay one with CSVHELPER_EXCEL_FUZZ_SEED=<seed> and CSVHELPER_EXCEL_FUZZ_INPUTS=1.");
	}

	/// <summary>Reads a workbook to the end of each worksheet, under small limits.</summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="settings">How to read it.</param>
	/// <returns>How it ended: <c>read</c>, or the expected exception that ended it (or that the first sheet to fail threw).</returns>
	private static async Task<string> ReadAsync(byte[] zip, Settings settings)
	{
		var async = settings.Async;
		try
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), settings.Options) : ExcelWorkbook.Open(new MemoryStream(zip), settings.Options);
			var outcome = "read";
			foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
			{
				try
				{
					if (settings.Mode == ReadMode.Records)
					{
						await ReadRecordsAsync(workbook, sheet, settings);
					}
					else
					{
						await ReadFieldsAsync(workbook, sheet, settings);
					}
				}
				catch (Exception e) when (IsExpected(e))
				{
					outcome = outcome == "read" ? "a sheet threw " + e.GetType().Name : outcome;
				}
			}

			return outcome;
		}
		catch (Exception e) when (IsExpected(e))
		{
			return "the workbook threw " + e.GetType().Name;
		}
	}

	/// <summary>
	/// Reads a sheet's fields as strings, and in <see cref="ReadMode.Fields"/> the fields of its first records as typed
	/// values too.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="settings">How to read it.</param>
	/// <returns>A task that completes when the sheet is read.</returns>
	private static async Task ReadFieldsAsync(ExcelWorkbook workbook, ExcelSheet sheet, Settings settings)
	{
		var async = settings.Async;
		using var reader = await workbook.OpenReaderAsync(async, sheet);
		var records = 0;
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				ReadString(reader, i, settings.Options.ErrorCells);
				if (settings.Mode == ReadMode.Fields && records < ParsedRecords)
				{
					ParseField(reader, i, (records + i) % ParsedTypes);
				}
			}

			records++;
		}
	}

	/// <summary>
	/// Reads a sheet's records as <see cref="FuzzRecord"/>s. A record that can't be read goes to <c>OnRecordError</c>,
	/// which skips it; why it can't must be a field that doesn't parse.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet.</param>
	/// <param name="settings">How to read it.</param>
	/// <returns>A task that completes when the sheet is read.</returns>
	/// <exception cref="InvalidOperationException">A record failed for another reason.</exception>
	private static async Task ReadRecordsAsync(ExcelWorkbook workbook, ExcelSheet sheet, Settings settings)
	{
		var errors = new List<CsvRecordError>();
		var options = settings.Options with
		{
			Csv = settings.Options.Csv with
			{
				OnRecordError = error =>
				{
					errors.Add(error);

					return true;
				},
			},
		};
		using var reader = await workbook.OpenReaderAsync(settings.Async, sheet, options);
		if (settings.Async)
		{
			await foreach (var record in reader.GetRecordsAsync<FuzzRecord>())
			{
				GC.KeepAlive(record);
			}
		}
		else
		{
			foreach (var record in reader.GetRecords<FuzzRecord>())
			{
				GC.KeepAlive(record);
			}
		}

		foreach (var error in errors)
		{
			if (error.Exception is not CsvFormatException { InnerException: FormatException or OverflowException })
			{
				throw new InvalidOperationException($"Record {error.RowNumber} ({error.Location}) failed with {error.Exception.InnerException?.GetType().FullName ?? "nothing inside"}: {error.Exception}");
			}
		}
	}

	/// <summary>Reads a field as a string: an error cell may fail, when error cells fail every read.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="errorCells">How error cells read.</param>
	private static void ReadString(CsvReader reader, int index, ExcelErrorCells errorCells)
	{
		try
		{
			reader.GetString(index);
		}
		catch (FormatException e) when (errorCells == ExcelErrorCells.Error && e is not CsvFormatException && e.Message.Contains(" holds the error "))
		{
			// The policy's failure, which names the cell.
		}
	}

	/// <summary>Parses a field as each type a cell can convert to: <c>TryParse</c> for each, and <c>Parse</c> for one.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="parse">Which type <c>Parse</c> is called for, from 0 to <see cref="ParsedTypes"/> less one.</param>
	private static void ParseField(CsvReader reader, int index, int parse)
	{
		Parse<double>(reader, index, parse == 0);
		Parse<decimal>(reader, index, parse == 1);
		Parse<long>(reader, index, parse == 2);
		Parse<int>(reader, index, parse == 3);
		Parse<byte>(reader, index, parse == 4);
		Parse<bool>(reader, index, parse == 5);
		Parse<DateTime>(reader, index, parse == 6);
		Parse<DateTimeOffset>(reader, index, parse == 7);
		Parse<TimeSpan>(reader, index, parse == 8);
#if NET
		Parse<DateOnly>(reader, index, parse == 9);
		Parse<TimeOnly>(reader, index, parse == 10);
#endif
	}

	/// <summary>
	/// Parses a field with <c>TryParse</c>, which must not throw, and when asked with <c>Parse</c> too, which must give the
	/// same value, or throw <see cref="FormatException"/> or <see cref="OverflowException"/> where <c>TryParse</c> failed.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="parse">Whether to call <c>Parse</c> too.</param>
	/// <exception cref="InvalidOperationException">The two disagree.</exception>
	private static void Parse<T>(CsvReader reader, int index, bool parse)
#if NET
		where T : ISpanParsable<T>
#endif
	{
		var parsed = reader.TryParse<T>(index, out var value);
		if (!parse)
		{
			return;
		}

		T again;
		try
		{
			again = reader.Parse<T>(index);
		}
		catch (Exception e) when (e is FormatException or OverflowException)
		{
			if (parsed)
			{
				throw new InvalidOperationException($"Field {index} ({Shown(reader, index)}): TryParse<{typeof(T).Name}> read {value}, but Parse threw {e.GetType().Name}: {e.Message}");
			}

			return;
		}

		if (!parsed || !EqualityComparer<T>.Default.Equals(value, again))
		{
			throw new InvalidOperationException($"Field {index} ({Shown(reader, index)}): Parse<{typeof(T).Name}> read {again}, but TryParse {(parsed ? $"read {value}" : "failed")}.");
		}
	}

	/// <summary>A field's stored form, for a failure.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <returns>The field's bytes as text, cut short.</returns>
	private static string Shown(CsvReader reader, int index) => CellValue.Show(Encoding.UTF8.GetString(reader.GetSpan(index).ToArray()));

	/// <summary>Whether an exception is one a workbook that can't be read may end in.</summary>
	/// <param name="e">The exception.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsExpected(Exception e) => e is InvalidDataException or CsvFormatException or NotSupportedException;

	/// <summary>Makes the input of a seed: a source workbook, mutated in one XML part or in its zip bytes.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The input.</returns>
	private static Input Mutate(int seed)
	{
		var random = new Random(seed);
		var sources = Sources.Value;
		var source = sources[random.Next(sources.Count)];
		var steps = new List<string>();
		byte[] zip;
		if (random.Next(3) < 2)
		{
			// Mostly the parts the scanner reads: sheets and shared strings, then the package's parts.
			var parts = source.Entries.Select((e, i) => (e.Name, Index: i)).Where(e => e.Name.EndsWith(".xml", StringComparison.Ordinal) || e.Name.EndsWith(".rels", StringComparison.Ordinal)).ToList();
			var sheets = parts.Where(p => p.Name.Contains("worksheets/") || p.Name.Contains("sharedStrings")).ToList();
			var (name, index) = sheets.Count > 0 && random.Next(4) > 0 ? sheets[random.Next(sheets.Count)] : parts[random.Next(parts.Count)];
			var data = source.Entries[index].Data;
			for (var n = random.Next(1, 5); n > 0; n--)
			{
				data = MutateXml(random, data, steps);
			}

			var entries = source.Entries.ToList();
			entries[index] = (name, data);
			var level = random.Next(4) == 0 ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
			zip = Zip(entries, level);
			steps.Insert(0, $"{name} ({level})");
		}
		else
		{
			zip = source.Zip.ToArray();
			for (var n = random.Next(1, 4); n > 0; n--)
			{
				zip = MutateZip(random, zip, steps);
			}

			steps.Insert(0, "zip bytes");
		}

		return new Input(source.Name, string.Join(", ", steps), zip);
	}

	/// <summary>Applies one mutation to an XML part's bytes.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="data">The part.</param>
	/// <param name="steps">Takes a description of the mutation.</param>
	/// <returns>The mutated part.</returns>
	private static byte[] MutateXml(Random random, byte[] data, List<string> steps)
	{
		var at = random.Next(data.Length + 1);
		switch (random.Next(7))
		{
			case 0 when data.Length > 0:
				at = Math.Min(at, data.Length - 1);
				var bit = random.Next(8);
				steps.Add($"flip bit {bit} at {at}");

				return Replace(data, at, 1, [(byte)(data[at] ^ (1 << bit))]);
			case 1 when data.Length > 0:
				var length = Math.Min(random.Next(1, 33), data.Length - Math.Min(at, data.Length - 1));
				at = Math.Min(at, data.Length - length);
				steps.Add($"delete {length} at {at}");

				return Replace(data, at, length, []);
			case 2:
				var token = Tokens[random.Next(Tokens.Length)];
				steps.Add($"insert {Show(token)} at {at}");

				return Replace(data, at, 0, Encoding.UTF8.GetBytes(token));
			case 3 when data.Length > 0:
				var from = random.Next(data.Length);
				var copy = Math.Min(random.Next(1, 400), data.Length - from);
				steps.Add($"copy {copy} from {from} to {at}");

				return Replace(data, at, 0, data.AsSpan(from, copy).ToArray());
			case 4:
				steps.Add($"truncate at {at}");

				return data.AsSpan(0, at).ToArray();
			case 5:
				var digits = Enumerable.Range(0, data.Length).Where(i => data[i] >= '0' && data[i] <= '9' && (i == 0 || data[i - 1] < '0' || data[i - 1] > '9')).ToList();
				if (digits.Count == 0)
				{
					goto default;
				}

				var start = digits[random.Next(digits.Count)];
				var end = start;
				while (end < data.Length && data[end] >= '0' && data[end] <= '9')
				{
					end++;
				}

				var number = Numbers[random.Next(Numbers.Length)];
				steps.Add($"number {Show(number)} at {start}");

				return Replace(data, start, end - start, Encoding.UTF8.GetBytes(number));
			default:
				var value = (byte)random.Next(256);
				at = Math.Min(at, Math.Max(data.Length - 1, 0));
				steps.Add($"byte {value} at {at}");

				return data.Length == 0 ? [value] : Replace(data, at, 1, [value]);
		}
	}

	/// <summary>Applies one mutation to a zip's bytes: in a header, anywhere, or at its end.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="zip">The zip.</param>
	/// <param name="steps">Takes a description of the mutation.</param>
	/// <returns>The mutated zip.</returns>
	private static byte[] MutateZip(Random random, byte[] zip, List<string> steps)
	{
		if (zip.Length == 0)
		{
			// A truncation left nothing: the next mutation is a byte.
			var only = (byte)random.Next(256);
			steps.Add($"byte {only}");

			return [only];
		}

		switch (random.Next(4))
		{
			case 0:
			case 1:
				// A byte of a local header (30 bytes), a central directory record (46) or the end record (22).
				var headers = Enumerable.Range(0, Math.Max(zip.Length - 3, 0))
					.Where(i => zip[i] == 'P' && zip[i + 1] == 'K' && (zip[i + 2], zip[i + 3]) is (3, 4) or (1, 2) or (5, 6))
					.ToList();
				if (headers.Count == 0)
				{
					goto default;
				}

				var header = headers[random.Next(headers.Count)];
				var size = zip[header + 2] == 3 ? 30 : zip[header + 2] == 1 ? 46 : 22;
				var at = Math.Min(header + 4 + random.Next(size - 4), zip.Length - 1);
				var value = random.Next(3) == 0 ? (byte)0xFF : (byte)random.Next(256);
				steps.Add($"header byte {value} at {at}");

				return Replace(zip, at, 1, [value]);
			case 2:
				var end = random.Next(zip.Length);
				steps.Add($"truncate at {end}");

				return zip.AsSpan(0, end).ToArray();
			default:
				var anywhere = random.Next(zip.Length);
				var any = (byte)random.Next(256);
				steps.Add($"byte {any} at {anywhere}");

				return Replace(zip, anywhere, 1, [any]);
		}
	}

	/// <summary>Replaces a range of bytes with others.</summary>
	/// <param name="data">The bytes.</param>
	/// <param name="at">The range's start.</param>
	/// <param name="length">The range's length.</param>
	/// <param name="with">The bytes to put there.</param>
	/// <returns>The new bytes.</returns>
	private static byte[] Replace(byte[] data, int at, int length, byte[] with)
	{
		var result = new byte[data.Length - length + with.Length];
		data.AsSpan(0, at).CopyTo(result);
		with.CopyTo(result.AsSpan(at));
		data.AsSpan(at + length).CopyTo(result.AsSpan(at + with.Length));

		return result;
	}

	/// <summary>Writes entries as a zip, in order.</summary>
	/// <param name="entries">The entries.</param>
	/// <param name="level">Their compression.</param>
	/// <returns>The zip's bytes.</returns>
	private static byte[] Zip(List<(string Name, byte[] Data)> entries, CompressionLevel level)
	{
		var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
		{
			foreach (var (name, data) in entries)
			{
				using var entry = zip.CreateEntry(name, level).Open();
				entry.Write(data, 0, data.Length);
			}
		}

		return stream.ToArray();
	}

	/// <summary>
	/// The workbooks the inputs start from: four synthetic packages (Excel's shape with every kind of cell, the orders
	/// workbook, numbers, and formulas with typed cells) and every corpus workbook CsvHelper.Excel reads.
	/// </summary>
	/// <returns>The sources.</returns>
	private static List<Source> LoadSources()
	{
		var variety = Workbooks.Data(
				"<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"D1\" t=\"inlineStr\"><is><r><t xml:space=\"preserve\"> rich </t></r><r><t>run</t></r></is></c></row>" +
				"<row><c t=\"b\"><v>1</v></c><c t=\"e\"><v>#N/A</v></c><c s=\"1\"><v>45293.5</v></c><c t=\"str\"><f>A1</f><v>_x0041_&amp;</v></c></row>" +
				"<row r=\"5\" spans=\"1:3\"><c r=\"C5\" t=\"d\"><v>2024-01-02T03:04:05</v></c><c r=\"D5\"><v> 1.5E-3 </v></c></row>")
			.SharedStrings("<t>Name</t>", "<r><rPr><b/></rPr><t>Bold</t></r><r><t xml:space=\"preserve\"> plain</t></r><rPh sb=\"0\" eb=\"1\"><t>no</t></rPh>");

		// Shared formulas (a group, a group of two columns, and one whose defining cell has no text), an array, a data table
		// and a formula with a structured reference, among dates, a t="d" date and duration, an error and 17 digits. Style 3
		// is an elapsed time.
		var formulas = Workbooks.Data(
				"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A3\" si=\"0\">B1*2+$C$1</f><v>2</v></c><c r=\"B1\" s=\"1\"><v>45293.5</v></c>" +
				"<c r=\"C1\" t=\"d\" s=\"1\"><v>2024-01-02T03:04:05.123</v></c><c r=\"D1\" t=\"d\" s=\"3\"><v>PT36H0M0.000S</v></c><c r=\"E1\" t=\"e\"><v>#DIV/0!</v></c></row>" +
				"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/><v>4</v></c><c r=\"B2\"><f t=\"array\" ref=\"B2:B3\">C1:C2*2</f><v>1</v></c>" +
				"<c r=\"C2\" t=\"str\"><f>SUM(Orders[[#This Row],[Qty]])&amp;\"A1\"</f><v>x</v></c><c r=\"D2\"><f t=\"shared\" ref=\"D2:E3\" si=\"1\">'Other Sheet'!A1+D1</f><v>5</v></c>" +
				"<c r=\"E2\"><f t=\"shared\" si=\"1\"/><v>6</v></c></row>" +
				"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\">ignored</f><v>6</v></c><c r=\"B3\"><v>1</v></c><c r=\"C3\"><f t=\"dataTable\" ref=\"C3:C4\" dt2D=\"0\" dtr=\"0\" r1=\"A1\"/><v>7</v></c>" +
				"<c r=\"D3\" t=\"b\"><f t=\"shared\" si=\"1\"/><v>1</v></c><c r=\"E3\" s=\"2\"><v>12345678901234567</v></c><c r=\"F3\"><f t=\"shared\" ref=\"F3:F4\" si=\"2\"/><v>8</v></c></row>" +
				"<row r=\"4\"><c r=\"F4\"><f t=\"shared\" si=\"2\"/><v>9</v></c><c r=\"G4\" s=\"3\"><v>1.5</v></c><c r=\"H4\" t=\"d\"><v>1900-02-28</v></c></row>")
			.Replace("xl/styles.xml", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/>", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"[h]:mm:ss\"/>")
			.Replace("xl/styles.xml", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/>", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/>");
		var packages = new[]
		{
			("synthetic/variety", variety), ("synthetic/orders", Workbooks.Orders()), ("synthetic/numbers", Workbooks.Data(Workbooks.Numbers(40))), ("synthetic/formulas", formulas),
		};
		var sources = packages.Select(p => Source.Of(p.Item1, p.Item2.ToArray())).ToList();
		sources.AddRange(CorpusFiles.Workbooks.Select(file => Source.Of(file, File.ReadAllBytes(CorpusFiles.PathOf(file)))));

		return sources;
	}

	/// <summary>The value of an integer environment variable.</summary>
	/// <param name="name">The variable.</param>
	/// <param name="fallback">The value when it isn't set.</param>
	/// <returns>The value.</returns>
	private static int Setting(string name, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

	/// <summary>Text for a description: control characters as code points.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text to show.</returns>
	private static string Show(string text) => "\"" + string.Concat(text.Select(c => c < ' ' || c == '\uFEFF' ? $"\\u{(int)c:X4}" : c.ToString())) + "\"";

	/// <summary>A workbook an input starts from: its zip, and its entries' inflated bytes in order.</summary>
	/// <param name="name">Where it comes from.</param>
	/// <param name="zip">The zip's bytes.</param>
	/// <param name="entries">The entries.</param>
	private sealed class Source(string name, byte[] zip, List<(string Name, byte[] Data)> entries)
	{
		/// <summary>Where it comes from.</summary>
		public string Name { get; } = name;

		/// <summary>The zip's bytes.</summary>
		public byte[] Zip { get; } = zip;

		/// <summary>The entries, in the zip's order.</summary>
		public List<(string Name, byte[] Data)> Entries { get; } = entries;

		/// <summary>Reads a workbook's entries.</summary>
		/// <param name="name">Where it comes from.</param>
		/// <param name="zip">The zip's bytes.</param>
		/// <returns>The source.</returns>
		public static Source Of(string name, byte[] zip)
		{
			using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
			var entries = archive.Entries.Select(e =>
			{
				using var stream = e.Open();
				var data = new MemoryStream();
				stream.CopyTo(data);

				return (e.FullName, data.ToArray());
			}).ToList();

			return new Source(name, zip, entries);
		}
	}

	/// <summary>An input: a mutated workbook.</summary>
	/// <param name="source">The workbook it starts from.</param>
	/// <param name="steps">The mutations.</param>
	/// <param name="zip">Its bytes.</param>
	private sealed class Input(string source, string steps, byte[] zip)
	{
		/// <summary>Its bytes.</summary>
		public byte[] Zip { get; } = zip;

		/// <summary>Describes the input for a failure.</summary>
		/// <param name="seed">Its seed.</param>
		/// <param name="settings">How it was read.</param>
		/// <returns>The description.</returns>
		public string Describe(int seed, Settings settings) => $"Seed {seed} ({settings}), {source}: {steps}";
	}

	/// <summary>
	/// How a seed reads its input: synchronously or not, with which CSV options (alternating with the seed, as before), and
	/// with which formulas, error cells, significant digits and reading (chosen by numbers of the seed's own).
	/// </summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="options">The options, under small limits.</param>
	/// <param name="mode">How to read the fields.</param>
	private sealed class Settings(bool async, ExcelReaderOptions options, ReadMode mode)
	{
		/// <summary>Whether to read asynchronously.</summary>
		public bool Async { get; } = async;

		/// <summary>The options.</summary>
		public ExcelReaderOptions Options { get; } = options;

		/// <summary>How to read the fields.</summary>
		public ReadMode Mode { get; } = mode;

		/// <summary>The settings of a seed.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The settings.</returns>
		public static Settings Of(int seed)
		{
			var random = new Random(Mix(seed));
			var options = new ExcelReaderOptions
			{
				MaxPartSize = 1 << 20,
				MaxUncompressedSize = 16 << 20,
				Formulas = random.Next(2) == 0 ? ExcelFormulas.CachedValue : ExcelFormulas.FormulaText,
				ErrorCells = (ExcelErrorCells)random.Next(3),
				SignificantDigits = Digits[random.Next(Digits.Length)],
				Csv = new() { HasHeader = seed % 3 != 0, SkipEmptyLines = seed % 5 != 0, TrimWhitespace = seed % 7 == 0 },
			};

			return new Settings(seed % 2 == 1, options, (ReadMode)random.Next(3));
		}

		/// <summary>
		/// A seed for the settings' numbers, its bits mixed (MurmurHash3's finalizer), so that they don't follow the
		/// mutations': <see cref="Random"/> gives seeds near each other nearly the same first numbers, and takes
		/// <c>~seed</c> as <c>seed + 1</c>.
		/// </summary>
		/// <param name="seed">The input's seed.</param>
		/// <returns>The settings' seed.</returns>
		private static int Mix(int seed)
		{
			var x = unchecked((uint)seed * 0x9E3779B9u);
			x ^= x >> 16;
			x = unchecked(x * 0x85EBCA6Bu);
			x ^= x >> 13;
			x = unchecked(x * 0xC2B2AE35u);
			x ^= x >> 16;

			return (int)(x & int.MaxValue);
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Async ? "async" : "sync")}, {Mode}, formulas {Options.Formulas}, error cells {Options.ErrorCells}, digits {Options.SignificantDigits?.ToString() ?? "null"}, " +
			$"header {Options.Csv.HasHeader}, skip empty {Options.Csv.SkipEmptyLines}, trim {Options.Csv.TrimWhitespace}";
	}
}
