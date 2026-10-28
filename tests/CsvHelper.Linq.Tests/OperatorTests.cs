using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>Every LINQ operator returns over CSV what it returns over the same records in memory.</summary>
public class OperatorTests
{
	[Fact]
	public void Filters()
	{
		Same(q => q.Where(o => o.Total > 100));
		Same(q => q.Where(o => o.Status == Status.Shipped && o.Quantity > 3).Select(o => o.Id));
		Same(q => q.Where((o, i) => i % 3 == 0).Select(o => o.Id));
		Same(q => q.Where(o => o.Note == null).Select(o => o.Id));
		Same(q => q.Where(o => o.Note != null && o.Note.Contains("rush")).Select(o => new { o.Id, o.Note }));
		Same(q => q.Where(o => o.Total > 10).Where(o => o.Customer.StartsWith("A")).Select(o => o.Total));
		Same(q => q.Where(o => o.Discounted > 100).Select(o => o.Id));
		Same(q => q.Where(o => false));
		Same(q => q.Where(o => true).Count());
	}

	[Fact]
	public void Projections()
	{
		Same(q => q.Select(o => new { o.Id, o.Customer }));
		Same(q => q.Select((o, i) => new { i, o.Total }));
		Same(q => q.Select(o => o));
		Same(q => q.Select(o => o.Discounted));
		Same(q => q.Select(o => new Tuple<int, string>(o.Id, o.Customer)));
		Same(q => q.Select(o => new { o.Id, Twice = new { Total = o.Total * 2 } }).Select(x => x.Twice.Total));
		Same(q => q.Select(o => o.Placed.Year * 100 + o.Placed.Month).Distinct());
		Same(q => q.SelectMany(o => o.Customer).Count());
		Same(q => q.SelectMany(o => Enumerable.Range(0, o.Quantity), (o, n) => new { o.Id, n }).Where(x => x.n > 7));
	}

	[Fact]
	public void Ordering()
	{
		Same(q => q.OrderBy(o => o.Customer).ThenByDescending(o => o.Total).Select(o => o.Id));
		Same(q => q.OrderByDescending(o => o.Placed).ThenBy(o => o.Id).Select(o => new { o.Id, o.Placed }));
		Same(q => q.OrderBy(o => o.Total).Take(3));
		Same(q => q.Reverse().Select(o => o.Id));
		Same(q => q.OrderBy(o => o.Status).ThenBy(o => o.Customer).Select(o => o.Status + "/" + o.Customer));
	}

	[Fact]
	public void Grouping()
	{
		Same(q => q.GroupBy(o => o.Customer));
		Same(q => q.GroupBy(o => o.Customer, o => o.Total));
		Same(q => q.GroupBy(o => o.Customer, (k, os) => new { k, Sum = os.Sum(x => x.Total), Count = os.Count() }));
		Same(q => q.GroupBy(o => o.Status, o => o.Quantity, (k, qs) => new { k, Max = qs.Max() }));
		Same(q => q.GroupBy(o => o.Customer).Select(g => new { g.Key, Count = g.Count(), Max = g.Max(x => x.Total), First = g.First().Id }));
		Same(q => q.GroupBy(o => o.Customer).Where(g => g.Count() > 8).SelectMany(g => g).Select(o => o.Id));
		Same(q => q.GroupBy(o => new { o.Customer, o.Status }).Select(g => new { g.Key.Customer, g.Key.Status, Total = g.Sum(x => x.Total) }));
		Same(q => q.GroupBy(o => o.Customer).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key));
	}

	[Fact]
	public void Joins()
	{
		Same((o, c, l) => o.Join(c, x => x.Customer, y => y.Name, (x, y) => new { x.Id, y.Country }));
		Same((o, c, l) => c.Join(o, y => y.Name, x => x.Customer, (y, x) => new { y.Name, x.Total }).Where(r => r.Total > 50));
		Same((o, c, l) => o.GroupJoin(l, x => x.Id, y => y.OrderId, (x, ys) => new { x.Id, Lines = ys.Count(), Sum = ys.Sum(y => y.Price * y.Count) }));
		Same((o, c, l) => c.SelectMany(x => o.Where(y => y.Customer == x.Name).Take(2), (x, y) => new { x.Name, y.Id }));
		Same((o, c, l) => o.Join(Data.Customers, x => x.Customer, n => n, (x, n) => x.Id));
		Same((o, c, l) => o.Join(o, x => x.Id, y => y.Id - 1, (x, y) => new { x.Id, Next = y.Total }));
	}

	[Fact]
	public void QuerySyntax()
	{
		Same(q =>
			from o in q
			where o.Total > 50
			let doubled = o.Total * 2
			orderby doubled descending
			select new { o.Id, doubled });
		Same((orders, customers, lines) =>
			from o in orders
			join c in customers on o.Customer equals c.Name
			where c.Country == "NO" && o.Quantity > 2
			select new { o.Id, c.Rank });
		Same((orders, customers, lines) =>
			from o in orders
			join c in customers on o.Customer equals c.Name into matched
			from c in matched.DefaultIfEmpty()
			select new { o.Id, Country = c == null ? "?" : c.Country });
		Same((orders, customers, lines) =>
			from o in orders
			from l in lines
			where l.OrderId == o.Id && l.Count > 2
			group l.Price by o.Customer into g
			select new { g.Key, Sum = g.Sum() });
	}

	[Fact]
	public void Partitioning()
	{
		Same(q => q.Skip(5).Select(o => o.Id));
		Same(q => q.Take(5).Select(o => o.Id));
		Same(q => q.Skip(5).Take(5).Select(o => o.Id));
		Same(q => q.Skip(2).Skip(3).Where(o => o.Total > 20).Select(o => o.Id));
		Same(q => q.Where(o => o.Total > 20).Skip(3).Select(o => o.Id));
		Same(q => q.Skip(-1).Take(2).Select(o => o.Id));
		Same(q => q.Take(0));
		Same(q => q.Skip(1000));
		Same(q => q.SkipWhile(o => o.Total < 200).Select(o => o.Id));
		Same(q => q.TakeWhile(o => o.Total < 200).Select(o => o.Id));
		Same(q => q.Skip(10).Count());
	}

	[Fact]
	public void Sets()
	{
		Same(q => q.Select(o => o.Customer).Distinct());
		Same(q => q.Distinct().Count());
		Same(q => q.Where(o => o.Total > 100).Select(o => o.Id).Union(q.Where(o => o.Quantity > 5).Select(o => o.Id)));
		Same(q => q.Select(o => o.Customer).Intersect(Data.Customers.Take(3)));
		Same(q => q.Select(o => o.Customer).Except(new[] { "Ada", "Bob" }));
		Same(q => q.Take(3).Concat(q.Skip(57)).Select(o => o.Id));
		Same(q => q.Select(o => o.Id).SequenceEqual(Enumerable.Range(1, 60)));
		Same(q => q.Take(2).Append(new Order { Id = 99 }).Prepend(new Order { Id = 98 }).Select(o => o.Id));
	}

	[Fact]
	public void Aggregates()
	{
		Same(q => q.Count());
		Same(q => q.Count(o => o.Status == Status.Cancelled));
		Same(q => q.LongCount(o => o.Total > 100));
		Same(q => q.Sum(o => o.Total));
		Same(q => q.Sum(o => o.Quantity));
		Same(q => q.Min(o => o.Placed));
		Same(q => q.Max(o => o.Total));
		Same(q => q.Average(o => o.Total));
		Same(q => q.Average(o => o.Quantity));
		Same(q => q.Select(o => o.Total).Max());
		Same(q => q.Aggregate(0m, (sum, o) => sum + o.Total));
		Same(q => q.Aggregate(0, (sum, o) => sum + o.Quantity, sum => sum * 2));
		Same(q => q.Aggregate((a, b) => a.Total >= b.Total ? a : b));
		Same(q => q.Where(o => o.Total > 1000).Sum(o => o.Total));
		Same(q => q.Where(o => o.Total > 1000).Average(o => o.Total));
	}

	[Fact]
	public void Elements()
	{
		Same(q => q.First());
		Same(q => q.First(o => o.Total > 200));
		Same(q => q.FirstOrDefault(o => o.Total > 10_000));
		Same(q => q.Last());
		Same(q => q.LastOrDefault(o => o.Customer == "Bob"));
		Same(q => q.Single(o => o.Id == 7));
		Same(q => q.Single());
		Same(q => q.SingleOrDefault(o => o.Id == 700));
		Same(q => q.ElementAt(3));
		Same(q => q.ElementAt(1000));
		Same(q => q.ElementAtOrDefault(1000));
		Same(q => q.Any());
		Same(q => q.Any(o => o.Status == Status.Open));
		Same(q => q.All(o => o.Quantity > 0));
		Same(q => q.Contains(new Order()));
		Same(q => q.Select(o => o.Id).Contains(17));
		Same(q => q.Where(o => o.Id > 1000).DefaultIfEmpty().Select(o => o == null ? -1 : o.Id));
		Same(q => q.Where(o => o.Id > 1000).Select(o => o.Id).DefaultIfEmpty(7));
	}

	[Fact]
	public void Conversions()
	{
		Same(q => q.Select(o => (object)o.Id).Cast<int>().Sum());
		Same(q => q.Select(o => o.Id % 2 == 0 ? (object)o.Id : o.Customer).OfType<string>());
		Same(q => q.AsQueryable().Where(o => o.Id < 4).Select(o => o.Id));
		Same(q => q.AsEnumerable().Where(o => o.Id < 4).Select(o => o.Id).ToList());
	}

	[Fact]
	public void Comparers()
	{
		Same(q => q.Select(o => o.Customer.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase));
		Same(q => q.OrderBy(o => o.Customer, StringComparer.Ordinal).ThenByDescending(o => o.Id, Comparer<int>.Default).Select(o => o.Id));
		Same(q => q.Select(o => o.Customer).Contains("ada", StringComparer.OrdinalIgnoreCase));
		Same(q => q.GroupBy(o => o.Customer.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase).Select(g => new { g.Key, Count = g.Count() }));
		Same(q => q.Select(o => o.Id).SequenceEqual(Enumerable.Range(1, 60), EqualityComparer<int>.Default));
		Same((o, c, l) => o.Join(c, x => x.Customer.ToUpperInvariant(), y => y.Name, (x, y) => new { x.Id, y.Country }, StringComparer.OrdinalIgnoreCase));
#if NET
		Same(q => q.MinBy(o => o.Customer, StringComparer.Ordinal));
		Same(q => q.DistinctBy(o => o.Customer.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase).Select(o => o.Id));
#endif
	}

	[Fact]
	public void Zipping()
	{
		Same(q => q.Zip(q.Skip(1), (a, b) => b.Total - a.Total));
#if NET
		Same(q => q.Zip(q.Skip(1)).Select(p => p.First.Id + p.Second.Id));
#endif
	}

#if NET
	[Fact]
	public void NewerOperators()
	{
		Same(q => q.Chunk(7).Select(c => c.Length));
		Same(q => q.MinBy(o => o.Total));
		Same(q => q.MaxBy(o => o.Placed));
		Same(q => q.DistinctBy(o => o.Customer).Select(o => o.Id));
		Same(q => q.Take(5).UnionBy(q.Skip(3).Take(5), o => o.Id).Select(o => o.Id));
		Same(q => q.IntersectBy(new[] { 2, 4, 6 }, o => o.Id).Select(o => o.Total));
		Same(q => q.ExceptBy(new[] { "Ada" }, o => o.Customer).Count());
		Same(q => q.Select(o => o.Quantity).Order());
		Same(q => q.Select(o => o.Customer).OrderDescending().First());
		Same(q => q.Take(5..8).Select(o => o.Id));
		Same(q => q.SkipLast(55).Select(o => o.Id));
		Same(q => q.TakeLast(2).Select(o => o.Id));
		Same(q => q.Where(o => o.Id > 1000).FirstOrDefault(new Order { Id = -1 }));
		Same(q => q.ElementAt(^1));
		Same(q => q.ElementAtOrDefault(^100));
	}
#endif

#if NET9_0_OR_GREATER
	[Fact]
	public void Net9Operators()
	{
		Same(q => q.CountBy(o => o.Customer));
		Same(q => q.AggregateBy(o => o.Customer, 0m, (sum, o) => sum + o.Total));
		Same(q => q.Index().Where(p => p.Index % 10 == 0).Select(p => p.Item.Customer));
	}
#endif

#if NET10_0_OR_GREATER
	[Fact]
	public void Net10Operators()
	{
		Same((o, c, l) => o.LeftJoin(c, x => x.Customer, y => y.Name, (x, y) => new { x.Id, Country = y == null ? null : y.Country }));
		Same((o, c, l) => c.RightJoin(o, y => y.Name, x => x.Customer, (y, x) => new { x.Id, Rank = y == null ? 0 : y.Rank }));
	}
#endif

	[Fact]
	public void QueriesRunEachTimeTheyAreEnumerated()
	{
		var query = Orders().Where(o => o.Total > 100).Select(o => o.Id);
		Assert.Equal(query.ToList(), query.ToList());
		Assert.Equal(Data.Orders.Count(o => o.Total > 100), query.Count());
	}

	[Fact]
	public void QueriesOfAReaderRunOnce()
	{
		using var reader = CsvReader.FromText(Data.OrdersCsv);
		Assert.True(reader.Read());
		var query = reader.Query<Order>().Select(o => o.Id);
		// The reader is past the first record already.
		Assert.Equal(Enumerable.Range(2, 59), query.ToList());
		Assert.Throws<InvalidOperationException>(() => query.ToList());
	}

	[Fact]
	public async Task QueriesOfAReaderCanUseItInSeveralPlaces()
	{
		var orders = Data.Orders;
		Assert.Equal(orders.Zip(orders.Skip(1), (a, b) => b.Total - a.Total), OverReader(o => o.Zip(o.Skip(1), (a, b) => b.Total - a.Total)));
		Assert.Equal(orders.Join(orders, x => x.Id, y => y.Id - 1, (x, y) => new { x.Id, Next = y.Total }),
			OverReader(o => o.Join(o, x => x.Id, y => y.Id - 1, (x, y) => new { x.Id, Next = y.Total })));
		Assert.Equal(orders.Where(x => x.Total > orders.Average(y => y.Total)).Select(x => x.Id),
			OverReader(o => o.Where(x => x.Total > o.Average(y => y.Total)).Select(x => x.Id)));
		Assert.Equal(orders.Where(x => x.Total > 100).Select(x => x.Id).Concat(orders.Skip(50).Select(x => x.Id)),
			OverReader(o => o.Where(x => x.Total > 100).Select(x => x.Id).Concat(o.Skip(50).Select(x => x.Id))));

		using (var reader = CsvReader.FromText(Data.OrdersCsv))
		{
			var query = reader.Query<Order>();
			var joined = query.Join(query, x => x.Id, y => y.Id - 1, (x, y) => y.Total - x.Total);
			var source = Assert.Single(CsvQueryable.Explain(joined).Sources);
			Assert.Contains("2 places", Assert.Single(source.Notes));
			Assert.Equal(orders.Join(orders, x => x.Id, y => y.Id - 1, (x, y) => y.Total - x.Total), await joined.AsCsvAsync().ToListAsync());
		}

		using (var reader = CsvReader.FromText(Data.OrdersCsv))
		{
			var rows = reader.Query();
			var next = rows.Join(rows, x => x.Parse<int>("Id"), y => y.Parse<int>("Id") - 1, (x, y) => y["Customer"]).ToList();
			Assert.Equal(orders.Skip(1).Select(o => o.Customer), next);
		}
	}

	[Fact]
	public void MethodsThatTakeQueries()
	{
		var customers = Customers();
		var list = Data.CustomerList;
		Assert.Equal(Data.Orders.Select(o => list.Count(c => c.Name == o.Customer)), Orders().Select(o => CountOf(customers, o.Customer)).ToList());
		Assert.Equal(Data.Orders.Select(o => list.Where(c => c.Rank > 1).Count(c => c.Name == o.Customer)),
			Orders().Select(o => CountOf(customers.Where(c => c.Rank > 1), o.Customer)).ToList());
		Assert.Equal(Data.Orders.Select(o => list.OrderByDescending(c => c.Rank).First(c => c.Name != o.Customer).Name),
			Orders().Select(o => FirstOther(customers.OrderByDescending(c => c.Rank), o.Customer)).ToList());
	}

	[Fact]
	public void MarkersOfOtherProvidersThrowAsInMemory()
	{
		// An operator that only adds itself to the query, for a provider that knows it, has nothing to run in memory.
		Same(q => q.Where(o => o.Total > 100).Tagged("orders").Select(o => o.Id));
	}

	[Fact]
	public void SourcesOfAllKinds()
	{
		var bytes = System.Text.Encoding.UTF8.GetBytes(Data.OrdersCsv);
		var expected = Data.Orders.Select(o => o.Id).ToList();
		Assert.Equal(expected, CsvQueryable.FromBytes<Order>(bytes).Select(o => o.Id).ToList());
		Assert.Equal(expected, CsvQueryable.FromStream<Order>(() => new MemoryStream(bytes)).Select(o => o.Id).ToList());
		Assert.Equal(expected, CsvQueryable.FromTextReader<Order>(() => new StringReader(Data.OrdersCsv)).Select(o => o.Id).ToList());
		var path = Path.GetTempFileName();
		try
		{
			File.WriteAllText(path, Data.OrdersCsv);
			var table = CsvQueryable.FromFile<Order>(path);
			Assert.Equal(path, table.Path);
			Assert.Equal(expected, table.Select(o => o.Id).ToList());
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void StreamsAreDisposed()
	{
		var streams = new List<CountingStream>();
		var query = CsvQueryable.FromStream<Order>(() =>
		{
			var stream = new CountingStream(System.Text.Encoding.UTF8.GetBytes(Data.OrdersCsv));
			streams.Add(stream);

			return stream;
		});
		Assert.Equal(1, query.Select(o => o.Id).First());
		Assert.Equal(60, query.Count());
		Assert.Equal(2, streams.Count);
		Assert.All(streams, s => Assert.True(s.Disposed));
	}

	[Fact]
	public void RecordsWithoutAMapperFail()
	{
		var e = Assert.Throws<InvalidOperationException>(() => CsvQueryable.FromText<Uri>("a\nb\n").ToList());
		Assert.Contains("has no CSV mapper", e.Message);
	}

	[Fact]
	public void ProviderCreatesQueries()
	{
		IQueryable query = Orders();
		var created = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), "Take", [typeof(Order)], query.Expression, Expression.Constant(2)));
		Assert.IsAssignableFrom<CsvQuery<Order>>(created);
		Assert.Equal(2, created.Cast<Order>().Count());
		Assert.Equal(60, query.Provider.Execute(Expression.Call(typeof(Queryable), "Count", [typeof(Order)], query.Expression)));
		Assert.Throws<ArgumentException>(() => query.Provider.CreateQuery<string>(query.Expression));
		Assert.Throws<ArgumentException>(() => query.Provider.CreateQuery(Expression.Constant(3)));
	}

	/// <summary>Runs a query of the orders of a reader, and asserts that it cannot run again.</summary>
	private static List<TResult> OverReader<TResult>(Func<IQueryable<Order>, IQueryable<TResult>> query)
	{
		using var reader = CsvReader.FromText(Data.OrdersCsv);
		var built = query(reader.Query<Order>());
		var results = built.ToList();
		Assert.Throws<InvalidOperationException>(() => built.ToList());

		return results;
	}

	private static int CountOf(IQueryable<Customer> customers, string name) => customers.Count(c => c.Name == name);

	private static string FirstOther(IOrderedQueryable<Customer> customers, string name) => customers.First(c => c.Name != name).Name;
}

/// <summary>An operator that only adds itself to a query, as the operators of providers such as Entity Framework Core do.</summary>
public static class Markers
{
	public static IQueryable<T> Tagged<T>(this IQueryable<T> source, string tag) =>
		source.Provider.CreateQuery<T>(Expression.Call(null, new Func<IQueryable<T>, string, IQueryable<T>>(Tagged).Method, source.Expression, Expression.Constant(tag)));
}
