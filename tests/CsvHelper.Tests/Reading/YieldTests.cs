using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
    public class YieldTests
    {
		[TestMethod]
        public void GetRecordsGeneric_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			IEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecords<Foo>();
			}
			Assert.ThrowsException<ObjectDisposedException>(() => records.ToList());
		}

		[TestMethod]
		public void GetRecords_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			IEnumerable<object> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecords(typeof(Foo));
			}

			Assert.ThrowsException<ObjectDisposedException>(() => records.ToList());
		}

		[TestMethod]
		public void EnumerateRecords_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			Foo record = null;
			IEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.EnumerateRecords(record);
			}

			Assert.ThrowsException<ObjectDisposedException>(() => records.ToList());
		}

#if NETCOREAPP
		[TestMethod]
		public async Task GetRecordsAsyncGeneric_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			IAsyncEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecordsAsync<Foo>();
			}

			await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}

		[TestMethod]
		public async Task GetRecordsAsync_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			IAsyncEnumerable<object> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecordsAsync(typeof(Foo));
			}

			await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}

		[TestMethod]
		public async Task EnumerateRecordsAsync_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue(new[] { "Id", "Name" });
			queue.Enqueue(new[] { "1", "one" });
			queue.Enqueue(null);
			var parserMock = new ParserMock(queue);

			Foo record = null;
			IAsyncEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.EnumerateRecordsAsync(record);
			}

			await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}
#endif

		private class Foo
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
    }
}
