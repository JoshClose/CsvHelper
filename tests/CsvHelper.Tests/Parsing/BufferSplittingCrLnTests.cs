// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Text;
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
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 4
			};
			using( var reader = new StringReader( s.ToString() ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				var row = parser.Read();
				Assert.IsFalse( row[0].EndsWith( "\r" ) );
				Assert.IsFalse( parser.FieldReader.Context.RawRecord.ToString().EndsWith( "\r" ) );
			}
		}

		[TestMethod]
		public void BufferSplitsCrLfWithLastFieldQuotedTest()
		{
			var s = new StringBuilder();
			s.Append( "\"1\"\r\n" );
			s.Append( "2\r\n" );
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 4
			};
			using( var reader = new StringReader( s.ToString() ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				var row = parser.Read();
				Assert.IsFalse( row[0].EndsWith( "\r" ) );
				Assert.IsFalse( parser.FieldReader.Context.RawRecord.ToString().EndsWith( "\r" ) );
			}
		}
	}
}
