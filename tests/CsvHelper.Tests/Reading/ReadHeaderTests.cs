// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
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
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Read();
			csv.ReadHeader();

			Assert.IsNotNull( csv.Context.HeaderRecord );
			Assert.AreEqual( "Id", csv.Context.HeaderRecord[0] );
			Assert.AreEqual( "Name", csv.Context.HeaderRecord[1] );
		}

		[TestMethod]
		public void ReadHeaderDoesNotAffectCurrentRecordTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual( "Id", csv.Context.Record[0] );
			Assert.AreEqual( "Name", csv.Context.Record[1] );
		}

		[TestMethod]
		public void ReadingHeaderFailsWhenReaderIsDoneTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.HasHeaderRecord = false;
			while( csv.Read() ) { }

			try
			{
				csv.ReadHeader();
				Assert.Fail();
			}
			catch( ReaderException )
			{
			}
		}

		[TestMethod]
		public void ReadingHeaderFailsWhenNoHeaderRecordTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.HasHeaderRecord = false;

			try
			{
				csv.ReadHeader();
				Assert.Fail();
			}
			catch( ReaderException )
			{
			}
		}

		[TestMethod]
		public void ReadingHeaderDoesNotFailWhenHeaderAlreadyReadTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Read();
			csv.ReadHeader();
			csv.ReadHeader();
		}

		[TestMethod]
		public void ReadHeaderResetsNamedIndexesTest()
		{
			var parser = new ParserMock
			{
				new [] { "Id", "Name" },
				new [] { "Name", "Id" },
			};
			var csv = new CsvReader(parser);
			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual(0, csv.Context.NamedIndexes["Id"][0]);
			Assert.AreEqual(1, csv.Context.NamedIndexes["Name"][0]);

			csv.GetField("Id");
			csv.GetField("Name");

			csv.Read();
			csv.ReadHeader();

			Assert.AreEqual(1, csv.Context.NamedIndexes["Id"][0]);
			Assert.AreEqual(0, csv.Context.NamedIndexes["Name"][0]);
		}

		[TestMethod]
		public void MultipleReadHeaderCallsWorksWithNamedIndexCacheTest()
		{
			var parser = new ParserMock
			{
				new [] { "Id", "Name", "Id", "Name" },
				new [] { "1", "one", "2", "two" },
				new [] { "Name", "Id", "Name", "Id" },
				new [] { "three", "3", "four", "4" },
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
