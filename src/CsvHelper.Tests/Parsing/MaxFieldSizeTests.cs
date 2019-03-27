// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class MaxFieldSizeTests
	{
		[TestMethod]
		[ExpectedException(typeof(MaxFieldSizeException))]
		public void LargeRecordFieldThrowsMaxFieldSizeExceptionTest()
		{
			var config = new CsvHelper.Configuration.Configuration
			{
				Delimiter = ",",
				MaxFieldSize = 10
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,2,3\r\n");
				writer.Write("ok,1234567890,x\r\n");
				writer.Write("nok,12345678901,y\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				parser.Read();
				parser.Read();
			}
		}

		[TestMethod]
		[ExpectedException(typeof(MaxFieldSizeException))]
		public void LargeHeaderFieldThrowsMaxFieldSizeExceptionTest()
		{
			var config = new CsvHelper.Configuration.Configuration
			{
				Delimiter = ",",
				MaxFieldSize = 10
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.Write("1,very long header name\r\n");
				writer.Write("2,some data\r\n");
				writer.Write("3,more data\r\n");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
			}
		}
	}
}