using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterOptionsTests
	{
		[TestMethod]
		public void GlobalNullValueTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "," );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.TypeConverterOptionsFactory.GetOptions<string>().NullValues.Add( string.Empty );
				var records = csv.GetRecords<Test>().ToList();

				Assert.IsNull( records[0].Id );
				Assert.IsNull( records[0].Name );
			}
		}

		[TestMethod]
		public void MappingNullValueTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "," );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.IsNull( records[0].Id );
				Assert.IsNull( records[0].Name );
			}
		}

		[TestMethod]
		public void GlobalAndMappingNullValueTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "," );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.HasHeaderRecord = false;
				csv.Configuration.TypeConverterOptionsFactory.GetOptions<string>().NullValues.Add( "null" );
				csv.Configuration.RegisterClassMap<TestMap>();
				var records = csv.GetRecords<Test>().ToList();

				Assert.IsNull( records[0].Id );
				Assert.IsNull( records[0].Name );
			}
		}

		private class Test
		{
			public int? Id { get; set; }
			public string Name { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Id );
				Map( m => m.Name ).TypeConverterOption.NullValues( string.Empty );
			}
		}

		// auto map options have defaults
		// map options could be default or custom if set
		// global has defaults or custom
		// merge global with map
	}
}
