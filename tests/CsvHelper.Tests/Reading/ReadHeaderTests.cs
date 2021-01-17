// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class ReadHeaderTests
	{
		[TestMethod]
		public void ReadHeaderReadsHeaderTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "One" },
				{ "2", "two" },
				null
			};

			var csv = new CsvReader(parser);
			csv.Read();
			csv.ReadHeader();

			Assert.IsNotNull(csv.HeaderRecord);
			Assert.AreEqual("Id", csv.HeaderRecord[0]);
			Assert.AreEqual("Name", csv.HeaderRecord[1]);
		}

		[TestMethod]
		public void ReadHeaderDoesNotAffectCurrentRecordTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "One" },
				{ "2", "two" },
				null,
			};

			var csv = new CsvReader(parser);
			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual("Id", csv.Parser[0]);
			Assert.AreEqual("Name", csv.Parser[1]);
		}

		[TestMethod]
		public void ReadingHeaderFailsWhenReaderIsDoneTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "Id", "Name" },
				{ "1", "One" },
				{ "2", "two" },
				null,
			};

			var csv = new CsvReader(parser);
			while (csv.Read()) { }

			Assert.ThrowsException<ReaderException>(() => csv.ReadHeader());
		}

		[TestMethod]
		public void ReadingHeaderFailsWhenNoHeaderRecordTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "Id", "Name" },
				{ "1", "One" },
				{ "2", "two" },
				null
			};

			var csv = new CsvReader(parser);

			Assert.ThrowsException<ReaderException>(() => csv.ReadHeader());
		}

		[TestMethod]
		public void ReadingHeaderDoesNotFailWhenHeaderAlreadyReadTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "One" },
				{ "2", "two" },
				null
			};

			var csv = new CsvReader(parser);
			csv.Read();
			csv.ReadHeader();
			csv.ReadHeader();
		}

		[TestMethod]
		public void ReadHeaderResetsNamedIndexesTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "Name", "Id" },
			};
			var csv = new CsvReader(parser);
			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual(0, csv.GetFieldIndex("Id"));
			Assert.AreEqual(1, csv.GetFieldIndex("Name"));

			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual(1, csv.GetFieldIndex("Id"));
			Assert.AreEqual(0, csv.GetFieldIndex("Name"));
		}

		[TestMethod]
		public void MultipleReadHeaderCallsWorksWithNamedIndexCacheTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name", "Id", "Name" },
				{ "1", "one", "2", "two" },
				{ "Name", "Id", "Name", "Id" },
				{ "three", "3", "four", "4" },
			};
			var csv = new CsvReader(parser);

			csv.Read();
			csv.ReadHeader();
			csv.Read();

			Assert.AreEqual(1, csv.GetField<int>("Id"));
			Assert.AreEqual("one", csv.GetField("Name"));
			Assert.AreEqual(2, csv.GetField<int>("Id", 1));
			Assert.AreEqual("two", csv.GetField("Name", 1));

			csv.Read();
			csv.ReadHeader();
			csv.Read();

			Assert.AreEqual(3, csv.GetField<int>("Id"));
			Assert.AreEqual("three", csv.GetField("Name"));
			Assert.AreEqual(4, csv.GetField<int>("Id", 1));
			Assert.AreEqual("four", csv.GetField("Name", 1));
		}
	}
}
