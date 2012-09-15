using System;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvReaderErrorMessageTests
	{
		[Fact]
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
					Assert.True( ex.ToString().Contains( "Row: '1'" ) );
					Assert.True( ex.ToString().Contains( "Field Index: '0'" ) );
					// There is no header so a field name should not be in the message.
					Assert.True( !ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.True( ex.ToString().Contains( "Field Value: ''" ) );
				}
			}
		}

		[Fact]
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
					Assert.True( ex.ToString().Contains( "Row: '3'" ) );
					Assert.True( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.True( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.True( ex.ToString().Contains( "Field Value: ''" ) );
				}
			}
		}

		[Fact]
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
					Assert.True( ex.ToString().Contains( "Row: '4'" ) );
					Assert.True( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.True( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.True( ex.ToString().Contains( "Field Value: ''" ) );
				}
			}
		}

		[Fact]
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
					Assert.True( ex.ToString().Contains( "Row: '3'" ) );
					Assert.True( ex.ToString().Contains( "Field Index: '0'" ) );
					Assert.True( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.True( ex.ToString().Contains( "Field Value: 'one'" ) );
				}
			}
		}

		[Fact]
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
					Assert.True( ex.ToString().Contains( "Row: '2'" ) );
					Assert.True( ex.ToString().Contains( "Field Index: '1'" ) );
					Assert.True( ex.ToString().Contains( "Field Name: 'IntColumn'" ) );
					Assert.True( ex.ToString().Contains( "Field Value: ''" ) );
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
	}
}
