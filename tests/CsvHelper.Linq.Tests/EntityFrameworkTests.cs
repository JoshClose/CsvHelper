#if NET
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>CSV queries that join with Entity Framework Core tables, on SQLite: the SQL they cause, and what they return.</summary>
public sealed class EntityFrameworkTests : IDisposable
{
	private static readonly List<Account> Accounts =
	[
		new() { Name = "Ada", Country = "NO", Rank = 1, Status = Status.Open },
		new() { Name = "Bob", Country = "SE", Rank = 2, Status = Status.Shipped },
		new() { Name = "Cy", Country = "NO", Rank = 3, Status = Status.Open },
		new() { Name = "Dee", Country = "DK", Rank = 4, Status = Status.Cancelled },
		new() { Name = "Zed", Country = "FI", Rank = 9, Status = Status.Open },
	];

	private readonly SqliteConnection connection = new("Data Source=:memory:");
	private readonly List<string> commands = [];
	private readonly Shop db;

	public EntityFrameworkTests()
	{
		connection.Open();
		db = new Shop(connection, commands);
		db.Database.EnsureCreated();
		db.Accounts.AddRange(Accounts.Select(a => new Account { Name = a.Name, Country = a.Country, Rank = a.Rank, Status = a.Status }));
		db.SaveChanges();
		db.ChangeTracker.Clear();
		commands.Clear();
	}

	public void Dispose()
	{
		db.Dispose();
		connection.Dispose();
	}

	private List<string> Selects => commands.Where(c => c.Contains("SELECT")).ToList();

	[Fact]
	public void JoinsAskTheDatabaseForTheKeysOfTheRecords()
	{
		var query = Orders(new CsvQueryOptions { ForeignKeyBatchSize = 3 })
			.Join(db.Accounts.AsNoTracking(), o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Country });
		var expected = Data.Orders.Join(Accounts, o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Country });
		Assert.Equal(expected, query.ToList());
		var distinct = Data.Orders.Select(o => o.Customer).Distinct().Count();
		Assert.Equal((distinct + 2) / 3, Selects.Count);
		// The keys are a parameter of the query, not its text.
		Assert.All(Selects, s => Assert.Contains("json_each", s));
		Assert.All(Selects, s => Assert.DoesNotContain("'Ada'", s));
	}

	[Fact]
	public void FiltersOfTheTableRunInTheDatabase()
	{
		var query = Orders().Join(db.Accounts.Where(a => a.Country == "NO"), o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Rank });
		var expected = Data.Orders.Join(Accounts.Where(a => a.Country == "NO"), o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Rank });
		Assert.Equal(expected, query.ToList());
		Assert.Contains("\"Country\" = 'NO'", Assert.Single(Selects));
	}

	[Fact]
	public void GroupJoinsAndLeftJoins()
	{
		var query =
			from o in Orders()
			join a in db.Accounts on o.Customer equals a.Name into matched
			from a in matched.DefaultIfEmpty()
			select new { o.Id, Rank = a == null ? 0 : a.Rank };
		var expected =
			from o in Data.Orders
			join a in Accounts on o.Customer equals a.Name into matched
			from a in matched.DefaultIfEmpty()
			select new { o.Id, Rank = a == null ? 0 : a.Rank };
		Assert.Equal(expected, query.ToList());
		Assert.Single(Selects);
	}

	[Fact]
	public void CompositeKeys()
	{
		var query = Orders().Join(db.Accounts, o => new { Name = o.Customer, o.Status }, a => new { a.Name, a.Status }, (o, a) => o.Id);
		var expected = Data.Orders.Join(Accounts, o => new { Name = o.Customer, o.Status }, a => new { a.Name, a.Status }, (o, a) => o.Id);
		Assert.Equal(expected, query.ToList());
		Assert.Single(Selects);
	}

	[Fact]
	public void SubqueriesRunOnceInTheDatabase()
	{
		var names = Orders().Where(o => db.Accounts.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => Accounts.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id), names.ToList());
		Assert.Single(Selects);

		commands.Clear();
		var ranked = Orders().Where(o => db.Accounts.Any(a => a.Name == o.Customer && a.Rank < 3)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => Accounts.Any(a => a.Name == o.Customer && a.Rank < 3)).Select(o => o.Id), ranked.ToList());
		var select = Assert.Single(Selects);
		Assert.Contains("\"Rank\" < 3", select);
		Assert.DoesNotContain("\"Country\"", select);

		commands.Clear();
		var average = Orders().Where(o => o.Quantity > db.Accounts.Average(a => a.Rank)).Select(o => o.Id);
		Assert.Equal(Data.Orders.Where(o => o.Quantity > Accounts.Average(a => a.Rank)).Select(o => o.Id), average.ToList());
		Assert.Contains("AVG", Assert.Single(Selects));
	}

	[Fact]
	public async Task RunsAsynchronously()
	{
		var query = Orders().Where(o => db.Accounts.Select(a => a.Name).Contains(o.Customer)).Select(o => o.Id);
		Assert.Equal(query.ToList(), await query.AsCsvAsync().ToListAsync());
		// Entity Framework Core's own ToListAsync works on CSV queries, which are asynchronous enumerables.
		Assert.Equal(query.ToList(), await EntityFrameworkQueryableExtensions.ToListAsync(query));
		var joined = Orders().Join(db.Accounts, o => o.Customer, a => a.Name, (o, a) => new { o.Id, a.Rank }).OrderBy(x => x.Rank).ThenBy(x => x.Id);
		Assert.Equal(joined.ToList(), await joined.AsCsvAsync().ToListAsync());
	}

	[Fact]
	public async Task WorksAlongsideEntityFrameworkCoreAndAsyncEnumerable()
	{
		// This file uses Microsoft.EntityFrameworkCore, System.Linq (with .NET 10's AsyncEnumerable) and CsvHelper.Linq
		// together: none of these calls may be ambiguous, which would not compile.
		var table = CsvQueryable.FromText<Order>(Data.OrdersCsv);
		var big = table.Where(o => o.Total > 100);
		Assert.Equal(Data.Orders.Count(o => o.Total > 100), await big.AsCsvAsync().CountAsync());
		Assert.Equal(Data.Orders.Count(o => o.Total > 100), (await big.ToListAsync()).Count);
		var count = 0;
		await foreach (var order in table)
		{
			count++;
		}

		Assert.Equal(60, count);
		await foreach (var order in big.AsAsyncEnumerable())
		{
			count--;
		}

		Assert.Equal(60 - Data.Orders.Count(o => o.Total > 100), count);
		var stream = new MemoryStream();
		Assert.Equal(Accounts.Count, await db.Accounts.WriteCsvAsync(stream, leaveOpen: true));
		Assert.StartsWith("Name,Country,Rank,Status\r\nAda,NO,1,Open\r\n", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
		Assert.Equal(Accounts.Count, db.Accounts.WriteCsv(new StringWriter()));
	}

	[Fact]
	public void TheDatabaseCannotRunCsvQueries()
	{
		// Entity Framework Core cannot translate a CSV query inside its own: read it first, and use the list.
		var names = Customers().Where(c => c.Rank < 3).Select(c => c.Name).ToList();
		var accounts = db.Accounts.Where(a => names.Contains(a.Name)).Select(a => a.Name).OrderBy(n => n).ToList();
		Assert.Equal(["Ada", "Bob"], accounts);
	}

	/// <summary>A database of accounts.</summary>
	private sealed class Shop(SqliteConnection connection, List<string> commands) : DbContext
	{
		public DbSet<Account> Accounts => Set<Account>();

		protected override void OnConfiguring(DbContextOptionsBuilder options) =>
			options.UseSqlite(connection).LogTo(commands.Add, [RelationalEventId.CommandExecuted]);

		protected override void OnModelCreating(ModelBuilder model) => model.Entity<Account>().HasKey(a => a.Name);
	}
}
#endif
