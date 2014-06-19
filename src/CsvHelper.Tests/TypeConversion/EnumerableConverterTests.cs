// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System.IO;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumerableConverterTests
	{
		[TestMethod]
		public void ConvertTest()
		{
			var converter = new EnumerableConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.IsTrue( converter.CanConvertFrom( typeof( string ) ) );
			Assert.IsTrue( converter.CanConvertTo( typeof( string ) ) );
			try
			{
				converter.ConvertFromString( typeConverterOptions, "" );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
			try
			{
				converter.ConvertToString( typeConverterOptions, 5 );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}

		[TestMethod]
		public void ReadTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "Id", "Names" } );
			queue.Enqueue( new[] { "1", "one" } );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );
			var csv = new CsvReader( parserMock );
			csv.Read();
			var record = csv.GetRecord<Test>();
			Assert.IsNull( record.Names );
		}

		[TestMethod]
		public void WriteTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var test = new Test { Id = 1, Names = new List<int> { 1, 2 } };
				csv.WriteRecord( test );
				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				Assert.AreEqual( "1\r\n", data );
			}
		}

		private class Test
		{
			public int Id { get; set; }
			public List<int> Names { get; set; }
		}
	}
}
