// Copyright 2009-2014 Josh Close and Contributors
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
	public class CharConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new CharConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};
			Assert.AreEqual( "a", converter.ConvertToString( typeConverterOptions, 'a' ) );

			Assert.AreEqual( "True", converter.ConvertToString( typeConverterOptions, true ) );

			Assert.AreEqual( "", converter.ConvertToString( typeConverterOptions, null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new CharConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};
			Assert.AreEqual( 'a', converter.ConvertFromString( typeConverterOptions, "a" ) );
			Assert.AreEqual( 'a', converter.ConvertFromString( typeConverterOptions, " a " ) );

			try
			{
				converter.ConvertFromString( typeConverterOptions, null );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
