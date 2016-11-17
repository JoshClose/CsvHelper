﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Globalization;
using CsvHelper.Configuration;
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
			var propertyMapData = new CsvPropertyMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var dateTime = DateTime.Now;
			var timeSpan = new TimeSpan( dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond );

			// Valid conversions.
			Assert.AreEqual( timeSpan.ToString(), converter.ConvertToString( timeSpan, null, propertyMapData ) );

			// Invalid conversions.
			Assert.AreEqual( "1", converter.ConvertToString( 1, null, propertyMapData ) );
			Assert.AreEqual( "", converter.ConvertToString( null, null, propertyMapData ) );
		}
		
		[TestMethod]
		public void ComponentModelCompatibilityTest()
		{
			var converter = new TimeSpanConverter();
			var cmConverter = new System.ComponentModel.TimeSpanConverter();

			var propertyMapData = new CsvPropertyMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			try
			{
				cmConverter.ConvertFromString( "" );
				Assert.Fail();
			}
			catch( FormatException ) {}

			try
			{
				var val = (DateTime)converter.ConvertFromString( "", null, propertyMapData );
				Assert.Fail();
			}
			catch( CsvTypeConverterException ) {}

			try
			{
				cmConverter.ConvertFromString( null );
				Assert.Fail();
			}
			catch( NotSupportedException ) { }

			try
			{
				converter.ConvertFromString( null, null, propertyMapData );
				Assert.Fail();
			}
			catch( CsvTypeConverterException ) { }
		}
	}
}
