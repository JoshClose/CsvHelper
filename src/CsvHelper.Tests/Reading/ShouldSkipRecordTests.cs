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
			Assert.AreEqual( "1", csv.GetField( 0 ) );
			Assert.AreEqual( "2", csv.GetField( 1 ) );
		}
	}
}
