using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// Compares the reader with the reference parser on random input, across vector widths (512, 256, 128 and
/// scalar only), input sources and buffer sizes.
/// </summary>
public class FuzzTests
{
	private static readonly int[] Widths = TestUtil.Widths;

	public static TheoryData<int> Seeds()
	{
		var data = new TheoryData<int>();
		for (var i = 1; i <= 24; i++) data.Add(i * 7919);

		return data;
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public void RandomCharacters(int seed) => Run(seed, iterations: 150, structured: false);

	[Theory]
	[MemberData(nameof(Seeds))]
	public void StructuredRecords(int seed) => Run(seed, iterations: 150, structured: true);

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task AsyncChunkedStreams(int seed)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < 60; iter++)
		{
			var o = RandomOptions(rng);
			var text = rng.Next(2) == 0 ? RandomText(rng, o) : StructuredText(rng, o);
			var expected = ReferenceCsv.Parse(text, o, out _);
			var utf8 = Encoding.UTF8.GetBytes(text);
			var bufferSize = 256 << rng.Next(4);
			await using (var reader = CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 300), yieldAsync: true),
				o.ToReaderOptions(bufferSize: bufferSize)))
			{
				var actual = await TestUtil.ReadAllAsync(reader);
				TestUtil.AssertSame(expected, actual, $"async chunked, seed {seed}, iter {iter}, {o}, buffer {bufferSize}, text {TestUtil.Escape(text)}");
			}

			await using (var reader = CsvReader.Create(new ChunkedTextReader(text, rng.Next(), rng.Next(1, 300)), o.ToReaderOptions(bufferSize: bufferSize)))
			{
				var actual = await TestUtil.ReadAllAsync(reader);
				TestUtil.AssertSame(expected, actual, $"async text reader, seed {seed}, iter {iter}, {o}, buffer {bufferSize}, text {TestUtil.Escape(text)}");
			}
		}
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task FixedWidthLines(int seed)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < 80; iter++)
		{
			var widths = new int[rng.Next(1, 8)];
			for (var k = 0; k < widths.Length; k++) widths[k] = rng.Next(1, 13);
			bool? trim = rng.Next(3) switch { 0 => null, 1 => true, _ => false };
			var options = new CsvReaderOptions
			{
				Mode = CsvMode.FixedWidth,
				FieldWidths = widths,
				TrimWhitespace = trim,
				SkipEmptyLines = rng.Next(4) != 0,
				CommentPrefix = rng.Next(4) == 0 ? '#' : null,
				PoolStrings = rng.Next(2) == 0,
				HasHeader = false,
			};
			var text = FixedWidthText(rng, widths);
			var expected = ReferenceFixedWidth.Parse(text, widths, trim ?? true, options.SkipEmptyLines, options.CommentPrefix);
			var utf8 = Encoding.UTF8.GetBytes(text);
			var ctx = $"seed {seed}, iter {iter}, widths [{string.Join(",", widths)}], trim {trim}, skip {options.SkipEmptyLines}, " +
					  $"comment {options.CommentPrefix}, text {TestUtil.Escape(text)}";
			foreach (var width in Widths)
			{
				using var reader = CsvReader.Create(utf8, options);
				reader.vectorWidth = width;
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"memory, width {width}, {ctx}");
			}

			{
				var bufferSize = 256 << rng.Next(4);
				using var reader = CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 400)), options with { BufferSize = bufferSize });
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"chunked stream, buffer {bufferSize}, {ctx}");
			}

			{
				using var reader = CsvReader.FromText(text, options with { BufferSize = 256 << rng.Next(4) });
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"string, {ctx}");
			}

			{
				using var reader = CsvReader.Create(new ChunkedTextReader(text, rng.Next(), rng.Next(1, 500)), options with { BufferSize = 256 << rng.Next(4) });
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"text reader, {ctx}");
			}

			await using (var reader = CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 300), yieldAsync: true),
				options with { BufferSize = 256 << rng.Next(4) }))
			{
				TestUtil.AssertSame(expected, await TestUtil.ReadAllAsync(reader), $"async chunked, {ctx}");
			}
		}
	}

	/// <summary>Lines around the width of a record: short, exact and long, with multibyte text, emoji and comments.</summary>
	internal static string FixedWidthText(Random rng, int[] widths)
	{
		var total = widths.Sum();
		const string Alphabet = "abcxyz0123456789  \t#é€😀";
		var sb = new StringBuilder();
		string[] newlines = ["\n", "\r\n", "\r"];
		var lines = rng.Next(0, 40);
		for (var l = 0; l < lines; l++)
		{
			var length = rng.Next(6) switch
			{
				0 => rng.Next(0, total + 1),
				1 => rng.Next(total, total + 20),
				2 => rng.Next(100, 300),
				3 => 0,
				_ => total,
			};
			for (var i = 0; i < length; i++)
			{
				var k = rng.Next(Alphabet.Length - 1);
				// The emoji is a surrogate pair: two chars.
				sb.Append(k == Alphabet.Length - 2 ? "😀" : Alphabet[k].ToString());
			}

			if (l < lines - 1 || rng.Next(2) == 0) sb.Append(newlines[rng.Next(3)]);
		}

		return sb.ToString();
	}

	private static void Run(int seed, int iterations, bool structured)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < iterations; iter++)
		{
			var o = RandomOptions(rng);
			var text = structured ? StructuredText(rng, o) : RandomText(rng, o);
			var expected = ReferenceCsv.Parse(text, o, out var malformed);
			var utf8 = Encoding.UTF8.GetBytes(text);
			var ctx = $"seed {seed}, iter {iter}, {o}, text {TestUtil.Escape(text)}";

			// Detection without skipping: every malformed record is reported exactly once.
			if (!o.SkipMalformed)
			{
				var reports = new List<CsvMalformedRecord>();
				var detect = o.ToReaderOptions(bufferSize: 256 << rng.Next(4)) with
				{
					OnMalformedRecord = m => { if (m.Reason != CsvMalformedReason.FieldCountMismatch) reports.Add(m); },
				};
				foreach (var width in Widths)
				{
					reports.Clear();
					using var reader = CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 400)), detect);
					reader.vectorWidth = width;
					TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"detecting, width {width}, {ctx}");
					if (malformed != reports.Count)
					{
						ReferenceCsv.Parse(text, o, out _, out var badRecords);
						var k = 0;
						while (k < Math.Min(reports.Count, badRecords.Count) && reports[k].RawRecord == badRecords[k]) k++;
						Assert.Fail($"expected {malformed} malformed records, got {reports.Count}; width {width}; first difference at #{k}\n" +
							$"Reader: {(k < reports.Count ? reports[k].Reason + " " + TestUtil.Escape(reports[k].RawRecord) : "-")}\n" +
							$"Reference: {(k < badRecords.Count ? TestUtil.Escape(badRecords[k]) : "-")}\n" +
							$"Previous: {(k > 0 ? TestUtil.Escape(badRecords[k - 1]) : "-")}\n{ctx.Substring(0, Math.Min(ctx.Length, 300))}");
					}
				}
			}

			foreach (var width in Widths)
			{
				using var reader = CsvReader.Create(utf8, o.ToReaderOptions());
				reader.vectorWidth = width;
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"memory, width {width}, {ctx}");
			}

			// In-memory data inside a larger array, with junk around it.
			{
				var padded = new byte[utf8.Length + 200];
				padded.AsSpan().Fill((byte)'\r');
				utf8.CopyTo(padded, 100);
				using var reader = CsvReader.Create(new ReadOnlyMemory<byte>(padded, 100, utf8.Length), o.ToReaderOptions());
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"memory segment, {ctx}");
			}

			{
				var bufferSize = 256 << rng.Next(4);
				using var reader = CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 400)), o.ToReaderOptions(bufferSize: bufferSize));
				reader.vectorWidth = Widths[rng.Next(Widths.Length)];
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"chunked stream, buffer {bufferSize}, {ctx}");
			}

			{
				using var reader = CsvReader.FromText(text, o.ToReaderOptions(bufferSize: 256 << rng.Next(4)));
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"string, {ctx}");
			}

			// ByteOffset and LineNumber, from a random source (an array slice, which does not start the array, too).
			{
				var tracking = o.ToReaderOptions(bufferSize: 256 << rng.Next(4)) with { TrackLineNumbers = true };
				var padded = new byte[utf8.Length + 100];
				utf8.CopyTo(padded, 100);
				var source = rng.Next(3);
				using var reader = source switch
				{
					0 => CsvReader.Create(new ReadOnlyMemory<byte>(padded, 100, utf8.Length), tracking),
					1 => CsvReader.Create(new ChunkedTextReader(text, rng.Next(), rng.Next(1, 500)), tracking),
					_ => CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 400)), tracking),
				};
				reader.vectorWidth = Widths[rng.Next(Widths.Length)];
				TestUtil.AssertSame(expected, TestUtil.ReadCheckingPositions(reader, utf8, $"source {source}, {ctx}"), $"positions, source {source}, {ctx}");
			}

			{
				using var reader = CsvReader.Create(new ChunkedTextReader(text, rng.Next(), rng.Next(1, 500)), o.ToReaderOptions(bufferSize: 256 << rng.Next(4)));
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"text reader, {ctx}");
			}
		}
	}

	internal static ReferenceOptions RandomOptions(Random rng)
	{
		var sep = ",;\t|"[rng.Next(4)];
		// Rfc4180 with " or ', Escape with \ or ^, or NoEscape.
		var q = rng.Next(14);
		char? quote = q < 7 ? '"' : q < 9 ? '\'' : null;
		char? escape = q is >= 9 and < 13 ? "\\^"[rng.Next(2)] : null;

		return new ReferenceOptions
		{
			Separator = sep,
			Quote = quote,
			Escape = escape,
			Trim = rng.Next(4) == 0,
			// Custom whitespace: an underscore, which the random text contains, with or without space and tab.
			Whitespace = rng.Next(5) == 0 ? (rng.Next(2) == 0 ? "_" : " _\t") : " \t",
			TrimInsideQuotes = quote is not null && rng.Next(5) == 0,
			Comment = rng.Next(5) == 0 ? '#' : null,
			SkipEmptyLines = rng.Next(10) < 7,
			LineBreaksInQuotes = rng.Next(5) != 0,
			SkipMalformed = rng.Next(5) == 0,
			SkipFieldCountMismatch = rng.Next(7) == 0,
			PoolStrings = rng.Next(2) == 0,
		};
	}

	internal static string RandomText(Random rng, ReferenceOptions o)
	{
		// \u0001 stands for an emoji (a surrogate pair), so no lone surrogates are generated.
		var alphabet = "ab xé\t_,;|\"'\\\r\n#\u0001" + o.Separator + o.Separator + (o.Quote?.ToString() ?? "") +
					   (o.Escape is char e ? new string(e, 3) : "") + "\n";
		var length = rng.Next(4) switch
		{
			0 => rng.Next(0, 70),
			1 => rng.Next(60, 200),
			2 => rng.Next(200, 1500),
			_ => rng.Next(1500, 5000),
		};
		var sb = new StringBuilder(length);
		// Vary the density of special characters between runs.
		var plainWeight = rng.Next(0, 20);
		for (var i = 0; i < length; i++)
		{
			if (rng.Next(plainWeight + 1) > 0) sb.Append((char)('a' + rng.Next(26)));
			else
			{
				var ch = alphabet[rng.Next(alphabet.Length)];
				if (ch == '\u0001') sb.Append("😀");
				else sb.Append(ch);
			}
		}

		return sb.ToString();
	}

	internal static string StructuredText(Random rng, ReferenceOptions o)
	{
		var sb = new StringBuilder();
		var records = rng.Next(0, 60);
		string[] newlines = ["\n", "\r\n", "\r"];
		var nl = newlines[rng.Next(3)];
		var columns = rng.Next(1, 12);
		var quote = o.Quote ?? '"';
		for (var r = 0; r < records; r++)
		{
			if (rng.Next(15) == 0) nl = newlines[rng.Next(3)];
			if (o.Comment is char c && rng.Next(10) == 0)
			{
				sb.Append(c).Append("comment, with \"quotes ").Append(o.Separator).Append(quote).Append(Word(rng, 0, 3));
				if (rng.Next(2) == 0) sb.Append(o.Separator).Append(quote).Append(Word(rng, 0, 80));
				sb.Append(nl);
				continue;
			}

			if (rng.Next(20) == 0)
			{
				sb.Append(nl);
				continue;
			}

			var n = rng.Next(8) == 0 ? rng.Next(1, 20) : columns;
			for (var f = 0; f < n; f++)
			{
				if (f > 0) sb.Append(o.Separator);
				if (o.Escape is char e && rng.Next(3) == 0)
				{
					EscapedField(rng, sb, o, e, nl);
					continue;
				}

				switch (rng.Next(14))
				{
					case 0: break;
					case 1:
						sb.Append(quote).Append(quote);
						break;
					case 2:
					case 3:
						sb.Append(quote).Append(Word(rng, 0, 12)).Append(o.Separator).Append(quote, 2).Append(Word(rng, 0, 5)).Append(nl).Append(Word(rng, 0, 4)).Append(quote);
						break;
					case 4:
						sb.Append(Word(rng, 1, 5)).Append(quote).Append(Word(rng, 0, 5));   // stray quote
						break;
					case 5:
						sb.Append(quote).Append(Word(rng, 0, 5)).Append(quote).Append(Word(rng, 1, 3));   // text after quote
						break;
					case 6:
						sb.Append(' ', rng.Next(3)).Append(quote).Append(Word(rng, 0, 6)).Append(o.Separator).Append(quote).Append(' ', rng.Next(3));
						break;
					case 7:
						sb.Append(Word(rng, 60, 300));   // crosses block boundaries
						break;
					case 8:
						sb.Append(quote).Append(Word(rng, 50, 200)).Append(nl).Append(quote, 2).Append(Word(rng, 50, 200)).Append(quote);
						break;
					case 9:
						sb.Append(' ').Append(Word(rng, 1, 6)).Append('\t');
						break;
					default:
						sb.Append(Word(rng, 1, 10));
						break;
				}
			}

			if (r < records - 1 || rng.Next(2) == 0) sb.Append(nl);
		}

		if (rng.Next(10) == 0) sb.Append(quote).Append(Word(rng, 0, 20));   // unterminated at the end
		if (o.Escape is char escape && rng.Next(10) == 0) sb.Append(escape);   // trailing escape

		return sb.ToString();
	}

	/// <summary>A field with escape characters: escaped separators, line breaks, escapes and whitespace, runs of escapes.</summary>
	private static void EscapedField(Random rng, StringBuilder sb, ReferenceOptions o, char e, string nl)
	{
		var parts = rng.Next(1, 5);
		for (var p = 0; p < parts; p++)
		{
			switch (rng.Next(9))
			{
				case 0: sb.Append(e).Append(o.Separator); break;
				case 1: sb.Append(e).Append(nl); break;
				case 2: sb.Append(e).Append("\r\n"); break;
				case 3: sb.Append(e, rng.Next(1, 6)); break;                          // runs of escapes, odd or even
				case 4: sb.Append(e).Append(' ').Append(' ', rng.Next(2)); break;     // escaped whitespace (kept by trimming)
				case 5: sb.Append(e).Append('"').Append(e).Append('#'); break;
				case 6: sb.Append(Word(rng, 50, 120)); break;                          // pushes escapes across block edges
				default: sb.Append(Word(rng, 0, 6)); break;
			}
		}
	}

	private static string Word(Random rng, int min, int max)
	{
		var n = rng.Next(min, max + 1);
		var chars = new char[n];
		for (var i = 0; i < n; i++)
		{
			var k = rng.Next(40);
			chars[i] = k < 26 ? (char)('a' + k) : k < 36 ? (char)('0' + k - 26) : k == 36 ? 'é' : k == 37 ? '€' : ' ';
		}

		var word = new string(chars);

		// Sometimes add a surrogate pair (emoji) so pairs get split across chunks and buffers.
		return rng.Next(8) == 0 ? word + "😀" : word;
	}
}
