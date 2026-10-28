using System.Text;
using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>What the reader does itself: the members it reads, the rows it skips, and how early it stops.</summary>
public class PushdownTests
{
	private static CsvSourcePlan Source(IQueryable query) => Assert.Single(CsvQueryable.Explain(query).Sources);

	private static CsvSourcePlan Source<T>(IQueryable<Order> query, Func<IQueryable<Order>, T> terminal)
	{
		CsvQueryPlan? plan = null;
		var source = CsvQueryable.FromText<Order>(Data.OrdersCsv, new CsvQueryOptions { OnExecute = p => plan = p });
		terminal(source.Provider.CreateQuery<Order>(new Rebase(source).Visit(query.Expression)!));

		return Assert.Single(plan!.Sources);
	}

	[Fact]
	public void ReadsOnlyTheMembersAQueryUses()
	{
		var plan = Source(Orders().Where(o => o.Total > 100).Select(o => new { o.Id, o.Customer }));
		Assert.Equal(["Where"], plan.PushedOperators);
		Assert.Equal(["Total"], plan.FilterMembers);
		Assert.Equal(["Customer", "Id"], plan.Members);
		Assert.Empty(plan.Notes);
	}

	[Fact]
	public void CountingReadsNoMembers()
	{
		var plan = Source(Orders(), q => q.Count());
		Assert.Equal([], plan.Members);
		plan = Source(Orders().Where(o => o.Status == Status.Open), q => q.LongCount());
		Assert.Equal(["Status"], plan.FilterMembers);
		Assert.Equal([], plan.Members);
	}

	[Fact]
	public void WholeRecordsNeedAllMembers()
	{
		var plan = Source(Orders().Where(o => o.Total > 100));
		Assert.Null(plan.Members);
		Assert.Contains("returns whole records", Assert.Single(plan.Notes));
		Assert.Null(Source(Orders(), q => q.First()).Members);
		Assert.Null(Source(Orders().Select(o => new { o, o.Id })).Members);
	}

	[Fact]
	public void ComputedMembersNeedAllMembers()
	{
		var plan = Source(Orders().Select(o => o.Discounted));
		Assert.Null(plan.Members);
		Assert.Contains("Discounted", Assert.Single(plan.Notes));
	}

	[Fact]
	public void RecordsPassedToMethodsNeedAllMembers()
	{
		var plan = Source(Orders().Where(o => IsBig(o)).Select(o => o.Id));
		Assert.Null(plan.FilterMembers);
		Assert.Equal(["Id"], plan.Members);
		Assert.Contains("IsBig", plan.Notes[0]);
	}

	private static bool IsBig(Order order) => order.Total > 100;

	[Fact]
	public void FollowsGroups()
	{
		var plan = Source(Orders().GroupBy(o => o.Customer).Select(g => new { g.Key, Sum = g.Sum(x => x.Total), First = g.First().Placed }));
		Assert.Equal(["Customer", "Placed", "Total"], plan.Members);
		plan = Source(Orders().GroupBy(o => o.Customer, o => o.Quantity));
		Assert.Equal(["Customer", "Quantity"], plan.Members);
	}

	[Fact]
	public void FollowsQuerySyntax()
	{
		var query =
			from o in Orders()
			where o.Quantity > 1
			let doubled = o.Total * 2
			orderby doubled descending
			select new { o.Id, doubled };
		var plan = Source(query);
		Assert.Equal(["Quantity"], plan.FilterMembers);
		Assert.Equal(["Id", "Total"], plan.Members);
	}

	[Fact]
	public void FollowsJoins()
	{
		var query =
			from o in Orders()
			join c in Customers() on o.Customer equals c.Name into matched
			from c in matched.DefaultIfEmpty()
			where o.Quantity > 2
			select new { o.Id, Country = c == null ? "?" : c.Country };
		var plan = CsvQueryable.Explain(query);
		Assert.Equal(2, plan.Sources.Count);
		Assert.Equal(["Customer", "Id", "Quantity"], plan.Sources[0].Members);
		Assert.Equal(["Country", "Name"], plan.Sources[1].Members);
	}

	[Fact]
	public void FollowsTuples()
	{
#if NET9_0_OR_GREATER
		Assert.Equal(["Customer"], Source(Orders().Index().Select(p => p.Item.Customer)).Members);
#endif
		var plan = CsvQueryable.Explain(Orders().Zip(Customers(), (o, c) => o.Id + c.Name));
		Assert.Equal(["Id"], plan.Sources[0].Members);
		Assert.Equal(["Name"], plan.Sources[1].Members);
	}

	[Fact]
	public void ComparingRecordsNeedsAllMembers()
	{
		Assert.Null(Source(Orders().Distinct(), q => q.Count()).Members);
		Assert.Null(Source(Orders().GroupBy(o => o)).Members);
		Assert.Null(Source(Orders().OrderBy(o => o).Select(o => o.Id)).Members);
	}

	[Fact]
	public void SkipsRowsWithoutReadingThem()
	{
		var plan = Source(Orders().Skip(10).Skip(5).Where(o => o.Total > 5).Select(o => o.Id));
		Assert.Equal(15, plan.SkippedRows);
		Assert.Equal(["Skip(10)", "Skip(5)", "Where"], plan.PushedOperators);
		// A Skip after a Where counts the records that pass: the reader does not run it.
		plan = Source(Orders().Where(o => o.Total > 5).Skip(3).Select(o => o.Id));
		Assert.Equal(0, plan.SkippedRows);
		Assert.Equal(["Where"], plan.PushedOperators);
		Assert.Equal(["Skip", "Select"], CsvQueryable.Explain(Orders().Where(o => o.Total > 5).Skip(3).Select(o => o.Id)).InMemoryOperators);
	}

	[Fact]
	public void UnreadColumnsAreNotParsed()
	{
		var bad = Data.OrdersCsv.Replace(",Open,", ",Lost,");
		var query = CsvQueryable.FromText<Order>(bad);
		Assert.Throws<CsvFormatException>(() => query.ToList());
		Assert.Equal(Data.Orders.Select(o => o.Customer), query.Select(o => o.Customer).ToList());
		Assert.Equal(60, query.Count());
		Assert.Throws<CsvFormatException>(() => CsvQueryable.FromText<Order>(bad, new CsvQueryOptions { PruneColumns = false }).Select(o => o.Customer).ToList());
	}

	[Fact]
	public void UnreadColumnsMayBeMissing()
	{
		var query = CsvQueryable.FromText<Order>("Id,Customer\n1,Ada\n2,Bob\n");
		Assert.Equal(["Ada", "Bob"], query.Select(o => o.Customer).ToList());
		Assert.Throws<KeyNotFoundException>(() => query.Select(o => o.Total).ToList());
	}

	[Fact]
	public void PruningCanBeTurnedOff()
	{
		var plan = Source(Orders(new CsvQueryOptions { PruneColumns = false }).Select(o => o.Id));
		Assert.Null(plan.Members);
		Assert.Equal("PruneColumns is off", Assert.Single(plan.Notes));
	}

	[Fact]
	public void StopsReadingEarly()
	{
		var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(Data.OrdersCsv, 1).Concat(Enumerable.Range(0, 2000).Select(_ => Data.OrdersCsv.Substring(Data.OrdersCsv.IndexOf('\n') + 1)))));
		CountingStream? stream = null;
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { BufferSize = 4096 } };
		var query = CsvQueryable.FromStream<Order>(() => stream = new CountingStream(bytes), options);
		Assert.Equal(1, query.Select(o => o.Id).First());
		Assert.True(stream!.BytesRead < bytes.Length / 10, $"{stream.BytesRead} of {bytes.Length}");
		Assert.True(stream.Disposed);
		Assert.Equal([2, 3], query.Skip(1).Take(2).Select(o => o.Id).ToList());
		Assert.True(stream.BytesRead < bytes.Length / 10);
		Assert.Equal(120_060, query.Count());
		Assert.Equal(bytes.Length, stream.BytesRead);
	}

	[Fact]
	public void ConstructorsThatRejectDefaultsGetEveryMember()
	{
		// The projection gives Code "", which the constructor rejects: the record is read whole instead.
		var query = CsvQueryable.FromText<Checked>("Code,Value\nA,1\nB,2\n");
		Assert.Equal(["Value"], Source(query.Select(c => c.Value)).Members);
		Assert.Equal([1, 2], query.Select(c => c.Value).ToList());
		Assert.Throws<CsvFormatException>(() => CsvQueryable.FromText<Checked>("Code,Value\n,1\n").Select(c => c.Value).ToList());
	}

	[Fact]
	public void RecordErrorsOfReadMembersAreSkipped()
	{
		var errors = new List<CsvRecordError>();
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { OnRecordError = e => { errors.Add(e); return true; } } };
		var csv = "Id,Customer,Status,Total,Quantity,Placed\n1,Ada,Open,x,1,2026-01-01\n2,Bob,Open,5,y,2026-01-01\n";
		var query = CsvQueryable.FromText<Order>(csv, options);
		Assert.Equal([2], query.Select(o => new { o.Id, o.Total }).Select(o => o.Id).ToList());
		Assert.Single(errors);
		Assert.Equal([1], query.Where(o => o.Quantity > 0).Select(o => o.Id).ToList());
	}

	[Fact]
	public void OnExecuteGetsThePlan()
	{
		var plans = new List<CsvQueryPlan>();
		var query = Orders(new CsvQueryOptions { OnExecute = plans.Add }).Where(o => o.Quantity > 5).OrderBy(o => o.Total).Select(o => o.Id);
		_ = query.ToList();
		_ = query.Count();
		Assert.Equal(2, plans.Count);
		Assert.Equal(["OrderBy", "Select"], plans[0].InMemoryOperators);
		Assert.Equal(["OrderBy", "Select", "Count"], plans[1].InMemoryOperators);
		Assert.Contains("filter reads Quantity; reads Id, Total", plans[0].ToString());
	}

	[Fact]
	public void ExplainsOnlyCsvQueries()
	{
		Assert.Throws<ArgumentException>(() => CsvQueryable.Explain(Data.Orders.AsQueryable()));
		var query = (CsvQuery<Order>)Orders().Take(1);
		Assert.Equal(CsvQueryable.Explain(query).ToString(), query.Explain().ToString());
	}

	/// <summary>Replaces the CSV source of a query with another, to run a query made for one with the options of the other.</summary>
	private sealed class Rebase(CsvQuery<Order> source) : ExpressionVisitor
	{
		protected override Expression VisitConstant(ConstantExpression node) => node.Value is CsvQuery<Order> ? source.Expression : node;
	}
}
