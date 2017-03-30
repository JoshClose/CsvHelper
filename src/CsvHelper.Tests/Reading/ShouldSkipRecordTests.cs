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
	public class ShouldSkipRecordTests
	{
		[TestMethod]
		public void SkipEmptyHeaderTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { " " } );
			rows.Enqueue( new[] { "First,Second" } );
			rows.Enqueue( new[] { "1", "2" } );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.ShouldSkipRecord = row => row.All( string.IsNullOrWhiteSpace );

			csv.Read();
			csv.ReadHeader();
			csv.Read();
			Assert.AreEqual( "1", csv.GetField( 0 ) );
			Assert.AreEqual( "2", csv.GetField( 1 ) );
		}

		[TestMethod]
		public void SkipEmptyRowTest()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "First,Second" } );
			rows.Enqueue( new[] { " " } );
			rows.Enqueue( new[] { "1", "2" } );
			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.ShouldSkipRecord = row => row.All( string.IsNullOrWhiteSpace );

			csv.Read();
			csv.ReadHeader();
			csv.Read();
			Assert.AreEqual( "1", csv.GetField( 0 ) );
			Assert.AreEqual( "2", csv.GetField( 1 ) );
		}

		[TestMethod]
		public void ShouldSkipWithEmptyRows()
		{
			var rows = new Queue<string[]>();
			rows.Enqueue( new[] { "First,Second" } );
			rows.Enqueue( new[] { "skipme," } );
			rows.Enqueue( new[] { "" } );
			rows.Enqueue( new[] { "1", "2" } );

			var parser = new ParserMock( rows );

			var csv = new CsvReader( parser );
			csv.Configuration.ShouldSkipRecord = row => row[0].StartsWith( "skipme" );
			csv.Configuration.SkipEmptyRecords = true;

			csv.Read();
			csv.Read();
			Assert.AreEqual( "1", csv.GetField( 0 ) );
			Assert.AreEqual( "2", csv.GetField( 1 ) );
		}
	}
}
