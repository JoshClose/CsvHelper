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
	public class DateTimeOffsetConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new DateTimeOffsetConverter();
			var propertyMapData = new MemberMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var dateTime = DateTimeOffset.Now;

			// Valid conversions.
			Assert.AreEqual( dateTime.ToString(), converter.ConvertToString( dateTime, null, propertyMapData ) );

			// Invalid conversions.
			Assert.AreEqual( "1", converter.ConvertToString( 1, null, propertyMapData ) );
			Assert.AreEqual( "", converter.ConvertToString( null, null, propertyMapData ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new DateTimeOffsetConverter();

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockRow = new Mock<IReaderRow>();

			var dateTime = DateTimeOffset.Now;

			// Valid conversions.
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( dateTime.ToString(), null, propertyMapData ).ToString() );
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( dateTime.ToString( "o" ), null, propertyMapData ).ToString() );
			Assert.AreEqual( dateTime.ToString(), converter.ConvertFromString( " " + dateTime + " ", null, propertyMapData ).ToString() );

			// Invalid conversions.
			try
			{
				converter.ConvertFromString( null, mockRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException )
			{
			}
		}

		[TestMethod]
		public void ComponentModelCompatibilityTest()
		{
			var converter = new DateTimeOffsetConverter();
			var cmConverter = new System.ComponentModel.DateTimeOffsetConverter();

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockRow = new Mock<IReaderRow>();

			try
			{
				cmConverter.ConvertFromString( null );
				Assert.Fail();
			}
			catch( NotSupportedException ) { }

			try
			{
				converter.ConvertFromString( null, mockRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException ) { }

			try
			{
				cmConverter.ConvertFromString( "blah" );
				Assert.Fail();
			}
			catch( FormatException ) { }

			try
			{
				converter.ConvertFromString( "blah", mockRow.Object, propertyMapData );
			}
			catch( FormatException ) { }
		}
	}
}
