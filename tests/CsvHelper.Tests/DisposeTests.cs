// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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
				using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
				using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
