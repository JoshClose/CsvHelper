// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests.Async;


public class ReadingTests
{
	[Fact]
	public async Task ReadingTest()
	{
		var parser = new ParserMock
		{
			{ "Id", "Name" },
			{ "1", "one" },
			{ "2", "two" },
			null
		};
		using (var csv = new CsvReader(parser))
		{
			var records = new List<Simple>();
			await csv.ReadAsync();
			csv.ReadHeader();
			while (await csv.ReadAsync())
			{
				records.Add(csv.GetRecord<Simple>());
			}

			Assert.Equal(2, records.Count);

			var record = records[0];
			Assert.Equal(1, record.Id);
			Assert.Equal("one", record.Name);

			record = records[1];
			Assert.Equal(2, record.Id);
			Assert.Equal("two", record.Name);
		}
	}

#if NETCOREAPP
	[Fact]
	public async Task GetRecordsTest()
	{
		var parser = new ParserMock
		{
			{ "Id", "Name" },
			{ "1", "one" },
			{ "2", "two" },
			null
		};
		using (var csv = new CsvReader(parser))
		{
			var records = csv.GetRecordsAsync<Simple>().GetAsyncEnumerator();
			await records.MoveNextAsync();

			Assert.Equal(1, records.Current.Id);
			Assert.Equal("one", records.Current.Name);

			await records.MoveNextAsync();

			Assert.Equal(2, records.Current.Id);
			Assert.Equal("two", records.Current.Name);
		}
	}

	[Fact]
	public async Task GetRecordsTestCanceled()
	{
		var parser = new ParserMock
		{
			{ "Id", "Name" },
			{ "1", "one" },
			{ "2", "two" },
			null
		};
		using (var source = new CancellationTokenSource())
		using (var csv = new CsvReader(parser))
		{
			source.Cancel();
			var records = csv.GetRecordsAsync<Simple>(source.Token).GetAsyncEnumerator();
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await records.MoveNextAsync());
		}
	}
#endif

	private class Simple
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;
	}
}
