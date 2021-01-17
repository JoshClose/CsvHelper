// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Exceptions
{
	[TestClass]
	public class ExceptionMessageTests
	{
		[TestMethod]
		public void GetMissingFieldTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			reader.Read();
			reader.Read();
			try
			{
				reader.GetField(2);
				Assert.Fail();
			}
			catch (MissingFieldException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				Assert.AreEqual(2, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetGenericMissingFieldWithTypeTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			reader.Read();
			reader.Read();
			try
			{
				reader.GetField<int>(2);
				Assert.Fail();
			}
			catch (MissingFieldException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				Assert.AreEqual(2, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetRecordGenericTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			reader.Read();
			try
			{
				reader.GetRecord<Simple>();
				Assert.Fail();
			}
			catch (TypeConverterException ex)
			{
				//var expected = "Row: '2' (1 based)\r\n" +
				//        "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
				//        "Field Index: '0' (0 based)\r\n" +
				//        "Field Name: 'Id'\r\n" +
				//        "Field Value: 'a'\r\n";
				//Assert.AreEqual( expected, ex.Data["CsvHelper"] );

				Assert.AreEqual(2, ex.Context.Parser.Row);
				//Assert.AreEqual( typeof( Simple ), ex.Type );
				Assert.AreEqual(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetRecordTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			reader.Read();
			try
			{
				reader.GetRecord(typeof(Simple));
				Assert.Fail();
			}
			catch (TypeConverterException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				Assert.AreEqual(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetRecordsGenericTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			try
			{
				reader.GetRecords<Simple>().ToList();
				Assert.Fail();
			}
			catch (TypeConverterException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				Assert.AreEqual(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			try
			{
				reader.GetRecords(typeof(Simple)).ToList();
				Assert.Fail();
			}
			catch (TypeConverterException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				//Assert.AreEqual( typeof( Simple ), ex.Type );
				Assert.AreEqual(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[TestMethod]
		public void GetFieldIndexTest()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "a", "b" },
				null
			};

			var reader = new CsvReader(parser);
			reader.Read();
			reader.ReadHeader();
			reader.Read();

			try
			{
				reader.GetField("c");
				Assert.Fail();
			}
			catch (MissingFieldException ex)
			{
				Assert.AreEqual(2, ex.Context.Parser.Row);
				Assert.AreEqual(-1, ex.Context.Reader.CurrentIndex);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
