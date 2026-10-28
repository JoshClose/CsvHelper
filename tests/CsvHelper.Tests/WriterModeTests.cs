using System.Text;

namespace CsvHelper.Tests;

/// <summary>The Escape, NoEscape and FixedWidth modes of the writer, and round trips through the reader.</summary>
public class WriterModeTests
{
	private static string Write(Action<CsvWriter> write, CsvWriterOptions options)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true)) write(writer);

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private static readonly CsvWriterOptions Escaped = new() { Mode = CsvMode.Escape };
	private static readonly CsvWriterOptions Plain = new() { Mode = CsvMode.NoEscape };

	private static CsvWriterOptions Fixed(params int[] widths) => new() { Mode = CsvMode.FixedWidth, FieldWidths = widths };

	// ----- Escape -----

	[Fact]
	public void EscapesSpecialCharacters() =>
		Assert.Equal("a\\,b,c\\\\d,e\\\r\\\nf,\"q\",\r\n1,2,3,4,\\,\r\n",
			Write(w =>
			{
				w.WriteRecord("a,b", "c\\d", "e\r\nf", "\"q\"", "");
				w.WriteRecord("1", "2", "3", "4", ",");
			}, Escaped));

	[Fact]
	public void EscapesEveryWayOfWritingAField()
	{
		var csv = Write(w =>
		{
			w.WriteField("x,y");                       // short ASCII
			w.WriteField(new string('é', 40) + ",");    // long, not ASCII
			w.WriteField("a\\b"u8);                    // UTF-8
			w.WriteField((object)"o\n");               // object
			w.WriteField(1.5);
			w.EndRecord();
		}, Escaped with { Separator = ';' });
		Assert.Equal("x,y;" + new string('é', 40) + ",;a\\\\b;o\\\n;1.5\r\n", csv);
	}

	[Fact]
	public void EscapesNumbersWhenTheEscapeCanAppearInThem() =>
		Assert.Equal("1..5,-2\r\n", Write(w => { w.WriteField(1.5); w.WriteField(-2); w.EndRecord(); }, Escaped with { Escape = '.' }));

	[Fact]
	public void LoneEmptyFieldIsAnEmptyLine()
	{
		Assert.Equal("\r\nx\r\n", Write(w => { w.WriteRecord(""); w.WriteRecord("x"); }, Escaped));
		Assert.Equal("\r\nx\r\n", Write(w => { w.WriteRecord(""); w.WriteRecord("x"); }, Plain));
	}

	[Fact]
	public void QuotingOptionDoesNotApplyToOtherModes() =>
		Assert.Equal("a,\\,\r\n", Write(w => w.WriteRecord("a", ","), Escaped with { Quoting = CsvQuoting.Always }));

	// ----- NoEscape -----

	[Fact]
	public void NoEscapeWritesAsIs() =>
		Assert.Equal("a,b,\"c\r\n,\\\r\n", Write(w => { w.WriteRecord("a,b", "\"c"); w.WriteRecord("", "\\"); }, Plain with { ValidateFieldCount = false }));

	[Fact]
	public void NoEscapeAllowsTheQuoteAsSeparator() =>
		Assert.Equal("a\"b\r\n", Write(w => w.WriteRecord("a", "b"), Plain with { Separator = '"' }));

	// ----- FixedWidth -----

	[Fact]
	public void PadsToColumnWidths() =>
		Assert.Equal("Id Name \r\n1  Bob  \r\n42 é😀  \r\n",
			Write(w =>
			{
				w.WriteHeader("Id", "Name");
				w.WriteRecord("1", "Bob");
				w.WriteField(42);
				w.WriteField("é😀");
				w.EndRecord();
			}, Fixed(3, 5)));

	[Fact]
	public void FixedWidthWritesSeparatorsAndQuotesAsText() =>
		Assert.Equal("a,\"b \r\n", Write(w => w.WriteRecord("a,", "\"b"), Fixed(2, 3)));

	[Fact]
	public void FixedWidthRejectsWhatDoesNotFit()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, Fixed(2, 2), leaveOpen: true))
		{
			writer.WriteRecord("a", "b");
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("a", "bcd"));
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("😀", "b😀"));
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("a", "b", "c"));
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("a"));
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("a\n", "b"));
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("a", "\r"));
			Assert.Equal(0, writer.FieldCount);
			writer.WriteRecord("c", "😀");
		}

		// The records that failed were discarded.
		Assert.Equal("a b \r\nc 😀\r\n", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Fact]
	public void PadsLongValues()
	{
		var ascii = new string('a', 35);
		var accented = new string('é', 44) + "😀";
		Assert.Equal(ascii + "     " + accented + "    \r\n", Write(w => w.WriteRecord(ascii, accented), Fixed(40, 50)));
		Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteRecord(ascii + "\n", "b"), Fixed(40, 50)));
		Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteRecord(ascii, accented + "x", "c"), Fixed(40, 46, 1)));
	}

	[Fact]
	public void FixedWidthShortRecordsWithoutValidation() =>
		Assert.Equal("ab \r\nabcde\r\n", Write(w => { w.WriteRecord("ab"); w.WriteRecord("abc", "de"); }, Fixed(3, 2) with { ValidateFieldCount = false }));

	[Fact]
	public void WriterOptionsAreValidated()
	{
		static void Create(CsvWriterOptions options) => CsvWriter.Create(new MemoryStream(), options).Dispose();
		Assert.Throws<ArgumentException>(() => Create(Escaped with { Escape = ',' }));
		Assert.Throws<ArgumentException>(() => Create(Escaped with { Escape = ' ' }));
		Assert.Throws<ArgumentException>(() => Create(Escaped with { Escape = 'é' }));
		Assert.Throws<ArgumentException>(() => Create(new CsvWriterOptions { Separator = '"' }));
		Assert.Throws<ArgumentException>(() => Create(new CsvWriterOptions { Mode = CsvMode.FixedWidth }));
		Assert.Throws<ArgumentException>(() => Create(Fixed(1, -1)));
		Assert.Throws<ArgumentOutOfRangeException>(() => Create(new CsvWriterOptions { Mode = (CsvMode)7 }));
		// Separator and escape do not matter in FixedWidth mode; the escape does not matter in Rfc4180 mode.
		Create(Fixed(1) with { Separator = '\n', Escape = ' ' });
		Create(new CsvWriterOptions { Escape = ',' });
	}

	// ----- Round trips -----

	public static TheoryData<int> Seeds()
	{
		var data = new TheoryData<int>();
		for (var i = 1; i <= 12; i++) data.Add(i * 31337);

		return data;
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task RoundTrips(int seed)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < 60; iter++)
		{
			var mode = (CsvMode)rng.Next(4);
			var separator = ",;\t|"[rng.Next(4)];
			var columns = rng.Next(1, 8);
			var widths = Enumerable.Range(0, columns).Select(_ => rng.Next(1, 12)).ToArray();
			var records = new List<string[]>();
			for (var r = rng.Next(0, 50); r > 0; r--)
				records.Add(Enumerable.Range(0, columns).Select(c => Value(rng, mode, separator, widths[c])).ToArray());
			var writeOptions = new CsvWriterOptions
			{
				Mode = mode,
				Separator = separator,
				Escape = "\\^"[rng.Next(2)],
				FieldWidths = widths,
				BufferSize = 256 << rng.Next(4),
			};
			var readOptions = new CsvReaderOptions
			{
				Mode = mode,
				Separator = separator,
				Escape = writeOptions.Escape,
				FieldWidths = widths,
				HasHeader = false,
				// A record of one empty field is an empty line in Escape and NoEscape modes.
				SkipEmptyLines = false,
			};
			var ctx = $"seed {seed}, iter {iter}, {mode}, separator {TestUtil.Escape(separator.ToString())}, widths [{string.Join(",", widths)}]";

			var stream = new MemoryStream();
			using (var writer = CsvWriter.Create(stream, writeOptions, leaveOpen: true))
			{
				foreach (var record in records) writer.WriteRecord(record);
			}

			var csv = stream.ToArray();
			using (var reader = CsvReader.Create(csv, readOptions))
			{
				TestUtil.AssertSame(records, TestUtil.ReadAll(reader), $"stream, {ctx}, csv {TestUtil.Escape(Encoding.UTF8.GetString(csv))}");
			}

			var text = new StringWriter();
			using (var writer = CsvWriter.Create(text, writeOptions))
			{
				foreach (var record in records) writer.WriteRecord(record);
			}

			Assert.Equal(Encoding.UTF8.GetString(csv), text.ToString());

			var asyncStream = new MemoryStream();
			await using (var writer = CsvWriter.Create(asyncStream, writeOptions, leaveOpen: true))
			{
				foreach (var record in records) await writer.WriteRecordAsync(record);
			}

			Assert.Equal(csv, asyncStream.ToArray());
		}
	}

	/// <summary>A value that the mode can write and read back unchanged.</summary>
	private static string Value(Random rng, CsvMode mode, char separator, int width)
	{
		var length = mode == CsvMode.FixedWidth ? rng.Next(0, width + 1) : rng.Next(0, 3) switch { 0 => 0, 1 => rng.Next(1, 8), _ => rng.Next(8, 80) };
		const string Alphabet = "abcxyz0189 ,;|\t\"'\\^#\r\né€😀";
		var sb = new StringBuilder();
		while (sb.Length < length)
		{
			var k = rng.Next(Alphabet.Length - 1);
			var s = k == Alphabet.Length - 2 ? "😀" : Alphabet[k].ToString();
			if (mode == CsvMode.NoEscape && (s[0] == separator || s[0] == '\r' || s[0] == '\n')) continue;
			if (mode == CsvMode.FixedWidth && (s[0] == '\r' || s[0] == '\n' || sb.Length + s.Length > length)) continue;
			sb.Append(s);
		}

		// The reader trims fixed-width values.
		return mode == CsvMode.FixedWidth ? sb.ToString().Trim(' ', '\t') : sb.ToString();
	}
}
