using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of <c>.xlsx</c> → CSV, as Phase 6's "Testing" describes: the reader fuzzer's mutated workbooks
/// (<see cref="FuzzTests.MutatedWorkbook"/>), each converted with random reading and writing options, from a file to a file
/// (now and then over an existing one), to a file for each worksheet, or from a stream to a stream, synchronously or not.
/// Each conversion either succeeds, with the same CSV as a synchronous conversion of streams gives (of each sheet, for a
/// file each), or fails with the reader's exceptions: <see cref="InvalidDataException"/>, <see cref="CsvFormatException"/>
/// or <see cref="NotSupportedException"/>, or an error cell's <see cref="FormatException"/> when error cells fail every
/// read. A failure leaves no file half written: a file's conversion leaves no file, or the one that was there as it was;
/// a file each leaves the complete files of the sheets before the one that failed, and no other. Every file is closed
/// after, which deleting the seed's folder checks. Each input must finish within five seconds; the test runs alone
/// (<see cref="TimedCollection"/>). <c>CSVHELPER_EXCEL_FUZZ_CONVERSIONS</c> sets the number of inputs, and
/// <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows how the inputs ended.</param>
[Collection(TimedCollection.Name)]
public class ToCsvFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of inputs a run converts, unless <c>CSVHELPER_EXCEL_FUZZ_CONVERSIONS</c> says otherwise.</summary>
	private const int DefaultInputs = 1000;

	/// <summary>The text of a CSV file that was there before a conversion over it.</summary>
	private const string Old = "old,file\r\n";

	/// <summary>How long one input may take.</summary>
	private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

	/// <summary>The significant digits a seed rounds numbers to.</summary>
	private static readonly int?[] Digits = [15, 17, null, 1];

	/// <summary>How a seed converts its workbook.</summary>
	private enum Mode
	{
		/// <summary>From a file to a file: <see cref="ExcelConvert.ToCsv(string, string, ExcelToCsvOptions?)"/>.</summary>
		File,

		/// <summary>To a file for each worksheet: <see cref="ExcelConvert.ToCsvFiles(string, string, ExcelToCsvOptions?)"/>.</summary>
		Files,

		/// <summary>From a stream to a stream: <see cref="ExcelConvert.ToCsv(Stream, Stream, ExcelToCsvOptions?)"/>.</summary>
		Stream,
	}

	[Fact]
	public async Task ConvertsOrRejectsEveryMutatedWorkbook()
	{
		var inputs = Setting("CSVHELPER_EXCEL_FUZZ_CONVERSIONS", DefaultInputs);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var outcomes = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var failures = new List<string>();
		var root = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}");
		try
		{
			for (var seed = first; seed < first + inputs && failures.Count < MaxFailures; seed++)
			{
				var (description, zip) = FuzzTests.MutatedWorkbook(seed);
				var settings = Settings.Of(seed);
				var folder = Path.Combine(root, seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
				Directory.CreateDirectory(folder);
				var run = Task.Run(() => ConvertAsync(zip, folder, settings));
				using (var stop = new CancellationTokenSource())
				{
					var finished = await Task.WhenAny(run, Task.Delay(TimeLimit, stop.Token));
					stop.Cancel();
					if (finished != run)
					{
						// The input's thread goes on, and may hold the folder's files, so the run stops here.
						failures.Add($"Seed {seed} ({settings}), {description}: still running after {TimeLimit.TotalSeconds} s");

						break;
					}
				}

				try
				{
					Count(outcomes, $"{settings.Mode}: {await run}");
				}
				catch (Exception e)
				{
					failures.Add($"Seed {seed} ({settings}), {description}: {(e is Xunit.Sdk.XunitException ? e.Message : Describe(e))}");
				}

				try
				{
					Directory.Delete(folder, recursive: true);
				}
				catch (IOException e)
				{
					failures.Add($"Seed {seed} ({settings}), {description}: a file is still open: {e.Message}");
				}
			}
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}

		output.WriteLine($"Seeds {first} to {first + inputs - 1}:");
		foreach (var pair in outcomes)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_CONVERSIONS");
	}

	/// <summary>Converts a workbook as the seed says, and checks what the conversion left.</summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="folder">The seed's folder, empty.</param>
	/// <param name="settings">How to convert it.</param>
	/// <returns>A task whose result is how it ended: <c>converted</c>, or the exception that ended it.</returns>
	private static async Task<string> ConvertAsync(byte[] zip, string folder, Settings settings)
	{
		var excelPath = Path.Combine(folder, "book.xlsx");
		File.WriteAllBytes(excelPath, zip);
		var csvPath = Path.Combine(folder, "book.csv");
		var filesFolder = Path.Combine(folder, "sheets");
		if (settings.Existing)
		{
			File.WriteAllText(csvPath, Old);
		}

		using var input = new MemoryStream(zip);
		using var csv = new MemoryStream();
		try
		{
			switch (settings.Mode)
			{
				case Mode.File:
					await (settings.Async ? ExcelConvert.ToCsvAsync(excelPath, csvPath, settings.Options) : Sync(() => ExcelConvert.ToCsv(excelPath, csvPath, settings.Options)));
					Assert.True(Expected(zip, settings.Options, null).SequenceEqual(File.ReadAllBytes(csvPath)), "the file's CSV isn't the stream's");

					break;
				case Mode.Files:
					var paths = settings.Async ? await ExcelConvert.ToCsvFilesAsync(excelPath, filesFolder, settings.Options) : ExcelConvert.ToCsvFiles(excelPath, filesFolder, settings.Options);
					CheckFiles(zip, filesFolder, settings.Options, paths.Count);
					Assert.Equal(paths.OrderBy(p => p, StringComparer.Ordinal), Files(filesFolder));

					break;
				default:
					await (settings.Async ? ExcelConvert.ToCsvAsync(input, csv, settings.Options) : Sync(() => ExcelConvert.ToCsv(input, csv, settings.Options)));
					Assert.True(Expected(zip, settings.Options, null).SequenceEqual(csv.ToArray()), "the asynchronous CSV isn't the synchronous one's");

					break;
			}

			return "converted";
		}
		catch (Exception e) when (FuzzTests.IsExpected(e) || IsErrorCell(e, settings.Options))
		{
			switch (settings.Mode)
			{
				case Mode.File:
					// No file, or the one that was there, as it was: the workbook failed before the CSV was written.
					if (File.Exists(csvPath))
					{
						Assert.True(settings.Existing && File.ReadAllText(csvPath) == Old, "a failure left a CSV file written");
					}

					break;
				case Mode.Files:
					CheckFiles(zip, filesFolder, settings.Options, null);

					break;
				default:
					Assert.True(csv.CanWrite, "a failure closed the CSV stream");

					break;
			}

			return (e.InnerException is null ? "" : "wrapped ") + e.GetType().Name;
		}
	}

	/// <summary>
	/// Checks a folder of a file for each worksheet: after a conversion that succeeded, a file for each; after one that
	/// failed, the files of the worksheets before one, in tab order, and no other. Each file holds its sheet as a synchronous
	/// conversion of a stream of that sheet gives it.
	/// </summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="folder">The folder.</param>
	/// <param name="options">The conversion's options.</param>
	/// <param name="count">The number of files the conversion gave, or <see langword="null"/> when it failed.</param>
	private static void CheckFiles(byte[] zip, string folder, ExcelToCsvOptions options, int? count)
	{
		var left = Files(folder);
		if (left.Count == 0 && count is null or 0)
		{
			return;
		}

		using var workbook = ExcelWorkbook.Open(new MemoryStream(zip), options.Excel);
		var sheets = workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet).ToList();
		var names = ConversionNames.FileNames([.. sheets.Select(s => (s.Name, s.Index + 1))]);
		var written = count ?? left.Count;
		Assert.True(count is null ? written < sheets.Count : written == sheets.Count, $"{written} files for {sheets.Count} worksheets");
		Assert.Equal(names.Take(written).Select(n => Path.Combine(folder, n)).OrderBy(p => p, StringComparer.Ordinal), left);
		for (var i = 0; i < written; i++)
		{
			Assert.True(Expected(zip, options, sheets[i].Index).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, names[i]))), $"{names[i]} isn't its sheet's CSV");
		}
	}

	/// <summary>The files in a folder, in ordinal order; none when it doesn't exist.</summary>
	/// <param name="folder">The folder.</param>
	/// <returns>The files' paths.</returns>
	private static List<string> Files(string folder) =>
		Directory.Exists(folder) ? [.. Directory.GetFileSystemEntries(folder).OrderBy(p => p, StringComparer.Ordinal)] : [];

	/// <summary>A workbook's CSV as a synchronous conversion of streams gives it, of the first worksheet or of one.</summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="options">The conversion's options.</param>
	/// <param name="sheet">The sheet's place in tab order, or <see langword="null"/> for the first worksheet.</param>
	/// <returns>The CSV's bytes.</returns>
	private static byte[] Expected(byte[] zip, ExcelToCsvOptions options, int? sheet)
	{
		using var input = new MemoryStream(zip);
		using var csv = new MemoryStream();
		ExcelConvert.ToCsv(input, csv, sheet is null ? options : options with { Excel = options.Excel with { SheetIndex = sheet } });

		return csv.ToArray();
	}

	/// <summary>Whether an exception is an error cell's, which fails every read when the options say so.</summary>
	/// <param name="e">The exception.</param>
	/// <param name="options">The conversion's options.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsErrorCell(Exception e, ExcelToCsvOptions options) =>
		options.Excel.ErrorCells == ExcelErrorCells.Error && e is FormatException and not CsvFormatException && e.Message.Contains(" holds the error ");

	/// <summary>Runs a synchronous conversion as a task, so that it is awaited as an asynchronous one is.</summary>
	/// <param name="convert">The conversion.</param>
	/// <returns>A completed task, or a failed one.</returns>
	private static ValueTask Sync(Action convert)
	{
		convert();

		return default;
	}

	/// <summary>How a seed converts its workbook: its mode, synchronously or not, its options, and whether a CSV file was there.</summary>
	/// <param name="mode">The mode.</param>
	/// <param name="async">Whether to convert asynchronously.</param>
	/// <param name="options">The options, under small limits.</param>
	/// <param name="existing">Whether a CSV file is there before a file's conversion.</param>
	private sealed class Settings(Mode mode, bool async, ExcelToCsvOptions options, bool existing)
	{
		/// <summary>The mode.</summary>
		public Mode Mode { get; } = mode;

		/// <summary>Whether to convert asynchronously.</summary>
		public bool Async { get; } = async;

		/// <summary>The options.</summary>
		public ExcelToCsvOptions Options { get; } = options;

		/// <summary>Whether a CSV file is there before a file's conversion.</summary>
		public bool Existing { get; } = existing;

		/// <summary>The settings of a seed.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The settings.</returns>
		public static Settings Of(int seed)
		{
			var random = new Random(Mix(seed));
			var excel = new ExcelReaderOptions
			{
				MaxPartSize = 1 << 20,
				MaxUncompressedSize = 16 << 20,
				Formulas = random.Next(2) == 0 ? ExcelFormulas.CachedValue : ExcelFormulas.FormulaText,
				ErrorCells = (ExcelErrorCells)random.Next(3),
				SignificantDigits = Digits[random.Next(Digits.Length)],
				SkipHiddenRows = random.Next(2) == 0,
				SkipHiddenColumns = random.Next(2) == 0,
				FillMergedCells = random.Next(2) == 0,
				Csv = new() { HasHeader = random.Next(3) > 0, SkipEmptyLines = random.Next(2) == 0, TrimWhitespace = random.Next(5) == 0 },
			};
			var csv = new CsvWriterOptions
			{
				Separator = random.Next(4) switch { 0 => ';', 1 => '\t', _ => ',' },
				Injection = (CsvInjection)random.Next(3),
				WriteByteOrderMark = random.Next(4) == 0,
			};

			return new Settings((Mode)random.Next(3), seed % 2 == 1, new ExcelToCsvOptions { Excel = excel, Csv = csv }, random.Next(4) == 0);
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{Mode}, {(Async ? "async" : "sync")}{(Existing && Mode == Mode.File ? ", over a file" : "")}, formulas {Options.Excel.Formulas}, " +
			$"error cells {Options.Excel.ErrorCells}, digits {Options.Excel.SignificantDigits?.ToString() ?? "null"}, header {Options.Excel.Csv.HasHeader}, " +
			$"skip empty {Options.Excel.Csv.SkipEmptyLines}, trim {Options.Excel.Csv.TrimWhitespace}, hidden rows {(Options.Excel.SkipHiddenRows ? "skipped" : "read")}, " +
			$"hidden columns {(Options.Excel.SkipHiddenColumns ? "skipped" : "read")}, merged cells {(Options.Excel.FillMergedCells ? "filled" : "not filled")}, " +
			$"separator {Show(Options.Csv.Separator.ToString())}, injection {Options.Csv.Injection}, byte order mark {Options.Csv.WriteByteOrderMark}";

		/// <summary>
		/// A seed for the settings' numbers, its bits mixed (MurmurHash3's finalizer), as <see cref="FuzzTests"/> mixes its
		/// settings' seed, so that they don't follow the mutations'.
		/// </summary>
		/// <param name="seed">The input's seed.</param>
		/// <returns>The settings' seed.</returns>
		private static int Mix(int seed)
		{
			var x = unchecked(((uint)seed * 0x9E3779B9u) + 0x7F4A7C15u);
			x ^= x >> 16;
			x = unchecked(x * 0x85EBCA6Bu);
			x ^= x >> 13;
			x = unchecked(x * 0xC2B2AE35u);
			x ^= x >> 16;

			return (int)(x & int.MaxValue);
		}
	}
}
