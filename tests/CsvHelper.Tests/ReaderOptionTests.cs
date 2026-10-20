using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// Reader options added for messy and hostile input: trimming inside quotes and custom whitespace, MaxRecordSize,
/// ByteOffset and LineNumber, values left out of exceptions, null and boolean texts, header names (normalized,
/// duplicated, replaced mid-file) and the skip predicate.
/// </summary>
public class ReaderOptionTests
{
	private static readonly CsvReaderOptions NoHeader = new() { HasHeader = false };

	private static List<string> Lines(string text, CsvReaderOptions options) =>
		TestUtil.ReadText(text, options).Select(r => string.Join("|", r)).ToList();

	[Fact]
	public void TrimInsideQuotes()
	{
		var csv = "\" a \",\"\tb\"\" \", c ,\"\"\n";

		Assert.Equal([" a |\tb\" | c |"], Lines(csv, NoHeader));
		Assert.Equal(["a|b\"| c |"], Lines(csv, NoHeader with { TrimInsideQuotes = true }));
		// Outside and inside the quotes.
		Assert.Equal(["a|b\"|c|"], Lines("  \" a \" ,\"\tb\"\" \", c ,\"\"\n", NoHeader with { TrimInsideQuotes = true, TrimWhitespace = true }));
		// Only Rfc4180 mode has quotes.
		Assert.Equal(["\" a \"|x"], Lines("\" a \",x", NoHeader with { Mode = CsvMode.NoEscape, TrimInsideQuotes = true }));
	}

	[Fact]
	public void CustomWhitespace()
	{
		var options = NoHeader with { TrimWhitespace = true, WhitespaceCharacters = " \t_" };

		Assert.Equal(["a|b|c d"], Lines("__a_ ,\t b__,_c d_\n", options));
		// A quote after leading whitespace still opens a quoted field; whitespace after the closing quote is dropped.
		Assert.Equal(["x,y|z"], Lines("__\"x,y\"__,z\n", options));
		// Lines of only whitespace are empty lines.
		Assert.Equal(["a", "b"], Lines("a\n__ _\nb\n", options));
		// Space and tab are no longer whitespace when the characters leave them out.
		Assert.Equal([" a\t| b"], Lines("_ a\t_,_ b\n", options with { WhitespaceCharacters = "_" }));
		// No characters: nothing is trimmed.
		Assert.Equal([" a "], Lines(" a \n", options with { WhitespaceCharacters = "" }));
		Assert.Equal(["a"], Lines("\"_a_\"\n", NoHeader with { TrimInsideQuotes = true, WhitespaceCharacters = "_" }));
	}

	[Theory]
	[InlineData("\r")]
	[InlineData("\n")]
	[InlineData("\"")]
	[InlineData("é")]
	public void RejectsInvalidWhitespaceCharacters(string characters) =>
		Assert.ThrowsAny<ArgumentException>(() => CsvReader.Create([], NoHeader with { WhitespaceCharacters = characters }));

	[Fact]
	public void WhitespaceCanBeTheQuoteOfAnotherMode()
	{
		using var reader = CsvReader.FromText("\"a\"\n", NoHeader with { Mode = CsvMode.Escape, TrimWhitespace = true, WhitespaceCharacters = "\"" });
		Assert.True(reader.Read());
		Assert.Equal("a", reader.GetString(0));
		Assert.ThrowsAny<ArgumentException>(() => CsvReader.Create([], NoHeader with { Mode = CsvMode.Escape, WhitespaceCharacters = "\\" }));
	}

	[Fact]
	public void MaxRecordSizeStopsARecordThatOutgrowsTheBuffer()
	{
		// An unclosed quote makes the rest of the input one record.
		var csv = "id,name\n1,\"open\n" + string.Concat(Enumerable.Repeat("2,text\n", 2000));
		var options = new CsvReaderOptions { BufferSize = 1024, MaxRecordSize = 4096, TrackLineNumbers = true };
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(csv)), options);

		var e = Assert.Throws<CsvFormatException>(() => TestUtil.ReadAll(reader));
		Assert.Contains("MaxRecordSize", e.Message);
		Assert.Equal(2, e.RecordNumber);
		Assert.Equal(8, e.ByteOffset);
		Assert.Equal(2, e.LineNumber);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public void MaxRecordSizeKeepsRecordsUpToTheLimit(int degree)
	{
		// Records of 1000 bytes with their line break, in a buffer that has to be refilled for each of them.
		var record = new string('x', 999) + "\n";
		var csv = string.Concat(Enumerable.Repeat(record, 300));
		var options = NoHeader with { BufferSize = 256, MaxRecordSize = 1000 };

		using (var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(csv), 1, 300), options))
		{
			Assert.Equal(300, reader.ParallelEnumerate(r => r.GetString(0).Length, degree).Count(n => n == 999));
		}

		// A longer record that is not in the buffer yet (in parallel, the buffer holds a 256 KiB batch).
		using (var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(csv + new string('y', 600_000) + "\n"), 1, 300), options))
		{
			Assert.Throws<CsvFormatException>(() => reader.ParallelEnumerate(r => r.GetString(0), degree).ToList());
		}

		using (var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(csv), 2, 300), options))
		{
			Assert.Equal(300, TestUtil.ReadAll(reader).Count);
		}
	}

	[Fact]
	public void MaxRecordSizeDoesNotApplyToInMemoryInput()
	{
		var csv = new string('x', 10_000) + "\n";

		Assert.Single(TestUtil.ReadText(csv, NoHeader with { MaxRecordSize = 100 }));
		Assert.Throws<ArgumentOutOfRangeException>(() => CsvReader.Create([], NoHeader with { MaxRecordSize = 0 }));
	}

	[Fact]
	public void ByteOffsetCountsUtf8BytesFromTheStartOfTheInput()
	{
		var csv = "name,city\nZoë,Köln\n\"multi\nline\",x\n";
		var offsets = new List<long>();
		using (var reader = CsvReader.Create(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray()))
		{
			while (reader.Read()) offsets.Add(reader.ByteOffset);
		}

		// The byte order mark (3 bytes) and the header (9 bytes and LF); "Zoë,Köln" is 10 bytes.
		Assert.Equal([13, 24], offsets);
		using (var reader = CsvReader.FromText(csv))
		{
			Assert.True(reader.Read());
			Assert.Equal(10, reader.ByteOffset);
		}

		using (var reader = CsvReader.Create(new MemoryStream(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(csv)).ToArray())))
		{
			// UTF-16 input counts the bytes of its UTF-8 form, without the byte order mark.
			Assert.True(reader.Read());
			Assert.True(reader.Read());
			Assert.Equal(21, reader.ByteOffset);
		}
	}

	[Fact]
	public void LineNumberCountsEveryLine()
	{
		var csv = "sep=;\nname;note\n\n# comment\nA;\"two\r\nlines\"\nB;x\r\rC;y";
		var options = new CsvReaderOptions { CommentPrefix = '#', TrackLineNumbers = true };
		var lines = new List<(string, long)>();
		using var reader = CsvReader.FromText(csv, options);
		while (reader.Read()) lines.Add((reader.GetString(0), reader.LineNumber));

		Assert.Equal([("A", 5), ("B", 7), ("C", 9)], lines);
		Assert.Throws<InvalidOperationException>(() => CsvReader.FromText(csv).LineNumber);
	}

	[Fact]
	public void ErrorsCarryTheLineNumber()
	{
		var csv = "a,b\n\"x\ny\",1\n2,\"bad\"quote\n";
		var reported = new List<CsvMalformedRecord>();
		var options = new CsvReaderOptions { TrackLineNumbers = true, OnMalformedRecord = reported.Add };
		TestUtil.ReadText(csv, options);

		Assert.Equal(4, Assert.Single(reported).LineNumber);
		var e = Assert.Throws<CsvFormatException>(() => TestUtil.ReadText(csv, options with { MalformedData = CsvMalformedDataHandling.Throw }));
		Assert.Equal(4, e.LineNumber);
		Assert.Contains("(line 4)", e.Message);
		e = Assert.Throws<CsvFormatException>(() => TestUtil.ReadText("a,b\n1,2\n\n3\n", options with { FieldCountMismatch = CsvFieldCountHandling.Throw }));
		Assert.Equal(4, e.LineNumber);
		Assert.Equal(0, Assert.Throws<CsvFormatException>(() => TestUtil.ReadText(csv, new CsvReaderOptions { MalformedData = CsvMalformedDataHandling.Throw })).LineNumber);
	}

	[Fact]
	public void ExceptionsCanLeaveValuesOut()
	{
		const string secret = "s3cr3t";
		var hidden = NoHeader with { IncludeValuesInExceptions = false };
		Action<CsvReader>[] getters =
		[
			r => r.GetInt32(0), r => r.GetInt64(0), r => r.GetDouble(0), r => r.GetSingle(0), r => r.GetDecimal(0),
			r => r.GetBoolean(0), r => r.GetDateTime(0), r => r.GetDateTimeOffset(0), r => r.GetGuid(0),
#if NET10_0_OR_GREATER
			r => r.Parse<short>(0), r => r.Parse<ulong>(0), r => r.Parse<TimeSpan>(0), r => r.Parse<DateOnly>(0),
#endif
		];
		foreach (var get in getters)
		{
			using var reader = CsvReader.FromText(secret, hidden);
			reader.Read();
			var e = Assert.ThrowsAny<FormatException>(() => get(reader));
			Assert.Contains("IncludeValuesInExceptions", e.Message);
			Assert.DoesNotContain(secret, e.ToString());
		}

		// The BCL's messages (which name the value on .NET, but not for GUIDs) are kept otherwise.
		using var shown = CsvReader.FromText(secret, NoHeader);
		shown.Read();
		Assert.Contains(secret, Assert.Throws<FormatException>(() => shown.GetBoolean(0)).Message);
#if NET
		Assert.Contains(secret, Assert.Throws<FormatException>(() => shown.GetInt32(0)).Message);
#endif
		using var overflow = CsvReader.FromText("99999999999", hidden);
		overflow.Read();
		Assert.Contains("Int32", Assert.Throws<OverflowException>(() => overflow.GetInt32(0)).Message);
	}

	[Fact]
	public void ExceptionsOfYourOwnParsersAreNotChanged()
	{
		using var reader = CsvReader.FromText("x", NoHeader with { IncludeValuesInExceptions = false });
		reader.Read();

		Assert.Equal("mine x", Assert.Throws<FormatException>(() => reader.Parse<int>(0, (s, _) => throw new FormatException("mine " + s))).Message);
	}

	[Fact]
	public void NullValues()
	{
		var options = NoHeader with { NullValues = ["NULL", @"\N"] };
		using var reader = CsvReader.FromText("NULL,\\N,,null,\"NULL\",7\n", options);
		reader.Read();

		Assert.Equal([true, true, true, false, true, false], Enumerable.Range(0, 6).Select(reader.IsNull));
		Assert.Equal([null, null, "", "null", null, "7"], Enumerable.Range(0, 6).Select(reader.GetStringOrNull));
		Assert.Null(reader.Current[0].ParseOrNull<int>(TryInt));
		Assert.Equal(7, reader.Current[5].ParseOrNull<int>(TryInt));
		// Without NullValues only empty fields are null, and GetStringOrNull reads them as "".
		using var plain = CsvReader.FromText("NULL,\n", NoHeader);
		plain.Read();
		Assert.Equal([false, true], new[] { plain.IsNull(0), plain.IsNull(1) });
		Assert.Equal("NULL", plain.GetStringOrNull(0));
		Assert.Equal("", plain.GetStringOrNull(1));
		// "" in the list makes empty strings null too.
		using var empty = CsvReader.FromText("a,\n", NoHeader with { NullValues = [""] });
		empty.Read();
		Assert.Null(empty.GetStringOrNull(1));

		static bool TryInt(string s, IFormatProvider provider, out int value) =>
			int.TryParse(s, System.Globalization.NumberStyles.Integer, provider, out value);
	}

	[Fact]
	public void NullValuesAreMatchedAfterTrimming()
	{
		using var reader = CsvReader.FromText(" NULL ,x\n", NoHeader with { NullValues = ["NULL"], TrimWhitespace = true });
		reader.Read();

		Assert.True(reader.IsNull(0));
		Assert.Throws<ArgumentException>(() => CsvReader.FromText("", NoHeader with { NullValues = [null!] }));
	}

	[Fact]
	public void BooleanTexts()
	{
		var options = NoHeader with { TrueValues = ["Ja", "J"], FalseValues = ["Nein", "N"] };
		using var reader = CsvReader.FromText("ja, J ,NEIN,n,true\n", options);
		reader.Read();

		Assert.Equal([true, true, false, false], Enumerable.Range(0, 4).Select(reader.GetBoolean));
		Assert.Contains("'true'", Assert.Throws<FormatException>(() => reader.GetBoolean(4)).Message);

		// Setting only one list keeps the built-in values of the other.
		using var yes = CsvReader.FromText("oui,no\n", NoHeader with { TrueValues = ["oui"] });
		yes.Read();
		Assert.Equal([true, false], new[] { yes.GetBoolean(0), yes.GetBoolean(1) });
		Assert.Throws<ArgumentException>(() => CsvReader.FromText("", NoHeader with { TrueValues = ["x"], FalseValues = ["X"] }));
	}

#if NET10_0_OR_GREATER
	[Fact]
	public void BooleanTextsApplyToTheGenericParse()
	{
		using var reader = CsvReader.FromText("Ja,Nein,x\n", NoHeader with { TrueValues = ["Ja"], FalseValues = ["Nein"] });
		reader.Read();

		Assert.True(reader.Parse<bool>(0));
		Assert.True(reader.TryParse<bool>(1, out var no));
		Assert.False(no);
		Assert.False(reader.TryParse<bool>(2, out _));
	}
#endif

	[Fact]
	public void NormalizeHeaderName()
	{
		var options = new CsvReaderOptions { NormalizeHeaderName = n => n.Replace("_", "").Replace(" ", "") };
		using var reader = CsvReader.FromText("unit_price,Order Id\n9.5,7\n", options);
		reader.Read();

		Assert.Equal(["unit_price", "Order Id"], reader.Headers);
		Assert.Equal(0, reader.GetFieldIndex("UnitPrice"));
		Assert.Equal(0, reader.GetFieldIndex("unit_price"));
		Assert.Equal(1, reader.GetFieldIndex("orderid"));
		Assert.Equal("7", reader["OrderId"].ToString());
	}

	[Fact]
	public void DuplicateHeaderNamesByPosition()
	{
		using var reader = CsvReader.FromText("Amount,Id, amount ,Amount\n1,x,2,3\n");
		reader.Read();

		Assert.Equal(0, reader.GetFieldIndex("Amount", 0));
		// Exact matches first, while there are enough of them.
		Assert.Equal(3, reader.GetFieldIndex("Amount", 1));
		// Then the loose matches, in header order.
		Assert.Equal(2, reader.GetFieldIndex("AMOUNT", 1));
		Assert.False(reader.TryGetFieldIndex("Amount", 3, out _));
		Assert.Contains("number 4", Assert.Throws<KeyNotFoundException>(() => reader.GetFieldIndex("Amount", 3)).Message);
		Assert.Throws<ArgumentOutOfRangeException>(() => reader.TryGetFieldIndex("Amount", -1, out _));
	}

	[Fact]
	public void ReadHeaderMidFileStartsANewDataSet()
	{
		var csv = "name,age\nAnn,30\nBob,41\nid,city,zip\n7,Oslo,0150\n";
		// FieldCountMismatch must allow the header of the next data set when it has another number of columns.
		using var reader = CsvReader.FromText(csv);
		var people = new List<string>();
		while (reader.Read() && reader.GetString(0) != "id") people.Add(reader.GetString(0) + reader.GetString("age"));
		reader.ReadHeader();

		Assert.Equal(["Ann30", "Bob41"], people);
		Assert.Equal(["id", "city", "zip"], reader.Headers);
		Assert.Equal(2, reader.RowNumber);
		Assert.True(reader.Read());
		// The row numbers go on.
		Assert.Equal(3, reader.RowNumber);
		Assert.Equal("Oslo", reader.GetString("city"));
		Assert.False(reader.TryGetFieldIndex("age", out _));
		Assert.False(reader.Read());
	}

	[Fact]
	public async Task ReadHeaderAsyncMidFile()
	{
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes("a\n1\nb,c\n2,3\n"), 3, 2, yieldAsync: true));
		Assert.True(await reader.ReadAsync());
		Assert.True(await reader.ReadAsync());
		await reader.ReadHeaderAsync();
		Assert.True(await reader.ReadAsync());

		Assert.Equal("3", reader.GetString("c"));
	}

	[Fact]
	public void ShouldSkipRecord()
	{
		var csv = "item,amount\nA,1\nSubtotal,1\nB,2\nTotal,3\n";
		var seen = new List<long>();
		var options = new CsvReaderOptions
		{
			ShouldSkipRecord = row =>
			{
				seen.Add(row.RowNumber);

				return row.GetString(0).EndsWith("total", StringComparison.OrdinalIgnoreCase);
			},
		};
		using var reader = CsvReader.FromText(csv, options);
		var rows = new List<(long, string)>();
		while (reader.Read()) rows.Add((reader.RowNumber, reader.GetString(0)));

		Assert.Equal([(1, "A"), (2, "B")], rows);
		// Not called for the header; a skipped record does not use up its row number.
		Assert.Equal([1, 2, 2, 3], seen);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public void ShouldSkipRecordInParallel(int degree)
	{
		var csv = "id\n" + string.Concat(Enumerable.Range(0, 100_000).Select(i => i + "\n"));
		var options = new CsvReaderOptions { ShouldSkipRecord = row => row.GetString(0).EndsWith("7", StringComparison.Ordinal) };
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(csv)), options);
		var rows = reader.ParallelEnumerate(r => (r.RowNumber, Id: r.GetInt32(0)), degree).ToList();

		Assert.Equal(Enumerable.Range(0, 100_000).Where(i => i % 10 != 7), rows.Select(r => r.Id));
		Assert.Equal(Enumerable.Range(1, rows.Count).Select(i => (long)i), rows.Select(r => r.RowNumber));
	}
}
