using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class BufferSplittingCrLnTests
	{
		[TestMethod]
		public void BufferSplitsCrLfTest()
		{
			var s = new StringBuilder();
			s.Append( "1,2\r\n" );
			s.Append( "3,4\r\n" );
			var config = new CsvConfiguration
			{
				BufferSize = 4
			};
			using( var reader = new StringReader( s.ToString() ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				var row = parser.Read();
				Assert.IsFalse( row[0].EndsWith( "\r" ) );
				Assert.IsFalse( parser.RawRecord.EndsWith( "\r" ) );
			}
		}

		[TestMethod]
		public void BufferSplitsCrLfWithLastFieldQuotedTest()
		{
			var s = new StringBuilder();
			s.Append( "\"1\"\r\n" );
			s.Append( "2\r\n" );
			var config = new CsvConfiguration
			{
				BufferSize = 4
			};
			using( var reader = new StringReader( s.ToString() ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				var row = parser.Read();
				Assert.IsFalse( row[0].EndsWith( "\r" ) );
				Assert.IsFalse( parser.RawRecord.EndsWith( "\r" ) );
			}
		}
	}
}