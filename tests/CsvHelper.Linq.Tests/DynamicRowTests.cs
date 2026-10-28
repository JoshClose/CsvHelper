using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>
/// Queries of CsvDynamicRecord rows: the reader runs their filters and projections on its records by column, without
/// making rows, and they return what the same queries return over the rows in memory.
/// </summary>
public class DynamicRowTests
{
	private static readonly List<CsvDynamicRecord> Rows = Data.Read<CsvDynamicRecord>(Data.OrdersCsv);

	private static void SameRows<TResult>(Func<IQueryable<CsvDynamicRecord>, TResult> query, CsvQueryOptions? options = null) =>
		Assert.Equal(Run(() => query(Rows.AsQueryable())), Run(() => query(CsvQueryable.FromText(Data.OrdersCsv, options))));

	private static CsvSourcePlan Plan(IQueryable query) => Assert.Single(CsvQueryable.Explain(query).Sources);

	[Fact]
	public void FiltersAndProjectsByColumn()
	{
		SameRows(q => q.Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"]));
		SameRows(q => q.Where(r => r["Status"] == "Open" && r.Parse<int>("Quantity") > 3).Select(r => new { Id = r.Parse<long>("Id"), r.FieldCount }));
		SameRows(q => q.Select(r => new { Placed = r.Parse<DateTime>("Placed"), Note = r[6], Has = r.ContainsKey("Note"), Lacks = r.ContainsKey("Nope") }));
		SameRows(q => q.Skip(5).Where(r => r.Parse<double>("Total") < 50).Select(r => r["Id"]).Take(3));
		SameRows(q => q.Where(r => r["Note"] == null).Count());
		SameRows(q => q.GroupBy(r => r["Customer"]).Select(g => new { g.Key, Sum = g.Sum(r => r.Parse<decimal>("Total")) }));
		SameRows(q => q.OrderBy(r => r.Parse<decimal>("Total")).Select(r => r["Id"]).First());
		SameRows(q => q.Where(r => r.Parse<Status>("Status") == Status.Shipped).Select(r => new { Id = r["Id"], Status = r.Parse<Status>("Status") }));
	}

	[Fact]
	public void ComparesTextAsTheRowsDo()
	{
		var readerOptions = new CsvReaderOptions { NullValues = ["NULL", ""] };
		const string csv = "A,B\n1,NULL\n2,\n3,x\n4,\"x\"\n5,é\n6,xy\n";
		List<CsvDynamicRecord> rows;
		using (var reader = CsvReader.FromText(csv, readerOptions))
		{
			rows = reader.GetRecords<CsvDynamicRecord>().ToList();
		}

		var query = CsvQueryable.FromText(csv, new CsvQueryOptions { ReaderOptions = readerOptions });
		foreach (var text in new[] { "NULL", "", "x", "é", "y" })
		{
			Assert.Equal(rows.Where(r => r["B"] == text).Select(r => r["A"]), query.Where(r => r["B"] == text).Select(r => r["A"]).ToList());
			Assert.Equal(rows.Where(r => text != r["B"]).Select(r => r["A"]), query.Where(r => text != r["B"]).Select(r => r["A"]).ToList());
		}

		Assert.Equal(["3", "4"], query.Where(r => r["B"] == "x").Select(r => r["A"]).ToList());
		Assert.Equal(["1", "2"], query.Where(r => r["B"] == null).Select(r => r["A"]).ToList());
		Assert.Equal(["B"], Plan(query.Where(r => r["B"] == "x")).FilterMembers);
	}

	[Fact]
	public void PushesTheColumnsIntoTheReader()
	{
		var plan = Plan(CsvQueryable.FromText(Data.OrdersCsv).Where(r => r.Parse<decimal>("Total") > 100).Select(r => new { Customer = r["Customer"], Id = r.Parse<int>("Id") }));
		Assert.Equal(["Where", "Select"], plan.PushedOperators);
		Assert.Equal(["Total"], plan.FilterMembers);
		Assert.Equal(["Customer", "Id"], plan.Members);
		var column = "Quantity";
		plan = Plan(CsvQueryable.FromText(Data.OrdersCsv).Where(r => r.Parse<int>(column) > 3));
		Assert.Equal(["Where"], plan.PushedOperators);
		Assert.Equal(["Quantity"], plan.FilterMembers);
		Assert.Null(plan.Members);

		// Enums too: an extension method of the row on .NET, the row's own Parse<T> elsewhere.
		plan = Plan(CsvQueryable.FromText(Data.OrdersCsv).Where(r => r.Parse<Status>("Status") == Status.Shipped));
		Assert.Equal(["Where"], plan.PushedOperators);
		Assert.Equal(["Status"], plan.FilterMembers);
	}

	[Fact]
	public void RowsUsedOtherwiseAreMade()
	{
		SameRows(q => q.Where(r => r.Names.Count > 3).Select(r => r["Id"]));
		SameRows(q => q.Where(r => r.Parse<int>("Id") > 50).Select(r => r));
		SameRows(q => q.Select(r => r.ToDictionary()).Select(d => d["Customer"]));
		var plan = Plan(CsvQueryable.FromText(Data.OrdersCsv).Where(r => r.Names.Count > 3).Select(r => r["Id"]));
		Assert.Equal(["Where"], plan.PushedOperators);
		Assert.Contains("Names", Assert.Single(plan.Notes));
		Assert.Equal(["Select"], CsvQueryable.Explain(CsvQueryable.FromText(Data.OrdersCsv).Select(r => r)).InMemoryOperators);
	}

	[Fact]
	public void MissingColumnsThrowAsRowsDo()
	{
		SameRows(q => q.Select(r => r["Nope"]));
		SameRows(q => q.Where(r => r.Parse<int>("Nope") > 0));
		SameRows(q => q.Where(r => r.ContainsKey("Nope")).Select(r => r["Nope"]));
		// An empty source never reads the column.
		Assert.Empty(CsvQueryable.FromText("Id\n").Select(r => r["Nope"]).ToList());
	}

	[Fact]
	public void RowsWithoutAHeaderAreReadWhole()
	{
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { HasHeader = false } };
		var csv = "1,Ada\n2,Bob\n";
		var query = CsvQueryable.FromText(csv, options).Where(r => r["Field1"] == "2").Select(r => r[1]);
		Assert.Equal(["Bob"], query.ToList());
		Assert.Equal("rows without a header are read whole", Assert.Single(Plan(query).Notes));
	}

	[Fact]
	public void PruningCanBeTurnedOff()
	{
		var options = new CsvQueryOptions { PruneColumns = false };
		SameRows(q => q.Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"]), options);
		Assert.Equal(["Where"], Plan(CsvQueryable.FromText(Data.OrdersCsv, options).Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"])).PushedOperators);
	}

	[Fact]
	public void JoinsWithRecords()
	{
		var customers = CsvQueryable.FromText(Data.CustomersCsv);
		var joined = customers.Join(Orders(), r => r["Name"], o => o.Customer, (r, o) => new { Name = r["Name"], o.Id }).ToList();
		var expected = Data.Read<CsvDynamicRecord>(Data.CustomersCsv).Join(Data.Orders, r => r["Name"], o => o.Customer, (r, o) => new { Name = r["Name"], o.Id });
		Assert.Equal(expected, joined);
	}

	[Fact]
	public async Task RunsAsynchronously()
	{
		var query = CsvQueryable.FromText(Data.OrdersCsv).Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"]);
		Assert.Equal(query.ToList(), await query.AsCsvAsync().ToListAsync());
		Assert.Equal(query.Count(), await query.AsCsvAsync().CountAsync());
		var ids = new List<string?>();
		await foreach (var row in CsvQueryable.FromText(Data.OrdersCsv))
		{
			ids.Add(row["Id"]);
		}

		Assert.Equal(Rows.Select(r => r["Id"]), ids);
	}

	[Fact]
	public void TablesOfFiles()
	{
		var path = Path.GetTempFileName();
		try
		{
			File.WriteAllText(path, Data.OrdersCsv);
			var table = CsvQueryable.FromFile(path);
			Assert.Equal(Rows.Count(r => r["Customer"] == "Ada"), table.Count(r => r["Customer"] == "Ada"));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
