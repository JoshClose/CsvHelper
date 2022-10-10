// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using Xunit;

namespace CsvHelper.Tests.Parsing
{
	public class MaxFieldSizeTests
	{
		[Fact]
		public void LargeRecordFieldThrowsMaxFieldSizeExceptionTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MaxFieldSize = 10
			};
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("1,2,3");
			s.AppendLine("ok,1234567890,x");
			s.AppendLine("nok,12345678901,y");
			using (var reader = new StringReader(s))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				parser.Read();
				Assert.Throws<MaxFieldSizeException>(() => parser.Read());
			}
		}

		[Fact]
		public void LargeHeaderFieldThrowsMaxFieldSizeExceptionTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MaxFieldSize = 10
			};
			var s = new TestStringBuilder(config.NewLine);
			s.AppendLine("1,very long header name");
			using (var reader = new StringReader(s))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.Throws<MaxFieldSizeException>(() => parser.Read());
			}
		}
	}
}
