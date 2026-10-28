using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>CSV queries that use queries of other providers: what they ask the other provider for, and what they return.</summary>
public class FederationTests
{
	private static readonly List<Account> Accounts =
	[
		new() { Name = "Ada", Country = "NO", Rank = 1, Status = Status.Open },
		new() { Name = "Bob", Country = "SE", Rank = 2, Status = Status.Shipped },
		new() { Name = "Cy", Country = "NO", Rank = 3, Status = Status.Open },
		new() { Name = "Dee", Country = "DK", Rank = 4, Status = Status.Cancelled },
		new() { Name = "Zed", Country = "FI", Rank = 9, Status = Status.Open },
	];

	private static int DistinctCustomers => Data.Orders.Select(o => o.Customer).Distinct().Count();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void JoinsAskOnlyForTheKeysOfTheRecords(bool extensionRoot)
	{
		var provider = new RecordingProvider<Account>(Accounts) { UseExtensionRoot = extensionRoot };
		var options = new CsvQueryOptions { ForeignKeyBatchSize = 2 };
		var query = Orders(options).Join(provider.Table, o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Country });
		var expected = Data.Orders.Join(Accounts, o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Country }).ToList();
		Assert.Equal(expected, query.ToList());
		// One query per batch of two keys, each with only its keys.
		Assert.Equal((DistinctCustomers + 1) / 2, provider.Executed.Count);
		Assert.All(provider.Executed, q => Assert.Contains("Contains", q));
		var plan = CsvQueryable.Explain(query);
		Assert.Equal("keys", Assert.Single(plan.ForeignQueries).Strategy);
		Assert.Equal(["Customer", "Id"], Assert.Single(plan.Sources).Members);
	}

	[Fact]
	public void GroupJoinsAndLeftJoinsAskForTheKeys()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var grouped = Orders().GroupJoin(provider.Table, o => o.Customer, a => a.Name, (o, accounts) => new { o.Id, Ranks = accounts.Sum(a => a.Rank) });
		Assert.Equal(Data.Orders.GroupJoin(Accounts, o => o.Customer, a => a.Name, (o, accounts) => new { o.Id, Ranks = accounts.Sum(a => a.Rank) }), grouped.ToList());
		var left =
			from o in Orders()
			join a in provider.Table on o.Customer equals a.Name into matched
			from a in matched.DefaultIfEmpty()
			select new { o.Id, Country = a == null ? "?" : a.Country };
		var expected =
			from o in Data.Orders
			join a in Accounts on o.Customer equals a.Name into matched
			from a in matched.DefaultIfEmpty()
			select new { o.Id, Country = a == null ? "?" : a.Country };
		Assert.Equal(expected, left.ToList());
		Assert.Equal(2, provider.Executed.Count);
		Assert.All(provider.Executed, q => Assert.Contains("Contains", q));
	}

	[Fact]
	public void CompositeKeysAskForEachMember()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var query = Orders().Join(provider.Table, o => new { Name = o.Customer, o.Status }, a => new { a.Name, a.Status }, (o, a) => new { o.Id, a.Rank });
		var expected = Data.Orders.Join(Accounts, o => new { Name = o.Customer, o.Status }, a => new { a.Name, a.Status }, (o, a) => new { o.Id, a.Rank });
		Assert.Equal(expected, query.ToList());
		var executed = Assert.Single(provider.Executed);
		Assert.Contains("Name", executed);
		Assert.Contains("Status", executed);
	}

	[Fact]
	public void TooManyKeysReadTheQueryOnce()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var query = Orders(new CsvQueryOptions { MaxForeignKeys = 2 }).Join(provider.Table, o => o.Customer, a => a.Name, (o, a) => o.Id);
		Assert.Equal(Data.Orders.Join(Accounts, o => o.Customer, a => a.Name, (o, a) => o.Id), query.ToList());
		Assert.DoesNotContain("Contains", Assert.Single(provider.Executed));
	}

	[Fact]
	public void KeysCanBeTurnedOff()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var query = Orders(new CsvQueryOptions { MaxForeignKeys = 0 }).Join(provider.Table, o => o.Customer, a => a.Name, (o, a) => o.Id);
		Assert.Equal(Data.Orders.Join(Accounts, o => o.Customer, a => a.Name, (o, a) => o.Id), query.ToList());
		Assert.Equal("once", Assert.Single(CsvQueryable.Explain(query).ForeignQueries).Strategy);
		Assert.DoesNotContain("Contains", Assert.Single(provider.Executed));
	}

	[Fact]
	public void UntranslatableKeysReadTheQueryOnce()
	{
		var provider = new RecordingProvider<Account>(Accounts) { RefuseContains = true };
		var query = Orders().Join(provider.Table.Where(a => a.Rank < 4), o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Rank });
		Assert.Equal(Data.Orders.Join(Accounts.Where(a => a.Rank < 4), o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Rank }), query.ToList());
		// The provider's own filter still runs on its side.
		Assert.Contains("Rank", Assert.Single(provider.Executed));
	}

	[Fact]
	public void ReaderSourcesAreReadOnceForTheirKeys()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		using var reader = CsvReader.FromText(Data.OrdersCsv);
		var query = reader.Query<Order>().Join(provider.Table, o => o.Customer, a => a.Name, (o, a) => o.Id);
		Assert.Equal(Data.Orders.Join(Accounts, o => o.Customer, a => a.Name, (o, a) => o.Id), query.ToList());
		Assert.Single(provider.Executed);
	}

	[Fact]
	public void SubqueriesRunOnce()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var names = Orders().Where(o => provider.Table.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => Accounts.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id), names.ToList());
		Assert.Single(provider.Executed);
		Assert.Equal("set", Assert.Single(CsvQueryable.Explain(names).ForeignQueries).Strategy);

		provider.Executed.Clear();
		var ranked = Orders().Where(o => provider.Table.Any(a => a.Name == o.Customer && a.Rank < 3)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => Accounts.Any(a => a.Name == o.Customer && a.Rank < 3)).Select(o => o.Id), ranked.ToList());
		// Only the names of the accounts with a rank below 3 are asked for.
		var executed = Assert.Single(provider.Executed);
		Assert.Contains("Rank", executed);
		Assert.Contains("Select", executed);

		provider.Executed.Clear();
		var average = Orders().Where(o => o.Quantity > provider.Table.Average(a => a.Rank)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => o.Quantity > Accounts.Average(a => a.Rank)).Select(o => o.Id), average.ToList());
		Assert.Single(provider.Executed);
		Assert.Equal("value", Assert.Single(CsvQueryable.Explain(average).ForeignQueries).Strategy);

		provider.Executed.Clear();
		var counts = Orders().Select(o => new { o.Id, Count = provider.Table.Count(a => a.Name == o.Customer || a.Rank > o.Quantity) });
		Assert.Equal(Data.Orders.Select(o => new { o.Id, Count = Accounts.Count(a => a.Name == o.Customer || a.Rank > o.Quantity) }), counts.ToList());
		Assert.Single(provider.Executed);
	}

	[Fact]
	public void QueriesInVariablesArePlanned()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var accounts = provider.Table.Where(a => a.Country == "NO");
		var opened = 0;
		var customers = CsvQueryable.FromStream<Customer>(() =>
		{
			opened++;

			return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Data.CustomersCsv));
		});
		var query = Orders()
			.Where(o => accounts.Any(a => a.Name == o.Customer))
			.Where(o => customers.Any(c => c.Name == o.Customer && c.Rank < 3))
			.Select(o => o.Id);
		var expected = Data.Orders
			.Where(o => Accounts.Where(a => a.Country == "NO").Any(a => a.Name == o.Customer))
			.Where(o => Data.CustomerList.Any(c => c.Name == o.Customer && c.Rank < 3))
			.Select(o => o.Id);
		Assert.Equal(expected, query.ToList());
		// Each runs once, not once for each order.
		Assert.Single(provider.Executed);
		Assert.Equal(1, opened);
		var plan = CsvQueryable.Explain(query);
		Assert.Equal(2, plan.Sources.Count);
		Assert.Equal(["Name", "Rank"], plan.Sources[1].Members);
	}

	[Fact]
	public void UnknownProvidersAreNotSupported()
	{
		var root = new AnonymousRoot();
		var call = Expression.Call(typeof(Queryable), nameof(Queryable.Join), [typeof(Order), typeof(Account), typeof(string), typeof(int)],
			Orders().Expression, root,
			Expression.Quote((Expression<Func<Order, string>>)(o => o.Customer)),
			Expression.Quote((Expression<Func<Account, string>>)(a => a.Name)),
			Expression.Quote((Expression<Func<Order, Account, int>>)((o, a) => o.Id)));
		var query = Orders().Provider.CreateQuery<int>(call);
		Assert.Contains("ToList", Assert.Throws<NotSupportedException>(() => query.ToList()).Message);
	}

	[Fact]
	public async Task RunsAsynchronously()
	{
		var provider = new RecordingProvider<Account>(Accounts);
		var query = Orders().Join(provider.Table, o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Country }).OrderBy(x => x.Country).ThenBy(x => x.Id);
		Assert.Equal(query.ToList(), await query.AsCsvAsync().ToListAsync());
		var subquery = Orders().Where(o => provider.Table.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id);
		Assert.Equal(subquery.ToList(), await subquery.AsCsvAsync().ToListAsync());
	}
}
