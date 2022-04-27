// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	
    public class RefillingTextReaderTests
    {
		[Fact]
        public void RefillTextReaderMultipleTimesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Flush();
				stream.Position = 0;

				Assert.True(parser.Read());
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
				Assert.False(parser.Read());

				var position = stream.Position;
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.True(parser.Read());
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
				Assert.False(parser.Read());

				position = stream.Position;
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.True(parser.Read());
				Assert.Equal("5", parser[0]);
				Assert.Equal("6", parser[1]);
				Assert.False(parser.Read());
			}
		}
    }
}
