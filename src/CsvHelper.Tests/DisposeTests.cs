using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	[TestClass]
    public class DisposeTests
    {
		[TestMethod]
		public void WriterFlushOnDisposeTest()
		{
			using( var writer = new StringWriter() )
			{
				using( var csv = new CsvWriter( writer ) )
				{
					csv.WriteField( "A" );
				}

				Assert.AreEqual( "A", writer.ToString() );
			}
		}

		[TestMethod]
		public void WriterFlushOnDisposeWithFlushTest()
		{
			using( var writer = new StringWriter() )
			{
				using( var csv = new CsvWriter( writer ) )
				{
					csv.WriteField( "A" );
					csv.Flush();
				}

				Assert.AreEqual( "A", writer.ToString() );
			}
		}

		[TestMethod]
		public void DisposeShouldBeCallableMultipleTimes()
		{
			var parserMock = new ParserMock( new Queue<string[]>() );
			var reader = new CsvReader( parserMock );

			reader.Dispose();
			reader.Dispose();
		}
	}
}
