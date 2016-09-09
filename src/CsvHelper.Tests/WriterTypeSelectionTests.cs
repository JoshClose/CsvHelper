using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
    [TestClass]
    public class WriterTypeSelectionTests
    {
	    [TestMethod]
	    public void WriteParentTest()
	    {
		    using( var stream = new MemoryStream() )
		    using( var reader = new StreamReader( stream ) )
		    using( var writer = new StreamWriter( stream ) )
		    using( var csv = new CsvWriter( writer ) )
		    {
			    csv.Configuration.RegisterClassMap<ParentMap>();
			    csv.Configuration.RegisterClassMap<ChildMap>();

			    var list = new List<Parent>
			    {
				    new Parent { Id = 1 },
				    new Parent { Id = 2 }
			    };

			    csv.WriteRecords( list );
			    writer.Flush();
			    stream.Position = 0;

			    var expected = new StringBuilder();
			    expected.AppendLine( "Id" );
				expected.AppendLine( "1" );
				expected.AppendLine( "2" );
			    var result = reader.ReadToEnd();

			    Assert.AreEqual( expected.ToString(), result );
		    }
	    }

	    [TestMethod]
        public void WriteChildTest()
        {
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<ParentMap>();
				csv.Configuration.RegisterClassMap<ChildMap>();

				var list = new List<Child>
				{
					new Child { Id = 1, Name = "one" },
					new Child { Id = 2, Name = "two" }
				};

				csv.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine( "Id,Name" );
				expected.AppendLine( "1,one" );
				expected.AppendLine( "2,two" );
				var result = reader.ReadToEnd();

				Assert.AreEqual( expected.ToString(), result );
			}
		}

		private class Parent
        {
            public int Id { get; set; }
        }

        private class Child : Parent
        {
            public string Name { get; set; }
        }

        private sealed class ParentMap : CsvClassMap<Parent>
        {
            public ParentMap()
            {
                Map( m => m.Id );
            }
        }

        private sealed class ChildMap : CsvClassMap<Child>
        {
            public ChildMap()
            {
                Map( m => m.Id );
                Map( m => m.Name );
            }
        }
    }
}
