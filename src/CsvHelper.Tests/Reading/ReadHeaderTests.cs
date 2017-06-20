// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
			catch( CsvReaderException )
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
			catch( CsvReaderException )
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
	}
}
