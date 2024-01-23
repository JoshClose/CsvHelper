// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Tests.Mocks;
using Xunit;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests
{
	
	public class DisposeTests
	{
		[Fact]
		public void WriterFlushOnDisposeTest()
		{
			using (var writer = new StringWriter())
			{
				using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
				{
					csv.WriteField("A");
				}

				Assert.Equal("A", writer.ToString());
			}
		}

		[Fact]
		public void WriterFlushOnDisposeWithFlushTest()
		{
			using (var writer = new StringWriter())
			{
				using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
				{
					csv.WriteField("A");
					csv.Flush();
				}

				Assert.Equal("A", writer.ToString());
			}
		}

		[Fact]
		public void DisposeShouldBeCallableMultipleTimes()
		{
			var parserMock = new ParserMock();
			var reader = new CsvReader(parserMock);

			reader.Dispose();
			reader.Dispose();
		}
	}
}
