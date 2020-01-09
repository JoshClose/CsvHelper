// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.Linq;
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
			csv.Configuration.ShouldSkipRecord = row => row[0].StartsWith( "skipme" ) || row.All( string.IsNullOrWhiteSpace );

			csv.Read();
			csv.Read();
			Assert.AreEqual( "1", csv.GetField( 0 ) );
			Assert.AreEqual( "2", csv.GetField( 1 ) );
		}
	}
}
