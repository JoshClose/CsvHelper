using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Defaults
{
	[TestClass]
	public class WritingDefaultsTests
	{
		[TestMethod]
		public void EmptyFieldsOnNullReferencePropertyTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<A>
				{
					new A
					{
						AId = 1,
					},
					new A
					{
						AId = 2,
						B = new B
						{
							BId = 3,
						},
					},
				};

				csv.Configuration.UseNewObjectForNullReferenceProperties = false;
				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords( records );

				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = "AId,BId,CId\r\n" +
				               "1,,\r\n" +
				               "2,3,0\r\n";
				Assert.AreEqual( expected, data );
			}
		}

		[TestMethod]
		public void DefaultFieldsOnNullReferencePropertyTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<A>
				{
					new A
					{
						AId = 1,
					},
					new A
					{
						AId = 2,
						B = new B
						{
							BId = 3,
						},
					},
				};

				csv.Configuration.RegisterClassMap<AMap>();
				csv.WriteRecords( records );

				writer.Flush();
				stream.Position = 0;

				var data = reader.ReadToEnd();
				var expected = "AId,BId,CId\r\n" +
							   "1,0,0\r\n" +
							   "2,3,0\r\n";
				Assert.AreEqual( expected, data );
			}
		}

		private class A
		{
			public int AId { get; set; }

			public B B { get; set; }
		}

		private sealed class AMap : CsvClassMap<A>
		{
			public AMap()
			{
				Map( m => m.AId ).Default( 1 );
				References<BMap>( m => m.B );
			}
		}

		public class B
		{
			public int BId { get; set; }
			public int CId { get; set; }
		}

		public sealed class BMap : CsvClassMap<B>
		{
			public BMap()
			{
				AutoMap();
			}
		}
	}
}
