// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class CharConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new CharConverter();
			var propertyMapData = new MemberMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.AreEqual( "a", converter.ConvertToString( 'a', null, propertyMapData ) );

			Assert.AreEqual( "True", converter.ConvertToString( true, null, propertyMapData ) );

			Assert.AreEqual( "", converter.ConvertToString( null, null, propertyMapData ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new CharConverter();

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockRow = new Mock<IReaderRow>();

			Assert.AreEqual( 'a', converter.ConvertFromString( "a", null, propertyMapData ) );
			Assert.AreEqual( 'a', converter.ConvertFromString( " a ", null, propertyMapData ) );
			Assert.AreEqual( ' ', converter.ConvertFromString( " ", null, propertyMapData ) );

			try
			{
				converter.ConvertFromString( null, mockRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException )
			{
			}
		}
	}
}
