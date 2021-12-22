using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;

using Xunit;
using Xunit.Sdk;

namespace CsvHelper.Tests.Mappings.ConstructorParameter;

public class NameAndIndexMapTests
{
	[Fact]
	public void MapNameAndIndexNoHeaderTest()
	{
		// Arrange
		const string oneString = "one";
		const string twoString = "two";
		const int oneDigit = 1;
		const int twoDigit = 2;
		const int expectedRecords = 2;
		StringBuilder sb = new StringBuilder();
		sb.AppendLine($"{oneDigit},{oneString}");
		sb.AppendLine($"{twoDigit},{twoString}");

		CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
		List<DataRecord> records;

		using (var csvReader = new CsvReader(new StringReader(sb.ToString()), config))
		{
			csvReader.Context.RegisterClassMap<NameAndIndexMap>();

			// Act
			records = csvReader.GetRecords<DataRecord>().ToList();
		}

		// Assert
		Assert.Equal(expectedRecords, records.Count);
		Assert.Equal(oneDigit, records[0].Id);
		Assert.Equal(oneString, records[0].Name);
		Assert.Equal(twoDigit, records[1].Id);
		Assert.Equal(twoString, records[1].Name);
	}

	[Fact]
	public void MapNameAndIndexWithHeaderTest()
	{
		var parser = new ParserMock()
		{
			{ "Id", "Name" },
			{ "1", "one" },
		};
		using (var csv = new CsvReader(parser))
		{
			var map = csv.Context.RegisterClassMap<NameAndIndexMap>();
			var records = csv.GetRecords<DataRecord>().ToList();

			Assert.Single(records);
			Assert.Equal(1, records[0].Id);
			Assert.Equal("one", records[0].Name);
		}
	}

	[Fact]
	public void MapNameAndIndexWriteTest()
	{
		var records = new List<DataRecord>
		{
			new DataRecord(1, "one"),
		};

		using (var writer = new StringWriter())
		using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
		{
			csv.Context.RegisterClassMap<NameAndIndexMap>();

			csv.WriteRecords(records);

			var expected = new StringBuilder();
			expected.Append("Id,Name\r\n");
			expected.Append("1,one\r\n");

			Assert.Equal(expected.ToString(), writer.ToString());
		}
	}

	private class NameAndIndexMap : ClassMap<DataRecord>
	{
		public NameAndIndexMap()
		{
			Parameter("id").Name("Id").Index(0);
			Parameter("name").Name("Name").Index(1);

			Map(mapped => mapped.Id).Name("Id").Index(0);
			Map(mapped => mapped.Name).Name("Name").Index(1);
		}
	}

	private class DataRecord
	{
		public DataRecord(int id, string name)
		{
			Id = id;
			Name = name;
		}

		public int Id { get; private set; }

		public string Name { get; private set; }
	}
}
