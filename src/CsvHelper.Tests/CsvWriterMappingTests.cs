using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvWriterMappingTests
	{
		[Fact]
		public void WriteMultipleNamesTest()
		{
			var records = new List<MultipleNamesAttributeClass>
			{
				new MultipleNamesAttributeClass { IntColumn = 1, StringColumn = "one" },
				new MultipleNamesAttributeClass { IntColumn = 2, StringColumn = "two" }
			};

			string csv;
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				csvWriter.Configuration.AttributeMapping<MultipleNamesAttributeClass>();
				csvWriter.WriteRecords( records );

				writer.Flush();
				stream.Position = 0;

				csv = reader.ReadToEnd();
			}

			var expected = string.Empty;
			expected += "int1,string1\r\n";
			expected += "1,one\r\n";
			expected += "2,two\r\n";

			Assert.NotNull( csv );
			Assert.Equal( expected, csv );
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
