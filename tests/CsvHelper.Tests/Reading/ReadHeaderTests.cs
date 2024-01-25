// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	
	public class ReadHeaderTests
	{
		[Fact]
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

			Assert.NotNull(csv.HeaderRecord);
			Assert.Equal("Id", csv.HeaderRecord[0]);
			Assert.Equal("Name", csv.HeaderRecord[1]);
		}

		[Fact]
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

			Assert.Equal("Id", csv.Parser[0]);
			Assert.Equal("Name", csv.Parser[1]);
		}

		[Fact]
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

			Assert.Throws<ReaderException>(() => csv.ReadHeader());
		}

		[Fact]
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

			Assert.Throws<ReaderException>(() => csv.ReadHeader());
		}

		[Fact]
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

		[Fact]
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

			Assert.Equal(0, csv.GetFieldIndex("Id"));
			Assert.Equal(1, csv.GetFieldIndex("Name"));

			csv.Read();
			csv.ReadHeader();

			Assert.Equal(1, csv.GetFieldIndex("Id"));
			Assert.Equal(0, csv.GetFieldIndex("Name"));
		}

		[Fact]
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

			Assert.Equal(1, csv.GetField<int>("Id"));
			Assert.Equal("one", csv.GetField("Name"));
			Assert.Equal(2, csv.GetField<int>("Id", 1));
			Assert.Equal("two", csv.GetField("Name", 1));

			csv.Read();
			csv.ReadHeader();
			csv.Read();

			Assert.Equal(3, csv.GetField<int>("Id"));
			Assert.Equal("three", csv.GetField("Name"));
			Assert.Equal(4, csv.GetField<int>("Id", 1));
			Assert.Equal("four", csv.GetField("Name", 1));
		}
	}
}
