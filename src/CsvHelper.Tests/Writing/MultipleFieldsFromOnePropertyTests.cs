using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class MultipleFieldsFromOnePropertyTests
    {
		[TestMethod]
		public void WriteMultipleFieldsFromSinglePropertyTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<Test>
				{
					new Test { Dob = DateTime.Parse( "9/6/2017" ) }
				};
				csv.Configuration.RegisterClassMap<TestMap>();
				csv.WriteRecords( records );
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine( "A,B,C" );
				expected.AppendLine( "9/6/2017 12:00:00 AM,9/6/2017 12:00:00 AM,9/6/2017 12:00:00 AM" );

				Assert.AreEqual( expected.ToString(), reader.ReadToEnd() );
			}
		}

		[TestMethod]
		public void ReadingWhenMultipleMapsForAPropertyAreSpecifiedUsesTheLastMapTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "A,B,C" );
				writer.WriteLine( "9/6/2017 12:00:00 AM,9/7/2017 12:00:00 AM,9/8/2017 12:00:00 AM" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.AreEqual( DateTime.Parse( "9/8/2017" ), records[0].Dob );
			}
		}

		private class Test
		{
			public DateTime Dob { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Dob, false ).Name( "A" );
				Map( m => m.Dob, false ).Name( "B" );
				Map( m => m.Dob, false ).Name( "C" );
			}
		}
	}
}
