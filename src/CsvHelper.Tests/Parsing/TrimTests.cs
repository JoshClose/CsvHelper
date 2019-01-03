// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class TrimTests
	{
		[TestMethod]
		public void OutsideStartTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  a,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideStartNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  a,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideStartSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  a b c,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideStartSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  a b c,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideEndTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("a  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideEndNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("a  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideEndSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("a b c  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideEndSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("a b c  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideBothTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  a  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideBothNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  a  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideBothSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  a b c  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideBothSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  a b c  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesStartTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  \"a\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesStartNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  \"a\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesStartSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  \"a b c\",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesStartSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  \"a b c\",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesEndTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"a\"  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesEndNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"a\"  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesEndSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"a b c\"  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesEndSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"a b c\"  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  \"a\"  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  \"a\"  ,b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  \"a b c\"  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  \"a b c\"  ,d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  a b c  ,  d e f  ");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void OutsideQuotesBothSpacesInFieldMultipleRecordsNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  a b c  ,  d e f  ");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"  a\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"  a\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"  a b c\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"  a b c\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldDelimiterInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\" a ,b c\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a ,b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesStartSpacesInFieldDelimiterInFieldSmallBufferNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\" a ,b c\",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				parser.Configuration.BufferSize = 1;
				var record = parser.Read();

				Assert.AreEqual("a ,b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesEndTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"a  \",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesEndNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"a  \",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesEndSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"a b c  \",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesEndSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"a b c  \",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"  a  \",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"  a  \",b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"  a b c  \",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"  a b c  \",d");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldMultipleRecordsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("\"  a b c  \",\"  d e f  \"");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesInFieldMultipleRecordsNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("\"  a b c  \",\"  d e f  \"");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void OutsideAndInsideQuotesTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.WriteLine("  \"  a b c  \"  ,  \"  d e f  \"  ");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void OutsideAndInsideQuotesNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Delimiter = ",";
				writer.Write("  \"  a b c  \"  ,  \"  d e f  \"  ");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a b c", record[0]);
				Assert.AreEqual("d e f", record[1]);
			}
		}

		[TestMethod]
		public void OutsideQuotesNoSpacesNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				writer.Write("abc");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("abc", record[0]);
			}
		}

		[TestMethod]
		public void OutsideQuotesNoSpacesHasSpaceInFieldNoNewlineTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader))
			{
				writer.Write("a b");
				writer.Flush();
				stream.Position = 0;

				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();

				Assert.AreEqual("a b", record[0]);
			}
		}

		[TestMethod]
		public void InsideNoSpacesQuotesFieldHasEscapedQuotesTest()
		{
			using (var reader = new StringReader("\"a \"\"b\"\" c\""))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a \"b\" c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesFieldHasEscapedQuotesTest()
		{
			using (var reader = new StringReader("\" a \"\"b\"\" c \"\r\n"))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a \"b\" c", record[0]);
			}
		}

		[TestMethod]
		public void InsideQuotesBothSpacesFieldHasEscapedQuotesNoNewLineTest()
		{
			using (var reader = new StringReader("\" a \"\"b\"\" c \""))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();

				Assert.AreEqual("a \"b\" c", record[0]);
			}
		}

		[TestMethod]
		public void ReadingTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("A,B");
				writer.WriteLine("  \"  a b c  \"  ,  \"  d e f  \"  ");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var records = csv.GetRecords<dynamic>().ToList();

				var record = records[0];
				Assert.AreEqual("a b c", record.A);
				Assert.AreEqual("d e f", record.B);
			}
		}
	}
}