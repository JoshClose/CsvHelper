using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>The writer's line endings, comments, per-field quoting, ShouldQuote callback and injection protection.</summary>
public class WriterOptionTests
{
	private static string Write(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			write(writer);
		}

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	[Theory]
	[InlineData("\r\n")]
	[InlineData("\n")]
	[InlineData("\r")]
	public void LineEndings(string newLine)
	{
		var options = new CsvWriterOptions { NewLine = newLine, CommentPrefix = '#', ValidateFieldCount = false };
		var csv = Write(w =>
		{
			w.WriteComment("note");
			w.WriteRecord("a", "b,c");
			w.WriteRecord("two\nlines", "");
			w.WriteField("");
			w.EndRecord();
			w.WriteField("open");
			w.WriteField("x");
		}, options);
		var expected = "#note\r\na,\"b,c\"\r\n\"two\nlines\",\r\n\"\"\r\nopen,x\r\n".Replace("\r\n", newLine);

		Assert.Equal(expected, csv);
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { HasHeader = false, CommentPrefix = '#' });
		Assert.Equal(["a|b,c", "two\nlines|", "", "open|x"], back.Select(r => string.Join("|", r)));
	}

	[Theory]
	[InlineData(CsvMode.Escape)]
	[InlineData(CsvMode.NoEscape)]
	[InlineData(CsvMode.FixedWidth)]
	public void LineEndingsInTheOtherModes(CsvMode mode)
	{
		var options = new CsvWriterOptions { Mode = mode, NewLine = "\n", FieldWidths = [3, 3] };
		var csv = Write(w =>
		{
			w.WriteRecord("a", "b");
			w.WriteRecord("c", "d");
		}, options);

		Assert.Equal(mode == CsvMode.FixedWidth ? "a  b  \nc  d  \n" : "a,b\nc,d\n", csv);
	}

	[Fact]
	public void EscapedCrBeforeALineFeedEndsTheRecordWithCrlf()
	{
		// An LF right after an escaped CR reads as an escaped CRLF, part of the value.
		var options = new CsvWriterOptions { Mode = CsvMode.Escape, NewLine = "\n" };
		var csv = Write(w =>
		{
			w.WriteRecord("a\r", "b\r");
			w.WriteRecord("c", "d");
			w.WriteField("e");
			w.WriteField("\r");
		}, options);

		Assert.Equal("a\\\r,b\\\r\r\nc,d\ne,\\\r\r\n", csv);
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { Mode = CsvMode.Escape, HasHeader = false });
		Assert.Equal(["a\r|b\r", "c|d", "e|\r"], back.Select(r => string.Join("|", r)));
	}

	[Theory]
	[InlineData("")]
	[InlineData("x")]
	[InlineData("\n\r")]
	[InlineData("\r\n\r\n")]
	[InlineData(null)]
	public void NewLineMustBeALineEnding(string? newLine) =>
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { NewLine = newLine! }));

	[Fact]
	public void WritesComments()
	{
		var options = new CsvWriterOptions { CommentPrefix = '#' };
		CsvWriter? writer = null;
		var csv = Write(w =>
		{
			writer = w;
			w.WriteComment("Exported, \"as is\"");
			w.WriteComment("two\r\nlines\nand\rmore");
			w.WriteComment((string?)null);
			w.WriteHeader("A", "B");
			w.WriteComment("é");
			w.WriteRecord("1", "2");
			Assert.Equal(2, w.RecordCount);
		}, options);

		Assert.Equal("#Exported, \"as is\"\r\n#two\r\n#lines\r\n#and\r\n#more\r\n#\r\nA,B\r\n#é\r\n1,2\r\n", csv);
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { CommentPrefix = '#' });
		Assert.Equal(["1|2"], back.Select(r => string.Join("|", r)));
	}

	[Fact]
	public void CommentsKeepTheByteOrderMarkFirst()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { CommentPrefix = ';', WriteByteOrderMark = true }, leaveOpen: true))
		{
			writer.WriteComment("x");
		}

		Assert.Equal("\uFEFF;x\r\n", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Fact]
	public void CommentsNeedThePrefixAndARecordBoundary()
	{
		var e = Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteComment("x")));
		Assert.Contains("CommentPrefix", e.Message);

		var csv = Write(w =>
		{
			w.WriteField("a");
			Assert.Throws<InvalidOperationException>(() => w.WriteComment("x"));
			w.EndRecord();
			w.WriteComment("x");
		}, new CsvWriterOptions { CommentPrefix = '#' });
		Assert.Equal("a\r\n#x\r\n", csv);
	}

	[Fact]
	public void RecordsThatStartWithThePrefixAreQuoted()
	{
		var options = new CsvWriterOptions { CommentPrefix = '#' };
		var csv = Write(w =>
		{
			w.WriteHeader("#Id", "Name");
			w.WriteRecord("#12", "#not first");
			w.WriteRecord("#", "a,b");
			w.WriteRecord("#\"q\"", "x");
			w.WriteField(42);
			w.WriteField("#");
			w.EndRecord();
		}, options);

		Assert.Equal("\"#Id\",Name\r\n\"#12\",#not first\r\n\"#\",\"a,b\"\r\n\"#\"\"q\"\"\",x\r\n42,#\r\n", csv);
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { CommentPrefix = '#', HasHeader = false });
		Assert.Equal(["#Id|Name", "#12|#not first", "#|a,b", "#\"q\"|x", "42|#"], back.Select(r => string.Join("|", r)));
	}

	[Fact]
	public void RecordsThatStartWithThePrefixInTheOtherModes()
	{
		var escape = Write(w => w.WriteRecord("#1", "#2"), new CsvWriterOptions { Mode = CsvMode.Escape, CommentPrefix = '#' });
		Assert.Equal("\\#1,#2\r\n", escape);
		var back = TestUtil.ReadText(escape, new CsvReaderOptions { Mode = CsvMode.Escape, CommentPrefix = '#', HasHeader = false });
		Assert.Equal(["#1|#2"], back.Select(r => string.Join("|", r)));

		// NoEscape writes values as they are, and so does it here.
		Assert.Equal("#1,#2\r\n", Write(w => w.WriteRecord("#1", "#2"), new CsvWriterOptions { Mode = CsvMode.NoEscape, CommentPrefix = '#' }));

		var fixedWidth = Write(w =>
		{
			var e = Assert.Throws<InvalidOperationException>(() => w.WriteRecord("#1", "a"));
			Assert.Contains("comment prefix '#'", e.Message);
			w.WriteRecord("1", "#a");
		}, new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [3, 3], CommentPrefix = '#' });
		Assert.Equal("1  #a \r\n", fixedWidth);
	}

	[Fact]
	public void RecordsFlushedHalfwayAreProtected()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { CommentPrefix = '#' }, leaveOpen: true))
		{
			writer.WriteField("#a");
			writer.Flush();
			writer.WriteField("b");
			writer.EndRecord();
			writer.WriteField("#c");
		}

		Assert.Equal("\"#a\",b\r\n\"#c\"\r\n", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Theory]
	[InlineData(CsvMode.Rfc4180, ',')]
	[InlineData(CsvMode.Rfc4180, '"')]
	[InlineData(CsvMode.Escape, '\\')]
	[InlineData(CsvMode.Rfc4180, 'é')]
	[InlineData(CsvMode.Rfc4180, '\n')]
	[InlineData(CsvMode.FixedWidth, '\n')]
	public void CommentPrefixMustBeUsable(CsvMode mode, char prefix) =>
		Assert.Throws<ArgumentException>(() =>
			CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Mode = mode, CommentPrefix = prefix, FieldWidths = [1] }));

	[Fact]
	public void QuotesSingleFields()
	{
		var csv = Write(w =>
		{
			w.WriteField("007", CsvQuoting.Always);
			w.WriteField("plain", CsvQuoting.WhenNeeded);
			w.WriteField("a,b", CsvQuoting.WhenNeeded);
			w.WriteField((string?)null, CsvQuoting.Always);
			w.WriteField("say \"hi\"".AsSpan(), CsvQuoting.Always);
			w.WriteField("é€", CsvQuoting.Always);
			w.EndRecord();
		});

		Assert.Equal("\"007\",plain,\"a,b\",\"\",\"say \"\"hi\"\"\",\"é€\"\r\n", csv);
		Assert.Throws<ArgumentOutOfRangeException>(() => Write(w => w.WriteField("x", (CsvQuoting)7)));
	}

	[Fact]
	public void FieldQuotingAppliesToRfc4180Only()
	{
		Assert.Equal("a\\,b,c\r\n", Write(w =>
		{
			w.WriteField("a,b", CsvQuoting.Always);
			w.WriteField("c", CsvQuoting.Always);
			w.EndRecord();
		}, new CsvWriterOptions { Mode = CsvMode.Escape }));
		Assert.Equal("ab c  \r\n", Write(w =>
		{
			w.WriteField("ab", CsvQuoting.Always);
			w.WriteField("c", CsvQuoting.Always);
			w.EndRecord();
		}, new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [3, 3] }));
	}

	[Fact]
	public void ShouldQuoteDecidesForFieldsThatDoNotNeedQuotes()
	{
		var calls = new List<string>();
		var options = new CsvWriterOptions
		{
			ShouldQuote = (field, type, index, record) =>
			{
				calls.Add($"{field.ToString()}:{type.Name}:{index}:{record}");

				return type == typeof(string);
			},
		};
		var when = new DateTime(2024, 3, 1, 10, 30, 0, DateTimeKind.Utc);
		var csv = Write(w =>
		{
			w.WriteHeader("Name", "Count");
			w.WriteField("Anna");
			w.WriteField(42);
			w.WriteField(1.5);
			w.WriteField(when);
			w.WriteField(true);
			w.WriteField((int?)null);
			w.WriteField("a,b"u8);
			w.WriteField((object)Guid.Empty);
			w.EndRecord();
		}, options with { ValidateFieldCount = false });

		Assert.Equal(
			"\"Name\",\"Count\"\r\n\"Anna\",42,1.5,2024-03-01T10:30:00.0000000Z,True,,\"a,b\",00000000-0000-0000-0000-000000000000\r\n", csv);
		Assert.Equal(
			["Name:String:0:0", "Count:String:1:0", "Anna:String:0:1", "42:Int32:1:1", "1.5:Double:2:1", "2024-03-01T10:30:00.0000000Z:DateTime:3:1",
			 "True:Boolean:4:1", ":Int32:5:1", "00000000-0000-0000-0000-000000000000:Guid:7:1"],
			calls);
	}

	[Fact]
	public void ShouldQuoteCannotRemoveNeededQuotes()
	{
		var options = new CsvWriterOptions { ShouldQuote = (_, _, _, _) => false };
		var csv = Write(w =>
		{
			w.WriteRecord("a,b", "say \"hi\"", "line\nbreak", "plain");
			w.WriteField("");
			w.EndRecord();
		}, options with { ValidateFieldCount = false });

		Assert.Equal("\"a,b\",\"say \"\"hi\"\"\",\"line\nbreak\",plain\r\n\"\"\r\n", csv);
	}

	[Fact]
	public void ShouldQuoteIsForRfc4180Only()
	{
		var options = new CsvWriterOptions { Mode = CsvMode.Escape, ShouldQuote = (_, _, _, _) => throw new InvalidOperationException("called") };

		Assert.Equal("a\\,b,1\r\n", Write(w =>
		{
			w.WriteField("a,b");
			w.WriteField(1);
			w.EndRecord();
		}, options));
	}

	[Fact]
	public void QuotingAlwaysWinsOverShouldQuote()
	{
		var options = new CsvWriterOptions { Quoting = CsvQuoting.Always, ShouldQuote = (_, _, _, _) => false };

		Assert.Equal("\"a\",\"1\"\r\n", Write(w =>
		{
			w.WriteField("a");
			w.WriteField(1);
			w.EndRecord();
		}, options));
	}

	[Fact]
	public void EscapesFormulas()
	{
		var options = new CsvWriterOptions { Injection = CsvInjection.Escape };
		var csv = Write(w =>
		{
			w.WriteRecord("=1+2", "@SUM(A1)", "+1", "-1", "\tx", "\rx", "safe", "a=b", "");
			w.WriteField("=cmd|' /C calc'!A0".AsSpan());
			w.WriteField("=x"u8);
			w.WriteField((object)"=y");
			w.WriteField(-5);
			w.WriteField(-1.5);
			w.WriteField(-2.5m);
			w.WriteField(TimeSpan.FromHours(-1));
			w.WriteField((object)-7);
			w.WriteField("say \"=\"");
			w.WriteField("=\"q\"");
			w.EndRecord();
		}, options with { ValidateFieldCount = false });

		Assert.Equal(
			"\"'=1+2\",\"'@SUM(A1)\",\"'+1\",\"'-1\",\"'\tx\",\"'\rx\",safe,a=b,\r\n" +
			"\"'=cmd|' /C calc'!A0\",\"'=x\",\"'=y\",-5,-1.5,-2.5,-01:00:00,-7,\"say \"\"=\"\"\",\"'=\"\"q\"\"\"\r\n",
			csv);
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { HasHeader = false });
		Assert.Equal("'=1+2", back[0][0]);
		Assert.Equal("'=\"q\"", back[1][back[1].Length - 1]);
	}

	[Fact]
	public void EscapesFormulasInTheOtherModes()
	{
		Assert.Equal("'=1\\,2,'-x\r\n", Write(w => w.WriteRecord("=1,2", "-x"), new CsvWriterOptions { Mode = CsvMode.Escape, Injection = CsvInjection.Escape }));
		Assert.Equal("'=1,2,'-x\r\n", Write(w => w.WriteRecord("=1,2", "-x"), new CsvWriterOptions { Mode = CsvMode.NoEscape, Injection = CsvInjection.Escape }));
		Assert.Equal("'=ab-5  \r\n", Write(w =>
		{
			w.WriteField("=ab");
			w.WriteField(-5);
			w.EndRecord();
		}, new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [4, 4], Injection = CsvInjection.Escape }));
		var tooLong = Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteRecord("=abc"),
			new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [4], Injection = CsvInjection.Escape }));
		Assert.Contains("5 characters long", tooLong.Message);
	}

	[Fact]
	public void StripsFormulas()
	{
		var options = new CsvWriterOptions { Injection = CsvInjection.Strip };
		var csv = Write(w =>
		{
			w.WriteRecord("=-+1", "@x", "safe", "=");
			w.WriteRecord("=");
			w.WriteField(-5);
			w.EndRecord();
		}, options with { ValidateFieldCount = false });

		Assert.Equal("1,x,safe,\r\n\"\"\r\n-5\r\n", csv);
	}

	[Fact]
	public void ThrowsForFormulasAndDiscardsTheRecord()
	{
		var options = new CsvWriterOptions { Injection = CsvInjection.Throw };
		var csv = Write(w =>
		{
			w.WriteHeader("A", "B");
			var e = Assert.Throws<InvalidOperationException>(() => w.WriteRecord("ok", "=1+2"));
			Assert.Equal("Field 1 of record 2 starts with '=', so a spreadsheet would run it as a formula (Injection = Throw). The record is discarded.", e.Message);
			var tab = Assert.Throws<InvalidOperationException>(() => w.WriteRecord("\tx", "y"));
			Assert.Contains("'\\t'", tab.Message);
			w.WriteRecord("ok", "-");
		}, options with { InjectionCharacters = "=\t" });

		Assert.Equal("A,B\r\nok,-\r\n", csv);
	}

	[Fact]
	public void InjectionOptionsAreValidated()
	{
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = CsvInjection.Escape, InjectionEscape = '=' }));
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = CsvInjection.Escape, InjectionEscape = '\n' }));
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = CsvInjection.Escape, InjectionEscape = 'é' }));
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = CsvInjection.Escape, InjectionEscape = ',' }));
		Assert.Throws<ArgumentException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { InjectionCharacters = "=é" }));
		Assert.Throws<ArgumentNullException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { InjectionCharacters = null! }));
		Assert.Throws<ArgumentOutOfRangeException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = (CsvInjection)9 }));
		Assert.Throws<ArgumentOutOfRangeException>(() => CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Quoting = (CsvQuoting)9 }));
		// The escape character only matters when it is used.
		CsvWriter.Create(new MemoryStream(), new CsvWriterOptions { Injection = CsvInjection.Strip, InjectionEscape = '=' }).Dispose();
	}

	[Fact]
	public void FormatsWithAnotherProvider()
	{
		var german = CultureInfo.GetCultureInfo("de-DE");
		var when = new DateTime(2024, 3, 1, 10, 30, 0);
		var csv = Write(w =>
		{
			w.WriteField(1234.5m, default, german);
			w.WriteField(1234.5m, "N1", german);
			w.WriteField(2.5, default, null);
			w.WriteField(when, "d", german);
			w.WriteField(when, default, german);
			w.WriteField(7, default, CultureInfo.InvariantCulture);
			w.EndRecord();
		});

		Assert.Equal("\"1234,5\",\"1.234,5\",2.5,01.03.2024,2024-03-01T10:30:00.0000000,7\r\n", csv);
	}

	[Fact]
	public void DatesWithoutAFormatAreIso8601()
	{
		var when = new DateTime(2024, 3, 1, 10, 30, 0);
		var csv = Write(w =>
		{
			w.WriteField((DateTime?)when);
			w.WriteField<DateTime>(when);
			w.WriteField((DateTimeOffset?)new DateTimeOffset(when, TimeSpan.FromHours(1)));
#if NET
			w.WriteField(new TimeOnly(10, 30, 15));
			w.WriteField((object)new TimeOnly(1, 2));
			w.WriteField((DateOnly?)new DateOnly(2024, 3, 1));
#endif
			w.EndRecord();
		});
#if NET
		Assert.Equal("2024-03-01T10:30:00.0000000,2024-03-01T10:30:00.0000000,2024-03-01T10:30:00.0000000+01:00,10:30:15.0000000,01:02:00.0000000,2024-03-01\r\n", csv);
#else
		Assert.Equal("2024-03-01T10:30:00.0000000,2024-03-01T10:30:00.0000000,2024-03-01T10:30:00.0000000+01:00\r\n", csv);
#endif
	}

	[Fact]
	public void SingleStringsAndArraysAreRecordsOfStrings()
	{
		// These must not bind to WriteRecord<T>, which writes a [CsvRecord] type.
		string[] fields = ["a", "b"];
		var csv = Write(w =>
		{
			w.WriteRecord("x");
			w.WriteRecord(fields);
			w.WriteRecord((string?)null);
		}, new CsvWriterOptions { ValidateFieldCount = false });

		Assert.Equal("x\r\na,b\r\n\"\"\r\n", csv);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public void RoundTripFuzzWithOptions(int seed)
	{
		var rng = new Random(seed);
		char[] alphabet = ['a', ' ', ',', ';', '"', '\r', '\n', '\t', 'é', '#', '\\', '=', '-', '1'];
		for (var iter = 0; iter < 200; iter++)
		{
			var mode = rng.Next(3) == 0 ? CsvMode.Escape : CsvMode.Rfc4180;
			var sep = ",;\t|"[rng.Next(4)];
			var newLine = new[] { "\r\n", "\n", "\r" }[rng.Next(3)];
			var columns = rng.Next(1, 6);
			var quoteRandomly = rng.Next(2) == 0;
			var decisions = new Random(seed * 1000 + iter);
			var options = new CsvWriterOptions
			{
				Mode = mode,
				Separator = sep,
				NewLine = newLine,
				CommentPrefix = '#',
				ShouldQuote = quoteRandomly ? (_, _, _, _) => decisions.Next(2) == 0 : null,
				BufferSize = 256 << rng.Next(3),
			};
			var records = new List<string[]>();
			var csv = Write(w =>
			{
				var n = rng.Next(0, 30);
				for (var r = 0; r < n; r++)
				{
					if (rng.Next(5) == 0)
					{
						w.WriteComment(new string(Enumerable.Range(0, rng.Next(0, 20)).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray()));
					}

					var record = new string[columns];
					for (var c = 0; c < columns; c++)
					{
						var length = rng.Next(4) == 0 ? 0 : rng.Next(1, rng.Next(3) == 0 ? 100 : 10);
						record[c] = new string(Enumerable.Range(0, length).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
					}

					// A record whose only field is empty is an empty line in Escape mode, which the reader skips.
					if (mode == CsvMode.Escape && columns == 1 && record[0].Length == 0)
					{
						record[0] = "a";
					}

					w.WriteRecord(record);
					records.Add(record);
				}
			}, options);

			var readOptions = new CsvReaderOptions { Mode = mode, HasHeader = false, Separator = sep, CommentPrefix = '#' };
			TestUtil.AssertSame(records, TestUtil.ReadText(csv, readOptions), $"seed {seed} iter {iter} mode {mode} sep {(int)sep}");
		}
	}

	[Fact]
	public void QuotesValuesWithLeadingOrTrailingWhitespace()
	{
		var options = new CsvWriterOptions { Quoting = CsvQuoting.WhenNeededOrWhitespace };
		var csv = Write(w =>
		{
			w.WriteRecord(" a", "b ", "\tc", "d e", "", "x\"y ");
			w.WriteField(-1);
			w.WriteField(" ".AsSpan());
			w.EndRecord();
		}, options with { ValidateFieldCount = false });

		Assert.Equal("\" a\",\"b \",\"\tc\",d e,,\"x\"\"y \"\r\n-1,\" \"\r\n", csv);
		// A reader that trims keeps the quoted whitespace.
		var back = TestUtil.ReadText(csv, new CsvReaderOptions { HasHeader = false, TrimWhitespace = true });
		Assert.Equal([" a", "b ", "\tc", "d e", "", "x\"y "], back[0]);
	}

	[Fact]
	public void QuotesOneFieldWithWhitespace()
	{
		var csv = Write(w =>
		{
			w.WriteField(" a", CsvQuoting.WhenNeededOrWhitespace);
			w.WriteField(" a");
			w.WriteField("b".AsSpan(), CsvQuoting.WhenNeededOrWhitespace);
			w.EndRecord();
		});

		Assert.Equal("\" a\", a,b\r\n", csv);
	}

	[Fact]
	public void WhitespaceQuotingAppliesToRfc4180ModeOnly()
	{
		var csv = Write(w => w.WriteRecord(" a", "b"), new CsvWriterOptions { Mode = CsvMode.Escape, Quoting = CsvQuoting.WhenNeededOrWhitespace });

		Assert.Equal(" a,b\r\n", csv);
	}

	[Fact]
	public void NullValue()
	{
		var options = new CsvWriterOptions { NullValue = "NULL", ValidateFieldCount = false };
		var csv = Write(w =>
		{
			w.WriteRecord("a", null, "");
			w.WriteField((string?)null);
			w.WriteField((object?)null);
			w.WriteField((int?)null);
			w.WriteField((double?)2.5);
			w.WriteField((string?)null, CsvQuoting.Always);
			w.EndRecord();
		}, options);

		Assert.Equal("a,NULL,\r\nNULL,NULL,NULL,2.5,\"NULL\"\r\n", csv);
		// Without NullValue, null is an empty field.
		Assert.Equal("a,,\r\n", Write(w => w.WriteRecord("a", null, "")));
		// A null value that needs quotes gets them.
		Assert.Equal("\"N,A\",b\r\n", Write(w => w.WriteRecord(null, "b"), new CsvWriterOptions { NullValue = "N,A" }));
	}

	[Fact]
	public void NullValueGoesThroughTheCustomHandling()
	{
		var options = new CsvWriterOptions { NullValue = "=NULL", Injection = CsvInjection.Escape, Quoting = CsvQuoting.Always };

		Assert.Equal("\"'=NULL\",\"x\"\r\n", Write(w => w.WriteRecord(null, "x"), options));
	}

	[Fact]
	public void BooleanText()
	{
		var options = new CsvWriterOptions { TrueValue = "yes", FalseValue = "no" };
		var csv = Write(w =>
		{
			w.WriteField(true);
			w.WriteField(false);
			w.WriteField((object)true);
			w.WriteField((bool?)false);
			w.EndRecord();
		}, options);

		Assert.Equal("yes,no,yes,no\r\n", csv);
		Assert.Equal("True,False\r\n", Write(w => { w.WriteField(true); w.WriteField(false); w.EndRecord(); }));
		Assert.Throws<ArgumentNullException>(() => Write(_ => { }, new CsvWriterOptions { TrueValue = null! }));
	}

	[Fact]
	public void RecordsWriteNullAndBooleanTextsTheReaderReadsBack()
	{
		var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, new CsvWriterOptions { NullValue = "NULL" }))
		{
			writer.WriteRecords([new Reading { Name = "A", Value = 1 }, new Reading { Name = "B", Value = 2, Rate = 0.5, Note = "" }]);
		}

		Assert.Equal("Name,Value,Rate,Note\r\nA,1,NULL,NULL\r\nB,2,0.5,\r\n", text.ToString());
		using var reader = CsvReader.FromText(text.ToString(), new CsvReaderOptions { NullValues = ["NULL"] });
		var back = reader.GetRecords<Reading>().ToList();
		Assert.Equal([null, 0.5], back.Select(r => r.Rate));
		Assert.Equal([null, ""], back.Select(r => r.Note));
	}
}
