﻿// Copyright 2009-2013 Josh Close
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
    public class TimeSpanConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new TimeSpanConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			var dateTime = DateTime.Now;
		    var timeSpan = new TimeSpan(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond);


			// Valid conversions.
            Assert.AreEqual(timeSpan.ToString(), converter.ConvertToString(typeConverterOptions, timeSpan));

			// Invalid conversions.
			Assert.AreEqual( "1", converter.ConvertToString( typeConverterOptions, 1 ) );
			Assert.AreEqual( "", converter.ConvertToString( typeConverterOptions, null ) );
		}
	}
}
