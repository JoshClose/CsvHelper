using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Async
{
	[TestClass]
	public class ReadingTests
	{
		[TestMethod]
		public async Task ReadingTest()
		{
			var parser = new ParserMock
			{
				new [] { "Id", "Name" },
				new [] { "1", "one" },
				new [] { "2", "two" },
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

				Assert.AreEqual(2, records.Count);

				var record = records[0];
				Assert.AreEqual(1, record.Id);
				Assert.AreEqual("one", record.Name);

				record = records[1];
				Assert.AreEqual(2, record.Id);
				Assert.AreEqual("two", record.Name);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}