using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// <see cref="CsvSeparatorStream"/>: reading separators of several characters, checked against the reference parser on
/// random input, across vector widths, compactions, chunked inputs and read sizes.
/// </summary>
public class SeparatorStreamTests
{
	private const char Unit = CsvSeparatorStream.DefaultReplacement;

	// Separators of identical characters can overlap (||| for ||); abab overlaps at 2; ¦ and §§ are not ASCII.
	private static readonly string[] Separators = ["||", "::", "<|>", "abab", "¦", "§§", "|", "~~~", "\t|", "||||"];

	// Record ends: $$ overlaps itself, ¤ is not ASCII, and the others share characters with separators and the text.
	private static readonly string[] RecordEnds = ["~@^*^@~", "<EOR>", "$$", "¤", "|~|", "ab", "\u001E", ":;"];

	private static readonly int[] Compactions = new[]
	{
		CsvSeparatorStream.CompactScalar, CsvSeparatorStream.CompactShuffle, CsvSeparatorStream.CompactPairShuffle,
		CsvSeparatorStream.CompactCompress,
	}.Where(CsvSeparatorStream.CompactionSupported).ToArray();

	public static TheoryData<int> Seeds()
	{
		var data = new TheoryData<int>();
		for (var i = 1; i <= 16; i++) data.Add(i * 7919);

		return data;
	}

	[Fact]
	public void ReadsRecordsWithSeparatorsInQuotedFields()
	{
		var text = "Id||Name||Price\r\n1||\"a||b\"||9.5\r\n2||\"say \"\"hi\"\"||\"||";
		using var reader = CsvReader.Create(Stream(text));

		Assert.Equal(["Id", "Name", "Price"], reader.Headers);
		Assert.Equal([["1", "a||b", "9.5"], ["2", "say \"hi\"||", ""]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void ReplacesOnlySeparatorsOutsideQuotes()
	{
		using var stream = Stream("a||b\n\"c||d\"||e");

		Assert.Equal($"a{Unit}b\n\"c||d\"{Unit}e", ReadAllText(stream));
	}

	[Fact]
	public void TakesOverlappingSeparatorsFromTheLeft()
	{
		using var reader = CsvReader.Create(Stream("a|||b||||c"), new CsvReaderOptions { HasHeader = false });

		Assert.Equal([["a", "|b", "", "c"]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void KeepsEscapedSeparators()
	{
		var options = new CsvReaderOptions { Mode = CsvMode.Escape, HasHeader = false };
		using var reader = CsvReader.Create(Stream("a\\||b||c\\\r\nd"), options);

		Assert.Equal([["a||b", "c\r\nd"]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void IgnoresCommentLines()
	{
		var options = new CsvReaderOptions { CommentPrefix = '#', HasHeader = false };
		using var reader = CsvReader.Create(Stream("﻿#note||\"open\n1||2\n#x||\"\n3||4"), options);

		Assert.Equal([["1", "2"], ["3", "4"]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void PassesSkippedLinesThrough()
	{
		// A quote at the start of a skipped line would otherwise open a field that runs over the records.
		var text = "\"Report||2026\r\r\nId||Name\n1||\"a||b\"\n";
		var options = new CsvReaderOptions { SkipLines = 2 };
		using (var reader = CsvReader.Create(Stream(text), options))
		{
			Assert.Equal(["Id", "Name"], reader.Headers);
			Assert.Equal([["1", "a||b"]], TestUtil.ReadAll(reader));
		}

		// The skipped lines are not changed, wherever the reads of the stream split them.
		for (var seed = 0; seed < 20; seed++)
		{
			var stream = new CsvSeparatorStream(new ChunkedStream(Encoding.UTF8.GetBytes(text), seed, 1 + seed % 3), "||");
			stream.Attach(options);
			Assert.Equal($"\"Report||2026\r\r\nId{Unit}Name\n1{Unit}\"a||b\"\n", ReadAllText(stream));
		}
	}

	[Fact]
	public void OpensQuotesAfterTrimmedWhitespace()
	{
		var options = new CsvReaderOptions { TrimWhitespace = true, HasHeader = false };
		using var reader = CsvReader.Create(Stream("  \"a||b\"  ||c|| \t\"d||\"\"e\"\n"), options);

		Assert.Equal([["a||b", "c", "d||\"e"]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void ReadsNonAsciiAndSingleByteSeparators()
	{
		var options = new CsvReaderOptions { HasHeader = false };
		using (var reader = CsvReader.Create(Stream("a¦b\nc¦\"d¦e\"", "¦"), options))
		{
			Assert.Equal([["a", "b"], ["c", "d¦e"]], TestUtil.ReadAll(reader));
		}

		var latin1 = Encoding.GetEncoding(28591);
		using (var reader = CsvReader.Create(new CsvSeparatorStream(new MemoryStream(latin1.GetBytes("a¦b\r\né¦\"ü¦\"")), "¦"), options with { Encoding = latin1 }))
		{
			Assert.Equal([["a", "b"], ["é", "ü¦"]], TestUtil.ReadAll(reader));
		}
	}

	[Fact]
	public void ThrowsOnTheReplacementOutsideQuotes()
	{
		var options = new CsvReaderOptions { HasHeader = false };
		using (var reader = CsvReader.Create(Stream($"\"a{Unit}b\"||c"), options))
		{
			Assert.Equal([[$"a{Unit}b", "c"]], TestUtil.ReadAll(reader));
		}

		using (var reader = CsvReader.Create(Stream($"x||y\na{Unit}b||c"), options))
		{
			var e = Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader));
			Assert.Equal(6, e.ByteOffset);
		}

		using (var reader = CsvReader.Create(Stream($"a{Unit}b||c", replacement: '\t'), options))
		{
			Assert.Equal([[$"a{Unit}b", "c"]], TestUtil.ReadAll(reader));
		}
	}

	[Fact]
	public void ChecksTheOptions()
	{
		Assert.Throws<ArgumentException>(() => CsvReader.Create(Stream("a||b"), new CsvReaderOptions { Separator = ';' }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create(Stream("a||b"), new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [1] }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create(Stream("a#|b", "#|"), new CsvReaderOptions { CommentPrefix = '#' }));
		Assert.Throws<NotSupportedException>(() => CsvReader.Create(Stream("a||b"), new CsvReaderOptions { Encoding = Encoding.Unicode }));
		using var reader = CsvReader.Create(Stream("a||b"), new CsvReaderOptions { Separator = Unit, HasHeader = false });
		Assert.Equal([["a", "b"]], TestUtil.ReadAll(reader));

		var read = Stream("a||b");
		read.ReadByte();
		Assert.Throws<InvalidOperationException>(() => CsvReader.Create(read));
	}

	[Fact]
	public void ChecksTheSeparator()
	{
		var input = new MemoryStream();
		Assert.Throws<ArgumentNullException>(() => new CsvSeparatorStream(input, null!));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, ""));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "|\n"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "\"|"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "|" + Unit));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, new string('|', 17)));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", replacement: '\n'));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", replacement: 'é'));
	}

	[Fact]
	public void ReadsRecordEnds()
	{
		// A vendor's file: quotes are text, and an LF follows each record end.
		var text = "Id~##~Name~##~Note~@^*^@~\n1~##~WIDGET~##~a \"big\" one~@^*^@~\n2~##~GADGET~##~~@^*^@~\n";
		var options = new CsvReaderOptions { Mode = CsvMode.NoEscape };
		using var reader = CsvReader.Create(Stream(text, "~##~", newLine: "~@^*^@~"), options);

		Assert.Equal(["Id", "Name", "Note"], reader.Headers);
		Assert.Equal([["1", "WIDGET", "a \"big\" one"], ["2", "GADGET", ""]], TestUtil.ReadAll(reader));
	}

	[Fact]
	public void PutsACrInPlaceOfEachRecordEnd()
	{
		// With the line break right after it, whichever it is, or none.
		using (var stream = Stream("a||b<E>\r\nc<E>\nd<E>\re<E>f<E>", newLine: "<E>"))
		{
			Assert.Equal($"a{Unit}b\rc\rd\re\rf\r", ReadAllText(stream));
		}

		// The start of a record end at the end of the input is text, even when the bytes after the input could complete it.
		using (var stream = Stream("a||b<\0", newLine: "<\0\0"))
		{
			Assert.Equal($"a{Unit}b<\0", ReadAllText(stream));
		}

		// Wherever the reads of the stream split them.
		var text = "a||b~@^*^@~\r\nc||d~@^*^@~\re~@^*^@~";
		for (var seed = 0; seed < 20; seed++)
		{
			var stream = new CsvSeparatorStream(new ChunkedStream(Encoding.UTF8.GetBytes(text), seed, 1 + seed % 3), "||", newLine: "~@^*^@~");
			Assert.Equal($"a{Unit}b\rc{Unit}d\re\r", ReadAllText(stream));
		}
	}

	[Fact]
	public void TakesRecordEndsAndSeparatorsFromTheLeft()
	{
		var options = new CsvReaderOptions { HasHeader = false };
		// ~@^*^@~##~ is a record end, then text: the separator that would start at its last ~ is part of it.
		using (var reader = CsvReader.Create(Stream("a~##~b~@^*^@~##~c~##~d", "~##~", newLine: "~@^*^@~"), options))
		{
			Assert.Equal([["a", "b"], ["##~c", "d"]], TestUtil.ReadAll(reader));
		}

		// And the other way around.
		using (var reader = CsvReader.Create(Stream("a~##~@^*^@~b", "~##~", newLine: "~@^*^@~"), options))
		{
			Assert.Equal([["a", "@^*^@~b"]], TestUtil.ReadAll(reader));
		}
	}

	[Fact]
	public void ThrowsOnLineBreaksThatAreNotText()
	{
		var options = new CsvReaderOptions { Mode = CsvMode.NoEscape, HasHeader = false };
		using (var reader = CsvReader.Create(Stream("a||b<E>\nc\nd||e<E>", newLine: "<E>"), options))
		{
			Assert.Equal(9, Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader)).ByteOffset);
		}

		// Only one line break after a record end is part of it.
		using (var reader = CsvReader.Create(Stream("a<E>\n\nb", newLine: "<E>"), options))
		{
			Assert.Equal(5, Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader)).ByteOffset);
		}
	}

	[Fact]
	public void ReadsLineBreaksAndRecordEndsInQuotedFields()
	{
		var options = new CsvReaderOptions { HasHeader = false };
		using (var reader = CsvReader.Create(Stream("1||\"a\r\nb<E>c\"<E>\n2||x", newLine: "<E>"), options))
		{
			Assert.Equal([["1", "a\r\nb<E>c"], ["2", "x"]], TestUtil.ReadAll(reader));
		}

		// When quoted fields cannot have line breaks, a record end ends one that is still open, and a line break throws.
		var strict = options with { AllowLineBreaksInQuotes = false };
		using (var reader = CsvReader.Create(Stream("1||\"a<E>2||x", newLine: "<E>"), strict))
		{
			var records = TestUtil.ReadAll(reader);
			Assert.Equal(2, records.Count);
			Assert.Equal(["2", "x"], records[1]);
		}

		using (var reader = CsvReader.Create(Stream("1||\"a\nb\"<E>", newLine: "<E>"), strict))
		{
			Assert.Equal(5, Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader)).ByteOffset);
		}
	}

	[Fact]
	public void KeepsEscapedLineBreaksAndRecordEnds()
	{
		var options = new CsvReaderOptions { Mode = CsvMode.Escape, HasHeader = false };
		using (var reader = CsvReader.Create(Stream("a\\\r\nb||c\\<E>d<E>\ne", newLine: "<E>"), options))
		{
			Assert.Equal([["a\r\nb", "c<E>d"], ["e"]], TestUtil.ReadAll(reader));
		}

		// An escaped CR right before a record end stays apart from the line break in its place.
		using (var reader = CsvReader.Create(Stream("a\\\r<E>b", newLine: "<E>"), options))
		{
			Assert.Equal([["a\r"], ["b"]], TestUtil.ReadAll(reader));
		}
	}

	[Fact]
	public void EndsCommentLinesWithTheRecordEnd()
	{
		var options = new CsvReaderOptions { CommentPrefix = '#', HasHeader = false };
		using (var reader = CsvReader.Create(Stream("#note||\"x<E>\n1||2<E>\n#y<E>3||4", newLine: "<E>"), options))
		{
			Assert.Equal([["1", "2"], ["3", "4"]], TestUtil.ReadAll(reader));
		}

		// The reader would end the comment at a line break, and read the rest of it as a record.
		using (var reader = CsvReader.Create(Stream("#note\n1||2<E>", newLine: "<E>"), options))
		{
			Assert.Equal(5, Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader)).ByteOffset);
		}
	}

	[Fact]
	public void PassesSkippedLinesThroughWithRecordEnds()
	{
		// The lines the reader skips end with line breaks, as they do without a record end.
		var options = new CsvReaderOptions { SkipLines = 1 };
		using (var reader = CsvReader.Create(Stream("Report<E>\r\nId||Name<E>\r\n1||x<E>\r\n", newLine: "<E>"), options))
		{
			Assert.Equal(["Id", "Name"], reader.Headers);
			Assert.Equal([["1", "x"]], TestUtil.ReadAll(reader));
		}

		// A skipped line that ends with a CR, then a record end: an empty record, not one CR LF line break.
		var keepEmpty = new CsvReaderOptions { SkipLines = 1, HasHeader = false, SkipEmptyLines = false };
		using (var reader = CsvReader.Create(Stream("Report\r<E>a||b<E>", newLine: "<E>"), keepEmpty))
		{
			Assert.Equal([[""], ["a", "b"]], TestUtil.ReadAll(reader));
		}
	}

	[Fact]
	public void ChecksTheRecordEnd()
	{
		var input = new MemoryStream();
		Assert.Throws<ArgumentNullException>(() => new CsvSeparatorStream(input, "||", newLine: null!));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: ""));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: "<E>\n"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: "\"E"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: "E" + Unit));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: new string('E', 17)));
		// Where one starts with the other, both would match.
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: "||"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "||", newLine: "|"));
		Assert.Throws<ArgumentException>(() => new CsvSeparatorStream(input, "|", newLine: "|~|"));
		// The reader's escape and comment characters.
		Assert.Throws<ArgumentException>(() => CsvReader.Create(Stream("a||b", newLine: "^E"), new CsvReaderOptions { Mode = CsvMode.Escape, Escape = '^' }));
		Assert.Throws<ArgumentException>(() => CsvReader.Create(Stream("a||b", newLine: "#E"), new CsvReaderOptions { CommentPrefix = '#' }));

		Assert.Equal("~@^*^@~", new CsvSeparatorStream(input, "~##~", newLine: "~@^*^@~").NewLine);
		Assert.Null(new CsvSeparatorStream(input, "||").NewLine);
	}

	[Fact]
	public void RejectsUtf16Input()
	{
		using var reader = CsvReader.Create(new CsvSeparatorStream(new MemoryStream(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("a||b")).ToArray()), "||"));

		Assert.Throws<NotSupportedException>(() => reader.Read());
	}

	[Fact]
	public void DisposesTheStreamUnlessLeftOpen()
	{
		var input = new MemoryStream([1, 2, 3]);
		new CsvSeparatorStream(input, "||", leaveOpen: true).Dispose();
		Assert.True(input.CanRead);

		var stream = new CsvSeparatorStream(input, "||");
		stream.Dispose();
		Assert.False(input.CanRead);
		Assert.False(stream.CanRead);
		Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
	}

	[Fact]
	public async Task ReadsAsynchronously()
	{
		var text = string.Concat(Enumerable.Range(0, 2000).Select(i => $"{i}||\"x||{i}\"||y\r\n"));
		await using var reader = CsvReader.Create(new CsvSeparatorStream(new ChunkedStream(Encoding.UTF8.GetBytes(text), 1, 300, yieldAsync: true), "||"),
			new CsvReaderOptions { HasHeader = false, BufferSize = 1024 });
		var records = await TestUtil.ReadAllAsync(reader);

		Assert.Equal(2000, records.Count);
		Assert.Equal(["1999", "x||1999", "y"], records[1999]);
	}

	[Fact]
	public void ReadsInParallel()
	{
		var text = string.Concat(Enumerable.Range(0, 20000).Select(i => $"{i}||\"x||{i}\"\n"));
		using var reader = CsvReader.Create(Stream(text), new CsvReaderOptions { HasHeader = false });
		var values = reader.ParallelEnumerate(r => r.GetString(1)).ToList();

		Assert.Equal(Enumerable.Range(0, 20000).Select(i => $"x||{i}"), values);
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public void RandomCharacters(int seed) => Run(seed, iterations: 60, structured: false, ends: false);

	[Theory]
	[MemberData(nameof(Seeds))]
	public void StructuredRecords(int seed) => Run(seed, iterations: 60, structured: true, ends: false);

	[Theory]
	[MemberData(nameof(Seeds))]
	public void RandomCharactersWithRecordEnds(int seed) => Run(seed, iterations: 60, structured: false, ends: true);

	[Theory]
	[MemberData(nameof(Seeds))]
	public void StructuredRecordsWithRecordEnds(int seed) => Run(seed, iterations: 60, structured: true, ends: true);

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task AsyncChunkedStreams(int seed)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < 30; iter++)
		{
			var (o, text) = RandomInput(rng, structured: rng.Next(2) == 0, ends: rng.Next(2) == 0);
			var ctx = $"async, seed {seed}, iter {iter}, {o}, text {TestUtil.Escape(text)}";
			var expected = Expected(text, o, out var errorOffset);
			var input = new ChunkedStream(Encoding.UTF8.GetBytes(text), rng.Next(), rng.Next(1, 300), yieldAsync: true);
			await using var reader = CsvReader.Create(Create(input, o.SeparatorText!, o.NewLineText), ReaderOptions(rng, o));
			if (expected is null) Assert.Equal(errorOffset, (await Assert.ThrowsAsync<CsvFormatException>(() => TestUtil.ReadAllAsync(reader))).ByteOffset);
			else TestUtil.AssertSame(expected, await TestUtil.ReadAllAsync(reader), ctx);
		}
	}

	private static void Run(int seed, int iterations, bool structured, bool ends)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < iterations; iter++)
		{
			var (o, text) = RandomInput(rng, structured, ends);
			var ctx = $"seed {seed}, iter {iter}, {o}, text {TestUtil.Escape(text)}";
			var expected = Expected(text, o, out var errorOffset);
			var bytes = Encoding.UTF8.GetBytes(text);
			var readerOptions = ReaderOptions(rng, o);

			// The exact path, read in one go, against the reference parser.
			var exact = Configured(new MemoryStream(bytes), o, readerOptions, width: 0, CsvSeparatorStream.CompactScalar);
			using (var reader = CsvReader.Create(exact, readerOptions))
			{
				AssertRead(expected, errorOffset, reader, $"exact, {ctx}");
			}

			// Every width and compaction, in random chunks and read sizes, gives the same bytes, or the same error.
			var reference = ReadAllBytes(Configured(new MemoryStream(bytes), o, readerOptions, width: 0, CsvSeparatorStream.CompactScalar), rng, small: false, out var error);
			Assert.True((expected is null) == (error is not null), $"exact stream error {error?.Message}, {ctx}");
			if (error is not null) Assert.True(error.ByteOffset == errorOffset, $"exact stream error at byte {error.ByteOffset}, expected {errorOffset}, {ctx}");
			foreach (var width in TestUtil.Widths)
			{
				foreach (var compaction in Compactions)
				{
					var stream = Configured(new ChunkedStream(bytes, rng.Next(), rng.Next(1, 400)), o, readerOptions, width, compaction);
					var small = rng.Next(4) == 0;
					var actual = ReadAllBytes(stream, rng, small, out var actualError);
					var configuration = $"width {width}, compaction {compaction}, small reads {small}";
					if (error is not null)
					{
						Assert.True(actualError is not null, $"{configuration}: no error, expected one at byte {error.ByteOffset}, {ctx}");
						Assert.True(error.ByteOffset == actualError.ByteOffset,
							$"{configuration}: error at byte {actualError.ByteOffset}, expected {error.ByteOffset}, {ctx}");
					}
					else if (actualError is not null || !reference.SequenceEqual(actual))
					{
						var at = 0;
						while (at < Math.Min(reference.Length, actual.Length) && reference[at] == actual[at]) at++;
						Assert.Fail($"{configuration}: output differs at byte {at} (expected {reference.Length} bytes, got {actual.Length}, " +
							$"error {actualError?.Message}), {ctx}");
					}
				}
			}

			{
				using var reader = CsvReader.Create(Create(new ChunkedStream(bytes, rng.Next(), rng.Next(1, 400)), o.SeparatorText!, o.NewLineText), readerOptions);
				AssertRead(expected, errorOffset, reader, $"chunked, {ctx}");
			}
		}
	}

	/// <summary>
	/// The records of the reference parser, or <see langword="null"/> when the text has a line break that is not text, with
	/// the byte offset of the first one in <paramref name="errorOffset"/>.
	/// </summary>
	private static List<string[]>? Expected(string text, ReferenceOptions o, out long errorOffset)
	{
		errorOffset = -1;
		try
		{
			return ReferenceCsv.Parse(text, o);
		}
		catch (StrayLineBreakException e)
		{
			// Its index is in the text after the skipped lines.
			var skipped = text.Length - ReferenceCsv.AfterLines(text, o.SkipLines).Length;
			errorOffset = Encoding.UTF8.GetByteCount(text.Substring(0, skipped + e.Index));

			return null;
		}
	}

	/// <summary>
	/// Reads all records, and checks them against <paramref name="expected"/>, or, when it is <see langword="null"/>, that
	/// reading throws for the line break at <paramref name="errorOffset"/>.
	/// </summary>
	private static void AssertRead(List<string[]>? expected, long errorOffset, CsvReader reader, string context)
	{
		if (expected is not null)
		{
			TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), context);

			return;
		}

		try
		{
			TestUtil.ReadAll(reader);
		}
		catch (CsvFormatException e)
		{
			Assert.True(e.ByteOffset == errorOffset, $"error at byte {e.ByteOffset}, expected {errorOffset}, {context}");

			return;
		}

		Assert.Fail($"no error, expected a line break that is not text at byte {errorOffset}, {context}");
	}

	/// <summary>Random options and text (from the reader's fuzz tests), with a separator of several characters, and a record end or not.</summary>
	private static (ReferenceOptions Options, string Text) RandomInput(Random rng, bool structured, bool ends)
	{
		var separator = Separators[rng.Next(Separators.Length)];
		var o = FuzzTests.RandomOptions(rng);
		// The generators write o.Separator; a placeholder becomes the separator.
		var placeholder = o with { Separator = '\u0002' };
		var text = new StringBuilder(structured ? FuzzTests.StructuredText(rng, placeholder) : FuzzTests.RandomText(rng, placeholder));
		// Characters of the separator on their own, to make partial and overlapping separators.
		var extra = rng.Next(text.Length / 8 + 1);
		for (var i = 0; i < extra; i++)
		{
			var at = rng.Next(text.Length + 1);
			if (at > 0 && at < text.Length && char.IsLowSurrogate(text[at])) at++;   // not inside a surrogate pair
			text.Insert(at, separator[rng.Next(separator.Length)]);
		}

		o = o with { SeparatorText = separator };
		if (!ends) return (o, text.Replace("\u0002", separator).ToString());

		// A record end that can be used with the separator and the options.
		string end;
		do
		{
			end = RecordEnds[rng.Next(RecordEnds.Length)];
		} while (end.StartsWith(separator, StringComparison.Ordinal) || separator.StartsWith(end, StringComparison.Ordinal) ||
				 end.IndexOfAny([o.Quote ?? '\0', o.Escape ?? '\0', o.Comment ?? '\0']) >= 0);

		return (o with { NewLineText = end }, WithRecordEnds(rng, text.ToString(), end).Replace("\u0002", separator));
	}

	/// <summary>
	/// The text with <paramref name="end"/> in place of its line breaks, each with a line break after it or not. In a third
	/// of the texts, some line breaks are kept, which are text in quoted fields and errors elsewhere.
	/// </summary>
	private static string WithRecordEnds(Random rng, string text, string end)
	{
		string[] after = ["", "\n", "\r\n", "\r"];
		var usual = after[rng.Next(after.Length)];
		var kept = rng.Next(3) == 0 ? 10 : 0;
		var sb = new StringBuilder(text.Length * 2);
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c != '\r' && c != '\n')
			{
				sb.Append(c);
				continue;
			}

			var lineBreak = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : c.ToString();
			i += lineBreak.Length - 1;
			if (rng.Next(100) < kept) sb.Append(lineBreak);
			else sb.Append(end).Append(rng.Next(10) == 0 ? after[rng.Next(after.Length)] : usual);
		}

		// Characters of the record end on their own, to make partial and overlapping record ends, in half of the texts (one
		// inside a record end makes the line break after it an error).
		var extra = rng.Next(2) == 0 ? 0 : rng.Next(sb.Length / 32 + 1);
		for (var k = 0; k < extra; k++)
		{
			var at = rng.Next(sb.Length + 1);
			if (at > 0 && at < sb.Length && char.IsLowSurrogate(sb[at])) at++;   // not inside a surrogate pair
			sb.Insert(at, end[rng.Next(end.Length)]);
		}

		return sb.ToString();
	}

	private static CsvReaderOptions ReaderOptions(Random rng, ReferenceOptions o) =>
		o.ToReaderOptions(bufferSize: 256 << rng.Next(5)) with { Separator = null };

	/// <summary>A stream as the reader would configure it, with a given width and compaction.</summary>
	private static CsvSeparatorStream Configured(Stream input, ReferenceOptions o, CsvReaderOptions readerOptions, int width, int compaction)
	{
		var stream = Create(input, o.SeparatorText!, o.NewLineText);
		stream.vectorWidth = width;
		stream.compaction = compaction;
		stream.Attach(readerOptions);

		return stream;
	}

	/// <summary>Reads a stream to its end, or to the format error it throws.</summary>
	private static byte[] ReadAllBytes(Stream stream, Random rng, bool small, out CsvFormatException? error)
	{
		var output = new MemoryStream();
		var buffer = new byte[70000];
		error = null;
		while (true)
		{
			var size = small ? rng.Next(1, 300) : rng.Next(256, 66000);
			var offset = rng.Next(buffer.Length - size + 1);
			int n;
			try
			{
				n = stream.Read(buffer, offset, size);
			}
			catch (CsvFormatException e)
			{
				error = e;
				break;
			}

			if (n == 0) break;
			Assert.InRange(n, 1, size);
			output.Write(buffer, offset, n);
		}

		return output.ToArray();
	}

	private static string ReadAllText(Stream stream)
	{
		using var reader = new StreamReader(stream, Encoding.UTF8);

		return reader.ReadToEnd();
	}

	private static CsvSeparatorStream Stream(string text, string separator = "||", char replacement = Unit, string? newLine = null) =>
		Create(new MemoryStream(Encoding.UTF8.GetBytes(text)), separator, newLine, replacement);

	private static CsvSeparatorStream Create(Stream input, string separator, string? newLine, char replacement = Unit) =>
		newLine is null ? new(input, separator, replacement) : new(input, separator, newLine, replacement);
}
