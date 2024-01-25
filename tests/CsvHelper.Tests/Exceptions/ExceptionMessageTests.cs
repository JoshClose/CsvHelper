// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.Exceptions
{
	
	public class ExceptionMessageTests
	{
		[Fact]
		public void GetMissingFieldTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreBlankLines = false,
			};
			var parser = new ParserMock(config)
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
				throw new XUnitException();
			}
			catch (MissingFieldException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				Assert.Equal(2, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (MissingFieldException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				Assert.Equal(2, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (TypeConverterException ex)
			{
				//var expected = "Row: '2' (1 based)\r\n" +
				//        "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
				//        "Field Index: '0' (0 based)\r\n" +
				//        "Field Name: 'Id'\r\n" +
				//        "Field Value: 'a'\r\n";
				//Assert.Equal( expected, ex.Data["CsvHelper"] );

				Assert.Equal(2, ex.Context.Parser.Row);
				//Assert.Equal( typeof( Simple ), ex.Type );
				Assert.Equal(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (TypeConverterException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				Assert.Equal(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (TypeConverterException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				Assert.Equal(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (TypeConverterException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				//Assert.Equal( typeof( Simple ), ex.Type );
				Assert.Equal(0, ex.Context.Reader.CurrentIndex);
			}
		}

		[Fact]
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
				throw new XUnitException();
			}
			catch (MissingFieldException ex)
			{
				Assert.Equal(2, ex.Context.Parser.Row);
				Assert.Equal(-1, ex.Context.Reader.CurrentIndex);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
