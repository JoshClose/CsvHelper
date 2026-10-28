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
	public void RandomCharacters(int seed) => Run(seed, iterations: 60, structured: false);

	[Theory]
	[MemberData(nameof(Seeds))]
	public void StructuredRecords(int seed) => Run(seed, iterations: 60, structured: true);

	[Theory]
	[MemberData(nameof(Seeds))]
	public async Task AsyncChunkedStreams(int seed)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < 30; iter++)
		{
			var (o, text) = RandomInput(rng, structured: rng.Next(2) == 0);
			var expected = ReferenceCsv.Parse(text, o);
			var input = new ChunkedStream(Encoding.UTF8.GetBytes(text), rng.Next(), rng.Next(1, 300), yieldAsync: true);
			await using var reader = CsvReader.Create(new CsvSeparatorStream(input, o.SeparatorText!), ReaderOptions(rng, o));
			TestUtil.AssertSame(expected, await TestUtil.ReadAllAsync(reader), $"async, seed {seed}, iter {iter}, {o}, text {TestUtil.Escape(text)}");
		}
	}

	private static void Run(int seed, int iterations, bool structured)
	{
		var rng = new Random(seed);
		for (var iter = 0; iter < iterations; iter++)
		{
			var (o, text) = RandomInput(rng, structured);
			var ctx = $"seed {seed}, iter {iter}, {o}, text {TestUtil.Escape(text)}";
			var expected = ReferenceCsv.Parse(text, o);
			var bytes = Encoding.UTF8.GetBytes(text);
			var readerOptions = ReaderOptions(rng, o);

			// The exact path, read in one go, against the reference parser.
			var exact = Configured(new MemoryStream(bytes), o, readerOptions, width: 0, CsvSeparatorStream.CompactScalar);
			using (var reader = CsvReader.Create(exact, readerOptions))
			{
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"exact, {ctx}");
			}

			// Every width and compaction, in random chunks and read sizes, gives the same bytes.
			var reference = ReadAllBytes(Configured(new MemoryStream(bytes), o, readerOptions, width: 0, CsvSeparatorStream.CompactScalar), rng, small: false);
			foreach (var width in TestUtil.Widths)
			{
				foreach (var compaction in Compactions)
				{
					var stream = Configured(new ChunkedStream(bytes, rng.Next(), rng.Next(1, 400)), o, readerOptions, width, compaction);
					var small = rng.Next(4) == 0;
					var actual = ReadAllBytes(stream, rng, small);
					if (!reference.SequenceEqual(actual))
					{
						var at = 0;
						while (at < Math.Min(reference.Length, actual.Length) && reference[at] == actual[at]) at++;
						Assert.Fail($"width {width}, compaction {compaction}, small reads {small}: output differs at byte {at} " +
							$"(expected {reference.Length} bytes, got {actual.Length}), {ctx}");
					}
				}
			}

			{
				using var reader = CsvReader.Create(new CsvSeparatorStream(new ChunkedStream(bytes, rng.Next(), rng.Next(1, 400)), o.SeparatorText!), readerOptions);
				TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"chunked, {ctx}");
			}
		}
	}

	/// <summary>Random options and text (from the reader's fuzz tests), with a separator of several characters.</summary>
	private static (ReferenceOptions Options, string Text) RandomInput(Random rng, bool structured)
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

		return (o with { SeparatorText = separator }, text.Replace("\u0002", separator).ToString());
	}

	private static CsvReaderOptions ReaderOptions(Random rng, ReferenceOptions o) =>
		o.ToReaderOptions(bufferSize: 256 << rng.Next(5)) with { Separator = null };

	/// <summary>A stream as the reader would configure it, with a given width and compaction.</summary>
	private static CsvSeparatorStream Configured(Stream input, ReferenceOptions o, CsvReaderOptions readerOptions, int width, int compaction)
	{
		var stream = new CsvSeparatorStream(input, o.SeparatorText!) { vectorWidth = width, compaction = compaction };
		stream.Attach(readerOptions);

		return stream;
	}

	private static byte[] ReadAllBytes(Stream stream, Random rng, bool small)
	{
		var output = new MemoryStream();
		var buffer = new byte[70000];
		while (true)
		{
			var size = small ? rng.Next(1, 300) : rng.Next(256, 66000);
			var offset = rng.Next(buffer.Length - size + 1);
			var n = stream.Read(buffer, offset, size);
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

	private static CsvSeparatorStream Stream(string text, string separator = "||", char replacement = Unit) =>
		new(new MemoryStream(Encoding.UTF8.GetBytes(text)), separator, replacement);
}
