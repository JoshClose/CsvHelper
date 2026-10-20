using System.Text;

namespace CsvHelper.Tests;

[CsvRecord]
public partial class FixedLine
{
	[CsvColumn(Index = 0)]
	public string Code { get; set; } = "";
	[CsvColumn(Index = 1)]
	public int Quantity { get; set; }
	[CsvColumn(Index = 2)]
	public decimal Price { get; set; }
}

/// <summary>The Escape, NoEscape and FixedWidth modes of the reader.</summary>
public class ModeTests
{
	private static readonly CsvReaderOptions Escaped = new() { HasHeader = false, Mode = CsvMode.Escape, Separator = ',' };

	private static void Expect(string text, CsvReaderOptions options, params string[][] expected)
	{
		foreach (var width in TestUtil.Widths)
		{
			using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), options);
			reader.vectorWidth = width;
			TestUtil.AssertSame(expected.ToList(), TestUtil.ReadAll(reader), $"width {width}, input {TestUtil.Escape(text)}");
		}

		if (options.Mode == CsvMode.FixedWidth)
		{
			var widths = options.FieldWidths!.ToArray();
			var reference = ReferenceFixedWidth.Parse(text, widths, options.TrimWhitespace ?? true, options.SkipEmptyLines, options.CommentPrefix);
			TestUtil.AssertSame(expected.ToList(), reference, $"reference, input {TestUtil.Escape(text)}");
		}
		else
		{
			// The reference parser must agree with the hand-written expectation too.
			var reference = ReferenceCsv.Parse(text, new ReferenceOptions
			{
				Separator = options.Separator ?? ',',
				Quote = options.Mode == CsvMode.Rfc4180 ? options.Quote : null,
				Escape = options.Mode == CsvMode.Escape ? options.Escape : null,
				Trim = options.TrimWhitespace ?? false,
				Comment = options.CommentPrefix,
				SkipEmptyLines = options.SkipEmptyLines,
			});
			TestUtil.AssertSame(expected.ToList(), reference, $"reference, input {TestUtil.Escape(text)}");
		}
	}

	// ----- Escape -----

	[Fact]
	public void EscapedSeparatorsAndEscapes() => Expect("a\\,b,c\\\\d,e\\nf\n1,2", Escaped, ["a,b", "c\\d", "enf"], ["1", "2"]);
	[Fact]
	public void EscapedLf() => Expect("a\\\nb,c\nd", Escaped, ["a\nb", "c"], ["d"]);
	[Fact]
	public void EscapedCrLf() => Expect("a\\\r\nb,c\r\nd", Escaped, ["a\r\nb", "c"], ["d"]);
	[Fact]
	public void EscapedCr() => Expect("a\\\rb,c\rd", Escaped, ["a\rb", "c"], ["d"]);
	[Fact]
	public void EscapedCrThenEscapedLf() => Expect("a\\\r\\\nb", Escaped, ["a\r\nb"]);
	[Fact]
	public void QuotesAreText() => Expect("\"a,b\",\"\"", Escaped, ["\"a", "b\"", "\"\""]);
	[Fact]
	public void TrailingEscapeIsKept() => Expect("a,b\\", Escaped, ["a", "b\\"]);
	[Fact]
	public void OtherEscapeCharacter() => Expect("a^,b,^^c,\\", Escaped with { Escape = '^' }, ["a,b", "^c", "\\"]);
	[Fact]
	public void EscapedFieldOnItsOwn() => Expect("\\,\n\\\\\n,\\x", Escaped, [","], ["\\"], ["", "x"]);

	[Fact]
	public void TrimKeepsEscapedWhitespace() =>
		Expect("  \\ a\\  ,  b  ,\\\t", Escaped with { TrimWhitespace = true }, [" a ", "b", "\t"]);

	[Fact]
	public void EscapesHaveNoEffectInComments() =>
		Expect("#a\\\nb,\\#c\n#\\,d\ne", Escaped with { CommentPrefix = '#' }, ["b", "#c"], ["e"]);

	[Fact]
	public void EscapedLineBreakIsNotEmptyLine() => Expect("\\\n\n\\\r\n", Escaped, ["\n"], ["\r\n"]);

	[Theory]
	[InlineData("\\,")]
	[InlineData("\\\n")]
	[InlineData("\\\r\n")]
	[InlineData("\\\\\\,")]
	public void EscapesAtBlockEdges(string escaped)
	{
		// The escape (or its run) ends at every position around the end of the first and second 64 byte blocks.
		for (var pad = 55; pad < 135; pad++)
		{
			var text = new string('x', pad) + escaped + "y,z\n" + new string('w', pad % 7) + escaped + escaped + ",q";
			var expected = ReferenceCsv.Parse(text, new ReferenceOptions { Quote = null, Escape = '\\' });
			foreach (var width in TestUtil.Widths)
			{
				using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), Escaped);
				reader.vectorWidth = width;
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"pad {pad}, width {width}");
			}
		}
	}

	[Fact]
	public void TrailingEscapeIsReported()
	{
		var reports = new List<CsvMalformedRecord>();
		Assert.Equal([["a"], ["b\\"]], TestUtil.ReadText("a\nb\\", Escaped with { OnMalformedRecord = reports.Add }));
		var report = Assert.Single(reports);
		Assert.Equal(CsvMalformedReason.TrailingEscape, report.Reason);
		Assert.Equal(2, report.RecordNumber);
		Assert.Equal(3, report.ByteOffset);
		Assert.Equal("b\\", report.RawRecord);

		Assert.Equal([["a"]], TestUtil.ReadText("a\nb\\", Escaped with { MalformedData = CsvMalformedDataHandling.Skip }));

		using var reader = CsvReader.Create("a\nb\\"u8.ToArray(), Escaped with { MalformedData = CsvMalformedDataHandling.Throw });
		Assert.True(reader.Read());
		var ex = Assert.Throws<CsvFormatException>(() => reader.Read());
		Assert.Equal(2, ex.RecordNumber);
	}

	[Fact]
	public void DetectsSeparatorIgnoringEscapedCharacters()
	{
		using var reader = CsvReader.Create("a\\,b\\,c;d\n1\\,2\\,3;4\n"u8.ToArray(), new CsvReaderOptions { Mode = CsvMode.Escape });
		Assert.Equal(';', reader.Separator);
		Assert.Equal(["a,b,c", "d"], reader.Headers);
	}

	[Fact]
	public void EscapeCannotBeASeparatorCandidate()
	{
		using var reader = CsvReader.Create("a|b;c|d;e\n"u8.ToArray(), new CsvReaderOptions { Mode = CsvMode.Escape, Escape = '|', HasHeader = false });
		Assert.Equal(';', reader.Separator);
		Assert.True(reader.Read());
		Assert.Equal(["ab", "cd", "e"], TestUtil.Record(reader));
	}

	[Fact]
	public void EscapeOptionsAreValidated()
	{
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Escaped with { Escape = ',' }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Escaped with { Escape = ' ' }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Escaped with { Escape = 'é' }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Escaped with { CommentPrefix = '\\' }));
		Assert.Throws<ArgumentOutOfRangeException>(() => CsvReader.Create([], Escaped with { Mode = (CsvMode)9 }));
		// The escape character does not matter in the other modes, and the quote does not matter here.
		using var _ = CsvReader.Create([], new CsvReaderOptions { Escape = ' ', Separator = ',' });
		using var __ = CsvReader.Create([], Escaped with { Quote = ',' });
	}

	// ----- NoEscape -----

	[Fact]
	public void NoEscapeReadsAsIs() =>
		Expect("\"a,b\"\\,c\n\"\"", new CsvReaderOptions { HasHeader = false, Mode = CsvMode.NoEscape, Separator = ',' },
			["\"a", "b\"\\", "c"], ["\"\""]);

	// ----- FixedWidth -----

	private static CsvReaderOptions Fixed(params int[] widths) => new() { HasHeader = false, Mode = CsvMode.FixedWidth, FieldWidths = widths };

	[Fact]
	public void FixedColumns() => Expect("abc12345xy\nde 1    z \n", Fixed(3, 5, 2), ["abc", "12345", "xy"], ["de", "1", "z"]);
	[Fact]
	public void FixedShortAndLongLines() => Expect("ab\nabcdefgh\nabcde", Fixed(3, 2), ["ab", ""], ["abc", "de"], ["abc", "de"]);
	[Fact]
	public void FixedLineEndings() => Expect("ab\r\ncd\ref\n\ngh", Fixed(1, 1), ["a", "b"], ["c", "d"], ["e", "f"], ["g", "h"]);
	[Fact]
	public void FixedEmptyLinesKept() => Expect("ab\n\n", Fixed(1, 1) with { SkipEmptyLines = false }, ["a", "b"], ["", ""]);
	[Fact]
	public void FixedBlankLinesSkipped() => Expect("ab\n    \n\t\ncd", Fixed(1, 1), ["a", "b"], ["c", "d"]);
	[Fact]
	public void FixedComments() => Expect("#x\nab\n#\n", Fixed(1, 1) with { CommentPrefix = '#' }, ["a", "b"]);
	[Fact]
	public void FixedOneColumn() => Expect("abc\r\n d\r\n  d", Fixed(2), ["ab"], ["d"], [""]);
	[Fact]
	public void FixedSeparatorsAndQuotesAreText() => Expect("a,\"b;c\"", Fixed(2, 3, 3), ["a,", "\"b;", "c\""]);
	[Fact]
	public void FixedMultibyte() => Expect("é€x\n日本語", Fixed(1, 1, 1), ["é", "€", "x"], ["日", "本", "語"]);

	[Fact]
	public void FixedSurrogatePairsCountTwo()
	{
		Expect("a😀b", Fixed(1, 2, 1), ["a", "😀", "b"]);
		// A boundary inside a pair moves after it: the first column takes the whole pair, and the next one is empty.
		Expect("a😀bc", Fixed(2, 1, 1), ["a😀", "", "b"]);
		Expect("ab😀", Fixed(2, 1), ["ab", "😀"]);
	}

	[Fact]
	public void FixedPaddingKeptWithoutTrimming() =>
		Expect(" a  b ", Fixed(3, 3) with { TrimWhitespace = false }, [" a ", " b "]);

	[Fact]
	public void FixedRawAccess()
	{
		using var reader = CsvReader.Create(" ab  12xyz\nq"u8.ToArray(), Fixed(4, 3));
		Assert.Equal('\0', reader.Separator);
		Assert.True(reader.Read());
		Assert.Equal(2, reader.FieldCount);
		Assert.Equal("ab", reader.GetString(0));
		Assert.Equal("12", reader.GetString(1));
		Assert.Equal(" ab ", Encoding.UTF8.GetString(reader.GetRawSpan(0)));
		Assert.Equal(" 12", Encoding.UTF8.GetString(reader.GetRawSpan(1)));
		Assert.Equal(" ab  12xyz", Encoding.UTF8.GetString(reader.RawRecord));
		Assert.Equal(12, reader.GetInt32(1));
		Assert.True(reader.Read());
		Assert.Equal(["q", ""], TestUtil.Record(reader));
		Assert.Equal("", Encoding.UTF8.GetString(reader.GetRawSpan(1)));
		Assert.False(reader.Read());
	}

	[Fact]
	public void FixedHeader()
	{
		using var reader = CsvReader.Create("Id  Name  \r\n1   Bob   \r\n22  Amy\r\n"u8.ToArray(),
			new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [4, 6] });
		Assert.Equal(["Id", "Name"], reader.Headers);
		Assert.True(reader.Read());
		Assert.Equal("Bob", reader.GetString("name"));
		Assert.True(reader.Read());
		Assert.Equal(22, reader.Parse("Id", int.Parse));
		Assert.Equal("Amy", reader.GetString("Name"));
		Assert.False(reader.Read());
	}

	[Fact]
	public void FixedRecords()
	{
		using var reader = CsvReader.Create("A1   3  2.50\nB22 10 10.00\n"u8.ToArray(), Fixed(4, 3, 5));
		var lines = reader.GetRecords<FixedLine>().ToList();
		Assert.Equal(2, lines.Count);
		Assert.Equal(("A1", 3, 2.50m), (lines[0].Code, lines[0].Quantity, lines[0].Price));
		Assert.Equal(("B22", 10, 10.00m), (lines[1].Code, lines[1].Quantity, lines[1].Price));
	}

	[Fact]
	public void FixedLatin1()
	{
		// A single-byte file: one byte per character, transcoded to UTF-8 by the reader.
		var latin1 = Encoding.GetEncoding(28591);
		var text = "àé  ç\nü ö  ";
		using var reader = CsvReader.Create(new MemoryStream(latin1.GetBytes(text)), Fixed(2, 2, 1) with { Encoding = latin1 });
		Assert.Equal([["àé", "", "ç"], ["ü", "ö", ""]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void FixedManyColumns()
	{
		// More columns than a 64 byte block has bytes, on short lines: the index must grow.
		var widths = Enumerable.Repeat(1, 300).ToArray();
		var text = string.Concat(Enumerable.Range(0, 200).Select(i => new string((char)('a' + i % 26), i % 5) + "\n"));
		var expected = ReferenceFixedWidth.Parse(text, widths, true, false, null);
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(text), 1, 7), Fixed(widths) with { SkipEmptyLines = false, BufferSize = 256 });
		TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), "300 columns");
	}

	[Fact]
	public void FixedLongLines()
	{
		// Lines much longer than the buffer and the scan step.
		var text = new string('a', 100_000) + "\r\n" + new string('b', 40_000) + "\r" + "c";
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(text), 2, 999), Fixed(3, 99_990, 5) with { BufferSize = 256 });
		Assert.Equal(
			[["aaa", new string('a', 99_990), "aaaaa"], ["bbb", new string('b', 39_997), ""], ["c", "", ""]],
			TestUtil.ReadAll(reader));
	}

	[Fact]
	public void FixedParallel()
	{
		var rng = new Random(5);
		int[] widths = [3, 7, 1, 12];
		var sb = new StringBuilder();
		while (sb.Length < 1_500_000) sb.Append(FuzzTests.FixedWidthText(rng, widths)).Append('\n');
		var text = sb.ToString();
		var expected = ReferenceFixedWidth.Parse(text, widths, true, true, null);
		var options = Fixed(widths) with { BufferSize = 4096 };
		foreach (var reader in new[] { CsvReader.FromText(text, options), CsvReader.Create(Encoding.UTF8.GetBytes(text), options) })
		{
			using (reader)
			{
				var results = reader.ParallelEnumerate(TestUtil.Record, 4).ToList();
				TestUtil.AssertSame(expected, results, "parallel");
			}
		}
	}

	[Fact]
	public void FixedOptionsAreValidated()
	{
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], new CsvReaderOptions { Mode = CsvMode.FixedWidth }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Fixed()));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Fixed(3, 0)));
		Assert.Throws<ArgumentException>(() => CsvReader.Create([], Fixed(int.MaxValue, 1)));
		// Separator, quote and escape do not matter in FixedWidth mode.
		using var _ = CsvReader.Create([], Fixed(1) with { Separator = '"', Escape = ' ' });
	}
}
