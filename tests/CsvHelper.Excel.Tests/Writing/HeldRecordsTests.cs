using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper.Excel.Tests.Security;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record with a cell of each kind auto-fit measures, for <see cref="HeldRecordsTests"/>.</summary>
[CsvRecord]
public partial class HeldRecordsRow
{
	/// <summary>ASCII text.</summary>
	public string Name { get; set; } = "";

	/// <summary>Text that isn't ASCII alone.</summary>
	public string Other { get; set; } = "";

	/// <summary>Text in a column that wraps.</summary>
	public string Note { get; set; } = "";

	/// <summary>An integer.</summary>
	public long Count { get; set; }

	/// <summary>A floating-point number.</summary>
	public double Ratio { get; set; }

	/// <summary>A decimal, in its column's format.</summary>
	public decimal Amount { get; set; }

	/// <summary>A date and time.</summary>
	public DateTime When { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A boolean.</summary>
	public bool Done { get; set; }
}

/// <summary>
/// Auto-fit's held records (Phase 9's decision 25): the cells' array is rented from the pool, grown by renting and
/// returned once, so that a write allocates the same for 100 rows and 10,000, and what it does without auto-fit, and a
/// sheet of 1,000 columns stays within a budget; the cells are 24 bytes; and the measures that made strings make none
/// and measure the same: numbers parsed as <c>double.Parse</c> reads them, durations' text, and text decoded to be
/// measured. It runs alone, in <see cref="TimedCollection"/>, since other tests' collections trim the array pool the
/// cells are rented from.
/// </summary>
/// <remarks>
/// The writes' allocations are measured on .NET 10 alone. On .NET 8, a short write's typed fields and measured numbers
/// run in the JIT's first tier, whose generic code boxes (130 to 330 bytes a field), until their methods are compiled
/// again optimized, as they are within a long write; with tiering off, .NET 8's writes allocate the same for any number
/// of rows too.
/// </remarks>
/// <param name="output">Shows what the tests measured.</param>
[Collection(TimedCollection.Name)]
public class HeldRecordsTests(ITestOutputHelper output)
{
	/// <summary>
	/// The most the 10,000 rows' write may allocate beyond the 100 rows': nothing grows with the rows, so this is room
	/// for the runtime's own allocations. They allocated 14,720 and 14,808 bytes on .NET 10 (2026-11-22); and 150,320
	/// and 150,408 bytes before the held records were pooled, since auto-fit holds 100 records whatever the rows.
	/// </summary>
	private const long RowsSlack = 1024;

	/// <summary>
	/// The most auto-fit may allocate beyond the same write without it (<see cref="ExcelSheetOptions.AutoFit"/> 0): 72
	/// bytes on .NET 10 (2026-11-22). Before the held records were pooled, it allocated 133,368 bytes more: the cells'
	/// array as it doubled, and strings of the numbers, durations and text that isn't ASCII that it measured.
	/// </summary>
	private const long AutoFitBudget = 2048;

	/// <summary>The wide sheet's columns.</summary>
	private const int WideColumns = 1_000;

	/// <summary>The wide sheet's records, past the 100 auto-fit holds.</summary>
	private const int WideRows = 200;

	/// <summary>
	/// The most the wide sheet's write may allocate: 52,008 bytes on .NET 10 (2026-11-22), of which 25,096 are the
	/// widths' arrays of 1,000 columns, with a margin of 13,528 bytes (26%) for the runtime's own allocations. Before
	/// the held records were pooled, it allocated 8,438,856 bytes, most of them the doublings of an array of 131,072
	/// cells.
	/// </summary>
	private const long WideBudget = 64 * 1024;

	/// <summary>The held rows' sheet: its notes wrap, its amounts are in a format, and its header has filter buttons.</summary>
	private static readonly ExcelSheetOptions Sheet = new()
	{
		AutoFilter = true,
		Columns = new Dictionary<string, ExcelColumnOptions>
		{
			["Note"] = new() { Wrap = true },
			["Amount"] = new() { Format = "#,##0.00" },
		},
	};

	/// <summary>The forms of .NET's numbers' texts that reach a number cell, and halfway cases past 19 digits.</summary>
	private static readonly string[] Forms =
	[
		"0", "-0", "0.0", "-0.0", "1", "-1", "0.5", "1E-05", "1.5E+20", "1E+15", "1E+16", "1E+308", "-1E+308",
		"1.7976931348623157E+308", "1.7976931348623157E+307", "2.2250738585072014E-308", "2.225073858507201E-308",
		"4.9406564584124654E-324", "5E-324", "-5E-324", "1E-320", "9.8813129168249309E-324", "1E-300",
		"123456789012345", "1234567890123456", "12345678901234567", "9007199254740992", "9007199254740993",
		"9007199254740995", "9223372036854775807", "-9223372036854775808", "18446744073709551615",
		"79228162514264337593543950335", "-79228162514264337593543950335", "7.9228162514264337593543950335",
		"0.3333333333333333333333333333", "0.1428571428571428571428571429", "1.0000000000000000000000000001",
		"0.0000000000000000000000000001", "12345678901234567890.123456789", "0.30000000000000004", "0.1", "0.2", "0.3",
		"2.00000000000000011102230246251565404236316680908203125",
		"2.00000000000000011102230246251565404236316680908203124",
		"2.00000000000000011102230246251565404236316680908203126", "9007199254740993.00000000000000000001",
		"9007199254740992.99999999999999999999", "1.00000000000000011102230246251565404236316680908203125",
	];

	[Fact]
	public void KeepsEachCellIn24Bytes()
	{
		// Cell is private to AutoFit, so InternalsVisibleTo doesn't reach it: Unsafe.SizeOf<Cell>() through reflection.
		var cell = typeof(AutoFit).GetNestedType("Cell", BindingFlags.NonPublic)!;
		var size = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(cell).Invoke(null, null)!;

		Assert.Equal(24, size);
	}

	[Fact]
	public void ReturnsTheCellsOnce()
	{
		// Disposed twice, the cells' array of 128 cells goes back to the pool once: two auto-fits that each grow to 128
		// cells at the same time then get two arrays. An array returned twice would be rented by both, and each would
		// measure the other's cells.
		var first = new AutoFit();
		for (var i = 0; i < 100; i++)
		{
			first.Add(0, 1, 1, typed: false);
		}

		Assert.Equal([ColumnWidths.Fit(1)], first.Widths(fit: true, ColumnWidths.FromCharacters(60))!);
		first.Dispose();
		first.Dispose();
		Assert.Null(first.Widths(fit: true, ColumnWidths.FromCharacters(60)));

		var narrow = new AutoFit();
		var wide = new AutoFit();
		for (var i = 0; i < 100; i++)
		{
			narrow.Add(0, 1, 10, typed: false);
			wide.Add(0, 1, 20, typed: false);
		}

		Assert.Equal([ColumnWidths.Fit(10)], narrow.Widths(fit: true, ColumnWidths.FromCharacters(60))!);
		Assert.Equal([ColumnWidths.Fit(20)], wide.Widths(fit: true, ColumnWidths.FromCharacters(60))!);
		narrow.Dispose();
		wide.Dispose();
	}

	[Fact]
	public void ParsesNumbersAsDoubleParseDoes()
	{
		// The writer's texts of random doubles (their bits at random, and typical values), of longs and of decimals,
		// the forms .NET gives (exponents, -0, long integers, decimals of 28 and 29 digits, the largest and smallest
		// doubles, subnormals, halfway cases past 19 digits, which FastFloat leaves to double.Parse), and on .NET
		// random texts of the invariant grammar: each is an invariant number, and its double is double.Parse's of it.
		// The writer's doubles read back as the doubles written.
		var written = WrittenDoubles();
		var texts = written.Select(w => w.Text).Concat(Forms).ToList();
		var random = new Random(28);
		var bytes = new byte[8];
		for (var i = 0; i < 5_000; i++)
		{
			random.NextBytes(bytes);
			texts.Add(BitConverter.ToInt64(bytes, 0).ToString(CultureInfo.InvariantCulture));
			texts.Add(new decimal(random.Next(), random.Next(), random.Next(), random.Next(2) == 0, (byte)random.Next(29)).ToString(CultureInfo.InvariantCulture));
		}

#if NET
		texts.AddRange(GrammarTexts(new Random(29), 20_000));
#endif

		var differ = new List<string>();
		foreach (var (value, text) in written)
		{
			if (BitConverter.DoubleToInt64Bits(Value(text)) != BitConverter.DoubleToInt64Bits(value))
			{
				differ.Add($"{text}: {Value(text):R}, written from {value:R}");
			}
		}

		var framework = 0;
		foreach (var text in texts)
		{
			var value = Value(text);
			var reference = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
			if (BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(reference))
			{
				continue;
			}

#if NET
			differ.Add($"{text}: {value:R} against {reference:R}");
#else
			// .NET Framework's double.Parse isn't exactly rounded, and reads -0 as 0: where it differs by those, the
			// measure in General doesn't.
			framework++;
			if (FormatWidth.GeneralPixels(value) != FormatWidth.GeneralPixels(reference))
			{
				differ.Add($"{text}: {value:R} against {reference:R}, {FormatWidth.GeneralPixels(value)} pixels against {FormatWidth.GeneralPixels(reference)}");
			}
#endif
		}

		output.WriteLine($"{written.Count:N0} doubles written and {texts.Count:N0} texts; {framework:N0} that .NET Framework's double.Parse reads otherwise; {differ.Count} differ.");
		Assert.Empty(differ);
	}

	[Fact]
	public void FormatsDurationsAsTheirStringsDid()
	{
		// The text of [h]:mm:ss and .000, as the invariant culture's strings made it, for durations Excel shows and any
		// other: every length of hours, with and without milliseconds.
		var random = new Random(30);
		var durations = new List<long> { 0, 1, 999, 1000, 59_999, 60_000, 3_599_999, 3_600_000, 35_999_999, 36_000_000, Serial.PastLast - 1, long.MaxValue };
		for (var i = 0; i < 10_000; i++)
		{
			var milliseconds = (long)(random.NextDouble() * Math.Pow(10, random.Next(1, 19)));
			durations.Add(i % 2 == 0 ? milliseconds / 1000 * 1000 : milliseconds);
		}

		var text = new char[SheetSink.MaxDurationLength];
		foreach (var milliseconds in durations)
		{
			var length = SheetSink.DurationText(milliseconds, text);
			Assert.Equal(StringDurationText(milliseconds), new string(text, 0, length));
		}
	}

	[Fact]
	public void MeasuresDecodedTextAsItsStringsDid()
	{
		// Text that isn't ASCII, and text that wraps, by its widest line: on the stack up to 256 bytes, in a pooled
		// array past them, the same pixels as through a string.
		var random = new Random(31);
		var texts = new List<byte[]>();
		foreach (var length in new[] { 1, 2, 85, 128, 255, 256, 257, 300, 1000, 5000 })
		{
			texts.Add(Encoding.UTF8.GetBytes(new string((char)0x00E9, length)));
			texts.Add(Encoding.UTF8.GetBytes(new string('a', length)));
		}

		for (var i = 0; i < 2_000; i++)
		{
			texts.Add(Encoding.UTF8.GetBytes(RandomText(random, random.Next(1, i % 10 == 0 ? 2000 : 200))));
		}

		foreach (var utf8 in texts)
		{
			foreach (var (bold, wrap) in new[] { (false, false), (true, false), (false, true), (true, true) })
			{
				Assert.Equal(StringTextPixels(utf8, bold, wrap), AutoFit.TextPixels(utf8, bold, wrap));
			}
		}
	}

#if NET
	[OptimizedFact]
	public void MeasuresDecodedTextWithoutAllocating()
	{
		// Short text on the stack, long text in an array rented from the pool and returned.
		byte[][] texts = [Encoding.UTF8.GetBytes("caf" + (char)0x00E9 + "\nau lait"), Encoding.UTF8.GetBytes(new string((char)0x4E2D, 500))];
		var pixels = Measure(texts);

		// The smaller of two passes, as below.
		var allocated = Math.Min(AllocatedByMeasuring(), AllocatedByMeasuring());
		Assert.Equal(0, allocated);

		long AllocatedByMeasuring()
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			var again = Measure(texts);
			var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.Equal(pixels, again);

			return bytes;
		}
	}

	/// <summary>Measures each text, in regular and bold, on one line and wrapped.</summary>
	/// <param name="texts">The texts.</param>
	/// <returns>The pixels' sum.</returns>
	private static double Measure(byte[][] texts)
	{
		var sum = 0.0;
		foreach (var text in texts)
		{
			sum += AutoFit.TextPixels(text, false, false) + AutoFit.TextPixels(text, true, true);
		}

		return sum;
	}

#endif

#if NET10_0_OR_GREATER
	[OptimizedFact]
	public void AllocatesTheSameFor100And10000Rows()
	{
		// Auto-fit holds the header and 100 records either way: their cells' array, and the numbers', durations' and
		// text's measures, allocate nothing once the pool has the arrays, so the write allocates what it does without
		// auto-fit, and the same for 100 rows and 10,000.
		var few = Rows(100);
		var many = Rows(10_000);
		var noAutoFit = Sheet with { AutoFit = 0 };
		Allocated(few, Sheet);
		Allocated(many, Sheet);
		Allocated(few, noAutoFit);

		// Each write is measured twice, and the smaller kept: the runtime allocates once on the thread at a moment the
		// JIT's tiering sets, which can fall in one pass but not in both, where an allocation per row would be in both.
		var fewBytes = Math.Min(Allocated(few, Sheet), Allocated(few, Sheet));
		var manyBytes = Math.Min(Allocated(many, Sheet), Allocated(many, Sheet));
		var plainBytes = Math.Min(Allocated(few, noAutoFit), Allocated(few, noAutoFit));
		output.WriteLine($"100 rows allocated {fewBytes:N0} bytes, 10,000 rows {manyBytes:N0}, and 100 rows without auto-fit {plainBytes:N0}.");

		Assert.True(Math.Abs(manyBytes - fewBytes) <= RowsSlack, $"100 rows allocated {fewBytes:N0} bytes and 10,000 rows {manyBytes:N0}.");
		Assert.True(fewBytes - plainBytes <= AutoFitBudget, $"Auto-fit allocated {fewBytes - plainBytes:N0} bytes beyond the write without it ({plainBytes:N0}).");
	}

	[OptimizedFact]
	public void HoldsAThousandColumnsWithinItsBudget()
	{
		// 1,000 columns of short text, 101 records held: 101,000 cells measured, in arrays rented from the pool as they
		// grow to 131,072 cells.
		var (header, rows) = Wide();
		Allocated(header, rows);

		// The smaller of two passes, as above.
		var allocated = Math.Min(Allocated(header, rows), Allocated(header, rows));
		output.WriteLine($"{WideColumns:N0} columns by {WideRows:N0} records allocated {allocated:N0} bytes (at most {WideBudget:N0}).");

		Assert.True(allocated <= WideBudget, $"{allocated:N0} bytes allocated, against at most {WideBudget:N0}.");
	}

	/// <summary>Writes a workbook of a sheet of held rows to a stream that drops it, and measures what the write allocates.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="options">The sheet's options.</param>
	/// <returns>The bytes the write allocated on this thread.</returns>
	private static long Allocated(HeldRecordsRow[] rows, ExcelSheetOptions options)
	{
		var stream = new ForwardOnlyStream(Stream.Null);
		var before = GC.GetAllocatedBytesForCurrentThread();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			workbook.WriteSheet("Held", rows, options);
		}

		return GC.GetAllocatedBytesForCurrentThread() - before;
	}

	/// <summary>Writes a workbook of the wide sheet, field by field, to a stream that drops it, and measures what the write allocates.</summary>
	/// <param name="header">The header.</param>
	/// <param name="rows">The records.</param>
	/// <returns>The bytes the write allocated on this thread.</returns>
	private static long Allocated(string[] header, string[][] rows)
	{
		var stream = new ForwardOnlyStream(Stream.Null);
		var before = GC.GetAllocatedBytesForCurrentThread();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		using (var writer = workbook.AddSheet("Wide"))
		{
			writer.WriteHeader(header);
			foreach (var row in rows)
			{
				foreach (var field in row)
				{
					writer.WriteField(field);
				}

				writer.EndRecord();
			}
		}

		return GC.GetAllocatedBytesForCurrentThread() - before;
	}

	/// <summary>Held rows, made before they are written, each with a cell of every kind auto-fit measures.</summary>
	/// <param name="count">The rows.</param>
	/// <returns>The rows.</returns>
	private static HeldRecordsRow[] Rows(int count)
	{
		var rows = new HeldRecordsRow[count];
		for (var i = 0; i < count; i++)
		{
			rows[i] = new HeldRecordsRow
			{
				Name = "name " + i.ToString(CultureInfo.InvariantCulture),
				Other = "caf" + (char)0x00E9 + " " + new string((char)0x4E2D, i % 5) + i.ToString(CultureInfo.InvariantCulture),
				Note = "first line\nsecond line " + new string('x', i % 20),
				Count = i * 1_000_003L,
				Ratio = i / 7.0,
				Amount = i * 1.25m,
				When = new DateTime(2024, 1, 1).AddMinutes(i),
				Took = TimeSpan.FromMilliseconds(i * 1_234_567L + (i % 3 == 0 ? 0 : 89)),
				Done = i % 2 == 0,
			};
		}

		return rows;
	}

	/// <summary>
	/// The wide sheet, as <c>ExcelShapesWrite</c>'s: a header of <see cref="WideColumns"/> names, and
	/// <see cref="WideRows"/> records of words of 3 to 10 lowercase letters, drawn from 4,096 made with a fixed seed.
	/// </summary>
	/// <returns>The header and the records.</returns>
	private static (string[] Header, string[][] Rows) Wide()
	{
		var random = new Random(11);
		var words = new string[4096];
		for (var i = 0; i < words.Length; i++)
		{
			var letters = new char[random.Next(3, 11)];
			for (var j = 0; j < letters.Length; j++)
			{
				letters[j] = (char)('a' + random.Next(26));
			}

			words[i] = new string(letters);
		}

		var header = Enumerable.Range(1, WideColumns).Select(i => "Column" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
		var rows = new string[WideRows][];
		for (var r = 0; r < rows.Length; r++)
		{
			rows[r] = Enumerable.Range(0, WideColumns).Select(_ => words[random.Next(words.Length)]).ToArray();
		}

		return (header, rows);
	}

#endif

#if NET
	/// <summary>
	/// Random texts of the invariant grammar, <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?(E[+-]?[0-9]+)?</c>: up to 48 digits,
	/// exponents up to 350 either way, and runs of 0 and 9, which put a text near a rounding boundary.
	/// </summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="count">The texts.</param>
	/// <returns>The texts.</returns>
	private static IEnumerable<string> GrammarTexts(Random random, int count)
	{
		for (var i = 0; i < count; i++)
		{
			var text = new StringBuilder();
			if (random.Next(2) == 0)
			{
				text.Append('-');
			}

			var integer = random.Next(4) == 0 ? 0 : random.Next(1, 25);
			text.Append(integer == 0 ? "0" : Digits(random, integer, first: true));
			if (random.Next(2) == 0)
			{
				text.Append('.').Append(Digits(random, random.Next(1, 25), first: false));
			}

			if (random.Next(2) == 0)
			{
				text.Append('E').Append("+-"[random.Next(2)]).Append(random.Next(0, 351).ToString(CultureInfo.InvariantCulture));
			}

			yield return text.ToString();
		}
	}

	/// <summary>Random digits, mostly in runs of the same digit, 0 and 9 above all.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="count">The digits.</param>
	/// <param name="first">Whether they start a number, so that the first isn't 0.</param>
	/// <returns>The digits.</returns>
	private static string Digits(Random random, int count, bool first)
	{
		var digits = new char[count];
		var run = (char)('0' + random.Next(10));
		for (var i = 0; i < count; i++)
		{
			if (random.Next(4) == 0)
			{
				run = random.Next(3) switch { 0 => '0', 1 => '9', _ => (char)('0' + random.Next(10)) };
			}

			digits[i] = run;
		}

		if (first && digits[0] == '0')
		{
			digits[0] = (char)('1' + random.Next(9));
		}

		return new string(digits);
	}
#endif

	/// <summary>
	/// The writer's texts of random doubles, with the doubles: of any bits, NaN and the infinities aside, of typical
	/// values, and between 0 and 1.
	/// </summary>
	/// <returns>The doubles and their texts.</returns>
	private static List<(double Value, string Text)> WrittenDoubles()
	{
		var random = new Random(27);
		var bytes = new byte[8];
		var values = new List<double>();
		while (values.Count < 60_000)
		{
			random.NextBytes(bytes);
			var value = BitConverter.ToDouble(bytes, 0);
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				continue;
			}

			values.Add(value);
			values.Add(Math.Round(random.NextDouble() * Math.Pow(10, random.Next(-10, 20)), random.Next(0, 6)));
			values.Add(random.NextDouble());
		}

		var csv = new StringWriter(CultureInfo.InvariantCulture);
		using (var writer = CsvWriter.Create(csv, leaveOpen: true))
		{
			foreach (var value in values)
			{
				writer.WriteField(value);
				writer.EndRecord();
			}
		}

		var texts = csv.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		Assert.Equal(values.Count, texts.Length);

		return [.. values.Select((value, i) => (value, texts[i]))];
	}

	/// <summary>The double that the writer measures for a number's text: its invariant text's, as <see cref="DotNetText.NumberValue"/> reads it.</summary>
	/// <param name="text">The text, an invariant number.</param>
	/// <returns>The double.</returns>
	private static double Value(string text)
	{
		var invariant = new byte[DotNetText.MaxNumberLength];
		var length = DotNetText.ToInvariantNumber(Encoding.ASCII.GetBytes(text), null, invariant, out _);
		Assert.True(length > 0, $"{text} isn't an invariant number.");

		return DotNetText.NumberValue(invariant.AsSpan(0, length));
	}

	/// <summary>A duration's text as the writer made it with strings, before it made it on the stack.</summary>
	/// <param name="milliseconds">The duration's milliseconds, not negative.</param>
	/// <returns>The text.</returns>
	private static string StringDurationText(long milliseconds)
	{
		var text = (milliseconds / 3_600_000).ToString(CultureInfo.InvariantCulture) + ":" +
			(milliseconds / 60_000 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
			(milliseconds / 1000 % 60).ToString("00", CultureInfo.InvariantCulture);

		return milliseconds % 1000 == 0 ? text : text + "." + (milliseconds % 1000).ToString("000", CultureInfo.InvariantCulture);
	}

	/// <summary>A text's pixels as auto-fit measured it through a string, before it decoded it on the stack.</summary>
	/// <param name="utf8">The text.</param>
	/// <param name="bold">Whether it is in bold.</param>
	/// <param name="wrap">Whether it wraps, so that its widest line counts.</param>
	/// <returns>The pixels.</returns>
	private static double StringTextPixels(byte[] utf8, bool bold, bool wrap)
	{
		var text = Encoding.UTF8.GetString(utf8);
		if (!wrap)
		{
			return ColumnWidths.TextPixels(text.AsSpan(), bold);
		}

		var widest = 0.0;
		foreach (var line in text.Split('\n'))
		{
			widest = Math.Max(widest, ColumnWidths.TextPixels(line.AsSpan(), bold));
		}

		return widest;
	}

	/// <summary>A random text: ASCII, Latin-1 letters, CJK characters, surrogate pairs, line feeds and CRs.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="length">Its parts, each a character or a pair.</param>
	/// <returns>The text.</returns>
	private static string RandomText(Random random, int length)
	{
		var text = new StringBuilder();
		for (var i = 0; i < length; i++)
		{
			switch (random.Next(8))
			{
				case 0:
					text.Append((char)random.Next(0xC0, 0x100));
					break;
				case 1:
					text.Append((char)random.Next(0x4E00, 0xA000));
					break;
				case 2:
					text.Append(char.ConvertFromUtf32(random.Next(0x1F300, 0x1F650)));
					break;
				case 3:
					text.Append(random.Next(4) == 0 ? '\r' : '\n');
					break;
				default:
					text.Append((char)random.Next(' ', '~' + 1));
					break;
			}
		}

		return text.ToString();
	}
}
