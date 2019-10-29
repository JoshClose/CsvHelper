// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;

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
			csv.Configuration.RegisterClassMap<TestMap>();
			var list = csv.GetRecords<Test>().ToList();
		}

		private class Test
		{
			public int IntColumn { get; set; }
		}

		private sealed class TestMap : ClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.IntColumn ).Index( 0 ).TypeConverter<Converter>();
			}
		}

		private class Converter : Int32Converter
		{
		}
		
		
		[TestMethod]
		public void CustomUriConverter()
		{
			var sw = new StringWriter();
			var entries = new[] {(new {Uri = new Uri("http://host/path")})};
			using (var cw = new CsvWriter(sw))
			{
				cw.Configuration.Delimiter = ";";
				cw.Configuration.TypeConverterCache.AddConverter<Uri>(new MyUriConverter());
				cw.WriteRecords(entries);
			}
			Assert.AreEqual("Uri\r\nhttp://host/path\r\n", sw.ToString());
		}

		private class MyUriConverter : DefaultTypeConverter
		{
			public override object ConvertFromString(string text,IReaderRow row,MemberMapData memberMapData) => 
				new Uri(text);

			// public override string ConvertToString(object value,IWriterRow row,MemberMapData memberMapData) => 
			//     value is Uri uri ? uri.ToString() : base.ConvertToString(value, row, memberMapData);
		}
	}
}
