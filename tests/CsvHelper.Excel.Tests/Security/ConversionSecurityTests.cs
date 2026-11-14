using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Phase 6's security cases for the conversions, as its "Testing" describes. Crafted sheet names, in a list and at random
/// (separators, <c>..</c>, drives and shares, streams, Windows' devices, control characters, long names, names alike),
/// whose files <see cref="ExcelConvert.ToCsvFiles(string, string, ExcelToCsvOptions?)"/> writes in its folder and nowhere
/// else, one for each worksheet, each a name every system takes, each holding its sheet. And a CSV with a text no cell can
/// take, which fails before the records that decide the columns' types are all held, while a text as long as a cell takes
/// is held. <c>CSVHELPER_EXCEL_FUZZ_SHEET_NAMES</c> sets the number of random workbooks, and
/// <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the runs wrote and read.</param>
public class ConversionSecurityTests(ITestOutputHelper output)
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The most UTF-16 code units a cell's text has.</summary>
	private const int MaxCell = 32767;

	/// <summary>The most UTF-16 code units of a file's name before its number and extension.</summary>
	private const int MaxStem = 64;

	/// <summary>The number of random workbooks a run converts, unless <c>CSVHELPER_EXCEL_FUZZ_SHEET_NAMES</c> says otherwise.</summary>
	private const int DefaultWorkbooks = 200;

	/// <summary>
	/// Sheet names a crafted workbook can have, which Excel's rules don't allow: paths up and out, absolute paths, drives,
	/// shares and streams, Windows' devices, control characters and white space, dots and spaces at the end, nothing, long
	/// names and a pair at the cut, names alike in case and in Unicode's forms, an escape, and the characters a file name
	/// can't have, and their look-alikes.
	/// </summary>
	private static readonly string[] CraftedNames =
	[
		"../x", @"..\x", "..", ".", "...", "../../../../etc/passwd", "/etc/passwd", @"\Windows\win.ini", "C:x", @"C:\x", @"\\server\share\x",
		"//server/share", "a:b", "x::$DATA", "CON", "con.csv", "NUL ", "COM1", "LPT" + Units(0xB9), "CONIN$", "CONOUT$.txt", "AUX.",
		Units(1, 0x1F), "tab\there", "line\nbreak", Units(0x7F), Units(0xFEFF) + "bom", Units(0x202E) + "right to left", "trailing. . .", "   ", "",
		new string('a', 300), new string('a', 63) + Units(0xD83D, 0xDE00) + "b", "Data", "DATA", "e" + Units(0x301), Units(0xE9), "Sheet3", "_x0041_",
		"<>:\"|?*", Units(0xFF0F) + "slash", Units(0x2215) + "slash", "a" + Units(0xD800) + "b", "NUL." + new string('w', 100),
	];

	/// <summary>
	/// The pieces of random sheet names: the crafted names' separators, dots, drives, devices, controls and look-alikes, and
	/// words, letters past ASCII, a pair, a lone surrogate, a byte order mark and an escape.
	/// </summary>
	private static readonly string[] NamePieces =
	[
		"..", ".", "/", "\\", ":", "*", "?", "\"", "<", ">", "|", " ", "\t", "\n", Units(1), Units(0x1F), Units(0x7F), Units(0x85), Units(0x2028),
		Units(0x202E), "CON", "con", "NUL", "COM1", "LPT" + Units(0xB2), "CONIN$", "conout$", "AUX", "PRN", "C:", @"\\server\share", "/etc/passwd",
		"::$DATA", "~", "%2e%2e%2f", "x", "Data", "data", Units(0xE9), "e" + Units(0x301), Units(0x130), Units(0x131), Units(0xDF), "SS",
		Units(0xD83D, 0xDE00), Units(0xD800), "_x0041_", Units(0xFEFF), Units(0xA0), Units(0x3000), Units(0xFF0E), Units(0xFF0F), Units(0x2215),
		new string('w', 40),
	];

	/// <summary>Windows' device names, which no file's name may be, before an extension or not.</summary>
	private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
	{
		"CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
		"COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM" + Units(0xB9), "COM" + Units(0xB2), "COM" + Units(0xB3),
		"LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT" + Units(0xB9), "LPT" + Units(0xB2), "LPT" + Units(0xB3),
	};

	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEveryCraftedSheetNameInsideTheFolder(bool async)
	{
		// The crafted names, a chartsheet and a hidden sheet among them; the chartsheet has no file.
		var sheets = CraftedNames.Select((name, i) => i == 5 ? new TestSheet(NameXml(name), type: "chartsheet") : new TestSheet(NameXml(name), state: i == 9 ? "hidden" : null, xml: Content(i))).ToArray();
		var files = await CheckAsync(TestPackage.Workbook(sheets).ToArray(), async);

		Assert.Equal(CraftedNames.Length - 1, files.Count);
		output.WriteLine(string.Join("\n", files.Select(Show)));

		// The names, as each rule makes them: separators, drives and streams as _, and the paths up and out numbered when
		// they meet; dots alone, white space and nothing as the sheet's number; devices with _ before them, before a dot
		// too; controls, DEL among them, as _; the cut, short of a pair; names alike in case or in Unicode's forms
		// numbered, the first keeping its own; an escape as it reads; look-alikes of separators and the bidirectional
		// controls kept, as file names may have them; a lone surrogate, which the workbook part's escape gives as U+FFFD;
		// and a device's name past the cut, whose _ counts within it.
		string[] expected =
		[
			".._x.csv", ".._x (2).csv", "Sheet3.csv", "Sheet4.csv", "Sheet5.csv", "_etc_passwd.csv", "_Windows_win.ini.csv", "C_x.csv", "C__x.csv",
			"__server_share_x.csv", "__server_share.csv", "a_b.csv", "x__$DATA.csv", "_CON.csv", "_con.csv.csv", "_NUL.csv", "_COM1.csv",
			"_LPT" + Units(0xB9) + ".csv", "_CONIN$.csv", "_CONOUT$.txt.csv", "_AUX.csv", "__.csv", "tab_here.csv", "line_break.csv",
			"_.csv", Units(0xFEFF) + "bom.csv", Units(0x202E) + "right to left.csv", "trailing.csv", "Sheet30.csv", "Sheet31.csv",
			new string('a', MaxStem) + ".csv", new string('a', 63) + ".csv", "Data.csv", "DATA (2).csv", "e" + Units(0x301) + ".csv",
			Units(0xE9) + " (2).csv", "Sheet3 (2).csv", "_x0041_.csv", "_______.csv", Units(0xFF0F) + "slash.csv", Units(0x2215) + "slash.csv",
			"a" + Units(0xFFFD) + "b.csv", "_NUL." + new string('w', 59) + ".csv",
		];
		Assert.Equal(expected.Select(Show), files.Select(Show));
	}

	[Fact]
	public async Task WritesRandomSheetNamesInsideTheFolder()
	{
		var workbooks = Setting("CSVHELPER_EXCEL_FUZZ_SHEET_NAMES", DefaultWorkbooks);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			var random = new Random(seed);
			var names = Enumerable.Range(0, random.Next(1, 7)).Select(_ => RandomName(random)).ToList();
			var sheets = names.Select((name, i) => random.Next(10) == 0 ? new TestSheet(NameXml(name), type: "chartsheet") : new TestSheet(NameXml(name), state: random.Next(5) == 0 ? "hidden" : null, xml: Content(i))).ToArray();
			try
			{
				var files = await CheckAsync(TestPackage.Workbook(sheets).ToArray(), seed % 2 == 1);
				Count(tally, "workbooks");
				tally["files"] = (tally.TryGetValue("files", out var count) ? count : 0) + files.Count;
				foreach (var file in files)
				{
					var stem = file.Substring(0, file.Length - 4);
					Count(tally, stem.EndsWith(")", StringComparison.Ordinal) && stem.Contains(" (") ? "files numbered" : stem.StartsWith("_", StringComparison.Ordinal) && Devices.Contains(stem.Substring(1).Split('.')[0].TrimEnd(' ')) ? "devices' names" : stem.StartsWith("Sheet", StringComparison.Ordinal) ? "named Sheet" : "other names");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed}, sheets {string.Join(", ", names.Select(Show))}: {(e is Xunit.Sdk.XunitException ? e.Message : Describe(e))}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + workbooks - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_SHEET_NAMES");
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StopsHoldingRecordsAtATextNoCellTakes(bool async)
	{
		// A first record with a text of 40,000 characters, then a thousand records of 30,000, about 30 MB, all of which the
		// records that decide the columns' types would hold before the first failed.
		var csv = new StringBuilder("n,t\r\n1,").Append('x', 40_000).Append("\r\n");
		for (var i = 0; i < 1000; i++)
		{
			csv.Append("2,").Append('y', 30_000).Append("\r\n");
		}

		var bytes = Encoding.UTF8.GetBytes(csv.ToString());
		var input = new ReadTrackingStream(bytes);
		using var workbook = new MemoryStream();
		var e = await Assert.ThrowsAsync<InvalidOperationException>(() => async ? ExcelConvert.ToExcelAsync(input, workbook).AsTask() : Task.Run(() => ExcelConvert.ToExcel(input, workbook)));

		Assert.Equal("Cell Sheet1!B2 has 40,000 characters, and Excel opens no cell of more than 32,767.", e.Message);
		var read = input.ReadInto(0, bytes.Length);
		output.WriteLine($"Read {read:N0} of the CSV's {bytes.Length:N0} bytes.");
		Assert.True(read < MiB, $"{read:N0} bytes read");
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task HoldsATextOfAsManyCodeUnitsAsACellTakes(bool async)
	{
		// 32,767 é, twice as many bytes, and 32,767 bytes that aren't UTF-8, each U+FFFD: as many code units as a cell takes.
		// The record is held, so its number makes its column's type.
		var longest = new string('\u00E9', MaxCell);
		var workbook = await ToExcelAsync(Csv(longest, Invalid(MaxCell)), async);

		Assert.Equal(["A1=Text:n", "B1=Text:t", "C1=Text:u", "A2=Number:1", "B2=Text:" + longest, "C2=Text:" + new string('\uFFFD', MaxCell)], Written.ClosedXmlValues(workbook));

		// One more is too long, in either form.
		var accented = await Assert.ThrowsAsync<InvalidOperationException>(() => ToExcelAsync(Csv(longest + "\u00E9", Invalid(1)), async));
		Assert.Equal("Cell Sheet1!B2 has 32,768 characters, and Excel opens no cell of more than 32,767.", accented.Message);
		var invalid = await Assert.ThrowsAsync<InvalidOperationException>(() => ToExcelAsync(Csv("t", Invalid(MaxCell + 1)), async));
		Assert.Equal("Cell Sheet1!C2 has 32,768 characters, and Excel opens no cell of more than 32,767.", invalid.Message);
	}

	/// <summary>A CSV of a header and one record: <c>1</c>, a text, and bytes.</summary>
	/// <param name="text">The record's second field.</param>
	/// <param name="bytes">Its third field's bytes.</param>
	/// <returns>The CSV's bytes.</returns>
	private static byte[] Csv(string text, byte[] bytes) => [.. Encoding.UTF8.GetBytes("n,t,u\r\n1," + text + ","), .. bytes, .. "\r\n"u8.ToArray()];

	/// <summary>Bytes that aren't UTF-8, each of which decodes to U+FFFD.</summary>
	/// <param name="count">How many.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Invalid(int count) => [.. Enumerable.Repeat((byte)0xFF, count)];

	/// <summary>Converts a CSV to a workbook through the stream overloads, as the mode says.</summary>
	/// <param name="csv">The CSV's bytes.</param>
	/// <param name="async">Whether to convert asynchronously.</param>
	/// <returns>A task whose result is the workbook's bytes.</returns>
	private static async Task<byte[]> ToExcelAsync(byte[] csv, bool async)
	{
		using var input = new MemoryStream(csv);
		using var workbook = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToExcelAsync(input, workbook);
		}
		else
		{
			ExcelConvert.ToExcel(input, workbook);
		}

		return workbook.ToArray();
	}

	/// <summary>
	/// Converts a workbook's worksheets to files in a folder in a folder of its own, beside the workbook, and checks them:
	/// a file for each worksheet, in the folder; nothing else in either folder; names every system takes, none a device's;
	/// each file closed, and holding its sheet as a stream's conversion of that sheet does.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="async">Whether to convert asynchronously.</param>
	/// <returns>A task whose result is the files' names, in tab order.</returns>
	private static async Task<List<string>> CheckAsync(byte[] workbook, bool async)
	{
		var parent = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}");
		Directory.CreateDirectory(parent);
		var excelPath = Path.Combine(parent, "book.xlsx");
		var folder = Path.Combine(parent, "out");
		File.WriteAllBytes(excelPath, workbook);
		try
		{
			var paths = async ? await ExcelConvert.ToCsvFilesAsync(excelPath, folder) : ExcelConvert.ToCsvFiles(excelPath, folder);
			using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
			var worksheets = book.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet).ToList();
			Assert.Equal(worksheets.Count, paths.Count);
			Assert.Equal(new[] { excelPath, folder }, Directory.GetFileSystemEntries(parent).OrderBy(p => p, StringComparer.Ordinal));
			Assert.Equal(paths.OrderBy(p => p, StringComparer.Ordinal), Directory.GetFileSystemEntries(folder).OrderBy(p => p, StringComparer.Ordinal));
			Assert.True(Workbooks.IsClosed(excelPath));
			var names = new List<string>();
			for (var i = 0; i < paths.Count; i++)
			{
				var name = Path.GetFileName(paths[i]);
				Assert.Equal(Path.Combine(folder, name), paths[i]);
				if (NameProblem(name) is { } problem)
				{
					Assert.Fail($"{Show(name)}, of sheet {Show(worksheets[i].Name)}, {problem}");
				}

				Assert.True(Workbooks.IsClosed(paths[i]));
				using var input = new MemoryStream(workbook);
				using var sheet = new MemoryStream();
				ExcelConvert.ToCsv(input, sheet, new ExcelToCsvOptions { Excel = new ExcelReaderOptions { SheetIndex = worksheets[i].Index } });
				Assert.True(sheet.ToArray().SequenceEqual(File.ReadAllBytes(paths[i])), $"{Show(name)} doesn't hold sheet {Show(worksheets[i].Name)}");
				names.Add(name);
			}

			return names;
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	/// <summary>
	/// What is wrong with a file's name, by the rules of every system: a <c>.csv</c> extension after a name of at most 64
	/// code units and a number; no control character, lone surrogate or character Windows' names can't have; no dot or space
	/// at the end; and no device's name before its first dot.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The problem, or <see langword="null"/>.</returns>
	private static string? NameProblem(string name)
	{
		if (!name.EndsWith(".csv", StringComparison.Ordinal))
		{
			return "has no .csv";
		}

		var stem = name.Substring(0, name.Length - 4);
		var numbered = System.Text.RegularExpressions.Regex.Match(stem, @"^(.*) \([0-9]+\)$", System.Text.RegularExpressions.RegexOptions.Singleline);
		var named = numbered.Success ? numbered.Groups[1].Value : stem;
		if (named.Length is 0 or > MaxStem)
		{
			return $"has a name of {named.Length} code units";
		}

		for (var i = 0; i < stem.Length; i++)
		{
			var c = stem[i];
			if (char.IsHighSurrogate(c) && i + 1 < stem.Length && char.IsLowSurrogate(stem[i + 1]))
			{
				i++;
			}
			else if (c < ' ' || char.IsSurrogate(c) || "<>:\"/\\|?*".IndexOf(c) >= 0)
			{
				return $"has {Show(c.ToString())}";
			}
		}

		if (stem[stem.Length - 1] is '.' or ' ')
		{
			return "ends with a dot or a space";
		}

		var device = stem.Split('.')[0].TrimEnd(' ');

		return Devices.Contains(device) ? $"is the device {device}" : null;
	}

	/// <summary>A random sheet name: pieces, nothing now and then, and now and then a long one.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The name.</returns>
	private static string RandomName(Random random)
	{
		var name = new StringBuilder();
		for (var count = random.Next(10) == 0 ? 0 : random.Next(1, 7); count > 0; count--)
		{
			name.Append(NamePieces[random.Next(NamePieces.Length)]);
		}

		if (random.Next(12) == 0)
		{
			name.Append(new string('L', random.Next(30, 300)));
		}

		return name.ToString();
	}

	/// <summary>
	/// A sheet's name as the workbook part holds it, so that it reads back as it is: XML's characters escaped, and control
	/// characters, lone surrogates, U+FFFE, U+FFFF and an <c>_</c> that starts an escape written as <c>_xHHHH_</c>, as
	/// Excel writes them.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The XML.</returns>
	private static string NameXml(string name)
	{
		var xml = new StringBuilder();
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
			{
				xml.Append(c).Append(name[++i]);
			}
			else if (c == '_' && System.Text.RegularExpressions.Regex.IsMatch(name.Substring(i), "^_[xX][0-9A-Fa-f]{4}_"))
			{
				xml.Append("_x005F_");
			}
			else if (c < ' ' || char.IsSurrogate(c) || c is '\uFFFE' or '\uFFFF')
			{
				xml.Append("_x").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
			}
			else
			{
				xml.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;", _ => c.ToString() });
			}
		}

		return xml.ToString();
	}

	/// <summary>A worksheet of its own: a row of text and its number, so that each sheet's file can be told from the others'.</summary>
	/// <param name="number">The sheet's number.</param>
	/// <returns>The worksheet part.</returns>
	private static string Content(int number) =>
		Sheet.Sheets.Worksheet($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>sheet</t></is></c><c r=\"B1\"><v>{number}</v></c></row>");
}
