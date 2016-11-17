﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CsvHelper.TypeConversion;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumConverterTests
	{
		[TestMethod]
		public void ConstructorTest()
		{
			try
			{
				new EnumConverter( typeof( string ) );
				Assert.Fail();
			}
			catch( ArgumentException ex )
			{
				Assert.AreEqual( "'System.String' is not an Enum.", ex.Message );
			}
		}

		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new EnumConverter( typeof( TestEnum ) );
			var propertyMapData = new CsvPropertyMapData( null )
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.AreEqual( "None", converter.ConvertToString( (TestEnum)0, null, propertyMapData ) );
			Assert.AreEqual( "None", converter.ConvertToString( TestEnum.None, null, propertyMapData ) );
			Assert.AreEqual( "One", converter.ConvertToString( (TestEnum)1, null, propertyMapData ) );
			Assert.AreEqual( "One", converter.ConvertToString( TestEnum.One, null, propertyMapData ) );
			Assert.AreEqual( "", converter.ConvertToString( null, null, propertyMapData ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new EnumConverter( typeof( TestEnum ) );

			var propertyMapData = new CsvPropertyMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( "One", null, propertyMapData ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( "one", null, propertyMapData ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( "1", null, propertyMapData ) );
			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( "", null, propertyMapData ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}

			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( null, null, propertyMapData ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}

		private enum TestEnum
		{
			None = 0,
			One = 1,
		}
	}
}
