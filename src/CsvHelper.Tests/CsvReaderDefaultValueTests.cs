﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderDefaultValueTests
	{
		[TestMethod]
		public void DefaultValueTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "Id,Name,Order" );
				writer.WriteLine( ",," );
				writer.WriteLine( "2,two,2" );
				writer.WriteLine( ",three," );
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.RegisterClassMap<TestMap>();

				var records = csvReader.GetRecords<Test>().ToList();

				var record = records[0];
				Assert.AreEqual( -1, record.Id );
				Assert.AreEqual( null, record.Name );
				Assert.AreEqual( -2, record.Order );

				record = records[1];
				Assert.AreEqual( 2, record.Id );
				Assert.AreEqual( "two", record.Name );
			}
		}

		private class Test
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public int Order { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Id ).Default( -1 );
				Map( m => m.Name ).Default( (string)null );
				Map( m => m.Order ).Default( -2 );
			}
		}
	}
}
