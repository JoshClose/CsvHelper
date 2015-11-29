using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class CommentTests
	{
		[TestMethod]
		public void CommentThatCrossesBuffersShouldNotAddToFieldTest()
		{
			var config = new CsvConfiguration
			{
				AllowComments = true,
				BufferSize = 10
			};
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "#commented line\r\n" );
				writer.Write( "3,4" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				var line = parser.Read();
				Assert.AreEqual( "3", line[0] );
				Assert.AreEqual( "4", line[1] );
			}
		}
	}
}
