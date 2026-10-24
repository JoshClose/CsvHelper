using System.Dynamic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;

namespace CsvHelper.Tests;

/// <summary>
/// Records read without a record type (<see cref="CsvDynamicRecord"/>): member access through <c>dynamic</c>, the values
/// of every kind of field and source, the dictionary view and <c>Parse&lt;T&gt;</c>, and writing dynamic records and dictionaries.
/// </summary>
public class DynamicRecordTests
{
	private const string People = "Name,Age,Unit Price\nAnn,31,9.5\n\"Smith, Bob\",42,\"1,000\"\n";

	[Fact]
	public void MembersAreTheColumns()
	{
		using var reader = CsvReader.FromText(People);
		var names = new List<string>();
		var ages = 0;
		foreach (var record in reader.GetRecords<dynamic>())
		{
			names.Add(record.Name);
			ages += int.Parse(record.Age, CultureInfo.InvariantCulture);
			ages += (int)record.Parse<int>("Age");
			Assert.Equal(3, (int)record.FieldCount);
		}

		Assert.Equal(["Ann", "Smith, Bob"], names);
		Assert.Equal(146, ages);
	}

	[Fact]
	public void NamesAreMatchedAsTheReaderMatchesThem()
	{
		using var reader = CsvReader.FromText(People);
		dynamic record = reader.GetRecords<dynamic>().First();

		// Ignoring case, and the indexer for names that are not identifiers.
		Assert.Equal("31", (string)record.age);
		Assert.Equal("9.5", (string)record["Unit Price"]);
		Assert.Equal("Ann", (string)record[0]);
		Assert.Throws<RuntimeBinderException>(() => record.Missing);
		Assert.Throws<KeyNotFoundException>(() => record["Missing"]);
	}

	[Fact]
	public void OneMemberAccessServesRecordsOfDifferentHeaders()
	{
		// The same call site sees records whose Name is in another column.
		static string NameOf(dynamic record) => record.Name;

		using var first = CsvReader.FromText("Name,Age\nAnn,31\n");
		using var second = CsvReader.FromText("Age,Name\n42,Bob\n");
		using var third = CsvReader.FromText("Name,Age\nCid,7\n");

		Assert.Equal("Ann", NameOf(first.GetRecords<dynamic>().Single()));
		Assert.Equal("Bob", NameOf(second.GetRecords<dynamic>().Single()));
		Assert.Equal("Cid", NameOf(third.GetRecords<dynamic>().Single()));
	}

	[Fact]
	public void ReadersWithTheSameHeaderAndOptionsShareASchema()
	{
		using var first = CsvReader.FromText("Name,Age\nAnn,31\n");
		using var second = CsvReader.Create(Encoding.UTF8.GetBytes("Name,Age\nBob,42\n"));
		using var other = CsvReader.FromText("Name,Age\nCid,7\n", new CsvReaderOptions { NullValues = ["NULL"] });

		var a = first.GetRecords<CsvDynamicRecord>().Single();
		var b = second.GetRecords<CsvDynamicRecord>().Single();
		var c = other.GetRecords<CsvDynamicRecord>().Single();
		Assert.Same(a.Schema, b.Schema);
		Assert.NotSame(a.Schema, c.Schema);
	}

	[Fact]
	public void ColumnsComeBeforeTheMembersOfTheClass()
	{
		using var reader = CsvReader.FromText("FieldCount,Names\n7,x\n");
		dynamic record = reader.GetRecords<dynamic>().Single();

		Assert.Equal("7", (string)record.FieldCount);
		Assert.Equal("x", (string)record.Names);
		Assert.Equal(2, ((CsvDynamicRecord)record).FieldCount);
	}

	[Fact]
	public void AMemberBoundForOneHeaderDoesNotHideAColumnOfAnother()
	{
		// The same call site reads the member FieldCount of one record, then the column FieldCount of another.
		static object FieldCountOf(dynamic record) => record.FieldCount;

		using var first = CsvReader.FromText("Name,Age\nAnn,31\n");
		using var second = CsvReader.FromText("FieldCount,Names\n7,x\n");

		Assert.Equal(2, FieldCountOf(first.GetRecords<dynamic>().Single()));
		Assert.Equal("7", FieldCountOf(second.GetRecords<dynamic>().Single()));
	}

	[Fact]
	public void ValuesFollowTheReaderOptions()
	{
		var options = new CsvReaderOptions { NullValues = ["NULL"], TrimWhitespace = true };
		using var reader = CsvReader.FromText("A,B,C\n NULL , \"x \" ,\n1\n", options);
		var records = reader.GetRecords<CsvDynamicRecord>().ToList();

		Assert.Null(records[0]["A"]);
		Assert.Equal("x ", records[0]["B"]);
		Assert.Equal("", records[0]["C"]);
		// A field the record lacks reads as empty.
		Assert.Equal("", records[1]["C"]);
		Assert.Equal(1, records[1].FieldCount);

		using var strict = CsvReader.FromText("A,B\n1\n", new CsvReaderOptions { MissingField = CsvMissingFieldHandling.Throw });
		var record = strict.GetRecords<CsvDynamicRecord>().Single();
		Assert.Throws<IndexOutOfRangeException>(() => record["B"]);
	}

	[Fact]
	public void FieldsWithoutAHeaderAreNumbered()
	{
		using var reader = CsvReader.FromText("a,b\nc,d,e\n", new CsvReaderOptions { HasHeader = false });
		var records = reader.GetRecords<dynamic>().ToList();

		Assert.Equal("a", (string)records[0].Field1);
		Assert.Equal("e", (string)records[1].Field3);
		Assert.Equal(["Field1", "Field2"], ((CsvDynamicRecord)records[0]).Names);
		Assert.False(((CsvDynamicRecord)records[0]).ContainsKey("Field3"));
		Assert.Equal(new Dictionary<string, string?> { ["Field1"] = "c", ["Field2"] = "d", ["Field3"] = "e" }, ((CsvDynamicRecord)records[1]).ToDictionary());
	}

	[Fact]
	public void RecordsAreReadOnlyDictionaries()
	{
		using var reader = CsvReader.FromText("A,B,A\n1,2,3\n");
		var record = reader.GetRecords<CsvDynamicRecord>().Single();
		IReadOnlyDictionary<string, string?> dictionary = record;

		// A name that comes twice is the first column with it.
		Assert.Equal(2, dictionary.Count);
		Assert.Equal(["A", "B"], dictionary.Keys);
		Assert.Equal(["1", "2"], dictionary.Values);
		Assert.Equal("1", record["A"]);
		Assert.Equal("3", record[2]);
		Assert.True(record.TryGetValue("b", out var b));
		Assert.Equal("2", b);
		Assert.False(record.TryGetValue("C", out _));
		Assert.Equal("A: 1, B: 2", record.ToString());
		Assert.Equal(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" }, record.ToDictionary());
	}

	[Fact]
	public void ParseParsesAsTheReaderDoes()
	{
		var options = new CsvReaderOptions { TrueValues = ["J"], FalseValues = ["N"], FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
		using var reader = CsvReader.FromText(
			"Int;Long;Double;Decimal;Bool;When;Text;Span;Status\n5;9000000000;1,5;2,25;J;01.03.2024;Zoë;01:30;on hold\n", options with { Separator = ';' });
		var record = reader.GetRecords<CsvDynamicRecord>().Single();

		Assert.Equal(5, record.Parse<int>("Int"));
		Assert.Equal(9000000000L, record.Parse<long>("Long"));
		Assert.Equal((short)5, record.Parse<short>("Int"));
		Assert.Equal(1.5, record.Parse<double>("Double"));
		Assert.Equal(2.25m, record.Parse<decimal>("Decimal"));
		Assert.True(record.Parse<bool>("Bool"));
		Assert.Equal(new DateTime(2024, 3, 1), record.Parse<DateTime>("When"));
		Assert.Equal("Zoë", record.Parse<string>("Text"));
		Assert.Throws<FormatException>(() => record.Parse<int>("Bool"));
		Assert.Throws<KeyNotFoundException>(() => record.Parse<int>("Nope"));
		Assert.Equal(new TimeSpan(1, 30, 0), record.Parse<TimeSpan>("Span"));
		Assert.Equal(new BigInteger(9000000000L), record.Parse<BigInteger>("Long"));
		Assert.Equal((sbyte)5, record.Parse<sbyte>("Int"));
		Assert.Equal(ReaderTests.Status.OnHold, record.Parse<ReaderTests.Status>("Status"));
		Assert.Throws<FormatException>(() => record.Parse<ReaderTests.Status>("Text"));
#if !NET
		Assert.Throws<NotSupportedException>(() => record.Parse<Uri>("Text"));
#endif
	}

	[Fact]
	public async Task RecordsKeepTheirValuesWhateverTheSource()
	{
		// Quoted fields, escaped quotes, line breaks in quotes, text after a closing quote, CRLF, empty fields, non-ASCII
		// text, and a quote that is never closed at the end.
		var csv = new StringBuilder("Id,Text,Note\r\n");
		var random = new Random(7);
		for (var i = 0; i < 3000; i++)
		{
			var text = random.Next(6) switch
			{
				0 => "plain" + i,
				1 => "\"quoted, " + i + "\"",
				2 => "\"say \"\"hi\"\"\nthere\"",
				3 => "\"ab\"c" + i,
				4 => "\"\"\"x\"\"\"",
				_ => "ünïcode " + i,
			};
			csv.Append(i).Append(',').Append(text).Append(',').Append(random.Next(3) == 0 ? "" : "n" + i).Append(i % 2 == 0 ? "\r\n" : "\n");
		}

		csv.Append("3000,\"never closed,x");

		var text16 = csv.ToString();
		var utf8 = Encoding.UTF8.GetBytes(text16);
		var expected = new List<string[]>();
		using (var plain = CsvReader.FromText(text16))
		{
			while (plain.Read())
			{
				expected.Add(plain.Current.ToArray());
			}
		}

		// In place (a byte array), copied from a stream whose buffer is reused, transcoded, in parallel, and async.
		var small = new CsvReaderOptions { BufferSize = 256 };
		var sources = new List<List<CsvDynamicRecord>>
		{
			CsvReader.Create(utf8).GetRecords<CsvDynamicRecord>().ToList(),
			CsvReader.Create(new MemoryStream(utf8), small).GetRecords<CsvDynamicRecord>().ToList(),
			CsvReader.FromText(text16, small).GetRecords<CsvDynamicRecord>().ToList(),
			CsvReader.Create(new MemoryStream(utf8), small).ParallelEnumerate<CsvDynamicRecord>(3).ToList(),
			await ReadAsync(CsvReader.Create(new MemoryStream(utf8), small)),
		};
		foreach (var records in sources)
		{
			Assert.Equal(expected.Count, records.Count);
			for (var r = 0; r < expected.Count; r++)
			{
				Assert.Equal(expected[r], Enumerable.Range(0, records[r].FieldCount).Select(i => records[r][i]!));
			}
		}

		static async Task<List<CsvDynamicRecord>> ReadAsync(CsvReader reader)
		{
			var records = new List<CsvDynamicRecord>();
			await foreach (CsvDynamicRecord record in reader.GetRecordsAsync<dynamic>())
			{
				records.Add(record);
			}

			return records;
		}
	}

	[Fact]
	public void ParallelRecordsOutliveTheirBatches()
	{
		// Several megabytes, so that the workers' batch buffers are used again while earlier records are kept.
		var csv = new StringBuilder("Id,Text\n");
		for (var i = 0; i < 200_000; i++)
		{
			csv.Append(i).Append(i % 3 == 0 ? ",\"quoted " : ",plain ").Append(i).Append(i % 3 == 0 ? "\"\n" : "\n");
		}

		var utf8 = Encoding.UTF8.GetBytes(csv.ToString());
		using var reader = CsvReader.Create(new MemoryStream(utf8));
		var records = reader.ParallelEnumerate<CsvDynamicRecord>(4).ToList();

		Assert.Equal(200_000, records.Count);
		for (var i = 0; i < records.Count; i++)
		{
			Assert.Equal(i.ToString(CultureInfo.InvariantCulture), records[i]["Id"]);
			Assert.Equal((i % 3 == 0 ? "quoted " : "plain ") + i.ToString(CultureInfo.InvariantCulture), records[i]["Text"]);
		}
	}

	[Fact]
	public void QuotedFieldsUseTheQuoteOfTheOptions()
	{
		var options = new CsvReaderOptions { Quote = '\'' };
		using var stream = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes("A,B\n'x,y',''''\n")), options);
		using var bytes = CsvReader.Create(Encoding.UTF8.GetBytes("A,B\n'x,y',''''\n"), options);

		Assert.Equal(["x,y", "'"], stream.GetRecords<CsvDynamicRecord>().Single().Select(p => p.Value));
		Assert.Equal(["x,y", "'"], bytes.GetRecords<CsvDynamicRecord>().Single().Select(p => p.Value));
	}

	[Fact]
	public void OtherModesDecodeTheirFields()
	{
		var escape = new CsvReaderOptions { Mode = CsvMode.Escape, HasHeader = false };
		using (var reader = CsvReader.FromText("a\\,b,c\n", escape))
		{
			Assert.Equal(["a,b", "c"], reader.GetRecords<CsvDynamicRecord>().Single().Select(p => p.Value));
		}

		var fixedWidth = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [3, 4], HasHeader = false };
		using (var reader = CsvReader.FromText("ab xyz \n", fixedWidth))
		{
			Assert.Equal(["ab", "xyz"], reader.GetRecords<CsvDynamicRecord>().Single().Select(p => p.Value));
		}
	}

	[Fact]
	public void DetachedRecordsHoldTheirOwnBytes()
	{
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(People)));
		var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();

		Assert.Equal(["Ann", "Smith, Bob"], records.Select(r => r["Name"]));
		Assert.Equal("1,000", records[1]["Unit Price"]);
	}

	[Fact]
	public void CurrentRowCopiesIntoADictionary()
	{
		using var reader = CsvReader.FromText("A,B,A\n1,NULL,3\n", new CsvReaderOptions { NullValues = ["NULL"] });
		reader.Read();

		Assert.Equal(new Dictionary<string, string?> { ["A"] = "1", ["B"] = null }, reader.Current.ToDictionary());
	}

	[Fact]
	public void DynamicRecordsAreWrittenBack()
	{
		using var reader = CsvReader.FromText(People);
		var records = reader.GetRecords<dynamic>().ToList();
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords(records);
		}

		Assert.Equal(People.Replace("\n", "\r\n"), output.ToString());
	}

	[Fact]
	public void ExpandoObjectsAndDictionariesAreWritten()
	{
		dynamic first = new ExpandoObject();
		first.Name = "Ann";
		first.Age = 31;
		dynamic second = new ExpandoObject();
		second.Age = 42;
		second.Name = null;
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output, new CsvWriterOptions { NullValue = "NULL" }))
		{
			// The first record names the columns; the others are written in them.
			writer.WriteRecords(new List<ExpandoObject> { first, second });
		}

		Assert.Equal("Name,Age\r\nAnn,31\r\nNULL,42\r\n", output.ToString());

		using var dictionaries = new StringWriter();
		using (var writer = CsvWriter.Create(dictionaries))
		{
			writer.WriteRecords(new[] { new Dictionary<string, string?> { ["A"] = "1", ["B"] = "x,y" }, new Dictionary<string, string?> { ["B"] = "2" } });
		}

		Assert.Equal("A,B\r\n1,\"x,y\"\r\n,2\r\n", dictionaries.ToString());

		// One record without a header, in the order of its own names.
		using var single = new StringWriter();
		using (var writer = CsvWriter.Create(single))
		{
			writer.WriteRecord(new Dictionary<string, object?> { ["C"] = 1.5, ["D"] = "x" });
		}

		Assert.Equal("1.5,x\r\n", single.ToString());
	}

	[Fact]
	public void DynamicRecordsOfAnotherHeaderAreWrittenByName()
	{
		using var first = CsvReader.FromText("A,B\n1,2\n");
		using var second = CsvReader.FromText("B,C,A\n5,6,4\n");
		var records = first.GetRecords<dynamic>().Concat(second.GetRecords<dynamic>()).ToList();
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords(records);
		}

		Assert.Equal("A,B\r\n1,2\r\n4,5\r\n", output.ToString());
	}

	[Fact]
	public void OnlyRecordsWithNamesCanBeWrittenAsDynamic()
	{
		using var writer = CsvWriter.Create(new StringWriter());

		Assert.Throws<InvalidOperationException>(() => writer.WriteHeader<dynamic>());
		Assert.Contains("is not a dynamic record", Assert.Throws<InvalidOperationException>(() => writer.WriteRecords(new object[] { new { Name = "x" } })).Message);
	}
}
