using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class MultipleGetRecordsTests
	{
		[TestMethod]
		public void Blah()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name" );
				writer.WriteLine( "1,one" );
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<Test>().ToList();

				var position = stream.Position;
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = position;

				records = csv.GetRecords<Test>().ToList();

				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = position;

				Assert.AreEqual( 1, records.Count );
				Assert.AreEqual( 2, records[0].Id );
				Assert.AreEqual( "two", records[0].Name );
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
