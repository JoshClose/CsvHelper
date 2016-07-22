using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Tests.Mocks;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

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
			csv.ReadHeader();

			Assert.IsNotNull( csv.FieldHeaders );
			Assert.AreEqual( "Id", csv.FieldHeaders[0] );
			Assert.AreEqual( "Name", csv.FieldHeaders[1] );
		}

		[TestMethod]
		public void ReadHeaderDoesNotReadRowTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.ReadHeader();

			try
			{
				var x = csv.CurrentRecord;
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
		}

		[TestMethod]
		public void GettingFieldHeadersFailsWhenHeaderNotReadTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );

			try
			{
				var x = csv.FieldHeaders;
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
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
		public void ReadingHeaderFailsWhenHeaderAlreadyReadTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "Id", "Name" } );
			rows.Enqueue( new[] { "1", "One" } );
			rows.Enqueue( new[] { "2", "two" } );
			rows.Enqueue( null );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.ReadHeader();

			try
			{
				csv.ReadHeader();
				Assert.Fail();
			}
			catch( CsvReaderException )
			{
			}
		}
	}
}
