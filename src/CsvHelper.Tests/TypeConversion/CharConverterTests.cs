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
	public class CharConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new CharConverter();

			Assert.AreEqual( "a", converter.ConvertToString( 'a' ) );

			Assert.AreEqual( "True", converter.ConvertToString( true ) );

			Assert.AreEqual( "", converter.ConvertToString( null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new CharConverter();

			Assert.AreEqual( 'a', converter.ConvertFromString( "a" ) );
			Assert.AreEqual( 'a', converter.ConvertFromString( " a " ) );

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
