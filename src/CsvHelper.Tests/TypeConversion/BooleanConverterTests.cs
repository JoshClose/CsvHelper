// Copyright 2009-2017 Josh Close and Contributors
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
	public class BooleanConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new BooleanConverter();

			var propertyMapData = new CsvPropertyMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.AreEqual( "True", converter.ConvertToString( true, null, propertyMapData ) );

			Assert.AreEqual( "False", converter.ConvertToString( false, null, propertyMapData ) );

			Assert.AreEqual( "", converter.ConvertToString( null, null, propertyMapData ) );
			Assert.AreEqual( "1", converter.ConvertToString( 1, null, propertyMapData ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			var propertyMapData = new CsvPropertyMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockRow = new Mock<ICsvReaderRow>();

			Assert.IsTrue( (bool)converter.ConvertFromString( "true", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "True", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "TRUE", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "1", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "yes", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "YES", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "y", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "Y", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " true ", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " yes ", null, propertyMapData ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " y ", null, propertyMapData ) );

			Assert.IsFalse( (bool)converter.ConvertFromString( "false", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "False", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "FALSE", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "0", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "no", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "NO", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "n", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "N", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " false ", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " 0 ", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " no ", null, propertyMapData ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " n ", null, propertyMapData ) );

			try
			{
				converter.ConvertFromString( null, mockRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
