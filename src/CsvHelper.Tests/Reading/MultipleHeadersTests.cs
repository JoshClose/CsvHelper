using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class MultipleHeadersTests
	{
		[TestMethod]
		public void ReadWithoutMapTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "A,B" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "Y,Z" );
				writer.WriteLine( "two,2" );
				writer.Flush();
				stream.Position = 0;

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.AreEqual( 1, csv.GetField<int>( "A" ) );
				Assert.AreEqual( "one", csv.GetField( "B" ) );

				csv.Read();
				csv.ReadHeader();
				csv.Read();

				Assert.AreEqual( "two", csv.GetField( "Y" ) );
				Assert.AreEqual( 2, csv.GetField<int>( "Z" ) );
			}
		}

		[TestMethod]
		public void ReadWithMapTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "A,B" );
				writer.WriteLine( "1,one" );
				writer.WriteLine( "Y,Z" );
				writer.WriteLine( "two,2" );
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.RegisterClassMap<AlphaMap>();
				csv.Configuration.RegisterClassMap<OmegaMap>();

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var alphaRecord = csv.GetRecord<Alpha>();

				Assert.AreEqual( 1, alphaRecord.A );
				Assert.AreEqual( "one", alphaRecord.B );

				csv.Read();
				csv.ReadHeader();

				csv.Read();
				var omegaRecord = csv.GetRecord<Omega>();

				Assert.AreEqual( "two", omegaRecord.Y );
				Assert.AreEqual( 2, omegaRecord.Z );
			}
		}

		private class Alpha
		{
			public int A { get; set; }
			public string B { get; set; }
		}

		private class Omega
		{
			public string Y { get; set; }
			public int Z { get; set; }
		}

		private sealed class AlphaMap : CsvClassMap<Alpha>
		{
			public AlphaMap()
			{
				Map( m => m.A );
				Map( m => m.B );
			}
		}

		private sealed class OmegaMap : CsvClassMap<Omega>
		{
			public OmegaMap()
			{
				Map( m => m.Y );
				Map( m => m.Z );
			}
		}
	}
}
