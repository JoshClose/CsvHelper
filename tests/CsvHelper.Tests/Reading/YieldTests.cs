// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Reading
{
	
	public class YieldTests
	{
		[Fact]
		public void GetRecordsGeneric_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			IEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecords<Foo>();
			}
			Assert.Throws<ObjectDisposedException>(() => records.ToList());
		}

		[Fact]
		public void GetRecords_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			IEnumerable<object> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecords(typeof(Foo));
			}

			Assert.Throws<ObjectDisposedException>(() => records.ToList());
		}

		[Fact]
		public void EnumerateRecords_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			Foo record = null;
			IEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.EnumerateRecords(record);
			}

			Assert.Throws<ObjectDisposedException>(() => records.ToList());
		}

#if !NET462
		[Fact]
		public async Task GetRecordsAsyncGeneric_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			IAsyncEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecordsAsync<Foo>();
			}

			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}

		[Fact]
		public async Task GetRecordsAsync_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			IAsyncEnumerable<object> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.GetRecordsAsync(typeof(Foo));
			}

			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}

		[Fact]
		public async Task EnumerateRecordsAsync_Disposed_ThrowsObjectDisposedExceptionTest()
		{
			var parserMock = new ParserMock
			{
				new[] { "Id", "Name" },
				new[] { "1", "one" },
				null
			};

			Foo record = null;
			IAsyncEnumerable<Foo> records;
			using (var csv = new CsvReader(parserMock))
			{
				records = csv.EnumerateRecordsAsync(record);
			}

			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await records.GetAsyncEnumerator().MoveNextAsync());
		}
#endif

		private class Foo
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
    }
}
