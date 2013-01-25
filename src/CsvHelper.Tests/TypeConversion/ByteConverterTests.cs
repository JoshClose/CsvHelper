using System;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class ByteConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new ByteConverter();

			Assert.AreEqual( "123", converter.ConvertToString( (byte)123 ) );

			Assert.AreEqual( "", converter.ConvertToString( null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new ByteConverter();

			Assert.AreEqual( (byte)123, converter.ConvertFromString( "123" ) );
			Assert.AreEqual( (byte)123, converter.ConvertFromString( " 123 " ) );

			try
			{
				converter.ConvertFromString( null );
				Assert.Fail();
			}
			catch( NotSupportedException )
			{
			}
		}
	}
}
