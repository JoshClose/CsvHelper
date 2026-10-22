using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

public class ReaderTests
{
	private static readonly CsvReaderOptions NoHeader = new() { HasHeader = false };

	private static List<string[]> Read(string text, CsvReaderOptions? options = null) => TestUtil.ReadText(text, options ?? NoHeader);

	private static void Expect(string text, CsvReaderOptions? options, params string[][] expected)
	{
		var actual = Read(text, options);
		TestUtil.AssertSame(expected.ToList(), actual, $"input {TestUtil.Escape(text)}");
		// The reference parser must agree with the hand-written expectation too.
		var o = options ?? NoHeader;
		var reference = ReferenceCsv.Parse(text, new ReferenceOptions
		{
			Separator = o.Separator ?? ',',
			Quote = o.Mode == CsvMode.Rfc4180 ? o.Quote : null,
			Escape = o.Mode == CsvMode.Escape ? o.Escape : null,
			Trim = o.TrimWhitespace ?? false,
			Comment = o.CommentPrefix,
			SkipEmptyLines = o.SkipEmptyLines,
			LineBreaksInQuotes = o.AllowLineBreaksInQuotes,
		});
		if (!o.HasHeader) TestUtil.AssertSame(expected.ToList(), reference, $"reference, input {TestUtil.Escape(text)}");
	}

	[Fact]
	public void Simple() => Expect("a,b,c\n1,2,3\n", null, ["a", "b", "c"], ["1", "2", "3"]);
	[Fact]
	public void Crlf() => Expect("a,b\r\n1,2\r\n", null, ["a", "b"], ["1", "2"]);
	[Fact]
	public void CrOnly() => Expect("a,b\r1,2\r", null, ["a", "b"], ["1", "2"]);
	[Fact]
	public void MixedLineEndings() => Expect("a\r\nb\nc\rd", null, ["a"], ["b"], ["c"], ["d"]);
	[Fact]
	public void NoTrailingNewline() => Expect("a,b\n1,2", null, ["a", "b"], ["1", "2"]);
	[Fact]
	public void EmptyInput() => Expect("", null);
	[Fact]
	public void OnlyNewlines() => Expect("\n\r\n\r", null);
	[Fact]
	public void EmptyFields() => Expect(",,\n,", null, ["", "", ""], ["", ""]);
	[Fact]
	public void EmptyQuoted() => Expect("\"\",x\n\"\"", null, ["", "x"], [""]);

	[Fact]
	public void QuotedFields() => Expect("\"a,b\",\"c\"\"d\",\"e\nf\"\n\"x\r\ny\"\r\n", null,
		["a,b", "c\"d", "e\nf"], ["x\r\ny"]);

	[Fact]
	public void EscapedQuotesOnly() => Expect("\"\"\"\"\"\",\"\"\"\"", null, ["\"\"", "\""]);

	[Fact]
	public void EmptyLinesSkippedByDefault() => Expect("a\n\n\r\n\rb\n", null, ["a"], ["b"]);

	[Fact]
	public void EmptyLinesKept() => Expect("a\n\nb\r\n\r\nc", NoHeader with { SkipEmptyLines = false }, ["a"], [""], ["b"], [""], ["c"]);

	[Fact]
	public void StrayQuoteIsText() => Expect("ab\"c,d\n5'10\",x", null, ["ab\"c", "d"], ["5'10\"", "x"]);
	[Fact]
	public void TextAfterClosingQuote() => Expect("\"ab\"c,d\n\"x\" ,y", null, ["abc", "d"], ["x ", "y"]);
	[Fact]
	public void UnterminatedQuoteRunsToEnd() => Expect("a,\"bc\nd,e", null, ["a", "bc\nd,e"]);

	[Fact]
	public void LineBreakEndsRecordWhenQuotedLineBreaksDisallowed() =>
		Expect("a,\"b\nc,d\n", NoHeader with { AllowLineBreaksInQuotes = false }, ["a", "b"], ["c", "d"]);

	[Fact]
	public void Trim() => Expect("  a , \" b \" ,c \n\t x\t", NoHeader with { TrimWhitespace = true }, ["a", " b ", "c"], ["x"]);

	[Fact]
	public void TrimAllowsQuoteAfterWhitespace() =>
		Expect("a,  \"b,c\"  ,d", NoHeader with { TrimWhitespace = true }, ["a", "b,c", "d"]);

	[Fact]
	public void Comments() => Expect("#c,\"x\na,b\n#x,y\n c,#d", NoHeader with { CommentPrefix = '#' }, ["a", "b"], [" c", "#d"]);

	[Fact]
	public void QuotesDisabled() => Expect("\"a,b\",c", NoHeader with { Mode = CsvMode.NoEscape }, ["\"a", "b\"", "c"]);
	[Fact]
	public void Semicolon() => Expect("a;b,c;d", NoHeader with { Separator = ';' }, ["a", "b,c", "d"]);
	[Fact]
	public void SingleQuote() => Expect("'a,b','c''d'", NoHeader with { Quote = '\'' }, ["a,b", "c'd"]);
	[Fact]
	public void Multibyte() => Expect("é,€,日本\n\"ü,ß\",😀", null, ["é", "€", "日本"], ["ü,ß", "😀"]);

	[Theory]
	[InlineData("a;b;c\n1;2;3\n", ';')]
	[InlineData("a,b,c\n1,2,3\n", ',')]
	[InlineData("a\tb\tc\n1\t2\t3\n", '\t')]
	[InlineData("a|b|c\n1|2|3\n", '|')]
	[InlineData("name,\"x;y;z\"\nb,\"q;r;s\"\n", ',')]
	[InlineData("single\nvalue\n", ',')]
	[InlineData("a;b;c,d\n1;2;3,4\n1;2;3,4\n", ';')]
	[InlineData("a;b,c\n1;2,3\n", ',')]
	[InlineData("title line\na;b;c\n1;2;3\n4;5;6\n", ';')]
	public void DetectsSeparator(string text, char expected)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text));
		Assert.Equal(expected, reader.Separator);
	}

	[Fact]
	public void Header()
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes("Name,Age, City \nBob,30,Paris\nAmy,41,Rome\n"));
		Assert.Equal(["Name", "Age", " City "], reader.Headers);
		Assert.Equal(1, reader.GetFieldIndex("Age"));
		Assert.Equal(1, reader.GetFieldIndex("age"));
		Assert.Equal(2, reader.GetFieldIndex("city"));
		Assert.False(reader.TryGetFieldIndex("Missing", out _));
		Assert.Throws<KeyNotFoundException>(() => reader.GetFieldIndex("Missing"));
		Assert.True(reader.Read());
		Assert.Equal(1, reader.RowNumber);
		Assert.Equal("Bob", reader.GetString("Name"));
#if NET10_0_OR_GREATER
		Assert.Equal(30, reader.Parse<int>("Age"));
#endif
		Assert.Equal(30, reader.Parse("Age", int.Parse));
		Assert.Equal("Paris", reader["city"].ToString());
		Assert.True(reader.Read());
		Assert.Equal("Amy", reader.GetString(0));
		Assert.False(reader.Read());
		Assert.False(reader.Read());
	}

	[Fact]
	public void HeaderOnly()
	{
		using var reader = CsvReader.Create("a,b\r\n"u8.ToArray());
		Assert.False(reader.Read());
		Assert.Equal(["a", "b"], reader.Headers);
	}

	[Fact]
	public void HeaderSkipsLeadingEmptyLinesAndComments()
	{
		using var reader = CsvReader.Create("\n#note\n\nx,y\n1,2\n"u8.ToArray(), new CsvReaderOptions { CommentPrefix = '#' });
		Assert.Equal(["x", "y"], reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal("2", reader.GetString("y"));
	}

	[Fact]
	public void Utf8Bom()
	{
		var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat("id,v\n1,2\n"u8.ToArray()).ToArray();
		using var reader = CsvReader.Create(bytes);
		Assert.Equal(["id", "v"], reader.Headers);
		using var stream = CsvReader.Create(new MemoryStream(bytes));
		Assert.Equal(["id", "v"], stream.Headers);
		Assert.True(stream.Read());
		Assert.Equal("1", stream.GetString(0));
	}

	[Theory]
	[InlineData("utf-16")]
	[InlineData("utf-16BE")]
	[InlineData("utf-32")]
	public void Utf16And32WithBom(string encodingName)
	{
		var encoding = Encoding.GetEncoding(encodingName);
		var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("név,v\n\"ü,1\",2\n")).ToArray();
		foreach (var reader in new[] { CsvReader.Create(new MemoryStream(bytes)), CsvReader.Create(bytes) })
		{
			using (reader)
			{
				Assert.Equal(["név", "v"], reader.Headers);
				Assert.True(reader.Read());
				Assert.Equal("ü,1", reader.GetString(0));
				Assert.Equal("2", reader.GetString(1));
				Assert.False(reader.Read());
			}
		}
	}

	[Fact]
	public void Latin1Encoding()
	{
		var bytes = Encoding.Latin1.GetBytes("café,x\nà,ÿ\n");
		using var reader = CsvReader.Create(new MemoryStream(bytes), new CsvReaderOptions { Encoding = Encoding.Latin1 });
		Assert.Equal(["café", "x"], reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal("à", reader.GetString(0));
		Assert.Equal("ÿ", reader.GetString(1));
	}

	[Theory]
	[InlineData("a,b\n1,x\"y\n", 2)]
	[InlineData("a,b\n1,\"x\"y\n", 2)]
	[InlineData("a,b\n1,2\n3,\"unterminated", 3)]
	public void StrictModeThrowsAtMalformedRecord(string text, int badRecord)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text),
			new CsvReaderOptions { HasHeader = false, MalformedData = CsvMalformedDataHandling.Throw });
		for (var i = 1; i < badRecord; i++) Assert.True(reader.Read());
		var ex = Assert.Throws<CsvFormatException>(() => reader.Read());
		Assert.Equal(badRecord, ex.RecordNumber);
	}

	[Fact]
	public void LineBreakInQuotesIsMalformedWhenNotAllowed()
	{
		const string text = "a,b\nc,\"d\ne\"\n";
		using var reader = CsvReader.FromText(text,
			new CsvReaderOptions { HasHeader = false, AllowLineBreaksInQuotes = false, MalformedData = CsvMalformedDataHandling.Throw });
		Assert.True(reader.Read());
		var ex = Assert.Throws<CsvFormatException>(() => reader.Read());
		Assert.Equal(2, ex.RecordNumber);
		Assert.Equal(8, ex.ByteOffset);
		Assert.Equal("Malformed CSV in record 2: a line break inside a quoted field (byte offset 8).", ex.Message);

		var reports = new List<CsvMalformedRecord>();
		Read(text, NoHeader with { AllowLineBreaksInQuotes = false, OnMalformedRecord = reports.Add });
		Assert.Equal(CsvMalformedReason.LineBreakInQuotes, reports[0].Reason);
		Assert.Equal(2, reports[0].RecordNumber);
	}

	[Fact]
	public void FormatExceptionWithoutAPosition()
	{
		var ex = new CsvFormatException("Too large.");
		Assert.IsAssignableFrom<FormatException>(ex);
		Assert.Equal("Too large.", ex.Message);
		Assert.Equal(0, ex.RecordNumber);
		Assert.Equal(0, ex.ByteOffset);

		var positioned = new CsvFormatException("Bad.", 3, 42);
		Assert.Equal(3, positioned.RecordNumber);
		Assert.Equal(42, positioned.ByteOffset);
	}

	[Fact]
	public void StrictModeAcceptsValidQuoting()
	{
		var strict = new CsvReaderOptions { HasHeader = false, MalformedData = CsvMalformedDataHandling.Throw };
		var records = Read("\"a\"\"b\",\"c,d\",\"\"\n\"x\ny\",z\n", strict);
		Assert.Equal(2, records.Count);
		Assert.Equal(["a\"b", "c,d", ""], records[0]);
	}

	[Fact]
	public void StrictModeLongValidQuotedData()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 5000; i++) sb.Append($"\"v{i},\"\"q\"\"\",{i},\"multi\nline\"\r\n");
		var strict = new CsvReaderOptions { HasHeader = false, MalformedData = CsvMalformedDataHandling.Throw };
		var records = Read(sb.ToString(), strict);
		Assert.Equal(5000, records.Count);
		Assert.Equal(["v4999,\"q\"", "4999", "multi\nline"], records[4999]);
	}

	[Fact]
	public void FieldCountMismatch()
	{
		var options = new CsvReaderOptions { FieldCountMismatch = CsvFieldCountHandling.Throw };
		using var reader = CsvReader.Create("a,b\n1,2\n3\n"u8.ToArray(), options);
		Assert.True(reader.Read());
		Assert.Throws<CsvFormatException>(() => reader.Read());
	}

	[Fact]
	public void MissingFields()
	{
		using var reader = CsvReader.Create("a,b,c\n1\n"u8.ToArray());
		Assert.True(reader.Read());
		Assert.Equal(1, reader.FieldCount);
		Assert.Equal("", reader.GetString(2));
		Assert.True(reader.IsEmpty(1));

		using var strict = CsvReader.Create("a,b,c\n1\n"u8.ToArray(), new CsvReaderOptions { MissingField = CsvMissingFieldHandling.Throw });
		Assert.True(strict.Read());
		Assert.Throws<IndexOutOfRangeException>(() => strict.GetString(2));
	}

	[Fact]
	public void TypedValues()
	{
		var text = "i,l,d,f,m,b,dt,g,neg\n" +
					  "42,-9000000000,1.5e-3,0.25,12.50,yes,2024-03-01T10:20:30.0000000Z,0f8fad5b-d9cb-469f-a165-70867728950e,-17\n";
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text));
		Assert.True(reader.Read());
		Assert.Equal(42, reader.GetInt32(0));
		Assert.Equal(-9000000000L, reader.GetInt64(1));
		Assert.Equal(1.5e-3, reader.GetDouble(2));
		Assert.Equal(0.25f, reader.GetSingle(3));
		Assert.Equal(12.50m, reader.GetDecimal(4));
		Assert.True(reader.GetBoolean(5));
		Assert.Equal(new DateTime(2024, 3, 1, 10, 20, 30, DateTimeKind.Utc), reader.GetDateTime(6).ToUniversalTime());
		Assert.Equal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), reader.GetGuid(7));
#if NET10_0_OR_GREATER
		Assert.Equal(-17, reader.Parse<int>(8));
		Assert.Equal(-17L, reader.Parse<long>(8));
		Assert.Equal((short)-17, reader.Parse<short>(8));
		Assert.Equal(-17.0, reader.Parse<double>(8));
		Assert.Equal(TimeSpan.FromHours(1), TimeSpan.Parse("01:00:00"));
		Assert.True(reader.TryParse<int>(0, out var i) && i == 42);
		Assert.False(reader.TryParse<int>(2, out _));
		Assert.Equal(42, reader[0].Parse<int>());
		Assert.Null(reader[5].ParseOrNull<int>());
#endif
		// The parser overloads work on every target.
		Assert.Equal(-17, reader.Parse(8, int.Parse));
		Assert.Equal(-17.0, reader.Parse(8, double.Parse));
		Assert.Equal(new DateTime(2024, 3, 1, 10, 20, 30, DateTimeKind.Utc), reader.Parse(6, (s, p) => DateTime.Parse(s, p, DateTimeStyles.RoundtripKind)).ToUniversalTime());
		Assert.True(reader.TryParse(0, (string s, IFormatProvider p, out int v) => int.TryParse(s, NumberStyles.Integer, p, out v), out var j) && j == 42);
		Assert.False(reader.TryParse(2, (string s, IFormatProvider p, out int v) => int.TryParse(s, NumberStyles.Integer, p, out v), out _));
		Assert.Equal(42, reader[0].Parse(int.Parse));
		Assert.Equal(42, reader.Current.Parse(0, int.Parse));
		Assert.True(reader[0].TryParse((string s, IFormatProvider p, out long v) => long.TryParse(s, NumberStyles.Integer, p, out v), out var k) && k == 42);
		Assert.Null(reader[5].ParseOrNull((string s, IFormatProvider p, out int v) => int.TryParse(s, NumberStyles.Integer, p, out v)));
		Assert.Equal(42, reader[0].ParseOrNull((string s, IFormatProvider p, out int v) => int.TryParse(s, NumberStyles.Integer, p, out v)));
		Assert.Throws<ArgumentNullException>(() => reader.Parse<int>(0, (Func<string, IFormatProvider, int>)null!));
	}

	[Fact]
	public void CultureAwareNumbers()
	{
		var options = new CsvReaderOptions { Separator = ';', FormatProvider = CultureInfo.GetCultureInfo("de-DE"), HasHeader = false };
		using var reader = CsvReader.Create("1,5;1.234,5\n"u8.ToArray(), options);
		Assert.True(reader.Read());
		Assert.Equal(1.5, reader.GetDouble(0));
		Assert.Equal(1234.5, reader.GetDouble(1));
	}

	[Fact]
	public void ReadHeaderReadsOnlyTheHeader()
	{
		using var reader = CsvReader.FromText("id;name\n1;Anna\n");
		reader.ReadHeader();
		Assert.Equal(["id", "name"], reader.Headers);
		Assert.Equal(';', reader.Separator);
		Assert.Equal(0, reader.RowNumber);
		reader.ReadHeader();
		Assert.True(reader.Read());
		Assert.Equal(["1", "Anna"], TestUtil.Record(reader));
		Assert.Equal(1, reader.RowNumber);
		Assert.False(reader.Read());
	}

	[Fact]
	public void ReadHeaderWithoutAHeaderOnlyDetectsTheSeparator()
	{
		using var reader = CsvReader.FromText("a;b\nc;d\n", NoHeader);
		reader.ReadHeader();
		Assert.Equal(';', reader.Separator);
		Assert.Empty(reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal(["a", "b"], TestUtil.Record(reader));

		var disposed = CsvReader.FromText("x\n");
		disposed.Dispose();
		Assert.Throws<ObjectDisposedException>(() => disposed.ReadHeader());
	}

	[Fact]
	public void FieldsByNameAndAsChars()
	{
		using var reader = CsvReader.FromText("id,name\n1,\"Zoë, Ann\"\n");
		Assert.True(reader.Read());
		Assert.Equal("Zoë, Ann"u8.ToArray(), reader.GetSpan("name").ToArray());
		Assert.Equal("1"u8.ToArray(), reader.GetSpan(" ID ").ToArray());
		Assert.Throws<KeyNotFoundException>(() => { reader.GetSpan("missing"); });

		var chars = new char[16];
		var written = reader.GetChars(1, chars);
		Assert.Equal("Zoë, Ann", new string(chars, 0, written));
		Assert.Equal(1, reader.GetChars(0, new char[1]));
		Assert.Throws<ArgumentException>(() => reader.GetChars(1, new char[3]));
	}

	[Fact]
	public void RowIsAViewOfTheCurrentRecord()
	{
		using var reader = CsvReader.FromText("id,name,score\n7,\"Ann, B\",2.5\n8,Cy,\n");
		Assert.True(reader.Read());
		var row = reader.Current;
		Assert.Equal(1, row.RowNumber);
		Assert.Equal(3, row.FieldCount);
		Assert.Equal("7"u8.ToArray(), row.GetSpan(0).ToArray());
		Assert.Equal("Ann, B", row.GetString(1));
		Assert.Equal("Ann, B", row["name"].ToString());
		Assert.Equal("Ann, B", row[" NAME"].ToString());
		Assert.Equal("7,\"Ann, B\",2.5", Encoding.UTF8.GetString(row.RawRecord.ToArray()));
		Assert.Equal(["7", "Ann, B", "2.5"], row.ToArray());
#if NET10_0_OR_GREATER
		Assert.Equal(2.5, row.Parse<double>(2));
		Assert.Equal(7, row.Parse<int>(0));
#endif
		Assert.Throws<KeyNotFoundException>(() => reader.Current["missing"].ToString());

		Assert.True(reader.Read());
		row = reader.Current;
		Assert.Equal(2, row.RowNumber);
		Assert.Equal(["8", "Cy", ""], row.ToArray());
		Assert.Equal("", row.GetString(2));
		Assert.True(row.GetSpan(2).IsEmpty);
	}

	[Fact]
	public void FieldLengthEmptinessAndTryParse()
	{
		using var reader = CsvReader.FromText("a,b,c,d,e\nZoë,,x7,42,01:30\n");
		Assert.True(reader.Read());
		Assert.Equal(4, reader[0].Length);
		Assert.False(reader[0].IsEmpty);
		Assert.Equal(0, reader[1].Length);
		Assert.True(reader[1].IsEmpty);
		Assert.Equal(2, reader["c"].Length);
#if NET10_0_OR_GREATER
		Assert.True(reader[3].TryParse<int>(out var number));
		Assert.Equal(42, number);
		Assert.False(reader[2].TryParse<int>(out var bad));
		Assert.Equal(0, bad);
		Assert.False(reader[1].TryParse<int>(out _));
		Assert.True(reader[0].TryParse<string>(out var text));
		Assert.Equal("Zoë", text);
		Assert.True(reader[4].TryParse<TimeSpan>(out var span));
		Assert.Equal(new TimeSpan(1, 30, 0), span);
#endif
	}

	[Fact]
	public void TypedValuesWithAFormatProvider()
	{
		var german = CultureInfo.GetCultureInfo("de-DE");
		using var reader = CsvReader.Create("-17;-9000000000;1.234,5;0,25;12,50;01.03.2024 10:20;01.03.2024 10:20 +02:00\n"u8.ToArray(),
			new CsvReaderOptions { Separator = ';', HasHeader = false });
		Assert.True(reader.Read());
		Assert.Equal(-17, reader.GetInt32(0, german));
		Assert.Equal(-9000000000L, reader.GetInt64(1, german));
		Assert.Throws<FormatException>(() => reader.GetDouble(2));
		Assert.Equal(1234.5, reader.GetDouble(2, german));
		Assert.Equal(0.25f, reader.GetSingle(3, german));
		Assert.Equal(12.50m, reader.GetDecimal(4, german));
		Assert.Equal(new DateTime(2024, 3, 1, 10, 20, 0), reader.GetDateTime(5, german));
		Assert.Equal(new DateTimeOffset(2024, 3, 1, 10, 20, 0, TimeSpan.FromHours(2)), reader.GetDateTimeOffset(6, german));
#if NET10_0_OR_GREATER
		Assert.Equal(1234.5, reader.Parse<double>(2, german));
		Assert.Equal(12.50m, reader.Parse<decimal>(4, german));
		Assert.Throws<ArgumentNullException>(() => reader.Parse<int>(0, (IFormatProvider)null!));
#endif
		Assert.Throws<ArgumentNullException>(() => reader.GetInt32(0, null!));
		Assert.Throws<ArgumentNullException>(() => reader.GetDecimal(4, null!));
	}

	[Fact]
	public void DatesInAnExactFormat()
	{
		using var reader = CsvReader.Create("20240301;202403011020+0200;2024-03-01;März 2024\n"u8.ToArray(),
			new CsvReaderOptions { Separator = ';', HasHeader = false });
		Assert.True(reader.Read());
		Assert.Equal(new DateTime(2024, 3, 1), reader.GetDateTime(0, "yyyyMMdd"));
		Assert.Equal(new DateTimeOffset(2024, 3, 1, 10, 20, 0, TimeSpan.FromHours(2)), reader.GetDateTimeOffset(1, "yyyyMMddHHmmzzz"));
		Assert.Throws<FormatException>(() => reader.GetDateTime(2, "yyyyMMdd"));
		Assert.Equal(new DateTime(2024, 3, 1), reader.GetDateTime(3, "MMMM yyyy", CultureInfo.GetCultureInfo("de-DE")));
		// Without an offset in the field, the local time zone's applies.
		Assert.Equal(new DateTime(2024, 3, 1), reader.GetDateTimeOffset(0, "yyyyMMdd", CultureInfo.InvariantCulture).DateTime);
		Assert.Throws<ArgumentNullException>(() => reader.GetDateTime(0, (string)null!));
		Assert.Throws<ArgumentNullException>(() => reader.GetDateTime(0, "yyyyMMdd", null!));
	}

	[Fact]
	public void FieldLargerThanBuffer()
	{
		var big = new string('x', 100_000);
		var text = $"a,{big},b\n\"{big.Replace('x', '"').Replace("\"", "\"\"")}\",c\n";
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(text), 1, 5000), NoHeader with { BufferSize = 256 });
		Assert.True(reader.Read());
		Assert.Equal(big, reader.GetString(1));
		Assert.True(reader.Read());
		Assert.Equal(new string('"', 100_000), reader.GetString(0));
		Assert.False(reader.Read());
	}

	[Fact]
	public void ManyColumns()
	{
		var row = string.Join(",", Enumerable.Range(0, 100_000));
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(row + "\n" + row)), NoHeader with { BufferSize = 1024 });
		for (var r = 0; r < 2; r++)
		{
			Assert.True(reader.Read());
			Assert.Equal(100_000, reader.FieldCount);
			Assert.Equal(99_999, reader.GetInt32(99_999));
			Assert.Equal(12_345, reader.GetInt32(12_345));
		}

		Assert.False(reader.Read());
	}

	[Fact]
	public void ManyRows()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 300_000; i++) sb.Append(i).Append(i % 7 == 0 ? "\r\n" : "\n");
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())), NoHeader);
		var sum = 0L;
		var count = 0;
		while (reader.Read())
		{
			sum += reader.GetInt32(0);
			count++;
		}

		Assert.Equal(300_000, count);
		Assert.Equal(299_999L * 300_000 / 2, sum);
	}

	[Fact]
	public void PoolStringsReusesInstances()
	{
		using var reader = CsvReader.Create("v\nhello\nhello\nworld\n"u8.ToArray(), new CsvReaderOptions { PoolStrings = true });
		Assert.True(reader.Read());
		var a = reader.GetString(0);
		Assert.True(reader.Read());
		var b = reader.GetString(0);
		Assert.True(reader.Read());
		Assert.Equal("hello", a);
		Assert.Same(a, b);
		Assert.Equal("world", reader.GetString(0));
	}

	[Fact]
	public void ForeachAndRawRecord()
	{
		using var reader = CsvReader.Create("h1,h2\n\"a,1\",b\nc,d\n"u8.ToArray());
		var raw = new List<string>();
		var first = new List<string>();
		foreach (var row in reader)
		{
			raw.Add(row.ToString());
			first.Add(row[0].ToString());
			Assert.Equal(2, row.FieldCount);
		}

		Assert.Equal(["\"a,1\",b", "c,d"], raw);
		Assert.Equal(["a,1", "c"], first);
	}

	[Fact]
	public async Task AwaitForeachAndAsyncFile()
	{
		var path = Path.GetTempFileName();
		try
		{
			var sb = new StringBuilder("id,name\r\n");
			for (var i = 0; i < 20_000; i++) sb.Append(i).Append(",\"name ").Append(i).Append("\"\r\n");
			File.WriteAllText(path, sb.ToString());
			await using var reader = CsvReader.FromFile(path, new CsvReaderOptions { BufferSize = 4096 }, FileOptions.Asynchronous | FileOptions.SequentialScan);
			await reader.ReadHeaderAsync();
			Assert.Equal(["id", "name"], reader.Headers);
			var n = 0;
			await foreach (var row in reader)
			{
#if NET10_0_OR_GREATER
				Assert.Equal(n, row[0].Parse<int>());
#else
				Assert.Equal(n, row[0].Parse(int.Parse));
#endif
				Assert.Equal("name " + n, row[1].ToString());
				n++;
			}

			Assert.Equal(20_000, n);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void TextReaderAndString()
	{
		const string text = "a,b\n\"x\ny\",€\n";
		using (var reader = CsvReader.Create(new StringReader(text)))
		{
			Assert.True(reader.Read());
			Assert.Equal("x\ny", reader.GetString(0));
			Assert.Equal("€", reader.GetString(1));
		}

		using (var reader = CsvReader.FromText(text))
		{
			Assert.True(reader.Read());
			Assert.Equal("€", reader.GetString(1));
		}
	}

	[Fact]
	public void GetSpanIsUtf8()
	{
		using var reader = CsvReader.Create("x\n\"a\"\"b\"\n"u8.ToArray());
		Assert.True(reader.Read());
		Assert.True(reader.GetSpan(0).SequenceEqual("a\"b"u8));
		Assert.True(reader.GetRawSpan(0).SequenceEqual("\"a\"\"b\""u8));
	}

	[Fact]
	public void LeaveOpen()
	{
		var stream = new MemoryStream("a\n1\n"u8.ToArray());
		using (var reader = CsvReader.Create(stream, leaveOpen: true)) { while (reader.Read()) { } }
		Assert.True(stream.CanRead);
		using (var reader = CsvReader.Create(stream)) { }
		Assert.False(stream.CanRead);
	}

	[Fact]
	public void TextAfterClosingQuoteAndWhitespaceAcrossBlocks()
	{
		// With trimming, "a"   m is text after the closing quote. When the quote and the spaces end a 64-byte block
		// that is scanned exactly and the text starts the next one, that block must not take the SIMD path, whose
		// carried state cannot express "after a closing quote".
		foreach (var pad in new[] { 40, 50, 57, 58, 59, 60 })
		{
			var text = new string('x', pad) + ",\"a\"" + new string(' ', 63 - pad - 3) + "m,z\n" +
						  string.Concat(Enumerable.Repeat("next,row\n", 20));
			var o = new ReferenceOptions { Trim = true };
			var expected = ReferenceCsv.Parse(text, o, out var malformed);
			Assert.Equal(1, malformed);
			foreach (var width in TestUtil.Widths)
			{
				var reports = new List<CsvMalformedRecord>();
				using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), o.ToReaderOptions() with { OnMalformedRecord = reports.Add });
				reader.vectorWidth = width;
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"pad {pad}, width {width}");
				Assert.Equal([CsvMalformedReason.TextAfterClosingQuote], reports.Select(r => r.Reason));
			}
		}
	}

	[Theory]
	[InlineData("\r")]
	[InlineData("\n")]
	[InlineData("\r\n")]
	public void CommentAfterLineBreakAtBlockEdge(string newline)
	{
		// The line break ends exactly at the end of a 64 byte block, so the comment starts the next block.
		foreach (var pad in new[] { 61, 62, 63, 64, 127 })
		{
			var text = new string('x', pad - newline.Length + 1) + newline + "#a,\"b" + newline + "c,d" + newline + "\"e\nf\",g";
			var options = NoHeader with { CommentPrefix = '#' };
			var expected = ReferenceCsv.Parse(text, new ReferenceOptions { Comment = '#' });
			Assert.Equal(3, expected.Count);
			foreach (var width in TestUtil.Widths)
			{
				using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), options);
				reader.vectorWidth = width;
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"pad {pad}, width {width}");
			}
		}
	}

	[Fact]
	public void SkipMalformedRecords()
	{
		var reports = new List<CsvMalformedRecord>();
		var options = NoHeader with
		{
			MalformedData = CsvMalformedDataHandling.Skip,
			OnMalformedRecord = reports.Add,
		};
		var records = Read("a,b\n1,x\"y\n3,4\n\"z\"q,5\n6,\"open", options);
		TestUtil.AssertSame([["a", "b"], ["3", "4"]], records, "skip");
		Assert.Equal([CsvMalformedReason.StrayQuote, CsvMalformedReason.TextAfterClosingQuote, CsvMalformedReason.UnterminatedQuote],
			reports.Select(r => r.Reason));
		Assert.All(reports, r => Assert.True(r.Skipped));
		Assert.Equal("1,x\"y", reports[0].RawRecord);
		Assert.Equal(2, reports[0].RecordNumber);
	}

	[Fact]
	public void RecoverAndReport()
	{
		var reports = new List<CsvMalformedRecord>();
		var records = Read("a,b\n1,x\"y\n", NoHeader with { OnMalformedRecord = reports.Add });
		TestUtil.AssertSame([["a", "b"], ["1", "x\"y"]], records, "recover");
		Assert.Single(reports);
		Assert.False(reports[0].Skipped);
	}

	[Fact]
	public void SkipFieldCountMismatch()
	{
		var reports = new List<CsvMalformedRecord>();
		var options = new CsvReaderOptions { FieldCountMismatch = CsvFieldCountHandling.Skip, OnMalformedRecord = reports.Add };
		using var reader = CsvReader.Create("a,b\n1,2\n3\n4,5,6\n7,8\n"u8.ToArray(), options);
		var records = TestUtil.ReadAll(reader);
		TestUtil.AssertSame([["1", "2"], ["7", "8"]], records, "field count");
		Assert.Equal(2, reports.Count);
		Assert.All(reports, r => Assert.Equal(CsvMalformedReason.FieldCountMismatch, r.Reason));
	}

	[Theory]
	[InlineData("sep=;\r\na;b\n1;2\n", ';')]
	[InlineData("SEP=|\na|b\n1|2\n", '|')]
	[InlineData("sep=\t\na\tb\n1\t2\n", '\t')]
	public void ExcelSeparatorHint(string text, char expected)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text));
		Assert.Equal(expected, reader.Separator);
		Assert.Equal(["a", "b"], reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal("2", reader.GetString("b"));
	}

	[Fact]
	public void ExcelSeparatorHintIgnoredWhenSeparatorDiffers()
	{
		using var reader = CsvReader.Create("sep=;\na,b\n"u8.ToArray(), new CsvReaderOptions { Separator = ',' });
		Assert.Equal(["sep=;"], reader.Headers);
	}

	[Fact]
	public void LongCrLfRunsAcrossBlocks()
	{
		var sb = new StringBuilder();
		var expected = new List<string[]>();
		for (var i = 0; i < 400; i++)
		{
			sb.Append(new string('v', i % 70)).Append(i).Append("\r\n");
			expected.Add([new string('v', i % 70) + i]);
		}

		foreach (var width in TestUtil.Widths)
		{
			using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(sb.ToString()), width, 63), NoHeader with { BufferSize = 256 });
			reader.vectorWidth = width;
			TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"width {width}");
		}
	}

	[Fact]
	public void DenseBlocks()
	{
		// Blocks of up to 64 terminators, with fields that need decoding among them, so that every group of 16 entries the
		// compress flattener writes, and every decode flag, is checked against the reference.
		var rng = new Random(4181);
		ReferenceOptions[] options =
		[
			new(),
			new() { Trim = true },
			new() { Quote = null, Escape = '\\' },
			new() { Quote = null },
		];
		foreach (var o in options)
		{
			for (var iter = 0; iter < 100; iter++)
			{
				var text = DenseText(rng, o);
				var expected = ReferenceCsv.Parse(text, o);
				foreach (var width in TestUtil.Widths)
				{
					foreach (var compress in new[] { false, true })
					{
						if (compress && (width != 512 || !CsvReader.CompressIndexSupported))
						{
							continue;
						}

						using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), o.ToReaderOptions());
						reader.vectorWidth = width;
						reader.compressIndex = compress;
						TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"width {width}, compress {compress}, {o}, text {TestUtil.Escape(text)}");
					}
				}
			}
		}
	}

	private static string DenseText(Random rng, ReferenceOptions o)
	{
		var sb = new StringBuilder();
		// A field is empty one time in this many: 1 gives blocks of nothing but separators.
		var emptyOneIn = rng.Next(1, 8);
		while (sb.Length < 1000)
		{
			if (rng.Next(emptyOneIn) != 0)
			{
				switch (rng.Next(4))
				{
					case 0 when o.Quote is char q:
						sb.Append(q).Append('a', rng.Next(3)).Append(q);
						break;
					case 1 when o.Escape is char e:
						sb.Append('b').Append(e).Append(',');
						break;
					case 2 when o.Trim:
						sb.Append(" c ");
						break;
					default:
						sb.Append('d', rng.Next(1, 4));
						break;
				}
			}

			sb.Append(rng.Next(16) switch { 0 => "\n", 1 => "\r\n", _ => "," });
		}

		return sb.ToString();
	}
}
