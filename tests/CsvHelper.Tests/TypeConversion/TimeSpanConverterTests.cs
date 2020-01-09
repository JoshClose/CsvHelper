// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TimeSpanConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new TimeSpanConverter();
			var propertyMapData = new MemberMapData( null )
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

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;
			var rowMock = new Mock<IReaderRow>();

			try
			{
				cmConverter.ConvertFromString( "" );
				Assert.Fail();
			}
			catch( FormatException ) {}

			try
			{
				var val = (DateTime)converter.ConvertFromString( "", rowMock.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException ) {}

			try
			{
				cmConverter.ConvertFromString( null );
				Assert.Fail();
			}
			catch( NotSupportedException ) { }

			try
			{
				converter.ConvertFromString( null, rowMock.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException ) { }
		}
	}
}
