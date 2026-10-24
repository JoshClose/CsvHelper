using System.Text;

namespace CsvHelper.Linq.Tests;

/// <summary>Queries whose reader runs on several threads return what they return on one, in the same order.</summary>
public class ParallelTests
{
	private static readonly string Many =
		Data.OrdersCsv + string.Concat(Enumerable.Repeat(Data.OrdersCsv.Substring(Data.OrdersCsv.IndexOf('\n') + 1), 2000));

	private static readonly CsvQueryOptions Parallel = new() { MaxDegreeOfParallelism = 4 };

	private static void Same<TResult>(Func<IQueryable<Order>, TResult> query) =>
		Assert.Equal(
			TestUtil.Run(() => query(CsvQueryable.FromText<Order>(Many))),
			TestUtil.Run(() => query(CsvQueryable.FromText<Order>(Many, Parallel))));

	[Fact]
	public void ReturnsWhatOneThreadReturnsInOrder()
	{
		Same(q => q.Where(o => o.Total > 100).Select(o => new { o.Id, o.Customer }).ToList());
		Same(q => q.Skip(1234).Where(o => o.Quantity > 5).Select(o => o.Total).Take(50).ToList());
		Same(q => q.Count(o => o.Status == Status.Cancelled));
		Same(q => q.Count());
		Same(q => q.GroupBy(o => o.Customer).Select(g => new { g.Key, Sum = g.Sum(o => o.Total) }).ToList());
		Same(q => q.Select(o => o).Skip(100_000).Take(3).ToList());
		Same(q => q.First(o => o.Total > 240));
	}

	[Fact]
	public void DynamicRows()
	{
		var sequential = CsvQueryable.FromText(Many).Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"]).ToList();
		var parallel = CsvQueryable.FromText(Many, Parallel).Where(r => r.Parse<decimal>("Total") > 100).Select(r => r["Customer"]).ToList();
		Assert.Equal(sequential, parallel);
		Assert.Equal(
			CsvQueryable.FromText(Many).Skip(7).Select(r => r["Id"]).ToList(),
			CsvQueryable.FromText(Many, Parallel).Skip(7).Where(r => true).Select(r => r).Select(r => r["Id"]).ToList());
	}

	[Fact]
	public void StopsEarly()
	{
		var query = CsvQueryable.FromText<Order>(Many, Parallel).Select(o => o.Id);
		Assert.Equal([1, 2, 3], query.Take(3).ToList());
		Assert.Equal(1, query.First());
	}

	[Fact]
	public void SkipsFromWhereACallersReaderIs()
	{
		using var reader = CsvReader.FromText(Many);
		for (var i = 0; i < 10; i++)
		{
			Assert.True(reader.Read());
		}

		var ids = reader.Query<Order>(Parallel).Skip(5).Select(o => o.Id).Take(3).ToList();
		Assert.Equal([16, 17, 18], ids);
	}

	[Fact]
	public void ProjectionsFallBackOnEveryThread()
	{
		var csv = new StringBuilder("Code,Value\n");
		for (var i = 0; i < 20_000; i++)
		{
			csv.Append("C").Append(i).Append(',').Append(i).Append('\n');
		}

		var values = CsvQueryable.FromText<Checked>(csv.ToString(), Parallel).Select(c => c.Value).ToList();
		Assert.Equal(Enumerable.Range(0, 20_000), values);
	}

	[Fact]
	public void RecordErrorsAreSkippedOnEveryThread()
	{
		var csv = new StringBuilder("Id,Customer,Status,Total,Quantity,Placed\n");
		for (var i = 1; i <= 20_000; i++)
		{
			csv.Append(i).Append(",A,Open,").Append(i % 1000 == 0 ? "x" : "1").Append(",1,2026-01-01\n");
		}

		var errors = 0;
		var options = Parallel with { ReaderOptions = new CsvReaderOptions { OnRecordError = _ => Interlocked.Increment(ref errors) > 0 } };
		var count = CsvQueryable.FromText<Order>(csv.ToString(), options).Where(o => o.Total > 0).Count();
		Assert.Equal(19_980, count);
		Assert.Equal(20, errors);
	}
}
