using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
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

			var reader = new CsvReader( parser );
			reader.Read();
			try
			{
				reader.GetField( 2 );
				Assert.Fail();
			}
			catch( CsvMissingFieldException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
				               "Type: 'System.String'\r\n" +
				               "Field Index: '2' (0 based)\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );
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

			var reader = new CsvReader( parser );
			reader.Read();
			try
			{
				reader.GetField<int>( 2 );
				Assert.Fail();
			}
			catch( CsvMissingFieldException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
				               "Type: 'System.Int32'\r\n" +
				               "Field Index: '2' (0 based)\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );
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

			var reader = new CsvReader( parser );
			reader.Read();
			try
			{
				reader.GetRecord<Simple>();
				Assert.Fail();
			}
			catch( CsvTypeConverterException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
				        "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
				        "Field Index: '0' (0 based)\r\n" +
				        "Field Name: 'Id'\r\n" +
				        "Field Value: 'a'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );

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

			var reader = new CsvReader( parser );
			reader.Read();
			try
			{
				reader.GetRecord( typeof( Simple ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
						"Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
						"Field Index: '0' (0 based)\r\n" +
						"Field Name: 'Id'\r\n" +
						"Field Value: 'a'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );

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

			var reader = new CsvReader( parser );
			try
			{
				reader.GetRecords<Simple>().ToList();
				Assert.Fail();
			}
			catch( CsvTypeConverterException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
							   "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
							   "Field Index: '0' (0 based)\r\n" +
							   "Field Name: 'Id'\r\n" +
							   "Field Value: 'a'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );

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

			var reader = new CsvReader( parser );
			try
			{
				reader.GetRecords( typeof( Simple ) ).ToList();
				Assert.Fail();
			}
			catch( CsvTypeConverterException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
				               "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n" +
				               "Field Index: '0' (0 based)\r\n" +
				               "Field Name: 'Id'\r\n" +
				               "Field Value: 'a'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );

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

			var reader = new CsvReader( parser );
			reader.Read();
			try
			{
				reader.GetField( "c" );
				Assert.Fail();
			}
			catch( CsvMissingFieldException ex )
			{
				var expected = "Row: '2' (1 based)\r\n" +
				               "Field Index: '-1' (0 based)\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );

			}
		}

		[TestMethod]
		public void WriteRecordGenericTest()
		{
			var serializer = new SerializerMock( true );
			var writer = new CsvWriter( serializer );
			try
			{
				writer.WriteRecord( new Simple() );
				Assert.Fail();
			}
			catch( Exception ex )
			{
				var expected = "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );
			}
		}

		[TestMethod]
		public void WriteRecordTest()
		{
			var serializer = new SerializerMock( true );
			var writer = new CsvWriter( serializer );
			try
			{
				writer.WriteRecord( typeof( Simple ), new Simple() );
				Assert.Fail();
			}
			catch( Exception ex )
			{
				var expected = "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );
			}
		}

		[TestMethod]
		public void WriteRecordsGenericTest()
		{
			var serializer = new SerializerMock( true );
			var writer = new CsvWriter( serializer );
			try
			{
				writer.WriteRecords( new List<Simple> { new Simple() } );
				Assert.Fail();
			}
			catch( Exception ex )
			{
				var expected = "Type: 'CsvHelper.Tests.Exceptions.ExceptionMessageTests+Simple'\r\n";
				Assert.AreEqual( expected, ex.Data["CsvHelper"] );
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
