// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class DateTimeConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new DateTimeConverter();

			var dateTime = DateTime.Now;

			// Valid conversions.
			Assert.AreEqual( dateTime.ToString(), converter.ConvertToString( CultureInfo.CurrentCulture, dateTime ) );

			// Invalid conversions.
			Assert.AreEqual( "1", converter.ConvertToString( CultureInfo.CurrentCulture, 1 ) );
			Assert.AreEqual( "", converter.ConvertToString( CultureInfo.CurrentCulture, null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new DateTimeConverter();

			var dateTime = DateTime.Now;

			// Valid conversions.
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( CultureInfo.CurrentCulture, dateTime.ToString() ).ToString() );
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( CultureInfo.CurrentCulture, dateTime.ToString( "o" ) ).ToString() );
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( CultureInfo.CurrentCulture, " " + dateTime + " " ).ToString() );

			// Invalid conversions.
			try
			{
				converter.ConvertFromString( CultureInfo.CurrentCulture, null );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
