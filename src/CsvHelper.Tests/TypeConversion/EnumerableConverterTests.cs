﻿// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.Globalization;
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
			queue.Enqueue( new[] { "Names" } );
			queue.Enqueue( new[] { "one" } );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );
			var csv = new CsvReader( parserMock );
			csv.Read();
			try
			{
				csv.GetRecord<Test>();
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}

		[TestMethod]
		public void WriteTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var test = new Test { Names = new List<int> { 1, 2 } };
				try
				{
					csv.WriteRecord( test );
					Assert.Fail();
				}
				catch( CsvTypeConverterException )
				{
				}
			}
		}

		private class Test
		{
			public List<int> Names { get; set; }
		}
	}
}
