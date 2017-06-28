// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Culture
{
	[TestClass]
	public class TypeConverterOptionsFactoryTests
	{
		[TestInitialize]
		public void TestInitialize()
		{
			CultureInfo.CurrentCulture = new CultureInfo( "en-US" );
		}

		[TestMethod]
		public void AddGetRemoveTest()
		{
			var config = new CsvConfiguration();
			var customOptions = new TypeConverterOptions
			{
				Formats = new string[] { "custom" },
			};
			config.TypeConverterOptionsFactory.AddOptions<string>( customOptions );
			var options = config.TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreEqual( customOptions.Formats, options.Formats );

			config.TypeConverterOptionsFactory.RemoveOptions<string>();

			options = config.TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreNotEqual( customOptions.Formats, options.Formats );
		}

		[TestMethod]
		public void GetFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "\"1,234\",\"5,678\"" );
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
				csvReader.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Read();
				Assert.AreEqual( 1234, csvReader.GetField<int>( 0 ) );
				Assert.AreEqual( 5678, csvReader.GetField( typeof( int ), 1 ) );
			}
		}

		[TestMethod]
		public void GetFieldSwitchCulturesTest()
		{
			GetFieldForCultureTest( "\"1234,32\",\"5678,44\"", "fr-FR", 1234.32M, 5678.44M );
			GetFieldForCultureTest( "\"9876.54\",\"3210.98\"", "en-GB", 9876.54M, 3210.98M );
			GetFieldForCultureTest( "\"4455,6677\",\"9988,77\"", "el-GR", 4455.6677M, 9988.77M );
		}

		private static void GetFieldForCultureTest( string csvText, string culture, decimal expected1, decimal expected2 )
		{
			using( var reader = new StringReader( csvText ) )
			using( var csvReader = new CsvReader( reader, new CsvConfiguration { CultureInfo = new CultureInfo( culture ) } ) )
			{
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Read();
				Assert.AreEqual( expected1, csvReader.GetField<decimal>( 0 ) );
				Assert.AreEqual( expected2, csvReader.GetField( typeof( decimal ), 1 ) );
			}
		}

		[TestMethod]
		public void GetRecordsTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "\"1,234\",\"5,678\"" );
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
				csvReader.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void GetRecordsAppliedWhenMappedTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader ) )
			{
				writer.WriteLine( "\"1,234\",\"$5,678\"" );
				writer.Flush();
				stream.Position = 0;

				var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
				csvReader.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Configuration.RegisterClassMap<TestMap>();
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void WriteFieldTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvWriter.WriteField( 1234 );
				csvWriter.NextRecord();
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual( "\"$1,234.00\"\r\n", record );
			}
		}

		[TestMethod]
		public void WriteRecordsTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvWriter.Configuration.HasHeaderRecord = false;
				csvWriter.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual( "\"$1,234.00\",\"$5,678.00\"\r\n", record );
			}
		}

		[TestMethod]
		public void WriteRecordsAppliedWhenMappedTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer ) )
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
				var options = new TypeConverterOptions { Formats = new string[] { "c" } };
				csvWriter.Configuration.TypeConverterOptionsFactory.AddOptions<int>( options );
				csvWriter.Configuration.HasHeaderRecord = false;
				csvWriter.Configuration.RegisterClassMap<TestMap>();
				csvWriter.WriteRecords( list );
				writer.Flush();
				stream.Position = 0;
				var record = reader.ReadToEnd();

				Assert.AreEqual( "\"$1,234.00\",\"5,678.00\"\r\n", record );
			}
		}

		private class Test
		{
			public int Number { get; set; }

			public int NumberOverridenInMap { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Number );
				Map( m => m.NumberOverridenInMap ).TypeConverterOption.NumberStyles( NumberStyles.AllowThousands | NumberStyles.AllowCurrencySymbol ).TypeConverterOption.Format( "N" );
			}
		}
	}
}
