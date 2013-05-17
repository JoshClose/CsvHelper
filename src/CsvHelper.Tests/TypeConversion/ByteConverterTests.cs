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
	public class ByteConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new ByteConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.AreEqual( "123", converter.ConvertToString( typeConverterOptions, (byte)123 ) );

			Assert.AreEqual( "", converter.ConvertToString( typeConverterOptions, null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new ByteConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.AreEqual( (byte)123, converter.ConvertFromString( typeConverterOptions, "123" ) );
			Assert.AreEqual( (byte)123, converter.ConvertFromString( typeConverterOptions, " 123 " ) );

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
