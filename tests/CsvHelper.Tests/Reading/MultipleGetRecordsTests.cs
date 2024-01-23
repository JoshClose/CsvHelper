// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	
	public class MultipleGetRecordsTests
	{
		[Fact]
		public void GetRecordsAfterRefillingReaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("Id,Name");
				writer.WriteLine("1,one");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Test>().ToList();

				var position = stream.Position;
				writer.WriteLine("2,two");
				writer.Flush();
				stream.Position = position;

				records = csv.GetRecords<Test>().ToList();

				Assert.Single(records);
				Assert.Equal(2, records[0].Id);
				Assert.Equal("two", records[0].Name);

				position = stream.Position;
				writer.WriteLine("3,three");
				writer.Flush();
				stream.Position = position;

				records = csv.GetRecords<Test>().ToList();

				Assert.Single(records);
				Assert.Equal(3, records[0].Id);
				Assert.Equal("three", records[0].Name);
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
