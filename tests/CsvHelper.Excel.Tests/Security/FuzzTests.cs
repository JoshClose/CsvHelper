using System.IO.Compression;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded mutation fuzzing, as the plan's "Fuzzing" describes: workbooks from synthetic packages and from the corpus,
/// mutated in an XML part or in their zip bytes, each read through the public API to the end of every worksheet. Each
/// input must finish within five seconds, and either read or throw <see cref="InvalidDataException"/>,
/// <see cref="CsvFormatException"/> or <see cref="NotSupportedException"/>. <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> sets the
/// number of inputs, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed, so that a failure can be replayed alone.
/// </summary>
/// <param name="output">Shows how the inputs ended.</param>
public class FuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of inputs a run reads, unless <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> says otherwise.</summary>
	private const int DefaultInputs = 3000;

	/// <summary>How long one input may take.</summary>
	private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

	/// <summary>Markup and values the XML mutations insert: the scanner's delimiters, its elements, and edge cases.</summary>
	private static readonly string[] Tokens =
	[
		"<", ">", "&", "\"", "'", "/", "=", "<row>", "</row>", "<row r=\"1048576\">", "<c>", "</c>", "<c r=\"XFD1048576\">", "<c r=\"A0\">",
		"<v>", "</v>", "<v/>", "<is>", "</is>", "<t>", "</t>", "<r>", "</r>", "<si>", "</si>", "<rPh>", "<sheetData>", "</sheetData>",
		"<x:c>", "<![CDATA[", "]]>", "<!--", "-->", "<?pi?>", "<!DOCTYPE x>", "&amp;", "&#0;", "&#x10FFFF;", "&#xD800;", "&bogus;",
		"_x0041_", "_xD83D_", "_x00", " xml:space=\"preserve\"", " t=\"s\"", " t=\"inlineStr\"", " t=\"b\"", " t=\"e\"", " t=\"d\"",
		" s=\"99999\"", " r=\"\"", " r=\"A1\"", "\uFEFF", "\u0000", "\r\n",
	];

	/// <summary>Values the XML mutations put in place of a run of digits.</summary>
	private static readonly string[] Numbers = ["0", "-1", "1048577", "2147483648", "99999999999999999999", "1E+308", "NaN", ""];

	/// <summary>The workbooks the inputs start from, loaded once.</summary>
	private static readonly Lazy<List<Source>> Sources = new(LoadSources);

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
			var async = seed % 2 == 1;
			var current = seed;
			var run = Task.Run(() => ReadAsync(input.Zip, current, async));
			using (var stop = new CancellationTokenSource())
			{
				var finished = await Task.WhenAny(run, Task.Delay(TimeLimit, stop.Token));
				stop.Cancel();
				if (finished != run)
				{
					// The input's thread goes on; the run stops here, since it may be using a core.
					failures.Add($"{input.Describe(seed, async)}: still running after {TimeLimit.TotalSeconds} s");

					break;
				}
			}

			try
			{
				var outcome = await run;
				outcomes[outcome] = outcomes.TryGetValue(outcome, out var count) ? count + 1 : 1;
			}
			catch (Exception e)
			{
				failures.Add($"{input.Describe(seed, async)}: {e.GetType().FullName}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(12))}");
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
	/// <param name="seed">The input's seed, which also chooses the CSV options.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <returns>How it ended: <c>read</c>, or the expected exception that ended it (or that the first sheet to fail threw).</returns>
	private static async Task<string> ReadAsync(byte[] zip, int seed, bool async)
	{
		var options = new ExcelReaderOptions
		{
			MaxPartSize = 1 << 20,
			MaxUncompressedSize = 16 << 20,
			Csv = new() { HasHeader = seed % 3 != 0, SkipEmptyLines = seed % 5 != 0, TrimWhitespace = seed % 7 == 0 },
		};
		try
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), options) : ExcelWorkbook.Open(new MemoryStream(zip), options);
			var outcome = "read";
			foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
			{
				try
				{
					using var reader = await workbook.OpenReaderAsync(async, sheet);
					while (async ? await reader.ReadAsync() : reader.Read())
					{
						for (var i = 0; i < reader.FieldCount; i++)
						{
							reader.GetString(i);
						}
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
	/// The workbooks the inputs start from: three synthetic packages (Excel's shape with every kind of cell, the orders
	/// workbook, and numbers) and every corpus workbook CsvHelper.Excel reads.
	/// </summary>
	/// <returns>The sources.</returns>
	private static List<Source> LoadSources()
	{
		var variety = Workbooks.Data(
				"<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"D1\" t=\"inlineStr\"><is><r><t xml:space=\"preserve\"> rich </t></r><r><t>run</t></r></is></c></row>" +
				"<row><c t=\"b\"><v>1</v></c><c t=\"e\"><v>#N/A</v></c><c s=\"1\"><v>45293.5</v></c><c t=\"str\"><f>A1</f><v>_x0041_&amp;</v></c></row>" +
				"<row r=\"5\" spans=\"1:3\"><c r=\"C5\" t=\"d\"><v>2024-01-02T03:04:05</v></c><c r=\"D5\"><v> 1.5E-3 </v></c></row>")
			.SharedStrings("<t>Name</t>", "<r><rPr><b/></rPr><t>Bold</t></r><r><t xml:space=\"preserve\"> plain</t></r><rPh sb=\"0\" eb=\"1\"><t>no</t></rPh>");
		var packages = new[] { ("synthetic/variety", variety), ("synthetic/orders", Workbooks.Orders()), ("synthetic/numbers", Workbooks.Data(Workbooks.Numbers(40))) };
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
		/// <param name="async">Whether it was read asynchronously.</param>
		/// <returns>The description.</returns>
		public string Describe(int seed, bool async) => $"Seed {seed} ({(async ? "async" : "sync")}), {source}: {steps}";
	}
}
