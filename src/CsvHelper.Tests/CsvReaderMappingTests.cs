using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderMappingTests
	{
		[Fact]
		public void ReadMultipleNamesTest()
		{
			var data = new List<string[]>
			{
				new[] { "int2", "string3" },
				new[] { "1", "one" },
				new[] { "2", "two" },
				null
			};

			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			var index = -1;
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				index++;
				return data[index];
			} );

			var csvReader = new CsvReader( parserMock.Object );
			csvReader.Configuration.AttributeMapping<MultipleNamesAttributeClass>();

			var records = csvReader.GetRecords<MultipleNamesAttributeClass>().ToList();

			Assert.NotNull( records );
			Assert.Equal( 2, records.Count );
			Assert.Equal( 1, records[0].IntColumn );
			Assert.Equal( "one", records[0].StringColumn );
			Assert.Equal( 2, records[1].IntColumn );
			Assert.Equal( "two", records[1].StringColumn );
		}

		private class MultipleNamesAttributeClass
		{
			[CsvField( Names = new[] { "int1", "int2", "int3" } )]
			public int IntColumn { get; set; }

			[CsvField( Names = new[] { "string1", "string2", "string3" } )]
			public string StringColumn { get; set; }
		}
	}
}
