using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterOptionsFactoryTests
	{
		[TestMethod]
		public void AddGetRemoveTest()
		{
			var customOptions = new TypeConverterOptions
			{
				Format = "custom",
			};
			TypeConverterOptionsFactory.AddOptions<string>( customOptions );
			var options = TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreEqual( customOptions.Format, options.Format );

			TypeConverterOptionsFactory.RemoveOptions<string>();

			options = TypeConverterOptionsFactory.GetOptions<string>();

			Assert.AreNotEqual( customOptions.Format, options.Format );
		}

		[TestMethod]
		public void GetFieldTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
			TypeConverterOptionsFactory.AddOptions<int>( options );

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader, new CsvConfiguration { CultureInfo = CultureInfo.InvariantCulture }  ) )
			{
				writer.WriteLine( "\"1,234\",\"5,678\"" );
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Read();
				Assert.AreEqual( 1234, csvReader.GetField<int>( 0 ) );
				Assert.AreEqual( 5678, csvReader.GetField( typeof( int ),  1 ) );
			}
		}

        [TestMethod]
        public void GetFieldFromTwoOrMoreDifferentStreamsUsingTwoOrMoreCulturesTest() {
            var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
            TypeConverterOptionsFactory.AddOptions<int>(options);
            // en-GB culture
            var britishCI = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
            var frenchCI = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var greekCI = System.Globalization.CultureInfo.GetCultureInfo("el-GR");
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvReader = new CsvReader(reader, new CsvConfiguration { CultureInfo = britishCI, Delimiter = ","  })) {
                writer.WriteLine("\"1,234.32\",\"5,678.44\"");
                writer.Flush();
                stream.Position = 0;

                csvReader.Configuration.HasHeaderRecord = false;
                csvReader.Read();
                Assert.AreEqual(1234.32M, csvReader.GetField<decimal>(0));
                Assert.AreEqual(5678.44M, csvReader.GetField(typeof(decimal), 1));
            }
            // fr-FR culture
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvReader = new CsvReader(reader, new CsvConfiguration { CultureInfo = frenchCI, Delimiter = ";" })) {
                writer.WriteLine("\"1 234,32\";\"5678,44\"");
                writer.Flush();
                stream.Position = 0;

                csvReader.Configuration.HasHeaderRecord = false;
                csvReader.Read();
                Assert.AreEqual(1234.32M, csvReader.GetField<decimal>(0));
                Assert.AreEqual(5678.44M, csvReader.GetField(typeof(decimal), 1));
            }
            // el-GR culture
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            using (var writer = new StreamWriter(stream))
            using (var csvReader = new CsvReader(reader, new CsvConfiguration { CultureInfo = greekCI, Delimiter = ";" })) {
                writer.WriteLine("\"1.234,32\";\"5678,44\"");
                writer.Flush();
                stream.Position = 0;

                csvReader.Configuration.HasHeaderRecord = false;
                csvReader.Read();
                Assert.AreEqual(1234.32M, csvReader.GetField<decimal>(0));
                Assert.AreEqual(5678.44M, csvReader.GetField(typeof(decimal), 1));
            }
        }

		[TestMethod]
		public void GetRecordsTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
			TypeConverterOptionsFactory.AddOptions<int>( options );

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader, new CsvConfiguration { CultureInfo = CultureInfo.InvariantCulture } ) )
			{
				writer.WriteLine( "\"1,234\",\"5,678\"" );
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void GetRecordsAppliedWhenMappedTest()
		{
			var options = new TypeConverterOptions { NumberStyle = NumberStyles.AllowThousands };
			TypeConverterOptionsFactory.AddOptions<int>( options );
            var config = new CsvConfiguration { CultureInfo = CultureInfo.GetCultureInfo("en-US") };

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvReader = new CsvReader( reader, config ) )
			{
				writer.WriteLine( "\"1,234\",\"$5,678\"" );
				writer.Flush();
				stream.Position = 0;

				csvReader.Configuration.HasHeaderRecord = false;
				csvReader.Configuration.RegisterClassMap<TestMap>();
				csvReader.GetRecords<Test>().ToList();
			}
		}

		[TestMethod]
		public void WriteFieldTest()
		{
			var options = new TypeConverterOptions { Format = "c" };
			TypeConverterOptionsFactory.AddOptions<int>( options );
            var config = new CsvConfiguration { CultureInfo = CultureInfo.GetCultureInfo("en-US") };

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer, config ) )
			{
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
			var options = new TypeConverterOptions { Format = "c" };
			TypeConverterOptionsFactory.AddOptions<int>( options );
            var config = new CsvConfiguration { CultureInfo = CultureInfo.GetCultureInfo("en-US") };
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
            using( var csvWriter = new CsvWriter( writer, config ) )
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
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
			var options = new TypeConverterOptions { Format = "c" };
			TypeConverterOptionsFactory.AddOptions<int>( options );
            var config = new CsvConfiguration { CultureInfo = CultureInfo.GetCultureInfo("en-US") };

			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csvWriter = new CsvWriter( writer, config ) )
			{
				var list = new List<Test>
				{
					new Test { Number = 1234, NumberOverridenInMap = 5678 },
				};
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

		private class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Number );
				Map( m => m.NumberOverridenInMap ).TypeConverterOption( NumberStyles.AllowThousands | NumberStyles.AllowCurrencySymbol ).TypeConverterOption( "N" );
			}
		}
	}
}
