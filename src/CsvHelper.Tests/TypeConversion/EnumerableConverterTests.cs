// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Globalization;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumerableConverterTests
	{
		[TestMethod]
		public void ListTest()
		{
			var converter = new EnumerableConverter();

			Assert.IsFalse( converter.CanConvertFrom( typeof( string ) ) );
			Assert.IsFalse( converter.CanConvertTo( typeof( string ) ) );
			try
			{
				converter.ConvertFromString( CultureInfo.CurrentCulture, "" );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
			try
			{
				converter.ConvertToString( CultureInfo.CurrentCulture, 5 );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
