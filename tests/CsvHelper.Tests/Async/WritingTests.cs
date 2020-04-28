// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Async
{
	[TestClass]
	public class WritingTests
	{
		[TestMethod]
		public async Task WritingTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" },
					new Simple { Id = 2, Name = "two" },
				};
				csv.WriteHeader<Simple>();
				await csv.NextRecordAsync();
				foreach (var record in records)
				{
					csv.WriteRecord(record);
					await csv.NextRecordAsync();
				}

				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");
				expected.AppendLine("2,two");

				Assert.AreEqual(expected.ToString(), reader.ReadToEnd());
			}
		}

		[TestMethod]
		public async Task WriteRecordsTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				var records = new List<Simple>
				{
					new Simple { Id = 1, Name = "one" },
					new Simple { Id = 2, Name = "two" },
				};
				await csv.WriteRecordsAsync(records);

				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");
				expected.AppendLine("2,two");

				Assert.AreEqual(expected.ToString(), reader.ReadToEnd());
			}
		}

#if NET472 || NETCOREAPP2_1 || NETCOREAPP3_1
		[TestMethod]
		public async Task WriteRecordsAsyncEnumerableTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
                     
				await csv.WriteRecordsAsync(GetRecords());

				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine("Id,Name");
				expected.AppendLine("1,one");
				expected.AppendLine("2,two");

				Assert.AreEqual(expected.ToString(), reader.ReadToEnd());
			}
		}

		[TestMethod]
		public async Task WriteRecordsAsyncEnumerableCancelledTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";

				var cts = new CancellationTokenSource();
				cts.Cancel();

				var exception = await Assert.ThrowsExceptionAsync<WriterException>(() => csv.WriteRecordsAsync(GetRecords(), cts.Token));

				Assert.IsTrue(exception.InnerException is OperationCanceledException);
			}
		}
#endif  // NET472 || NETCOREAPP2_1 || NETCOREAPP3_1

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

#if NET472 || NETCOREAPP2_1 || NETCOREAPP3_1
		private async IAsyncEnumerable<Simple> GetRecords([EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			yield return new Simple { Id = 1, Name = "one" };

			await Task.CompletedTask;

			yield return new Simple { Id = 2, Name = "two" };
		}
#endif
	}
}
