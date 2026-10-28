using System.Text;
using static CsvHelper.Linq.Tests.TestUtil;

namespace CsvHelper.Linq.Tests;

/// <summary>Queries that read the CSV asynchronously return what the same queries return synchronously.</summary>
public class AsyncTests
{
	private static readonly byte[] ManyOrders = Encoding.UTF8.GetBytes(
		Data.OrdersCsv + string.Concat(Enumerable.Repeat(Data.OrdersCsv.Substring(Data.OrdersCsv.IndexOf('\n') + 1), 2000)));

	[Fact]
	public async Task EnumeratesSourcesWithAwaitForeach()
	{
		var ids = new List<int>();
		await foreach (var order in Orders())
		{
			ids.Add(order.Id);
		}

		Assert.Equal(Data.Orders.Select(o => o.Id), ids);
	}

	[Fact]
	public async Task ComposedQueriesAreAsyncEnumerable()
	{
		var query = Assert.IsAssignableFrom<IAsyncEnumerable<int>>(Orders().Where(o => o.Total > 100).Select(o => o.Id));
		var ids = new List<int>();
		await foreach (var id in query)
		{
			ids.Add(id);
		}

		Assert.Equal(Data.Orders.Where(o => o.Total > 100).Select(o => o.Id), ids);
	}

	[Fact]
	public async Task OperatorsMatchTheirSynchronousVersions()
	{
		var query = Orders().Where(o => o.Quantity > 3);
		var expected = Data.Orders.Where(o => o.Quantity > 3).ToList();
		var async = query.AsCsvAsync();
		Assert.Equal(expected.Select(o => o.Id), (await async.ToListAsync()).Select(o => o.Id));
		Assert.Equal(expected.Select(o => o.Id), (await async.ToArrayAsync()).Select(o => o.Id));
		Assert.Equal(expected.Count, await async.CountAsync());
		Assert.Equal(expected.Count(o => o.Total > 50), await async.CountAsync(o => o.Total > 50));
		Assert.Equal(expected.LongCount(), await async.LongCountAsync());
		Assert.Equal(expected.LongCount(o => o.Total > 50), await async.LongCountAsync(o => o.Total > 50));
		Assert.True(await async.AnyAsync());
		Assert.False(await async.AnyAsync(o => o.Total > 10_000));
		Assert.True(await async.AllAsync(o => o.Quantity > 3));
		Assert.False(await async.AllAsync(o => o.Total > 100));
		Assert.Equal(expected[0].Id, (await async.FirstAsync()).Id);
		Assert.Equal(expected.First(o => o.Total > 100).Id, (await async.FirstAsync(o => o.Total > 100)).Id);
		Assert.Null(await async.FirstOrDefaultAsync(o => o.Total > 10_000));
		Assert.Equal(7, (await async.SingleAsync(o => o.Id == 7)).Id);
		Assert.Null(await async.SingleOrDefaultAsync(o => o.Id == 700));
		Assert.Equal(expected[3].Id, (await async.ElementAtAsync(3)).Id);
		Assert.Equal(expected[expected.Count - 1].Id, (await async.LastAsync()).Id);
		Assert.Equal(expected.Sum(o => o.Total), await async.ExecuteAsync(q => q.Sum(o => o.Total)));
		Assert.Equal(expected.Max(o => o.Placed), await async.ExecuteAsync(q => q.Max(o => o.Placed)));
		var ids = new List<int>();
		await async.ForEachAsync(o => ids.Add(o.Id));
		Assert.Equal(expected.Select(o => o.Id), ids);
	}

	[Fact]
	public async Task OperatorsThrowWhatLinqThrows()
	{
		var async = Orders().AsCsvAsync();
		await Assert.ThrowsAsync<InvalidOperationException>(() => async.SingleAsync());
		await Assert.ThrowsAsync<InvalidOperationException>(() => Orders().Where(o => o.Id > 1000).AsCsvAsync().FirstAsync());
		await Assert.ThrowsAsync<InvalidOperationException>(() => Orders().Where(o => o.Id > 1000).AsCsvAsync().LastAsync());
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => async.ElementAtAsync(1000));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => async.ElementAtAsync(-1));
		Assert.Null(await async.ExecuteAsync(q => q.ElementAtOrDefault(1000)));
		Assert.Equal(0, await Orders().Where(o => o.Id > 1000).AsCsvAsync().ExecuteAsync(q => q.Select(o => o.Quantity).DefaultIfEmpty().Sum()));
	}

	[Fact]
	public async Task QueriesTheReaderCannotRunAreBuffered()
	{
		var query = Orders().OrderByDescending(o => o.Total).ThenBy(o => o.Id).Select(o => new { o.Id, o.Total }).Take(5);
		Assert.Equal(query.ToList(), await query.AsCsvAsync().ToListAsync());
		var grouped = Orders().GroupBy(o => o.Customer).Select(g => new { g.Key, Count = g.Count() });
		Assert.Equal(grouped.ToList(), await grouped.AsCsvAsync().ToListAsync());
		Assert.Equal(grouped.Count(), await grouped.AsCsvAsync().CountAsync());
	}

	[Fact]
	public async Task StreamsWhenTheReaderRunsTheQuery()
	{
		CountingStream? stream = null;
		var options = new CsvQueryOptions { ReaderOptions = new CsvReaderOptions { BufferSize = 4096 } };
		var query = CsvQueryable.FromStream<Order>(() => stream = new CountingStream(ManyOrders), options);
		var third = Data.Orders.Where(o => o.Quantity > 2).Select(o => o.Id).Skip(1).First();
		Assert.Equal(third, await query.Where(o => o.Quantity > 2).Select(o => o.Id).Skip(1).AsCsvAsync().FirstAsync());
		Assert.True(stream!.BytesRead < ManyOrders.Length / 10, $"{stream.BytesRead} of {ManyOrders.Length}");
		Assert.True(stream.Disposed);
		Assert.Equal([1, 2, 3], await query.Select(o => o.Id).Take(3).AsCsvAsync().ToListAsync());
		Assert.True(stream.BytesRead < ManyOrders.Length / 10);
		Assert.Equal(120_060, await query.AsCsvAsync().CountAsync());
		Assert.Equal(ManyOrders.Length, stream.BytesRead);
	}

	[Fact]
	public async Task CanBeCanceled()
	{
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		var query = CsvQueryable.FromBytes<Order>(ManyOrders);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.Select(o => o.Id).AsCsvAsync().ToListAsync(canceled.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.OrderBy(o => o.Id).AsCsvAsync().ToListAsync(canceled.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.AsCsvAsync().CountAsync(canceled.Token));
	}

	[Fact]
	public void OnlyCsvQueriesRunAsynchronously()
	{
		Assert.Throws<ArgumentException>(() => Data.Orders.AsQueryable().AsCsvAsync());
		Assert.Throws<ArgumentNullException>(() => CsvQueryable.AsCsvAsync<Order>(null!));
	}
}
