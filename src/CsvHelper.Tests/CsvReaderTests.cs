using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvReaderTests
	{
		[TestMethod]
		public void GetFieldByIndexTest()
		{
			var data = new List<string>
			{
				"1",
				"blah",
				DateTime.Now.ToString(),
                "true",
                "c",
                "",
				Guid.NewGuid().ToString(),
			};

			var mockFactory = new MockFactory( MockBehavior.Default );
			var parserMock = mockFactory.Create<ICsvParser>();
			parserMock.Setup( m => m.Read() ).Returns( data );

			var reader = new CsvReader( parserMock.Object, new StreamReader( new MemoryStream() ) );
			reader.Read();

			Assert.AreEqual( Convert.ToInt16( data[0] ), reader.GetField<short>( 0 ) );
			Assert.AreEqual( Convert.ToInt16( data[0] ), reader.GetField<short?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<short?>( 5 ) );
			Assert.AreEqual( Convert.ToInt32( data[0] ), reader.GetField<int>( 0 ) );
			Assert.AreEqual( Convert.ToInt32( data[0] ), reader.GetField<int?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<int?>( 5 ) );
			Assert.AreEqual( Convert.ToInt64( data[0] ), reader.GetField<long>( 0 ) );
			Assert.AreEqual( Convert.ToInt64( data[0] ), reader.GetField<long?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<long?>( 5 ) );
			Assert.AreEqual( Convert.ToDecimal( data[0] ), reader.GetField<decimal>( 0 ) );
			Assert.AreEqual( Convert.ToDecimal( data[0] ), reader.GetField<decimal?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<decimal?>( 5 ) );
			Assert.AreEqual( Convert.ToSingle( data[0] ), reader.GetField<float>( 0 ) );
			Assert.AreEqual( Convert.ToSingle( data[0] ), reader.GetField<float?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<float?>( 5 ) );
			Assert.AreEqual( Convert.ToDouble( data[0] ), reader.GetField<double>( 0 ) );
			Assert.AreEqual( Convert.ToDouble( data[0] ), reader.GetField<double?>( 0 ) );
			Assert.AreEqual( null, reader.GetField<double?>( 5 ) );
			Assert.AreEqual( data[1], reader.GetField<string>( 1 ) );
			Assert.AreEqual( string.Empty, reader.GetField<string>( 5 ) );
			Assert.AreEqual( Convert.ToDateTime( data[2] ), reader.GetField<DateTime>( 2 ) );
			Assert.AreEqual( Convert.ToDateTime( data[2] ), reader.GetField<DateTime?>( 2 ) );
			Assert.AreEqual( null, reader.GetField<DateTime?>( 5 ) );
			Assert.AreEqual( Convert.ToBoolean( data[3] ), reader.GetField<bool>( 3 ) );
			Assert.AreEqual( Convert.ToBoolean( data[3] ), reader.GetField<bool?>( 3 ) );
			Assert.AreEqual( null, reader.GetField<bool?>( 5 ) );
			Assert.AreEqual( Convert.ToChar( data[4] ), reader.GetField<char>( 4 ) );
			Assert.AreEqual( Convert.ToChar( data[4] ), reader.GetField<char?>( 4 ) );
			Assert.AreEqual( null, reader.GetField<char?>( 5 ) );
			Assert.AreEqual( new Guid( data[6] ), reader.GetField<Guid>( 6 ) );
			Assert.AreEqual( null, reader.GetField<Guid?>( 5 ) );
		}

		[TestMethod]
		public void GetFieldByNameTest()
		{
			throw new NotImplementedException();
		}
	}
}
