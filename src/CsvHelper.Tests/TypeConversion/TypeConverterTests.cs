// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterTests
	{
		[TestMethod]
		public void ReaderInheritedConverter()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "1" } );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );
			var csv = new CsvReader( parserMock );
			csv.Configuration.HasHeaderRecord = false;
			csv.Configuration.ClassMapping<TestMap>();
			var list = csv.GetRecords<Test>().ToList();
		}

		private class Test
		{
			public int IntColumn { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.IntColumn ).Index( 0 ).TypeConverter<Converter>();
			}
		}

		private class Converter : Int32Converter
		{
		}
	}
}
