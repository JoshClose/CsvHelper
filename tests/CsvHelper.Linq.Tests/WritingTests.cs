using System.Text;
using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>A positional record without [CsvRecord], as code that does not know CsvHelper would declare it.</summary>
public record PlainPair(int A, string B);

public class Keyed
{
	public int Key { get; set; }
}

/// <summary>An entity as an ORM would have it: members of simple types, references and collections.</summary>
public class Entity : Keyed
{
	public string Name { get; set; } = "";

	[CsvIgnore]
	public string Secret { get; set; } = "hidden";

	[CsvColumn("Full Name")]
	public string? Full { get; set; }

	public Customer? Owner { get; set; }

	public List<int> Items { get; set; } = [];

	public DateTime When { get; set; }

	public Status? Status { get; set; }
}

public sealed class NothingToWrite
{
	public List<int> Items { get; set; } = [];
}

/// <summary>A sequence that is both synchronous and asynchronous, as Entity Framework Core's DbSet is.</summary>
public sealed class BothWays<T>(IEnumerable<T> items) : IEnumerable<T>, IAsyncEnumerable<T>
{
	public bool ReadAsynchronously { get; private set; }

	public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

	public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
	{
		ReadAsynchronously = true;
		foreach (var item in items)
		{
			await Task.Yield();

			yield return item;
		}
	}
}

/// <summary>Writing the results of queries as CSV, adding records to files, and removing and changing records in them.</summary>
public sealed class WritingTests : IDisposable
{
	private readonly string directory = Path.Combine(Path.GetTempPath(), "csvhelper-linq-" + Guid.NewGuid().ToString("N"));

	public WritingTests() => Directory.CreateDirectory(directory);

	public void Dispose() => Directory.Delete(directory, true);

	private string File(string name, string? content = null, bool bom = false)
	{
		var path = Path.Combine(directory, name);
		if (content is not null)
		{
			System.IO.File.WriteAllBytes(path, [.. (bom ? (byte[])[0xEF, 0xBB, 0xBF] : []), .. Encoding.UTF8.GetBytes(content)]);
		}

		return path;
	}

	private static string Text(string path) => Encoding.UTF8.GetString(System.IO.File.ReadAllBytes(path));

	private static string Written<T>(IEnumerable<T> records, CsvWriterOptions? options = null)
	{
		var text = new StringWriter();
		records.WriteCsv(text, options);

		return text.ToString();
	}

	[Fact]
	public void WritesAnonymousTypes()
	{
		var text = Written(Orders().Where(o => o.Id <= 2).Select(o => new { o.Id, Who = o.Customer, o.Total, o.Placed }));
		var expected = "Id,Who,Total,Placed\r\n" + string.Concat(Data.Orders.Take(2).Select(o =>
			$"{o.Id},{o.Customer},{o.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)},{o.Placed:yyyy-MM-ddTHH:mm:ss.fffffff}\r\n"));
		Assert.Equal(expected, text);
	}

	[Fact]
	public void WritesRecordTypesWithTheirGeneratedWriters()
	{
		var generated = new StringWriter();
		using (var writer = CsvWriter.Create(generated, leaveOpen: true))
		{
			writer.WriteRecords(Data.Orders);
		}

		Assert.Equal(generated.ToString(), Written(Orders()));
	}

	[Fact]
	public void WritesTuplesRecordsAndEntities()
	{
		Assert.Equal($"Item1,Item2\r\n1,{Data.Orders[0].Customer}\r\n", Written(Orders().Take(1).Select(o => ValueTuple.Create(o.Id, o.Customer)).ToList()));
		Assert.Equal("A,B\r\n1,x\r\n2,\"y,z\"\r\n", Written([new PlainPair(1, "x"), new PlainPair(2, "y,z")]));
		var entity = new Entity { Key = 7, Name = "E", Full = "Ed Ent", Owner = new Customer("C", "NO", 1), Items = [1], When = new DateTime(2026, 1, 2), Status = Status.Shipped };
		// Base members first; no Secret, Owner or Items.
		Assert.Equal("Key,Name,Full Name,When,Status\r\n7,E,Ed Ent,2026-01-02T00:00:00.0000000,Shipped\r\n", Written([entity]));
	}

	[Fact]
	public void WritesDynamicRowsAndDictionaries()
	{
		Assert.Equal(Data.CustomersCsv.Replace("\n", "\r\n"), Written(CsvQueryable.FromText(Data.CustomersCsv)));
		var rows = new List<Dictionary<string, object?>> { new() { ["A"] = 1, ["B"] = "x" }, new() { ["A"] = 2, ["B"] = null } };
		Assert.Equal("A,B\r\n1,x\r\n2,\r\n", Written(rows));
	}

	[Fact]
	public void CountsTheRecords()
	{
		var text = new StringWriter();
		Assert.Equal(60, Orders().Select(o => new { o.Id }).WriteCsv(text));
		Assert.Equal(0, Orders().Where(o => o.Id > 1000).Select(o => new { o.Id }).WriteCsv(text = new StringWriter()));
		Assert.Equal("Id\r\n", text.ToString());
		Assert.Equal(0, new List<Dictionary<string, object?>>().WriteCsv(new StringWriter()));
		Assert.Equal(2, Data.Orders.Take(2).WriteCsv(new StringWriter(), new CsvWriterOptions { HasHeader = false }));
	}

	[Fact]
	public void WritesValueTypesAndNullsOfEveryKind()
	{
		var rows = new[]
		{
			new { Id = 1, When = (DateTime?)new DateTime(2026, 1, 2), Price = 1.5m, Flag = true, Name = (string?)"a,b", Kind = (Status?)Status.Open },
			new { Id = 2, When = (DateTime?)null, Price = -2m, Flag = false, Name = (string?)null, Kind = (Status?)null },
		};
		Assert.Equal("Id,When,Price,Flag,Name,Kind\r\n1,2026-01-02T00:00:00.0000000,1.5,True,\"a,b\",Open\r\n2,,-2,False,,\r\n", Written(rows));
		Assert.Equal("Item1,Item2\r\n3,x\r\n", Written([(3, "x")]));
	}

	[Fact]
	public void TypesWithNothingToWriteFail() =>
		Assert.Contains("NothingToWrite", Assert.Throws<InvalidOperationException>(() => Written(new[] { new NothingToWrite() })).Message);

	[Fact]
	public void WritesFilesAndStreams()
	{
		var path = File("out.csv");
		Assert.Equal(60, Orders().Select(o => new { o.Id, o.Customer }).WriteCsv(path, new CsvWriterOptions { Separator = ';' }));
		Assert.StartsWith($"Id;Customer\r\n1;{Data.Orders[0].Customer}\r\n", Text(path));
		var stream = new MemoryStream();
		Orders().Take(1).Select(o => new { o.Id }).WriteCsv(stream, leaveOpen: true);
		Assert.Equal("Id\r\n1\r\n", Encoding.UTF8.GetString(stream.ToArray()));
		using var writer = CsvWriter.Create(new StringWriter());
		Assert.Equal(1, Orders().Take(1).WriteCsv(writer));
	}

	[Fact]
	public async Task WritesAsynchronously()
	{
		var path = File("async.csv");
		Assert.Equal(60, await Orders().Select(o => new { o.Id, o.Total }).WriteCsvAsync(path));
		Assert.Equal(Written(Orders().Select(o => new { o.Id, o.Total })), Text(path));
		Assert.Equal(60, await Orders().WriteCsvAsync(path));
		Assert.Equal(Written(Data.Orders), Text(path));
		var both = new BothWays<PlainPair>([new PlainPair(1, "x")]);
		var stream = new MemoryStream();
		Assert.Equal(1, await both.WriteCsvAsync(stream, leaveOpen: true));
		Assert.True(both.ReadAsynchronously);
		Assert.Equal("A,B\r\n1,x\r\n", Encoding.UTF8.GetString(stream.ToArray()));
		using var writer = CsvWriter.Create(new StringWriter());
		Assert.Equal(60, await Orders().Select(o => o.Id).Select(id => new { id }).WriteCsvAsync(writer));
	}

	[Fact]
	public void AppendsToNewEmptyAndBomOnlyFiles()
	{
		var path = File("new.csv");
		Assert.Equal(2, Data.Orders.Take(2).Select(o => new { o.Id, o.Customer }).AppendCsv(path));
		Assert.Equal(1, Data.Orders.Skip(2).Take(1).Select(o => new { o.Id, o.Customer }).AppendCsv(path));
		Assert.Equal("Id,Customer\r\n" + string.Concat(Data.Orders.Take(3).Select(o => $"{o.Id},{o.Customer}\r\n")), Text(path));
		path = File("empty.csv", "");
		new[] { new PlainPair(1, "x") }.AppendCsv(path);
		Assert.Equal("A,B\r\n1,x\r\n", Text(path));
		path = File("bom.csv", "", bom: true);
		new[] { new PlainPair(1, "x") }.AppendCsv(path);
		Assert.Equal("﻿A,B\r\n1,x\r\n", Text(path));
	}

	[Fact]
	public void AppendsWithTheFilesSeparatorAndLineBreak()
	{
		var path = File("lf.csv", "A;B\n1;x");
		Assert.Equal(1, new[] { new PlainPair(2, "y") }.AppendCsv(path));
		Assert.Equal("A;B\n1;x\n2;y\n", Text(path));
		path = File("cr.csv", "A,B\r1,x\r", bom: true);
		new[] { new PlainPair(2, "y") }.AppendCsv(path);
		Assert.Equal("﻿A,B\r1,x\r2,y\r", Text(path));
	}

	[Fact]
	public void AppendsOnlyRecordsWithTheFilesHeader()
	{
		var path = File("header.csv", "A,C\n1,x\n");
		var e = Assert.Throws<InvalidOperationException>(() => new[] { new PlainPair(2, "y") }.AppendCsv(path));
		Assert.Contains("A,C", e.Message);
		Assert.Equal("A,C\n1,x\n", Text(path));
		// Case and surrounding spaces do not matter, as in header lookups.
		path = File("case.csv", "a , b\n1,x\n");
		new[] { new PlainPair(2, "y") }.AppendCsv(path);
		Assert.Equal("a , b\n1,x\n2,y\n", Text(path));
	}

	[Fact]
	public async Task TablesAppendRecords()
	{
		var path = File("orders.csv", Data.OrdersCsv);
		var table = CsvQueryable.FromFile<Order>(path);
		Assert.Equal(1, table.AppendRecords([new Order { Id = 61, Customer = "New", Placed = new DateTime(2026, 6, 1) }]));
		Assert.Equal(2, await table.AppendRecordsAsync(new BothWays<Order>([new Order { Id = 62 }, new Order { Id = 63 }])));
		Assert.Equal(63, table.Count());
		Assert.Equal("New", table.Single(o => o.Id == 61).Customer);
		Assert.EndsWith("\n63,,Open,0,0,0001-01-01T00:00:00.0000000,\n", Text(path));
	}

	[Theory]
	[InlineData("\r\n")]
	[InlineData("\n")]
	[InlineData("\r")]
	public void DeletesRecordsWithTheirLineBreaks(string lineBreak)
	{
		string Lines(params string[] lines) => string.Concat(lines.Select(l => l + lineBreak));

		var path = File("delete.csv", Lines("Name,Country,Rank", "Ada,NO,1", "Bob,SE,2", "Cy,NO,3", "Dee,DK,4"));
		var table = CsvQueryable.FromFile<Customer>(path);
		Assert.Equal(2, table.Delete(c => c.Country == "NO"));
		Assert.Equal(Lines("Name,Country,Rank", "Bob,SE,2", "Dee,DK,4"), Text(path));
		Assert.Equal(0, table.Delete(c => c.Country == "NO"));
		Assert.Equal(Lines("Name,Country,Rank", "Bob,SE,2", "Dee,DK,4"), Text(path));
		Assert.Single(Directory.GetFiles(directory));
	}

	[Fact]
	public void DeletingKeepsEverythingElseByteForByte()
	{
		var content = "Name,Country,Rank,Extra\r\n# a comment\r\nAda,NO,1,\"keep,\r\nthis\"\r\n\r\n\"Bob\", SE ,2,x\r\nCy,NO,3,y";
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { CommentPrefix = '#', TrimWhitespace = true } };
		var path = File("bytes.csv", content, bom: true);
		var table = CsvQueryable.FromFile<Customer>(path, options);
		Assert.Equal(1, table.Delete(c => c.Name == "Cy"));
		// The last record had no line break: the one before it stays.
		Assert.Equal("﻿" + content.Substring(0, content.Length - "Cy,NO,3,y".Length), Text(path));
		Assert.Equal(1, table.Delete(c => c.Rank == 1));
		Assert.Equal("﻿Name,Country,Rank,Extra\r\n# a comment\r\n\r\n\"Bob\", SE ,2,x\r\n", Text(path));
	}

	[Fact]
	public void DeletesDynamicRows()
	{
		var path = File("rows.csv", "A,B\n1,x\n2,y\n");
		Assert.Equal(1, CsvQueryable.FromFile(path).Delete(r => r["B"] == "x"));
		Assert.Equal("A,B\n2,y\n", Text(path));
	}

	[Fact]
	public void UpdatesOnlyTheFieldsThatChange()
	{
		var content = "Id,Customer,Status,Total,Quantity,Placed,Note,Extra\r\n" +
					  "1,Ada,Open,1.50,1,2026-01-01,\"a, b\",x\r\n" +
					  "2,Bob,Open,2,2,2026-01-02,,y\r\n";
		var path = File("update.csv", content);
		var table = CsvQueryable.FromFile<Order>(path);
		Assert.Equal(1, table.Update(o => o.Id == 1, o =>
		{
			o.Status = Status.Shipped;

			return o;
		}));
		Assert.Equal(content.Replace("1,Ada,Open", "1,Ada,Shipped"), Text(path));
		Assert.Equal(2, table.Update(o => o.Quantity > 0, o => new Order
		{
			Id = o.Id, Customer = o.Customer, Status = o.Status, Total = o.Total, Quantity = o.Quantity, Placed = o.Placed, Note = o.Id == 2 ? "new, \"quoted\"" : o.Note,
		}));
		Assert.Equal(
			"Id,Customer,Status,Total,Quantity,Placed,Note,Extra\r\n" +
			"1,Ada,Shipped,1.50,1,2026-01-01,\"a, b\",x\r\n" +
			"2,Bob,Open,2,2,2026-01-02,\"new, \"\"quoted\"\"\",y\r\n",
			Text(path));
	}

	[Fact]
	public async Task UpdatesRecordsAndRows()
	{
		var path = File("customers.csv", "Name,Country,Rank\nAda,NO,1\nBob,SE,2\n");
		var table = CsvQueryable.FromFile<Customer>(path);
		Assert.Equal(1, await table.UpdateAsync(c => c.Name == "Bob", c => c with { Rank = 20 }));
		Assert.Equal("Name,Country,Rank\nAda,NO,1\nBob,SE,20\n", Text(path));
		Assert.Equal(1, CsvQueryable.FromFile(path).Update(r => r["Name"] == "Ada", r => r["Country"] = "SE"));
		Assert.Equal("Name,Country,Rank\nAda,SE,1\nBob,SE,20\n", Text(path));
		Assert.Equal(2, await CsvQueryable.FromFile(path).UpdateAsync(r => true, r => r["Rank"] = r["Rank"] + "0"));
		Assert.Equal("Name,Country,Rank\nAda,SE,10\nBob,SE,200\n", Text(path));
	}

	[Fact]
	public void UpdatesThatNeedMissingColumnsFail()
	{
		var path = File("partial.csv", "Name,Rank\nAda,1\n");
		var e = Assert.Throws<InvalidOperationException>(() => CsvQueryable.FromFile(path).Update(r => true, r => r["Country"] = "NO"));
		Assert.Contains("Country", e.Message);
		Assert.Equal("Name,Rank\nAda,1\n", Text(path));
		Assert.Single(Directory.GetFiles(directory));
	}

	[Fact]
	public async Task DeletesAsynchronouslyAndCanBeCanceled()
	{
		var path = File("cancel.csv", Data.CustomersCsv);
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		var table = CsvQueryable.FromFile<Customer>(path);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => table.DeleteAsync(c => true, canceled.Token).AsTask());
		Assert.Equal(Data.CustomersCsv, Text(path));
		Assert.Equal(6, await table.DeleteAsync(c => true));
		Assert.Equal("Name,Country,Rank\n", Text(path));
	}

	[Fact]
	public void KeepsRecordsThatTheErrorHandlerSkips()
	{
		var path = File("errors.csv", "Name,Country,Rank\nAda,NO,x\nBob,SE,2\n");
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { OnRecordError = _ => true } };
		Assert.Equal(1, CsvQueryable.FromFile<Customer>(path, options).Delete(c => c.Rank < 5));
		Assert.Equal("Name,Country,Rank\nAda,NO,x\n", Text(path));
		Assert.Throws<CsvFormatException>(() => CsvQueryable.FromFile<Customer>(path).Delete(c => c.Rank < 5));
	}

	[Fact]
	public void OnlyUtf8FilesChangeInPlace()
	{
		var path = File("utf16.csv");
		System.IO.File.WriteAllText(path, "Name,Country,Rank\nAda,NO,1\n", Encoding.Unicode);
		Assert.Throws<NotSupportedException>(() => CsvQueryable.FromFile<Customer>(path).Delete(c => true));
	}
}
