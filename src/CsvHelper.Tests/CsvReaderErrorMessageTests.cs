// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderErrorMessageTests
	{
		[TestMethod]
		public void FirstColumnEmptyFirstRowErrorWithNoHeaderTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Configuration.AllowComments = true;
				writer.WriteLine( ",one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch( CsvReaderException ex )
				{
					Assert.IsTrue( ex.ToString().Contains( "Row: '1'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Index: '0'" ) );
					// There is no header so a field name should not be in the message.
					Assert.IsTrue( !ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Value: ''" ) );

					Assert.AreEqual( 1, ex.Row );
					Assert.AreEqual( 0, ex.FieldIndex );
					Assert.IsNull( ex.FieldName );
					Assert.AreEqual( string.Empty, ex.FieldValue );
				}
			}
		}

		[TestMethod]
		public void FirstColumnEmptySecondRowErrorWithHeader()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				csvReader.Configuration.AllowComments = true;
				writer.WriteLine( "IntColumn,StringColumn" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( ",two" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch( CsvReaderException ex )
				{
					Assert.IsTrue( ex.ToString().Contains( "Row: '3'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Value: ''" ) );

					Assert.AreEqual( 3, ex.Row );
					Assert.AreEqual( 0, ex.FieldIndex );
					Assert.AreEqual( "IntColumn", ex.FieldName );
					Assert.AreEqual( string.Empty, ex.FieldValue );
				}
			}
		}

		[TestMethod]
		public void FirstColumnEmptyErrorWithHeaderAndCommentRowTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				csvReader.Configuration.AllowComments = true;
				writer.WriteLine( "IntColumn,StringColumn" );
				writer.WriteLine( "# comment" );
				writer.WriteLine();
				writer.WriteLine( ",one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch( CsvReaderException ex )
				{
					Assert.IsTrue( ex.ToString().Contains( "Row: '4'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Value: ''" ) );

					Assert.AreEqual( 4, ex.Row );
					Assert.AreEqual( 0, ex.FieldIndex );
					Assert.AreEqual( "IntColumn", ex.FieldName );
					Assert.AreEqual( string.Empty, ex.FieldValue );
				}
			}
		}

		[TestMethod]
		public void FirstColumnErrorTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "IntColumn,StringColumn" );
				writer.WriteLine();
				writer.WriteLine( "one,one" );
				writer.WriteLine( "2,two" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test1>().ToList();
					throw new Exception();
				}
				catch( CsvReaderException ex )
				{
					Assert.IsTrue( ex.ToString().Contains( "Row: '3'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Value: 'one'" ) );

					Assert.AreEqual( 3, ex.Row );
					Assert.AreEqual( 0, ex.FieldIndex );
					Assert.AreEqual( "IntColumn", ex.FieldName );
					Assert.AreEqual( "one", ex.FieldValue );
				}
			}
		}

		[TestMethod]
		public void SecondColumnEmptyErrorTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "StringColumn,IntColumn" );
				writer.WriteLine( "one," );
				writer.WriteLine( "two,2" );
				writer.Flush();
				stream.Position = 0;

				try
				{
					var records = csvReader.GetRecords<Test2>().ToList();
					throw new Exception();
				}
				catch( CsvReaderException ex )
				{
					Assert.IsTrue( ex.ToString().Contains( "Row: '2'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Index: '1'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.IsTrue( ex.ToString().Contains( "Field Value: ''" ) );
				}
			}
		}

		[TestMethod]
		public void Test()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine("1,9/24/2012");
				writer.Flush();
				stream.Position = 0;

				try
				{
					csvReader.Configuration.HasHeaderRecord = false;
					var records = csvReader.GetRecords<Test3>().ToList();
				}
				catch( CsvReaderException ex )
				{
					// Should throw this exception.
				}
			}
		}

		private class Test1
		{
			[CsvField( Index = 0 )]
			public int IntColumn { get; set; }

			[CsvField( Index = 1 )]
			public string StringColumn { get; set; }
		}

		private class Test2
		{
			public string StringColumn { get; set; }

			public int IntColumn { get; set; }
		}

		private class Test3
		{
			[CsvField(Index = 0)]
			public int Id { get; set; }

			[CsvField(Index = 1)]
			public DateTime CreationDate { get; set; }

			[CsvField(Index = 2)]
			public string Description { get; set; }
		}
	}
}
