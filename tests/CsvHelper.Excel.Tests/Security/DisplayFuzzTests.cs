using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of the displayed text of Phase 8 (its step 8): Phase 5's random codes
/// (<see cref="FormatCodeFuzzTests.Code"/>), every one, those Excel and the writer refuse too, over random numbers, dates,
/// texts and booleans. The renderer (<see cref="NumberText"/>, and through it <see cref="DateText"/>) never throws in the
/// invariant culture, German and Japanese, in either date system; a text it shows has at most 255 characters (but text
/// through <c>@</c> alone, which shows as it is), is valid UTF-8, and needs the same room whatever room it is given. A
/// reader under <see cref="ExcelText.Formatted"/> reads workbooks of the codes without throwing, every field its shown text
/// within 255 characters or <see cref="ExcelText.Raw"/>'s, and converts them to CSV. On .NET 10 in Release, rendering
/// through the reader's formatter allocates nothing per cell. <c>CSVHELPER_EXCEL_FUZZ_CODES</c> sets the number of codes,
/// and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed. Excel judges the codes' text on demand through the corpus tool's
/// <c>fuzzed-text</c> command, over the workbooks <see cref="FormatCodeFuzzTests"/> saves.
/// </summary>
/// <param name="output">Shows what the run checked.</param>
public class DisplayFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of codes the renderer's run checks, unless <c>CSVHELPER_EXCEL_FUZZ_CODES</c> says otherwise.</summary>
	private const int DefaultCodes = 10000;

	/// <summary>The number of codes the reader's run checks, unless <c>CSVHELPER_EXCEL_FUZZ_CODES</c> says otherwise.</summary>
	private const int DefaultReaderCodes = 400;

	/// <summary>The codes of each workbook the reader's run reads, a column each.</summary>
	private const int CodesPerWorkbook = 50;

	/// <summary>The room a text is rendered into first: more than 255 characters of three bytes, and four bytes a surrogate pair.</summary>
	private const int Room = 1024;

	/// <summary>UTF-8 that throws on bytes that aren't.</summary>
	private static readonly UTF8Encoding Strict = new(false, true);

	/// <summary>Numbers every code shows: signs, zeros, halves, the date system's edges, the 15th digit, fractions' and General's bounds, extremes.</summary>
	private static readonly double[] Fixed =
	[
		0, -0.0, 1, -1, 0.5, -0.5, 1.5, 0.1, 1.0 / 3, -2.0 / 3, 59.5, 60, 61, 45306.5208333, 45306.99999999, 2958465.99999, 2958466, -1E-20, 5E-324,
		2.2250738585072014E-308, 1E+15, 123456789012345678, 1E+300, -1E+300, double.MaxValue, double.MinValue, 2147483646.5, 2147483647, 32767.75,
		0.999999999, 1234.5678, -1234.5678, 1E-07, 9.87654321E-05, 12345678901, 99999999999.5,
	];

	/// <summary>Texts every code with a text section shows: empty, short, around 255 characters, the most a cell holds, surrogate pairs and other scripts.</summary>
	private static readonly string[] Texts =
	[
		"", "abc", " a ", "TRUE", new string('x', 254), new string('x', 255), new string('y', 256), new string('z', 32767), Units(0xD83D, 0xDE00),
		string.Concat(Enumerable.Repeat(Units(0xD83D, 0xDE00), 130)), Units(0x65E5, 0x672C, 0x8A9E), "=1+1", "\"q\";,",
	];

	/// <summary>The cultures the renderer's run shows in: Excel en-US, and German and Japanese, whose names and separators differ.</summary>
	private static readonly DisplayCulture[] Cultures =
	[
		DisplayCulture.Invariant, DisplayCulture.For(CultureInfo.GetCultureInfo("de-DE")), DisplayCulture.For(CultureInfo.GetCultureInfo("ja-JP")),
	];

	/// <summary>
	/// Each code over <see cref="Fixed"/> and twelve random numbers in each culture, in a date system chosen at random, and
	/// over <see cref="Texts"/>: bounded, valid UTF-8, and the same whatever the room.
	/// </summary>
	[Fact]
	public void ShowsRandomCodesOverRandomValues()
	{
		var codes = Setting("CSVHELPER_EXCEL_FUZZ_CODES", DefaultCodes);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var texts = Texts.Select(t => Encoding.UTF8.GetBytes(t)).ToArray();
		var room = new byte[Room];
		for (var seed = first; seed < first + codes && failures.Count < MaxFailures; seed++)
		{
			var code = FormatCodeFuzzTests.Code(seed);
			try
			{
				var compiled = FormatCode.Compile(code);
				Count(tally, compiled.IsReadable ? "codes read" : "codes that can't be read");
				var random = new Random(seed);
				var numbers = Fixed.Concat(Enumerable.Range(0, 12).Select(_ => Number(random))).ToArray();
				foreach (var culture in Cultures)
				{
					var date1904 = random.Next(2) == 0;
					foreach (var number in numbers)
					{
						if (CheckNumber(compiled, number, culture, date1904, room) is { } problem)
						{
							failures.Add($"Seed {seed} ({Show(code)}), {number.ToString("R", CultureInfo.InvariantCulture)}: {problem}");

							break;
						}

						Count(tally, "numbers checked");
					}
				}

				foreach (var text in texts)
				{
					if (CheckText(compiled, text, room, tally) is { } problem)
					{
						failures.Add($"Seed {seed} ({Show(code)}), text of {text.Length} bytes: {problem}");

						break;
					}
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed} ({Show(code)}): {Describe(e)}");
			}
		}

		Report(first, codes, tally);
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_CODES");
	}

	/// <summary>Workbooks of 50 codes each, read sync or async by their first seed, under both texts, and converted.</summary>
	/// <returns>A task.</returns>
	[Fact]
	public async Task ReadsRandomCodesThroughTheReader()
	{
		// The renderer's number of codes, scaled: 400 for its 10,000.
		var codes = Math.Max(1, (int)((long)Setting("CSVHELPER_EXCEL_FUZZ_CODES", DefaultCodes) * DefaultReaderCodes / DefaultCodes));
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var start = first; start < first + codes && failures.Count < MaxFailures; start += CodesPerWorkbook)
		{
			var seeds = Enumerable.Range(start, Math.Min(CodesPerWorkbook, first + codes - start)).ToList();
			try
			{
				if (await WorkbookProblemAsync(seeds, start % 2 == 0, tally) is { } problem)
				{
					failures.Add($"Seeds {seeds[0]} to {seeds[seeds.Count - 1]}: {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seeds {seeds[0]} to {seeds[seeds.Count - 1]}: {Describe(e)}");
			}
		}

		Report(first, codes, tally);
		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_CODES");
	}

#if NET10_0_OR_GREATER
	/// <summary>
	/// The renderer over 500 random codes allocates nothing once each code is compiled, whatever it shows: numbers, dates
	/// and text, in German and in the 1904 date system too.
	/// </summary>
	[OptimizedFact]
	public void RendersRandomCodesWithoutAllocating()
	{
		var codes = Enumerable.Range(1, 500).Select(FormatCodeFuzzTests.Code).Select(FormatCode.Compile).ToArray();
		var cultures = new[] { DisplayCulture.Invariant, DisplayCulture.For(CultureInfo.GetCultureInfo("de-DE")) };
		var texts = Texts.Select(t => Encoding.UTF8.GetBytes(t)).ToArray();
		var room = new byte[Room];
		Render();
		var before = GC.GetAllocatedBytesForCurrentThread();
		Render();
		Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

		void Render()
		{
			foreach (var culture in cultures)
			{
				foreach (var code in codes)
				{
					foreach (var number in Fixed)
					{
						var shown = new ShownText(room);
						NumberText.TryWrite(code, number, culture, ReferenceEquals(culture, DisplayCulture.Invariant), ref shown);
					}

					foreach (var text in texts)
					{
						var shown = new ShownText(room);
						NumberText.TryWriteText(code, text, ref shown);
					}
				}
			}
		}
	}

	/// <summary>
	/// A reader under <see cref="ExcelText.Formatted"/> of a sheet of 50 random codes, a column each, of numbers, booleans
	/// and shared strings, converts 1,000 rows to CSV allocating within 4 KiB of what it allocates for 100: nothing for each
	/// of the 45,000 more cells, through the reader's formatter and core's text paths.
	/// </summary>
	[OptimizedFact]
	public void ConvertsRandomCodesAllocatingNothingPerCell()
	{
		var codes = Enumerable.Range(1, CodesPerWorkbook).Select(FormatCodeFuzzTests.Code).Select(XmlSafe).ToList();
		var random = new Random(1);
		var rows = new StringBuilder();
		for (var r = 1; r <= 1000; r++)
		{
			rows.Append($"<row r=\"{r}\">");
			for (var c = 0; c < codes.Count; c++)
			{
				var reference = CellReference.ColumnName(c) + r.ToString(CultureInfo.InvariantCulture);
				var number = Number(random);
				rows.Append(((r + c) % 4) switch
				{
					0 => $"<c r=\"{reference}\" s=\"{c}\" t=\"s\"><v>{random.Next(Texts.Length)}</v></c>",
					1 => $"<c r=\"{reference}\" s=\"{c}\" t=\"b\"><v>{random.Next(2)}</v></c>",
					_ => $"<c r=\"{reference}\" s=\"{c}\"><v>{(double.IsFinite(number) ? number : r).ToString("R", CultureInfo.InvariantCulture)}</v></c>",
				});
			}

			rows.Append("</row>");
		}

		var workbook = Package(codes, rows.ToString()).ToArray();
		var last = CellReference.ColumnName(codes.Count - 1);

		long Allocated(int count)
		{
			var options = new ExcelReaderOptions { Text = ExcelText.Formatted, Range = $"A1:{last}{count}", Csv = new CsvReaderOptions { HasHeader = false } };
			using var reader = ExcelReader.Create(new MemoryStream(workbook), options);
			using var writer = CsvWriter.Create(Stream.Null);

			// The first record warms the reader's and the writer's buffers, and compiles the codes.
			Assert.True(reader.Read());
			var before = GC.GetAllocatedBytesForCurrentThread();
			ExcelConvert.ToCsv(reader, writer);

			return GC.GetAllocatedBytesForCurrentThread() - before;
		}

		// Each size is measured twice, and the smaller kept, as ConversionCorpusTests does: the runtime allocates once on
		// the thread at a moment the JIT's tiering sets.
		Allocated(1000);
		var few = Math.Min(Allocated(100), Allocated(100));
		var many = Math.Min(Allocated(1000), Allocated(1000));
		Assert.True(many - few < 4096, $"100 rows allocated {few:N0} bytes and 1,000 rows {many:N0}.");
	}
#endif

	/// <summary>A random number: an integer, a fraction of any size, a date or time, a negative time, any double's bits, a rounded decimal, or a tiny or huge one.</summary>
	/// <param name="random">The random numbers.</param>
	/// <returns>The number, NaN and infinities among them.</returns>
	private static double Number(Random random)
	{
		switch (random.Next(8))
		{
			case 0:
			{
				return random.Next(-100000, 100000);
			}
			case 1:
			{
				return (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-20, 21));
			}
			case 2:
			{
				return random.NextDouble() * 2958467;
			}
			case 3:
			{
				return -random.NextDouble() * 3;
			}
			case 4:
			{
				var bits = new byte[8];
				random.NextBytes(bits);

				return BitConverter.ToDouble(bits, 0);
			}
			case 5:
			{
				return Math.Round((random.NextDouble() - 0.3) * 1000, random.Next(0, 6));
			}
			case 6:
			{
				return random.Next(2) == 0 ? random.NextDouble() * 1E+16 : random.NextDouble() * 1E-300;
			}
			default:
			{
				return random.Next(1, 100) + random.Next(0, 86400) / 86400.0;
			}
		}
	}

	/// <summary>Checks a number's text in a code: bounded, valid, and the same whatever the room.</summary>
	/// <param name="code">The code.</param>
	/// <param name="number">The number.</param>
	/// <param name="culture">The culture.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <param name="room">A span of <see cref="Room"/> bytes.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? CheckNumber(FormatCode code, double number, DisplayCulture culture, bool date1904, byte[] room)
	{
		var text = new ShownText(room);
		var shown = NumberText.TryWrite(code, number, culture, date1904, ref text);
		var counting = new ShownText(Span<byte>.Empty);
		if (NumberText.TryWrite(code, number, culture, date1904, ref counting) != shown || (shown && counting.Length != text.Length))
		{
			return $"counting gives {counting.Length} bytes, the room {text.Length}";
		}

		if (!shown)
		{
			return null;
		}

		if (text.Chars > NumberText.MaxChars)
		{
			return $"{text.Chars} characters";
		}

		return Valid(text, room, (ref ShownText t) => NumberText.TryWrite(code, number, culture, date1904, ref t));
	}

	/// <summary>Checks a text's text through a code's text section: bounded but through <c>@</c> alone, valid, and the same whatever the room.</summary>
	/// <param name="code">The code.</param>
	/// <param name="value">The text, in UTF-8.</param>
	/// <param name="room">A span of <see cref="Room"/> bytes.</param>
	/// <param name="tally">Takes what was shown.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? CheckText(FormatCode code, byte[] value, byte[] room, SortedDictionary<string, int> tally)
	{
		var big = new byte[value.Length * 2 + Room];
		var text = new ShownText(big);
		var shown = NumberText.TryWriteText(code, value, ref text);
		var counting = new ShownText(Span<byte>.Empty);
		if (NumberText.TryWriteText(code, value, ref counting) != shown || (shown && counting.Length != text.Length))
		{
			return $"counting gives {counting.Length} bytes, the room {text.Length}";
		}

		if (!shown)
		{
			return null;
		}

		Count(tally, "texts shown");
		var tokens = code.Text!.Tokens;
		if (text.Chars > NumberText.MaxChars && !(tokens.Length == 1 && tokens[0].Kind == FormatTokenKind.At))
		{
			return $"{text.Chars} characters";
		}

		return Valid(text, big, (ref ShownText t) => NumberText.TryWriteText(code, value, ref t));
	}

	/// <summary>Renders into a text.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether it was shown.</returns>
	private delegate bool Render(ref ShownText text);

	/// <summary>
	/// Checks a shown text: it fits the room it was given, is valid UTF-8, and renders again the same into half its room
	/// (not fitting, its length the same) and into exactly its room.
	/// </summary>
	/// <param name="text">The text, rendered into <paramref name="room"/>.</param>
	/// <param name="room">Its room.</param>
	/// <param name="render">Renders it again.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	private static string? Valid(ShownText text, byte[] room, Render render)
	{
		if (!text.Fits)
		{
			return $"{text.Length} bytes didn't fit {room.Length}";
		}

		var bytes = text.Written.ToArray();
		try
		{
			_ = Strict.GetString(bytes);
		}
		catch (DecoderFallbackException)
		{
			return $"not UTF-8: {Show(bytes)}";
		}

		var half = new ShownText(new byte[bytes.Length / 2]);
		if (!render(ref half) || half.Length != bytes.Length || (bytes.Length > 0 && half.Fits))
		{
			return $"half the room gives {half.Length} bytes, fitting {half.Fits}, of {bytes.Length}";
		}

		var exact = new ShownText(new byte[bytes.Length]);
		if (!render(ref exact) || !exact.Fits || !exact.Written.SequenceEqual(bytes))
		{
			return "exactly the room gives other bytes";
		}

		return null;
	}

	/// <summary>
	/// Writes a workbook of a column for each code, its rows numbers, dates, texts (shared and inline), booleans and errors,
	/// and reads it under <see cref="ExcelText.Formatted"/> and <see cref="ExcelText.Raw"/>: every field is Raw's text or
	/// one of at most 255 characters, and the conversion to CSV runs.
	/// </summary>
	/// <param name="seeds">The codes' seeds.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="tally">Takes what was read.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> WorkbookProblemAsync(List<int> seeds, bool async, SortedDictionary<string, int> tally)
	{
		var codes = seeds.Select(FormatCodeFuzzTests.Code).Select(XmlSafe).ToList();
		var random = new Random(seeds[0]);
		var rows = new StringBuilder();
		for (var r = 1; r <= 24; r++)
		{
			rows.Append($"<row r=\"{r}\">");
			for (var c = 0; c < codes.Count; c++)
			{
				var reference = CellReference.ColumnName(c) + r.ToString(CultureInfo.InvariantCulture);
				var number = Number(random);
				rows.Append((r % 6) switch
				{
					0 => $"<c r=\"{reference}\" s=\"{c}\" t=\"s\"><v>{random.Next(Texts.Length)}</v></c>",
					1 => $"<c r=\"{reference}\" s=\"{c}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Sheets.Escape(Texts[random.Next(Texts.Length)])}</t></is></c>",
					2 => $"<c r=\"{reference}\" s=\"{c}\" t=\"b\"><v>{random.Next(2)}</v></c>",
					3 => $"<c r=\"{reference}\" s=\"{c}\" t=\"e\"><v>#N/A</v></c>",
					_ => $"<c r=\"{reference}\" s=\"{c}\"><v>{(double.IsNaN(number) || double.IsInfinity(number) ? Fixed[r] : number).ToString("R", CultureInfo.InvariantCulture)}</v></c>",
				});
			}

			rows.Append("</row>");
		}

		var package = Package(codes, rows.ToString());
		var formatted = await ReadAsync(package, async, ExcelText.Formatted);
		var raw = await ReadAsync(package, async, ExcelText.Raw);
		if (formatted.Count != raw.Count)
		{
			return $"{formatted.Count} fields Formatted, {raw.Count} Raw";
		}

		for (var i = 0; i < raw.Count; i++)
		{
			if (formatted[i] != raw[i])
			{
				Count(tally, "fields shown");
				if (formatted[i].Length > NumberText.MaxChars)
				{
					return $"field {i} shows {formatted[i].Length} characters: {Show(formatted[i])}";
				}
			}
		}

		Count(tally, "workbooks read");
		using var input = package.ToStream();
		using var csv = new MemoryStream();
		var options = new ExcelToCsvOptions { Excel = new ExcelReaderOptions { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } } };
		if (async)
		{
			await ExcelConvert.ToCsvAsync(input, csv, options);
		}
		else
		{
			ExcelConvert.ToCsv(input, csv, options);
		}

		return csv.Length > 0 ? null : "the conversion wrote nothing";
	}

	/// <summary>Reads every field of a workbook's sheet, in order.</summary>
	/// <param name="package">The workbook.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="text">What text the reader gives.</param>
	/// <returns>The fields.</returns>
	private static async Task<List<string>> ReadAsync(TestPackage package, bool async, ExcelText text)
	{
		var options = new ExcelReaderOptions { Text = text, Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } };
		using var workbook = await Workbooks.OpenAsync(package, async);
		using var reader = await workbook.OpenReaderAsync(async, options);
		var fields = new List<string>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				fields.Add(reader.GetString(i));
			}
		}

		return fields;
	}

	/// <summary>A workbook of one sheet whose cell formats are the codes, in order, and whose shared strings are <see cref="Texts"/>.</summary>
	/// <param name="codes">The codes, which XML can hold.</param>
	/// <param name="rows">The content of <c>sheetData</c>.</param>
	/// <returns>The package.</returns>
	private static TestPackage Package(List<string> codes, string rows)
	{
		var custom = new StringBuilder();
		var cells = new StringBuilder();
		for (var i = 0; i < codes.Count; i++)
		{
			custom.Append($"<numFmt numFmtId=\"{164 + i}\" formatCode=\"{Attribute(codes[i])}\"/>");
			cells.Append($"<xf numFmtId=\"{164 + i}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		}

		var styles = TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\">" +
			$"<numFmts count=\"{codes.Count}\">{custom}</numFmts>" +
			"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
			"<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
			$"<cellXfs count=\"{codes.Count}\">{cells}</cellXfs></styleSheet>";

		return TestPackage.Workbook(new TestSheet("Codes", xml: Sheets.Worksheet(rows)))
			.Set("xl/styles.xml", styles)
			.SharedStrings(Texts.Select(t => $"<t xml:space=\"preserve\">{Sheets.Escape(t)}</t>").ToArray());
	}

	/// <summary>A code XML 1.0 can hold: its control characters other than tab, line feed and carriage return dropped.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The code.</returns>
	private static string XmlSafe(string code) => new(code.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray());

	/// <summary>A code as an attribute's value: escaped, and tab, line feed and carriage return as references, which an attribute keeps.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The attribute's value.</returns>
	private static string Attribute(string code) =>
		Sheets.Escape(code).Replace("\"", "&quot;").Replace("\t", "&#9;").Replace("\n", "&#10;").Replace("\r", "&#13;");

	/// <summary>Shows what a run checked.</summary>
	/// <param name="first">The first seed.</param>
	/// <param name="codes">The number of codes.</param>
	/// <param name="tally">The counts.</param>
	private void Report(int first, int codes, SortedDictionary<string, int> tally)
	{
		output.WriteLine($"Seeds {first} to {first + codes - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}
	}
}
